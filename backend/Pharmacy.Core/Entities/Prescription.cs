namespace Pharmacy.Core.Entities;

/// <summary>A prescription from a registered medical practitioner, as entered at the counter.</summary>
public class Prescription
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public string PrescriberName { get; set; } = string.Empty;

    /// <summary>NMC or state medical council registration number.</summary>
    public string PrescriberRegNo { get; set; } = string.Empty;
    public string? PrescriberAddress { get; set; }
    public DateOnly IssuedOn { get; set; }

    /// <summary>Schedule X needs the prescription in duplicate, with one copy kept by the pharmacy.</summary>
    public bool CopyRetained { get; set; }

    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Entered;
    public string? RejectReason { get; set; }

    public int EnteredById { get; set; }
    public int? VerifiedById { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int? DispensedById { get; set; }
    public DateTime? DispensedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}
