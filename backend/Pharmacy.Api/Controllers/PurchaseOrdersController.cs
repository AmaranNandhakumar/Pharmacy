using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Api.DTOs;
using Pharmacy.Api.Services;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Buying stock: draft a purchase order (often from the low-stock list), send it, then receive deliveries
/// against it. Any staff member can receive a delivery; only Admins and Pharmacists raise or change orders.
/// </summary>
[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private const string Buyers = nameof(UserRole.Admin) + "," + nameof(UserRole.Pharmacist);

    private static readonly PurchaseOrderStatus[] OnOrderStatuses = { PurchaseOrderStatus.Ordered, PurchaseOrderStatus.PartiallyReceived };

    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;
    private readonly StockReceiver _receiver;

    public PurchaseOrdersController(PharmacyDbContext db, IAuditService audit, TimeProvider time, StockReceiver receiver)
    {
        _db = db;
        _audit = audit;
        _time = time;
        _receiver = receiver;
    }

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    [HttpGet]
    public async Task<ActionResult<List<PurchaseOrderSummaryDto>>> GetAll([FromQuery] PurchaseOrderStatus? status, [FromQuery] int? supplierId)
    {
        var query = _db.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Lines).AsNoTracking();
        if (status is not null) query = query.Where(p => p.Status == status);
        if (supplierId is not null) query = query.Where(p => p.SupplierId == supplierId);

        var list = await query.OrderByDescending(p => p.Id).Take(200).ToListAsync();
        return Ok(list.Select(PurchaseOrderSummaryDto.From).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PurchaseOrderDto>> GetById(int id)
    {
        var po = await Load(id, tracking: false);
        return po is null ? NotFound() : Ok(await ToDto(po));
    }

    /// <summary>
    /// Medicines at or below their reorder level, with a suggested quantity (up to twice the reorder level,
    /// less what's already on order) and the supplier of their latest batch.
    /// </summary>
    [HttpGet("suggestions")]
    [Authorize(Roles = Buyers)]
    public async Task<ActionResult<List<ReorderSuggestionDto>>> Suggestions()
    {
        var today = _time.LocalToday();
        var medicines = await _db.Medicines.Include(m => m.Batches).AsNoTracking().Where(m => m.IsActive).ToListAsync();
        var onOrder = await _db.PurchaseOrderLines
            .Where(l => OnOrderStatuses.Contains(l.PurchaseOrder.Status))
            .Select(l => new { l.MedicineId, l.QuantityOrdered, l.QuantityReceived })
            .ToListAsync();
        var suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).ToListAsync();

        var suggestions = new List<ReorderSuggestionDto>();
        foreach (var m in medicines)
        {
            var sellable = StockRules.SellableQuantity(m.Batches, today);
            var pending = onOrder.Where(o => o.MedicineId == m.Id).Sum(o => Math.Max(0, o.QuantityOrdered - o.QuantityReceived));
            var qty = PurchaseOrderRules.SuggestQuantity(sellable, m.ReorderLevel, pending);
            if (qty == 0) continue;

            var latest = m.Batches.OrderByDescending(b => b.ReceivedAt).FirstOrDefault();
            var supplier = latest?.SupplierName is null
                ? null
                : suppliers.FirstOrDefault(s => string.Equals(s.Name, latest.SupplierName, StringComparison.OrdinalIgnoreCase));

            suggestions.Add(new ReorderSuggestionDto
            {
                MedicineId = m.Id,
                Name = m.Name,
                Strength = m.Strength,
                Form = m.Form,
                SellableQuantity = sellable,
                ReorderLevel = m.ReorderLevel,
                OnOrder = pending,
                SuggestedQuantity = qty,
                LastSupplierId = supplier?.Id,
                LastSupplierName = supplier?.Name ?? latest?.SupplierName,
                LastPurchaseRate = latest?.PurchaseRate
            });
        }

        return Ok(suggestions.OrderBy(s => s.LastSupplierName ?? "~").ThenBy(s => s.Name).ToList());
    }

    [HttpPost]
    [Authorize(Roles = Buyers)]
    public async Task<ActionResult<PurchaseOrderDto>> Create(PurchaseOrderUpsertDto dto)
    {
        var po = new PurchaseOrder { CreatedById = User.GetUserId(), CreatedAt = UtcNow };
        var error = await ApplyAsync(dto, po);
        if (error is not null) return error;

        _db.PurchaseOrders.Add(po);
        if (!await SaveWithNumberAsync(po)) return Conflict(new { message = "Could not number the purchase order; try again." });

        _audit.Record("PurchaseOrderCreated", nameof(PurchaseOrder), po.Id, new { po.PoNumber, po.SupplierId, Lines = po.Lines.Count });
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = po.Id }, await ToDto((await Load(po.Id, tracking: false))!));
    }

    /// <summary>Changes a draft (supplier, notes, lines). Ordered purchase orders can't be changed.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Buyers)]
    public async Task<ActionResult<PurchaseOrderDto>> Update(int id, PurchaseOrderUpsertDto dto)
    {
        var po = await Load(id, tracking: true);
        if (po is null) return NotFound();
        if (!PurchaseOrderRules.IsEditable(po))
            return BadRequest(new { message = $"Only a draft can be changed; this one is {po.Status}." });

        _db.PurchaseOrderLines.RemoveRange(po.Lines);
        po.Lines.Clear();
        var error = await ApplyAsync(dto, po);
        if (error is not null) return error;

        _audit.Record("PurchaseOrderUpdated", nameof(PurchaseOrder), po.Id, new { po.PoNumber, Lines = po.Lines.Count });
        await _db.SaveChangesAsync();

        return Ok(await ToDto((await Load(po.Id, tracking: false))!));
    }

    [HttpPost("{id:int}/order")]
    [Authorize(Roles = Buyers)]
    public Task<ActionResult<PurchaseOrderDto>> MarkOrdered(int id) =>
        Transition(id, "PurchaseOrderSent", po => PurchaseOrderRules.MarkOrdered(po, UtcNow), null);

    /// <summary>Stops waiting for the rest: Closed if something arrived, Cancelled if nothing did.</summary>
    [HttpPost("{id:int}/close")]
    [Authorize(Roles = Buyers)]
    public Task<ActionResult<PurchaseOrderDto>> Close(int id, CloseOrderDto dto) =>
        Transition(id, "PurchaseOrderClosed", po => PurchaseOrderRules.Close(po, UtcNow), dto.Reason?.Trim());

    /// <summary>
    /// Records a delivery against the order: each line becomes stock in a batch (same rules as a plain
    /// receipt) and counts toward what was ordered. The whole delivery saves together or not at all.
    /// </summary>
    [HttpPost("{id:int}/receive")]
    public async Task<ActionResult<PurchaseOrderDto>> Receive(int id, ReceiveAgainstPoDto dto)
    {
        var po = await Load(id, tracking: true);
        if (po is null) return NotFound();

        var userId = User.GetUserId();
        var received = new List<object>();
        try
        {
            foreach (var r in dto.Lines)
            {
                var line = po.Lines.FirstOrDefault(l => l.Id == r.LineId)
                    ?? throw new PurchaseOrderRuleException($"Line {r.LineId} isn't on this purchase order.");

                PurchaseOrderRules.RecordReceipt(po, line, r.Quantity);
                var batch = await _receiver.ReceiveAsync(new ReceiptRequest(line.MedicineId, r.BatchNumber, r.ExpiryDate!.Value,
                    r.Mrp, r.SellingPrice, r.PurchaseRate, r.Quantity, po.Supplier.Name, dto.SupplierInvoiceNo, po.PoNumber), userId);
                received.Add(new { line.Medicine.Name, batch.BatchNumber, r.Quantity });
            }
        }
        catch (PurchaseOrderRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ReceiptException ex)
        {
            return ex.IsConflict ? Conflict(new { message = ex.Message }) : BadRequest(new { message = ex.Message });
        }

        PurchaseOrderRules.UpdateStatusAfterReceipt(po, UtcNow);
        _audit.Record("PurchaseOrderReceived", nameof(PurchaseOrder), po.Id,
            new { po.PoNumber, SupplierInvoiceNo = dto.SupplierInvoiceNo.Trim(), Status = po.Status.ToString(), Lines = received });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Stock changed while saving the delivery. Nothing was saved; try again." });
        }

        return Ok(await ToDto((await Load(po.Id, tracking: false))!));
    }

    private async Task<ActionResult<PurchaseOrderDto>> Transition(int id, string auditAction, Action<PurchaseOrder> change, string? reason)
    {
        var po = await Load(id, tracking: true);
        if (po is null) return NotFound();

        try
        {
            change(po);
        }
        catch (PurchaseOrderRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _audit.Record(auditAction, nameof(PurchaseOrder), po.Id, new { po.PoNumber, Status = po.Status.ToString(), Reason = reason });
        await _db.SaveChangesAsync();
        return Ok(await ToDto(po));
    }

    private async Task<ActionResult?> ApplyAsync(PurchaseOrderUpsertDto dto, PurchaseOrder po)
    {
        var supplier = await _db.Suppliers.FindAsync(dto.SupplierId);
        if (supplier is null || !supplier.IsActive) return BadRequest(new { message = "Supplier not found or no longer active." });

        var ids = dto.Lines.Select(l => l.MedicineId).Distinct().ToList();
        if (ids.Count != dto.Lines.Count) return BadRequest(new { message = "Each medicine can appear only once on a purchase order." });

        var medicines = await _db.Medicines.Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        foreach (var line in dto.Lines)
        {
            if (!medicines.TryGetValue(line.MedicineId, out var m) || !m.IsActive)
                return BadRequest(new { message = $"Medicine {line.MedicineId} not found or no longer active." });
            po.Lines.Add(new PurchaseOrderLine { Medicine = m, QuantityOrdered = line.Quantity, ExpectedRate = line.ExpectedRate });
        }

        po.Supplier = supplier;
        po.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        return null;
    }

    /// <summary>Numbers a new order PO/2026-27/0001, PO/2026-27/0002, ... and saves it, retrying if another took the number.</summary>
    private async Task<bool> SaveWithNumberAsync(PurchaseOrder po)
    {
        var prefix = $"PO/{GstCalculator.FinancialYear(_time.LocalToday())}/";
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var last = await _db.PurchaseOrders.Where(p => p.PoNumber.StartsWith(prefix))
                .OrderByDescending(p => p.PoNumber).Select(p => p.PoNumber).FirstOrDefaultAsync();
            var next = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
            po.PoNumber = PurchaseOrderRules.PoNumber(prefix[3..^1], next);

            try
            {
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                if (!await _db.PurchaseOrders.AsNoTracking().AnyAsync(p => p.PoNumber == po.PoNumber)) throw;
            }
        }
        return false;
    }

    private Task<PurchaseOrder?> Load(int id, bool tracking)
    {
        IQueryable<PurchaseOrder> query = _db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Lines).ThenInclude(l => l.Medicine).ThenInclude(m => m.Batches);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(p => p.Id == id);
    }

    private async Task<PurchaseOrderDto> ToDto(PurchaseOrder po)
    {
        var createdBy = await _db.Users.Where(u => u.Id == po.CreatedById).Select(u => u.FullName).FirstOrDefaultAsync();
        return PurchaseOrderDto.From(po, _time.LocalToday(), createdBy);
    }
}
