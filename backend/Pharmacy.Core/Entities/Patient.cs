namespace Pharmacy.Core.Entities;

/// <summary>A customer of the pharmacy. Health data under the DPDP Act 2023, so every read and edit is audited.</summary>
public class Patient
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }

    /// <summary>Known allergies as free text, separated by commas or semicolons (e.g. "penicillin, sulfa").</summary>
    public string? Allergies { get; set; }
    public string? Notes { get; set; }

    /// <summary>When the patient agreed to the pharmacy keeping their details (DPDP consent).</summary>
    public DateTime ConsentGivenAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}
