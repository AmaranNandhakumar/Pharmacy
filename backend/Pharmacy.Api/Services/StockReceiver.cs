using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Services;

/// <summary>One delivery line: a batch of a medicine arriving from a supplier.</summary>
public record ReceiptRequest(
    int MedicineId, string BatchNumber, DateOnly ExpiryDate, decimal Mrp, decimal SellingPrice, decimal PurchaseRate,
    int Quantity, string? SupplierName, string? SupplierInvoiceNo, string? ReferenceId);

/// <summary>A delivery line that can't be accepted. <see cref="IsConflict"/> means it clashes with stock already on the shelf.</summary>
public class ReceiptException : Exception
{
    public bool IsConflict { get; }
    public ReceiptException(string message, bool isConflict = false) : base(message) => IsConflict = isConflict;
}

/// <summary>
/// The rules for putting stock on the shelf, shared by plain receipts and receipts against a purchase order.
/// Adds to the unit of work only; the caller saves (so a whole delivery saves together).
/// </summary>
public class StockReceiver
{
    private readonly PharmacyDbContext _db;
    private readonly TimeProvider _time;

    public StockReceiver(PharmacyDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<Batch> ReceiveAsync(ReceiptRequest r, int userId)
    {
        var medicine = await _db.Medicines.FindAsync(r.MedicineId);
        if (medicine is null || !medicine.IsActive)
            throw new ReceiptException("Medicine not found or no longer active.");

        var priceError = StockRules.ValidatePrices(r.Mrp, r.SellingPrice, r.PurchaseRate);
        if (priceError is not null) throw new ReceiptException(priceError);

        if (r.ExpiryDate <= _time.LocalToday())
            throw new ReceiptException("This batch has already expired and can't be received.");
        if (r.Quantity <= 0)
            throw new ReceiptException("Quantity must be greater than zero.");

        var batchNumber = r.BatchNumber.Trim().ToUpperInvariant();
        // Look at batches added earlier in this same delivery before going to the database
        var batch = _db.Batches.Local.FirstOrDefault(b => b.MedicineId == medicine.Id && b.BatchNumber == batchNumber)
            ?? await _db.Batches.FirstOrDefaultAsync(b => b.MedicineId == medicine.Id && b.BatchNumber == batchNumber);

        if (batch is null)
        {
            batch = new Batch
            {
                Medicine = medicine,
                MedicineId = medicine.Id,
                BatchNumber = batchNumber,
                ExpiryDate = r.ExpiryDate,
                Mrp = r.Mrp,
                SellingPrice = r.SellingPrice,
                PurchaseRate = r.PurchaseRate,
                SupplierName = Clean(r.SupplierName),
                SupplierInvoiceNo = Clean(r.SupplierInvoiceNo)
            };
            _db.Batches.Add(batch);
        }
        else if (batch.ExpiryDate != r.ExpiryDate || batch.Mrp != r.Mrp)
        {
            throw new ReceiptException(
                $"Batch {batchNumber} of {medicine.Name} is already in stock with a different expiry date or MRP. Check the pack.", isConflict: true);
        }

        StockRules.Apply(batch, r.Quantity, StockMovementType.Receipt, userId, referenceId: Clean(r.ReferenceId) ?? Clean(r.SupplierInvoiceNo));
        return batch;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
