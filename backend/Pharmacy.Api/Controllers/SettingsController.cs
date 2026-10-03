using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.DTOs;
using Pharmacy.Api.Services;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Controllers;

/// <summary>The pharmacy's own details (name, GSTIN, drug licences) printed on every invoice.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;

    public SettingsController(PharmacyDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<PharmacySettingsDto>> Get() =>
        Ok(PharmacySettingsDto.From(await SettingsLoader.LoadAsync(_db)));

    [HttpPut]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<PharmacySettingsDto>> Update(PharmacySettingsDto dto)
    {
        var gstin = dto.Gstin.Trim().ToUpperInvariant();
        if (!gstin.StartsWith(dto.StateCode))
            return BadRequest(new { message = "The GSTIN must start with the state code." });

        var s = await SettingsLoader.LoadAsync(_db);
        s.Name = dto.Name.Trim();
        s.Address = dto.Address.Trim();
        s.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
        s.StateCode = dto.StateCode;
        s.Gstin = gstin;
        s.DrugLicence20 = dto.DrugLicence20.Trim();
        s.DrugLicence21 = dto.DrugLicence21.Trim();
        s.RegisteredPharmacistName = dto.RegisteredPharmacistName.Trim();
        s.RegisteredPharmacistRegNo = dto.RegisteredPharmacistRegNo.Trim();

        _audit.Record("SettingsUpdated", nameof(PharmacySettings), s.Id, new { s.Name, s.Gstin });
        await _db.SaveChangesAsync();

        return Ok(PharmacySettingsDto.From(s));
    }
}

public static class SettingsLoader
{
    /// <summary>The single settings row; created with placeholders if it is missing (e.g. a test database).</summary>
    public static async Task<PharmacySettings> LoadAsync(PharmacyDbContext db)
    {
        var s = await db.PharmacySettings.FindAsync(PharmacySettings.SingletonId);
        if (s is not null) return s;

        s = new PharmacySettings
        {
            Name = "Your Pharmacy Name",
            Address = "Shop address, City, State, PIN",
            StateCode = "33",
            Gstin = "33AAAAA0000A1Z5",
            DrugLicence20 = "DL-20-XXXX",
            DrugLicence21 = "DL-21-XXXX",
            RegisteredPharmacistName = "Registered Pharmacist",
            RegisteredPharmacistRegNo = "REG-XXXX"
        };
        db.PharmacySettings.Add(s);
        await db.SaveChangesAsync();
        return s;
    }
}
