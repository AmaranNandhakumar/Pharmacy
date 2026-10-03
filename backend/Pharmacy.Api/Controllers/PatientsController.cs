using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Api.DTOs;
using Pharmacy.Api.Services;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PatientsController : ControllerBase
{
    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public PatientsController(PharmacyDbContext db, IAuditService audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    /// <summary>Search by name or phone. Returns at most 50 matches, so the counter can search as you type.</summary>
    [HttpGet]
    public async Task<ActionResult<List<PatientDto>>> Search([FromQuery] string? search)
    {
        var query = _db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.FullName, pattern) || (p.Phone != null && EF.Functions.Like(p.Phone, pattern)));
        }

        var patients = await query.OrderBy(p => p.FullName).Take(50).ToListAsync();
        var today = Today;
        return Ok(patients.Select(p => PatientDto.From(p, today)).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PatientDetailDto>> GetById(int id)
    {
        var patient = await _db.Patients
            .Include(p => p.Prescriptions).ThenInclude(rx => rx.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (patient is null) return NotFound();

        // Health data: who opened which record is part of the trail
        _audit.Record("PatientViewed", nameof(Patient), patient.Id);
        await _db.SaveChangesAsync();

        return Ok(ToDetail(patient));
    }

    [HttpPost]
    public async Task<ActionResult<PatientDetailDto>> Create(PatientUpsertDto dto)
    {
        if (!dto.ConsentGiven)
            return BadRequest(new { message = "Record the patient's consent before saving their details." });
        var error = ValidateDateOfBirth(dto);
        if (error is not null) return error;

        var patient = new Patient { ConsentGivenAt = _time.GetUtcNow().UtcDateTime };
        Apply(dto, patient);
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        // Only the id goes in the audit row, never the patient's personal data
        _audit.Record("PatientCreated", nameof(Patient), patient.Id);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, ToDetail(patient));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PatientDetailDto>> Update(int id, PatientUpsertDto dto)
    {
        var patient = await _db.Patients.Include(p => p.Prescriptions).ThenInclude(rx => rx.Items).FirstOrDefaultAsync(p => p.Id == id);
        if (patient is null) return NotFound();

        var error = ValidateDateOfBirth(dto);
        if (error is not null) return error;

        var allergiesChanged = !string.Equals(patient.Allergies, Clean(dto.Allergies), StringComparison.Ordinal);
        Apply(dto, patient);
        patient.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        _audit.Record("PatientUpdated", nameof(Patient), patient.Id, new { AllergiesChanged = allergiesChanged });
        await _db.SaveChangesAsync();

        return Ok(ToDetail(patient));
    }

    private ActionResult? ValidateDateOfBirth(PatientUpsertDto dto)
    {
        var dob = dto.DateOfBirth!.Value;
        if (dob > Today) return BadRequest(new { message = "Date of birth can't be in the future." });
        if (dob < Today.AddYears(-130)) return BadRequest(new { message = "Check the date of birth." });
        return null;
    }

    private static void Apply(PatientUpsertDto dto, Patient p)
    {
        p.FullName = dto.FullName.Trim();
        p.DateOfBirth = dto.DateOfBirth!.Value;
        p.Phone = Clean(dto.Phone)?.Replace(" ", "").Replace("-", "");
        p.Address = Clean(dto.Address);
        p.Allergies = Clean(dto.Allergies);
        p.Notes = Clean(dto.Notes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private PatientDetailDto ToDetail(Patient p)
    {
        var detail = PatientDto.Fill(new PatientDetailDto(), p, Today);
        detail.Prescriptions = p.Prescriptions
            .OrderByDescending(rx => rx.CreatedAt)
            .Select(rx =>
            {
                var summary = PrescriptionSummaryDto.From(rx);
                summary.PatientName = p.FullName;
                return summary;
            })
            .ToList();
        return detail;
    }
}
