namespace Pharmacy.Core.Entities;

/// <summary>One manufacturer batch of a medicine on the shelf, with its own expiry date and printed MRP.</summary>
public class Batch
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }

    /// <summary>Maximum retail price printed on the pack (GST inclusive), per unit.</summary>
    public decimal Mrp { get; set; }

    /// <summary>Price charged per unit (GST inclusive); never above <see cref="Mrp"/>.</summary>
    public decimal SellingPrice { get; set; }

    /// <summary>What the pharmacy paid per unit, used for stock valuation.</summary>
    public decimal PurchaseRate { get; set; }

    /// <summary>Units on hand. Always the sum of this batch's <see cref="StockMovement"/> rows.</summary>
    public int QuantityOnHand { get; set; }

    public string? SupplierName { get; set; }
    public string? SupplierInvoiceNo { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Concurrency token, replaced on every stock change so two counters can't oversell.</summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();

    public bool IsExpired(DateOnly today) => ExpiryDate < today;
}
