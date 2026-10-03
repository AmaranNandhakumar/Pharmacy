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
public class SuppliersController : ControllerBase
{
    private const string Buyers = nameof(UserRole.Admin) + "," + nameof(UserRole.Pharmacist);

    private static readonly PurchaseOrderStatus[] OpenStatuses =
        { PurchaseOrderStatus.Draft, PurchaseOrderStatus.Ordered, PurchaseOrderStatus.PartiallyReceived };

    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;

    public SuppliersController(PharmacyDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<List<SupplierDto>>> GetAll([FromQuery] string? search, [FromQuery] bool includeInactive = false)
    {
        var query = _db.Suppliers.AsNoTracking();
        if (!includeInactive) query = query.Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(s => EF.Functions.Like(s.Name, pattern) || (s.Phone != null && EF.Functions.Like(s.Phone, pattern)));
        }

        var suppliers = await query.OrderBy(s => s.Name).ToListAsync();
        var open = await _db.PurchaseOrders.Where(p => OpenStatuses.Contains(p.Status))
            .GroupBy(p => p.SupplierId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return Ok(suppliers.Select(s => SupplierDto.From(s, open.GetValueOrDefault(s.Id))).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierDto>> GetById(int id)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        var open = await _db.PurchaseOrders.CountAsync(p => p.SupplierId == id && OpenStatuses.Contains(p.Status));
        return Ok(SupplierDto.From(supplier, open));
    }

    [HttpPost]
    [Authorize(Roles = Buyers)]
    public async Task<ActionResult<SupplierDto>> Create(SupplierUpsertDto dto)
    {
        if (await NameTaken(dto.Name, null)) return Conflict(new { message = "A supplier with this name already exists." });

        var supplier = new Supplier();
        Apply(dto, supplier);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        _audit.Record("SupplierCreated", nameof(Supplier), supplier.Id, new { supplier.Name });
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, SupplierDto.From(supplier));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Buyers)]
    public async Task<ActionResult<SupplierDto>> Update(int id, SupplierUpsertDto dto)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        if (await NameTaken(dto.Name, id)) return Conflict(new { message = "A supplier with this name already exists." });

        Apply(dto, supplier);
        _audit.Record("SupplierUpdated", nameof(Supplier), supplier.Id, new { supplier.Name });
        await _db.SaveChangesAsync();

        return Ok(SupplierDto.From(supplier));
    }

    /// <summary>Soft delete. Refused while the supplier still has open purchase orders.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Buyers)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        if (await _db.PurchaseOrders.AnyAsync(p => p.SupplierId == id && OpenStatuses.Contains(p.Status)))
            return BadRequest(new { message = "Close or cancel this supplier's open purchase orders first." });

        supplier.IsActive = false;
        _audit.Record("SupplierDeactivated", nameof(Supplier), supplier.Id);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Task<bool> NameTaken(string name, int? exceptId)
    {
        var trimmed = name.Trim();
        return _db.Suppliers.AnyAsync(s => s.Name == trimmed && s.Id != exceptId);
    }

    private static void Apply(SupplierUpsertDto dto, Supplier s)
    {
        s.Name = dto.Name.Trim();
        s.ContactPerson = Clean(dto.ContactPerson);
        s.Phone = Clean(dto.Phone);
        s.Email = Clean(dto.Email)?.ToLowerInvariant();
        s.Address = Clean(dto.Address);
        s.Gstin = Clean(dto.Gstin)?.ToUpperInvariant();
        s.DrugLicenceNo = Clean(dto.DrugLicenceNo);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
