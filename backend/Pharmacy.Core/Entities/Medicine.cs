namespace Pharmacy.Core.Entities;

/// <summary>A product in the catalogue. Prices live on each <see cref="Batch"/>, because MRP is printed per batch.</summary>
public class Medicine
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public string Form { get; set; } = string.Empty;
    public string? PackSize { get; set; }
    public string? Manufacturer { get; set; }
    public string? Barcode { get; set; }

    public DrugSchedule Schedule { get; set; }
    public string HsnCode { get; set; } = string.Empty;
    public decimal GstRatePercent { get; set; }

    /// <summary>Alert when total sellable stock falls to this many units or fewer.</summary>
    public int ReorderLevel { get; set; }

    // Soft delete: batches and sales keep pointing at deactivated medicines
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Batch> Batches { get; set; } = new List<Batch>();

    public bool RequiresPrescription => Schedule is DrugSchedule.H or DrugSchedule.H1 or DrugSchedule.X or DrugSchedule.Ndps;
}
