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
public class SalesController : ControllerBase
{
    private const string RegisterReaders = nameof(UserRole.Admin) + "," + nameof(UserRole.Pharmacist);

    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public SalesController(PharmacyDbContext db, IAuditService audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Checks out a cart: OTC / Schedule G items taken from stock FEFO, plus dispensed prescription fills
    /// (whose stock already left at dispense). Raises one GST invoice; everything saves together.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create(CreateSaleDto dto)
    {
        if (dto.Items.Count == 0 && dto.PrescriptionFillIds.Count == 0)
            return BadRequest(new { message = "Add at least one item to the bill." });
        if (dto.DiscountPercent > SaleRules.MaxDiscountPercent)
            return BadRequest(new { message = $"Discount can't be more than {SaleRules.MaxDiscountPercent}%." });

        var userId = User.GetUserId();
        var sale = new Sale
        {
            UserId = userId,
            CreatedAt = UtcNow,
            PaymentMethod = dto.PaymentMethod,
            DiscountPercent = dto.DiscountPercent,
            CustomerName = string.IsNullOrWhiteSpace(dto.CustomerName) ? null : dto.CustomerName.Trim()
        };
        var movements = new List<StockMovement>();
        var register = new List<ScheduleRegisterEntry>();

        // Prescription fills: bill exactly the batches that were handed over
        var fillIds = dto.PrescriptionFillIds.Distinct().ToList();
        var fills = await _db.PrescriptionFills
            .Include(f => f.Prescription).ThenInclude(p => p.Patient)
            .Include(f => f.Lines).ThenInclude(l => l.Batch)
            .Include(f => f.Lines).ThenInclude(l => l.PrescriptionItem).ThenInclude(i => i.Medicine)
            .Where(f => fillIds.Contains(f.Id))
            .ToListAsync();

        if (fills.Count != fillIds.Count) return BadRequest(new { message = "Prescription fill not found." });
        if (fills.Any(f => f.SaleId is not null)) return BadRequest(new { message = "That prescription has already been billed." });

        var patientIds = fills.Select(f => f.Prescription.PatientId).Distinct().ToList();
        if (patientIds.Count > 1 || (dto.PatientId is not null && patientIds.Count == 1 && patientIds[0] != dto.PatientId))
            return BadRequest(new { message = "All prescriptions on one bill must belong to the same patient." });

        sale.PatientId = patientIds.Count == 1 ? patientIds[0] : dto.PatientId;
        if (sale.PatientId is not null && fills.Count == 0 && !await _db.Patients.AnyAsync(p => p.Id == sale.PatientId))
            return BadRequest(new { message = "Patient not found." });

        foreach (var fill in fills)
        {
            fill.Sale = sale;
            foreach (var line in fill.Lines)
            {
                var item = AddLine(sale, line.PrescriptionItem.Medicine, line.Batch, line.Quantity, line.PrescriptionItemId);
                if (SaleRules.NeedsRegisterEntry(line.PrescriptionItem.Medicine.Schedule))
                    register.Add(RegisterEntry(fill, line, item));
            }
        }

        // Shelf items: only OTC and Schedule G, picked earliest expiry first
        var medicineIds = dto.Items.Select(i => i.MedicineId).Distinct().ToList();
        if (medicineIds.Count != dto.Items.Count)
            return BadRequest(new { message = "Each medicine can appear only once; change the quantity instead." });

        var medicines = await _db.Medicines.Include(m => m.Batches).Where(m => medicineIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        try
        {
            foreach (var request in dto.Items)
            {
                if (!medicines.TryGetValue(request.MedicineId, out var medicine) || !medicine.IsActive)
                    return BadRequest(new { message = $"Medicine {request.MedicineId} not found or no longer active." });
                if (!SaleRules.CanSellOverTheCounter(medicine))
                    return BadRequest(new { message = $"{medicine.Name} is a prescription medicine; dispense it against a prescription first." });

                foreach (var (batch, quantity) in FefoAllocator.Allocate(medicine.Batches, request.Quantity, Today))
                {
                    AddLine(sale, medicine, batch, quantity, prescriptionItemId: null);
                    movements.Add(StockRules.Apply(batch, -quantity, StockMovementType.Sale, userId));
                }
            }
        }
        catch (StockRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        sale.GrossAmount = sale.Items.Sum(i => i.GrossAmount);
        sale.Discount = sale.Items.Sum(i => i.Discount);
        sale.TaxableValue = sale.Items.Sum(i => i.TaxableValue);
        sale.Cgst = sale.Items.Sum(i => i.Cgst);
        sale.Sgst = sale.Items.Sum(i => i.Sgst);
        sale.Total = sale.Items.Sum(i => i.LineTotal);

        _db.Sales.Add(sale);
        _db.ScheduleRegister.AddRange(register);

        // Invoice numbers are sequential per financial year; the unique index catches two counters
        // taking the same number at once, and we simply take the next one.
        for (var attempt = 1; ; attempt++)
        {
            var invoiceNo = await NextInvoiceNo();
            sale.InvoiceNo = invoiceNo;
            movements.ForEach(m => m.ReferenceId = invoiceNo);
            register.ForEach(r => r.InvoiceNo = invoiceNo);

            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "Stock changed while billing. Nothing was saved; try again." });
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                // Another counter got this number first: loop for the next one. Anything else is a real error.
                if (!await InvoiceNoTaken(invoiceNo)) throw;
            }
        }

        _audit.Record("SaleCompleted", nameof(Sale), sale.Id,
            new { sale.InvoiceNo, sale.Total, Fills = fills.Select(f => f.Id), PaymentMethod = sale.PaymentMethod.ToString() });
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = sale.Id }, await ToDto(sale.Id));
    }

    [HttpGet]
    public async Task<ActionResult<List<SaleSummaryDto>>> GetAll([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (start, end) = UtcRange(from ?? Today, to ?? from ?? Today);
        var sales = await _db.Sales.Include(s => s.Patient).Include(s => s.Items).AsNoTracking()
            .Where(s => s.CreatedAt >= start && s.CreatedAt < end)
            .OrderByDescending(s => s.Id)
            .Take(500)
            .ToListAsync();
        return Ok(sales.Select(SaleSummaryDto.From).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SaleDto>> GetById(int id)
    {
        var dto = await ToDto(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Cancels a bill and puts its stock back on the shelf. Admin only, with a reason.</summary>
    [HttpPost("{id:int}/void")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<SaleDto>> Void(int id, VoidSaleDto dto)
    {
        var sale = await _db.Sales.Include(s => s.Items).ThenInclude(i => i.Batch).FirstOrDefaultAsync(s => s.Id == id);
        if (sale is null) return NotFound();
        if (sale.Status == SaleStatus.Voided) return BadRequest(new { message = "This sale is already voided." });

        var userId = User.GetUserId();
        foreach (var item in sale.Items)
            StockRules.Apply(item.Batch, item.Quantity, StockMovementType.Return, userId, reason: $"Void: {dto.Reason.Trim()}", referenceId: sale.InvoiceNo);

        sale.Status = SaleStatus.Voided;
        sale.VoidedById = userId;
        sale.VoidedAt = UtcNow;
        sale.VoidReason = dto.Reason.Trim();

        _audit.Record("SaleVoided", nameof(Sale), sale.Id, new { sale.InvoiceNo, sale.Total, Reason = sale.VoidReason });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Stock changed while voiding. Nothing was saved; try again." });
        }

        return Ok(await ToDto(sale.Id));
    }

    /// <summary>Dispensed prescription fills not yet billed, for the counter to pick up.</summary>
    [HttpGet("billable-fills")]
    public async Task<ActionResult<List<BillableFillDto>>> GetBillableFills([FromQuery] int? patientId)
    {
        var query = _db.PrescriptionFills.AsNoTracking()
            .Include(f => f.Prescription).ThenInclude(p => p.Patient)
            .Include(f => f.Lines).ThenInclude(l => l.Batch)
            .Include(f => f.Lines).ThenInclude(l => l.PrescriptionItem).ThenInclude(i => i.Medicine)
            .Where(f => f.SaleId == null);
        if (patientId is not null) query = query.Where(f => f.Prescription.PatientId == patientId);

        var fills = await query.OrderBy(f => f.Id).Take(100).ToListAsync();
        return Ok(fills.Select(f => new BillableFillDto
        {
            FillId = f.Id,
            PrescriptionId = f.PrescriptionId,
            PatientId = f.Prescription.PatientId,
            PatientName = f.Prescription.Patient.FullName,
            PrescriberName = f.Prescription.PrescriberName,
            IsRefill = f.IsRefill,
            DispensedAt = f.DispensedAt,
            Total = f.Lines.Sum(l => l.Batch.SellingPrice * l.Quantity),
            Lines = f.Lines.Select(l => new BillableFillLineDto
            {
                MedicineName = l.PrescriptionItem.Medicine.Name,
                BatchNumber = l.Batch.BatchNumber,
                Quantity = l.Quantity,
                UnitPrice = l.Batch.SellingPrice
            }).ToList()
        }).ToList());
    }

    /// <summary>The Schedule H1 or X register for a date range (kept for 3 years for H1).</summary>
    [HttpGet("register")]
    [Authorize(Roles = RegisterReaders)]
    public async Task<ActionResult<List<ScheduleRegisterEntryDto>>> GetRegister([FromQuery] DrugSchedule schedule = DrugSchedule.H1,
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        if (!SaleRules.NeedsRegisterEntry(schedule))
            return BadRequest(new { message = "Registers exist for Schedule H1 and X only." });

        var (start, end) = UtcRange(from ?? Today.AddDays(-30), to ?? Today);
        var entries = await _db.ScheduleRegister.AsNoTracking()
            .Where(r => r.Schedule == schedule && r.CreatedAt >= start && r.CreatedAt < end)
            .OrderBy(r => r.Id)
            .ToListAsync();

        var pharmacistIds = entries.Select(e => e.PharmacistId).Distinct().ToList();
        var names = await _db.Users.Where(u => pharmacistIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        return Ok(entries.Select(r => new ScheduleRegisterEntryDto
        {
            Id = r.Id,
            Schedule = r.Schedule,
            CreatedAt = r.CreatedAt,
            InvoiceNo = r.InvoiceNo,
            PatientName = r.PatientName,
            PatientAddress = r.PatientAddress,
            PrescriberName = r.PrescriberName,
            PrescriberRegNo = r.PrescriberRegNo,
            PrescriberAddress = r.PrescriberAddress,
            DrugName = r.DrugName,
            BatchNumber = r.BatchNumber,
            Quantity = r.Quantity,
            PharmacistName = names.GetValueOrDefault(r.PharmacistId)
        }).ToList());
    }

    private static SaleItem AddLine(Sale sale, Medicine medicine, Batch batch, int quantity, int? prescriptionItemId)
    {
        var money = GstCalculator.Calculate(batch.SellingPrice, quantity, medicine.GstRatePercent, sale.DiscountPercent);
        var item = new SaleItem
        {
            Medicine = medicine,
            Batch = batch,
            PrescriptionItemId = prescriptionItemId,
            Quantity = quantity,
            MedicineName = $"{medicine.Name} {medicine.Strength}".Trim(),
            BatchNumber = batch.BatchNumber,
            ExpiryDate = batch.ExpiryDate,
            Mrp = batch.Mrp,
            UnitPrice = batch.SellingPrice,
            HsnCode = medicine.HsnCode,
            GstRatePercent = medicine.GstRatePercent,
            GrossAmount = money.Gross,
            Discount = money.Discount,
            TaxableValue = money.TaxableValue,
            Cgst = money.Cgst,
            Sgst = money.Sgst,
            LineTotal = money.Total
        };
        sale.Items.Add(item);
        return item;
    }

    private static ScheduleRegisterEntry RegisterEntry(PrescriptionFill fill, PrescriptionFillLine line, SaleItem item) => new()
    {
        Schedule = line.PrescriptionItem.Medicine.Schedule,
        SaleItem = item,
        PatientName = fill.Prescription.Patient.FullName,
        PatientAddress = fill.Prescription.Patient.Address,
        PrescriberName = fill.Prescription.PrescriberName,
        PrescriberRegNo = fill.Prescription.PrescriberRegNo,
        PrescriberAddress = fill.Prescription.PrescriberAddress,
        DrugName = item.MedicineName,
        BatchNumber = item.BatchNumber,
        Quantity = item.Quantity,
        PharmacistId = fill.DispensedById
    };

    private async Task<string> NextInvoiceNo()
    {
        var prefix = GstCalculator.FinancialYear(Today) + "/";
        var last = await _db.Sales.Where(s => s.InvoiceNo.StartsWith(prefix))
            .OrderByDescending(s => s.InvoiceNo).Select(s => s.InvoiceNo).FirstOrDefaultAsync();
        var next = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return GstCalculator.InvoiceNo(prefix.TrimEnd('/'), next);
    }

    private Task<bool> InvoiceNoTaken(string invoiceNo) =>
        _db.Sales.AsNoTracking().AnyAsync(s => s.InvoiceNo == invoiceNo);

    /// <summary>Converts a range of local (pharmacy) dates to [start, end) in UTC, both days inclusive.</summary>
    private (DateTime start, DateTime end) UtcRange(DateOnly from, DateOnly to)
    {
        var zone = _time.LocalTimeZone;
        var start = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        return (start, end);
    }

    private async Task<SaleDto?> ToDto(int id)
    {
        var s = await _db.Sales.AsNoTracking().Include(x => x.Patient).Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return null;

        var userIds = new[] { s.UserId, s.VoidedById }.OfType<int>().Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var items = s.Items.OrderBy(i => i.Id).ToList();

        return new SaleDto
        {
            Id = s.Id,
            InvoiceNo = s.InvoiceNo,
            CreatedAt = s.CreatedAt,
            PatientId = s.PatientId,
            CustomerName = s.Patient?.FullName ?? s.CustomerName,
            PatientAddress = s.Patient?.Address,
            ItemCount = items.Count,
            PaymentMethod = s.PaymentMethod,
            Status = s.Status,
            BilledByName = names.GetValueOrDefault(s.UserId),
            DiscountPercent = s.DiscountPercent,
            GrossAmount = s.GrossAmount,
            Discount = s.Discount,
            TaxableValue = s.TaxableValue,
            Cgst = s.Cgst,
            Sgst = s.Sgst,
            Total = s.Total,
            VoidedByName = s.VoidedById is null ? null : names.GetValueOrDefault(s.VoidedById.Value),
            VoidedAt = s.VoidedAt,
            VoidReason = s.VoidReason,
            Items = items.Select(i => new SaleItemDto
            {
                Id = i.Id,
                MedicineId = i.MedicineId,
                MedicineName = i.MedicineName,
                PrescriptionItemId = i.PrescriptionItemId,
                HsnCode = i.HsnCode,
                BatchNumber = i.BatchNumber,
                ExpiryDate = i.ExpiryDate,
                Mrp = i.Mrp,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                GstRatePercent = i.GstRatePercent,
                GrossAmount = i.GrossAmount,
                Discount = i.Discount,
                TaxableValue = i.TaxableValue,
                Cgst = i.Cgst,
                Sgst = i.Sgst,
                LineTotal = i.LineTotal
            }).ToList(),
            GstSummary = items.GroupBy(i => i.GstRatePercent).OrderBy(g => g.Key).Select(g => new GstSummaryDto
            {
                GstRatePercent = g.Key,
                TaxableValue = g.Sum(i => i.TaxableValue),
                Cgst = g.Sum(i => i.Cgst),
                Sgst = g.Sum(i => i.Sgst)
            }).ToList(),
            Pharmacy = PharmacySettingsDto.From(await SettingsLoader.LoadAsync(_db))
        };
    }
}
