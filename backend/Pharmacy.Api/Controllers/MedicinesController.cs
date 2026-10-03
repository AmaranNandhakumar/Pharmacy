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
public class MedicinesController : ControllerBase
{
    private const string CatalogueEditors = nameof(UserRole.Admin) + "," + nameof(UserRole.Pharmacist);

    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public MedicinesController(PharmacyDbContext db, IAuditService audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    [HttpGet]
    public async Task<ActionResult<List<MedicineDto>>> GetAll([FromQuery] string? search, [FromQuery] bool includeInactive = false)
    {
        var query = _db.Medicines.Include(m => m.Batches).AsNoTracking();

        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(m => EF.Functions.Like(m.Name, pattern)
                || (m.GenericName != null && EF.Functions.Like(m.GenericName, pattern))
                || m.Barcode == search.Trim());
        }

        var medicines = await query.OrderBy(m => m.Name).Take(200).ToListAsync();
        return Ok(medicines.Select(m => MedicineDto.From(m, Today)).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MedicineDetailDto>> GetById(int id)
    {
        var medicine = await _db.Medicines.Include(m => m.Batches).AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (medicine is null) return NotFound();

        return Ok(ToDetail(medicine));
    }

    [HttpPost]
    [Authorize(Roles = CatalogueEditors)]
    public async Task<ActionResult<MedicineDetailDto>> Create(MedicineUpsertDto dto)
    {
        var error = await Validate(dto, null);
        if (error is not null) return error;

        var medicine = new Medicine();
        Apply(dto, medicine);
        _db.Medicines.Add(medicine);
        await _db.SaveChangesAsync();

        _audit.Record("MedicineCreated", nameof(Medicine), medicine.Id, new { medicine.Name, Schedule = medicine.Schedule.ToString() });
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = medicine.Id }, ToDetail(medicine));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = CatalogueEditors)]
    public async Task<ActionResult<MedicineDetailDto>> Update(int id, MedicineUpsertDto dto)
    {
        var medicine = await _db.Medicines.Include(m => m.Batches).FirstOrDefaultAsync(m => m.Id == id);
        if (medicine is null) return NotFound();

        var error = await Validate(dto, id);
        if (error is not null) return error;

        var oldSchedule = medicine.Schedule;
        Apply(dto, medicine);

        _audit.Record("MedicineUpdated", nameof(Medicine), medicine.Id,
            new { medicine.Name, OldSchedule = oldSchedule.ToString(), NewSchedule = medicine.Schedule.ToString(), medicine.GstRatePercent });
        await _db.SaveChangesAsync();

        return Ok(ToDetail(medicine));
    }

    /// <summary>Soft delete: the medicine disappears from search but its history stays.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = CatalogueEditors)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var medicine = await _db.Medicines.FindAsync(id);
        if (medicine is null) return NotFound();

        medicine.IsActive = false;
        _audit.Record("MedicineDeactivated", nameof(Medicine), medicine.Id);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<ActionResult?> Validate(MedicineUpsertDto dto, int? existingId)
    {
        if (!StockRules.AllowedGstRates.Contains(dto.GstRatePercent))
            return BadRequest(new { message = $"GST rate must be one of: {string.Join(", ", StockRules.AllowedGstRates.Order())}%." });

        var barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
        if (barcode is not null && await _db.Medicines.AnyAsync(m => m.Barcode == barcode && m.Id != existingId))
            return Conflict(new { message = "Another medicine already has this barcode." });

        return null;
    }

    private static void Apply(MedicineUpsertDto dto, Medicine m)
    {
        m.Name = dto.Name.Trim();
        m.GenericName = Clean(dto.GenericName);
        m.Strength = Clean(dto.Strength);
        m.Form = dto.Form.Trim();
        m.PackSize = Clean(dto.PackSize);
        m.Manufacturer = Clean(dto.Manufacturer);
        m.Barcode = Clean(dto.Barcode);
        m.Schedule = dto.Schedule;
        m.HsnCode = dto.HsnCode.Trim();
        m.GstRatePercent = dto.GstRatePercent;
        m.ReorderLevel = dto.ReorderLevel;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private MedicineDetailDto ToDetail(Medicine m)
    {
        var today = Today;
        var summary = MedicineDto.From(m, today);
        var detail = new MedicineDetailDto
        {
            Id = summary.Id,
            Name = summary.Name,
            GenericName = summary.GenericName,
            Strength = summary.Strength,
            Form = summary.Form,
            PackSize = summary.PackSize,
            Manufacturer = summary.Manufacturer,
            Barcode = summary.Barcode,
            Schedule = summary.Schedule,
            RequiresPrescription = summary.RequiresPrescription,
            HsnCode = summary.HsnCode,
            GstRatePercent = summary.GstRatePercent,
            ReorderLevel = summary.ReorderLevel,
            IsActive = summary.IsActive,
            SellableQuantity = summary.SellableQuantity,
            NearestExpiry = summary.NearestExpiry,
            Batches = m.Batches.OrderBy(b => b.ExpiryDate).Select(b => BatchDto.From(b, today)).ToList()
        };
        detail.Batches.ForEach(b => b.MedicineName = m.Name);
        return detail;
    }
}
