using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Api.DTOs;
using Pharmacy.Api.Services;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrescriptionsController : ControllerBase
{
    // Pharmacy Act 1948: prescription drugs are verified and handed over by a registered pharmacist
    private const string Pharmacists = nameof(UserRole.Pharmacist);

    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public PrescriptionsController(PharmacyDbContext db, IAuditService audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    [HttpGet]
    public async Task<ActionResult<List<PrescriptionSummaryDto>>> GetAll([FromQuery] PrescriptionStatus? status, [FromQuery] int? patientId)
    {
        var query = _db.Prescriptions.Include(rx => rx.Patient).Include(rx => rx.Items).AsNoTracking();

        if (status is not null) query = query.Where(rx => rx.Status == status);
        if (patientId is not null) query = query.Where(rx => rx.PatientId == patientId);

        var list = await query.OrderByDescending(rx => rx.Id).Take(200).ToListAsync();
        return Ok(list.Select(PrescriptionSummaryDto.From).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PrescriptionDto>> GetById(int id)
    {
        var rx = await Load(id, tracking: false);
        return rx is null ? NotFound() : Ok(await ToDto(rx));
    }

    /// <summary>Any staff member can enter a prescription; a pharmacist then verifies it.</summary>
    [HttpPost]
    public async Task<ActionResult<PrescriptionDto>> Create(CreatePrescriptionDto dto)
    {
        var patient = await _db.Patients.FindAsync(dto.PatientId);
        if (patient is null) return BadRequest(new { message = "Patient not found." });

        var issuedOn = dto.IssuedOn!.Value;
        if (issuedOn > Today) return BadRequest(new { message = "The prescription date can't be in the future." });

        var medicineIds = dto.Items.Select(i => i.MedicineId).Distinct().ToList();
        if (medicineIds.Count != dto.Items.Count)
            return BadRequest(new { message = "Each medicine can appear only once on a prescription." });

        var medicines = await _db.Medicines.Where(m => medicineIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        foreach (var medicineId in medicineIds)
        {
            if (!medicines.TryGetValue(medicineId, out var medicine))
                return BadRequest(new { message = $"Medicine {medicineId} not found." });
            var error = PrescriptionRules.ValidateItemMedicine(medicine);
            if (error is not null) return BadRequest(new { message = error });
        }

        var rx = new Prescription
        {
            Patient = patient,
            PrescriberName = dto.PrescriberName.Trim(),
            PrescriberRegNo = dto.PrescriberRegNo.Trim().ToUpperInvariant(),
            PrescriberAddress = string.IsNullOrWhiteSpace(dto.PrescriberAddress) ? null : dto.PrescriberAddress.Trim(),
            IssuedOn = issuedOn,
            CopyRetained = dto.CopyRetained,
            EnteredById = User.GetUserId(),
            Items = dto.Items.Select(i => new PrescriptionItem
            {
                Medicine = medicines[i.MedicineId],
                Dose = i.Dose.Trim(),
                Quantity = i.Quantity,
                Directions = i.Directions.Trim(),
                RefillsAllowed = i.RefillsAllowed
            }).ToList()
        };
        _db.Prescriptions.Add(rx);
        await _db.SaveChangesAsync();

        _audit.Record("PrescriptionEntered", nameof(Prescription), rx.Id, new { rx.PatientId, Items = rx.Items.Count });
        await _db.SaveChangesAsync();

        var created = await Load(rx.Id, tracking: false);
        return CreatedAtAction(nameof(GetById), new { id = rx.Id }, await ToDto(created!));
    }

    /// <summary>
    /// The pharmacist checks the prescription. If the patient's allergies match a medicine, the request
    /// must say the warnings were seen (<c>acknowledgeAllergyWarnings</c>), and that override is audited.
    /// </summary>
    [HttpPost("{id:int}/verify")]
    [Authorize(Roles = Pharmacists)]
    public async Task<ActionResult<PrescriptionDto>> Verify(int id, VerifyPrescriptionDto dto)
    {
        var rx = await Load(id, tracking: true);
        if (rx is null) return NotFound();

        var warnings = rx.Items
            .SelectMany(i => AllergyRules.FindMatches(rx.Patient.Allergies, i.Medicine).Select(a => $"{i.Medicine.Name}: {a}"))
            .ToList();
        if (warnings.Count > 0 && !dto.AcknowledgeAllergyWarnings)
            return Conflict(new
            {
                message = "This patient has allergies that match the prescription. Review and acknowledge them to verify.",
                allergyWarnings = warnings
            });

        try
        {
            PrescriptionRules.Verify(rx, User.GetUserId(), UtcNow);
        }
        catch (PrescriptionRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _audit.Record("PrescriptionVerified", nameof(Prescription), rx.Id,
            warnings.Count > 0 ? new { AllergyWarningsOverridden = warnings } : null);
        await _db.SaveChangesAsync();

        return Ok(await ToDto(rx));
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = Pharmacists)]
    public async Task<ActionResult<PrescriptionDto>> Reject(int id, RejectPrescriptionDto dto)
    {
        var rx = await Load(id, tracking: true);
        if (rx is null) return NotFound();

        try
        {
            PrescriptionRules.Reject(rx, dto.Reason.Trim());
        }
        catch (PrescriptionRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _audit.Record("PrescriptionRejected", nameof(Prescription), rx.Id, new { Reason = rx.RejectReason });
        await _db.SaveChangesAsync();

        return Ok(await ToDto(rx));
    }

    /// <summary>
    /// Hands over a verified prescription, or a refill of a dispensed one. Stock comes from the
    /// earliest-expiring batches first (FEFO), never from expired ones, and everything saves together or not at all.
    /// </summary>
    [HttpPost("{id:int}/dispense")]
    [Authorize(Roles = Pharmacists)]
    public async Task<ActionResult<DispenseResultDto>> Dispense(int id)
    {
        var rx = await Load(id, tracking: true);
        if (rx is null) return NotFound();

        var userId = User.GetUserId();
        var wasRefill = rx.Status == PrescriptionStatus.Dispensed;
        var lines = new List<DispensedLineDto>();
        var fill = new PrescriptionFill { Prescription = rx, IsRefill = wasRefill, DispensedById = userId, DispensedAt = UtcNow };

        try
        {
            var items = PrescriptionRules.ItemsToFill(rx);

            foreach (var item in items)
            {
                var error = PrescriptionRules.ValidateItemMedicine(item.Medicine);
                if (error is not null) throw new PrescriptionRuleException(error);

                var picks = FefoAllocator.Allocate(item.Medicine.Batches, item.Quantity, Today);
                foreach (var (batch, quantity) in picks)
                {
                    StockRules.Apply(batch, -quantity, StockMovementType.Dispense, userId, referenceId: $"RX-{rx.Id}");
                    fill.Lines.Add(new PrescriptionFillLine { PrescriptionItem = item, Batch = batch, Quantity = quantity });
                    lines.Add(new DispensedLineDto
                    {
                        PrescriptionItemId = item.Id,
                        MedicineName = item.Medicine.Name,
                        BatchId = batch.Id,
                        BatchNumber = batch.BatchNumber,
                        ExpiryDate = batch.ExpiryDate,
                        Mrp = batch.Mrp,
                        Quantity = quantity
                    });
                }
            }

            PrescriptionRules.RecordFill(rx, items, userId, UtcNow);
            // Billed later at the counter (POST /api/sales with this fill's id)
            _db.PrescriptionFills.Add(fill);
        }
        catch (Exception ex) when (ex is PrescriptionRuleException or StockRuleException)
        {
            return BadRequest(new { message = ex.Message });
        }

        _audit.Record(wasRefill ? "PrescriptionRefilled" : "PrescriptionDispensed", nameof(Prescription), rx.Id,
            new { Batches = lines.Select(l => new { l.BatchId, l.BatchNumber, l.Quantity }) });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Stock changed while dispensing. Nothing was saved; try again." });
        }

        return Ok(new DispenseResultDto { FillId = fill.Id, Prescription = await ToDto(rx), WasRefill = wasRefill, Lines = lines });
    }

    private Task<Prescription?> Load(int id, bool tracking)
    {
        IQueryable<Prescription> query = _db.Prescriptions
            .Include(rx => rx.Patient)
            .Include(rx => rx.Items).ThenInclude(i => i.Medicine).ThenInclude(m => m.Batches);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(rx => rx.Id == id);
    }

    private async Task<PrescriptionDto> ToDto(Prescription rx)
    {
        var userIds = new[] { rx.EnteredById, rx.VerifiedById, rx.DispensedById }.OfType<int>().Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        string? NameOf(int? userId) => userId is not null && names.TryGetValue(userId.Value, out var n) ? n : null;

        var today = Today;
        return new PrescriptionDto
        {
            Id = rx.Id,
            PatientId = rx.PatientId,
            PatientName = rx.Patient.FullName,
            PatientAllergies = rx.Patient.Allergies,
            PrescriberName = rx.PrescriberName,
            PrescriberRegNo = rx.PrescriberRegNo,
            PrescriberAddress = rx.PrescriberAddress,
            IssuedOn = rx.IssuedOn,
            CopyRetained = rx.CopyRetained,
            Status = rx.Status,
            RejectReason = rx.RejectReason,
            ItemCount = rx.Items.Count,
            CreatedAt = rx.CreatedAt,
            EnteredByName = NameOf(rx.EnteredById),
            VerifiedByName = NameOf(rx.VerifiedById),
            VerifiedAt = rx.VerifiedAt,
            DispensedByName = NameOf(rx.DispensedById),
            DispensedAt = rx.DispensedAt,
            Items = rx.Items.OrderBy(i => i.Id).Select(i => new PrescriptionItemDto
            {
                Id = i.Id,
                MedicineId = i.MedicineId,
                MedicineName = i.Medicine.Name,
                GenericName = i.Medicine.GenericName,
                Strength = i.Medicine.Strength,
                Schedule = i.Medicine.Schedule,
                Dose = i.Dose,
                Quantity = i.Quantity,
                Directions = i.Directions,
                RefillsAllowed = i.RefillsAllowed,
                RefillsUsed = i.RefillsUsed,
                QuantityDispensed = i.QuantityDispensed,
                SellableQuantity = StockRules.SellableQuantity(i.Medicine.Batches, today),
                AllergyWarnings = AllergyRules.FindMatches(rx.Patient.Allergies, i.Medicine).ToList()
            }).ToList()
        };
    }
}
