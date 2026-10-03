namespace Pharmacy.Core.Entities;

/// <summary>
/// One hand-over of a prescription (the first fill or a refill): which batches went out, and the
/// sale that billed them. Stock leaves the shelf at dispense; the counter bills the fill afterwards.
/// </summary>
public class PrescriptionFill
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public Prescription Prescription { get; set; } = null!;

    public bool IsRefill { get; set; }
    public int DispensedById { get; set; }
    public DateTime DispensedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The sale that billed this fill; null until it is billed at the counter.</summary>
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }

    public ICollection<PrescriptionFillLine> Lines { get; set; } = new List<PrescriptionFillLine>();
}

/// <summary>Units of one prescription item taken from one batch during a fill.</summary>
public class PrescriptionFillLine
{
    public int Id { get; set; }
    public int PrescriptionFillId { get; set; }
    public PrescriptionFill Fill { get; set; } = null!;

    public int PrescriptionItemId { get; set; }
    public PrescriptionItem PrescriptionItem { get; set; } = null!;

    public int BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public int Quantity { get; set; }
}
