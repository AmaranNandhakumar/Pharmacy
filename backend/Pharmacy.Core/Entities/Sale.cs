namespace Pharmacy.Core.Entities;

public enum PaymentMethod
{
    Cash,
    Card,
    Upi
}

public enum SaleStatus
{
    Completed,
    Voided
}

/// <summary>A GST invoice raised at the counter. Prices are GST-inclusive (MRP); tax is worked out backwards.</summary>
public class Sale
{
    public int Id { get; set; }

    /// <summary>Sequential per Indian financial year (April to March), e.g. 2026-27/000123.</summary>
    public string InvoiceNo { get; set; } = string.Empty;

    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Name printed on the bill when there is no registered patient (optional).</summary>
    public string? CustomerName { get; set; }

    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal DiscountPercent { get; set; }

    /// <summary>Sum of the lines before discount.</summary>
    public decimal GrossAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }

    /// <summary>Amount paid: <see cref="GrossAmount"/> minus <see cref="Discount"/>, GST included.</summary>
    public decimal Total { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public int? VoidedById { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}

/// <summary>One invoice line: one medicine from one batch, with its GST worked out.</summary>
public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    public int BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    /// <summary>Set when the line bills a dispensed prescription item.</summary>
    public int? PrescriptionItemId { get; set; }

    public int Quantity { get; set; }

    // Snapshots, so the invoice never changes when the catalogue does
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal UnitPrice { get; set; }
    public string HsnCode { get; set; } = string.Empty;
    public decimal GstRatePercent { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal LineTotal { get; set; }
}
