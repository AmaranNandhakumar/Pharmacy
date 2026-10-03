namespace Pharmacy.Core.Entities;

/// <summary>Draft → Ordered → PartiallyReceived → Received; or Cancelled, or Closed short when the rest won't come.</summary>
public enum PurchaseOrderStatus
{
    Draft,
    Ordered,
    PartiallyReceived,
    Received,
    Closed,
    Cancelled
}

/// <summary>An order for stock sent to a supplier, received in one or more deliveries.</summary>
public class PurchaseOrder
{
    public int Id { get; set; }

    /// <summary>Sequential per financial year, e.g. PO/2026-27/0001.</summary>
    public string PoNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public string? Notes { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OrderedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public class PurchaseOrderLine
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    /// <summary>Units ordered (the smallest dispensable unit, as everywhere else).</summary>
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }

    /// <summary>Agreed purchase rate per unit, if known; the actual rate is entered at delivery.</summary>
    public decimal? ExpectedRate { get; set; }

    public int QuantityOutstanding => Math.Max(0, QuantityOrdered - QuantityReceived);
}
