namespace Pharmacy.Core.Entities;

/// <summary>One medicine on a prescription.</summary>
public class PrescriptionItem
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public Prescription Prescription { get; set; } = null!;

    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    /// <summary>e.g. "1 tablet", "5 ml".</summary>
    public string Dose { get; set; } = string.Empty;

    /// <summary>Units handed over per fill.</summary>
    public int Quantity { get; set; }

    /// <summary>e.g. "Twice a day after food for 5 days".</summary>
    public string Directions { get; set; } = string.Empty;

    /// <summary>Extra fills allowed after the first one.</summary>
    public int RefillsAllowed { get; set; }
    public int RefillsUsed { get; set; }

    /// <summary>Total units handed over across all fills.</summary>
    public int QuantityDispensed { get; set; }

    public bool HasRefillsLeft => RefillsUsed < RefillsAllowed;
}
