namespace Pharmacy.Core.Entities;

/// <summary>The pharmacy's own details, printed on every invoice. There is exactly one row (Id 1).</summary>
public class PharmacySettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>Two-digit GST state code, e.g. 33 for Tamil Nadu.</summary>
    public string StateCode { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;

    /// <summary>Retail drug licence numbers: Form 20 (non-biological) and Form 21 (biological).</summary>
    public string DrugLicence20 { get; set; } = string.Empty;
    public string DrugLicence21 { get; set; } = string.Empty;

    public string RegisteredPharmacistName { get; set; } = string.Empty;
    public string RegisteredPharmacistRegNo { get; set; } = string.Empty;
}
