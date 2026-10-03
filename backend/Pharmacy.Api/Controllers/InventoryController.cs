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
public class InventoryController : ControllerBase
{
    private const string StockManagers = nameof(UserRole.Admin) + "," + nameof(UserRole.Pharmacist);

    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;
    private readonly TimeProvider _time;

    public InventoryController(PharmacyDbContext db, IAuditService audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    [HttpGet("batches")]
    public async Task<ActionResult<List<BatchDto>>> GetBatches([FromQuery] int? medicineId, [FromQuery] bool includeEmpty = false)
    {
        var query = _db.Batches.Include(b => b.Medicine).AsNoTracking();

        if (medicineId is not null)
            query = query.Where(b => b.MedicineId == medicineId);
        if (!includeEmpty)
            query = query.Where(b => b.QuantityOnHand > 0);

        var batches = await query.OrderBy(b => b.ExpiryDate).ToListAsync();
        var today = Today;
        return Ok(batches.Select(b => BatchDto.From(b, today)).ToList());
    }

    [HttpGet("batches/{id:int}/movements")]
    [Authorize(Roles = StockManagers)]
    public async Task<ActionResult<List<StockMovementDto>>> GetMovements(int id)
    {
        if (!await _db.Batches.AnyAsync(b => b.Id == id)) return NotFound();

        var movements = await _db.StockMovements.AsNoTracking()
            .Where(m => m.BatchId == id)
            .OrderBy(m => m.Id)
            .Select(m => new StockMovementDto
            {
                Id = m.Id,
                BatchId = m.BatchId,
                Quantity = m.Quantity,
                Type = m.Type,
                Reason = m.Reason,
                ReferenceId = m.ReferenceId,
                UserId = m.UserId,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return Ok(movements);
    }

    /// <summary>
    /// Records stock arriving from a supplier. A new batch number creates a batch; a known one
    /// adds to it, as long as the expiry date and MRP match what is already on the shelf.
    /// </summary>
    [HttpPost("receipts")]
    public async Task<ActionResult<BatchDto>> Receive(ReceiveStockDto dto)
    {
        var medicine = await _db.Medicines.FindAsync(dto.MedicineId);
        if (medicine is null || !medicine.IsActive)
            return BadRequest(new { message = "Medicine not found or no longer active." });

        var priceError = StockRules.ValidatePrices(dto.Mrp, dto.SellingPrice, dto.PurchaseRate);
        if (priceError is not null) return BadRequest(new { message = priceError });

        var expiry = dto.ExpiryDate!.Value;
        if (expiry <= Today)
            return BadRequest(new { message = "This batch has already expired and can't be received." });

        var batchNumber = dto.BatchNumber.Trim().ToUpperInvariant();
        var batch = await _db.Batches.FirstOrDefaultAsync(b => b.MedicineId == medicine.Id && b.BatchNumber == batchNumber);

        if (batch is null)
        {
            batch = new Batch
            {
                Medicine = medicine,
                BatchNumber = batchNumber,
                ExpiryDate = expiry,
                Mrp = dto.Mrp,
                SellingPrice = dto.SellingPrice,
                PurchaseRate = dto.PurchaseRate,
                SupplierName = dto.SupplierName?.Trim(),
                SupplierInvoiceNo = dto.SupplierInvoiceNo?.Trim()
            };
            _db.Batches.Add(batch);
        }
        else if (batch.ExpiryDate != expiry || batch.Mrp != dto.Mrp)
        {
            return Conflict(new { message = $"Batch {batchNumber} is already in stock with a different expiry date or MRP. Check the pack." });
        }

        StockRules.Apply(batch, dto.Quantity, StockMovementType.Receipt, User.GetUserId(),
            referenceId: string.IsNullOrWhiteSpace(dto.SupplierInvoiceNo) ? null : dto.SupplierInvoiceNo.Trim());

        if (!await TrySave()) return StockChangedConflict();

        _audit.Record("StockReceived", nameof(Batch), batch.Id, new { medicine.Name, batch.BatchNumber, dto.Quantity, dto.SupplierInvoiceNo });
        await _db.SaveChangesAsync();

        return Ok(BatchDto.From(batch, Today));
    }

    /// <summary>Corrects stock for damage, loss or a count mismatch. Always needs a reason.</summary>
    [HttpPost("adjustments")]
    [Authorize(Roles = StockManagers)]
    public async Task<ActionResult<BatchDto>> Adjust(AdjustStockDto dto)
    {
        var batch = await _db.Batches.Include(b => b.Medicine).FirstOrDefaultAsync(b => b.Id == dto.BatchId);
        if (batch is null) return NotFound();

        try
        {
            StockRules.Apply(batch, dto.QuantityChange, StockMovementType.Adjustment, User.GetUserId(), reason: dto.Reason.Trim());
        }
        catch (StockRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _audit.Record("StockAdjusted", nameof(Batch), batch.Id,
            new { batch.Medicine.Name, batch.BatchNumber, dto.QuantityChange, Reason = dto.Reason.Trim() });

        if (!await TrySave()) return StockChangedConflict();

        return Ok(BatchDto.From(batch, Today));
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<StockAlertsDto>> GetAlerts([FromQuery] int expiringWithinDays = 90)
    {
        if (expiringWithinDays is < 1 or > 365)
            return BadRequest(new { message = "expiringWithinDays must be between 1 and 365." });

        var today = Today;
        var horizon = today.AddDays(expiringWithinDays);
        var medicines = await _db.Medicines.Include(m => m.Batches).AsNoTracking().Where(m => m.IsActive).ToListAsync();

        var stocked = medicines.SelectMany(m => m.Batches).Where(b => b.QuantityOnHand > 0).ToList();

        return Ok(new StockAlertsDto
        {
            ExpiringWithinDays = expiringWithinDays,
            LowStock = medicines
                .Select(m => new { m, qty = StockRules.SellableQuantity(m.Batches, today) })
                .Where(x => x.qty <= x.m.ReorderLevel)
                .OrderBy(x => x.qty).ThenBy(x => x.m.Name)
                .Select(x => new LowStockItemDto
                {
                    MedicineId = x.m.Id,
                    Name = x.m.Name,
                    Strength = x.m.Strength,
                    Form = x.m.Form,
                    SellableQuantity = x.qty,
                    ReorderLevel = x.m.ReorderLevel
                })
                .ToList(),
            ExpiringSoon = stocked
                .Where(b => !b.IsExpired(today) && b.ExpiryDate <= horizon)
                .OrderBy(b => b.ExpiryDate)
                .Select(b => BatchDto.From(b, today))
                .ToList(),
            Expired = stocked
                .Where(b => b.IsExpired(today))
                .OrderBy(b => b.ExpiryDate)
                .Select(b => BatchDto.From(b, today))
                .ToList()
        });
    }

    private async Task<bool> TrySave()
    {
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private ConflictObjectResult StockChangedConflict() =>
        Conflict(new { message = "Stock for this batch changed while you were saving. Reload and try again." });
}
