namespace Pharmacy.Core.Entities;

/// <summary>A distributor or wholesaler the pharmacy buys stock from.</summary>
public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>Supplier's GSTIN, needed to claim input tax credit on their invoices.</summary>
    public string? Gstin { get; set; }

    /// <summary>Wholesale drug licence (Form 20B / 21B). A pharmacy must buy only from licensed wholesalers.</summary>
    public string? DrugLicenceNo { get; set; }

    // Soft delete: purchase orders keep pointing at deactivated suppliers
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
