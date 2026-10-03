using System.ComponentModel.DataAnnotations;
using Pharmacy.Core.Entities;

namespace Pharmacy.Api.DTOs;

public class CreatePrescriptionDto
{
    [Range(1, int.MaxValue)]
    public int PatientId { get; set; }

    [Required, MinLength(2), MaxLength(100)]
    public string PrescriberName { get; set; } = string.Empty;

    /// <summary>NMC or state medical council registration number.</summary>
    [Required, MinLength(3), MaxLength(50)]
    public string PrescriberRegNo { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? PrescriberAddress { get; set; }

    [Required]
    public DateOnly? IssuedOn { get; set; }

    /// <summary>The pharmacy kept a copy (required for Schedule X).</summary>
    public bool CopyRetained { get; set; }

    [Required, MinLength(1), MaxLength(20)]
    public List<CreatePrescriptionItemDto> Items { get; set; } = new();
}

public class CreatePrescriptionItemDto
{
    [Range(1, int.MaxValue)]
    public int MedicineId { get; set; }

    [Required, MaxLength(100)]
    public string Dose { get; set; } = string.Empty;

    [Range(1, 10_000)]
    public int Quantity { get; set; }

    [Required, MaxLength(300)]
    public string Directions { get; set; } = string.Empty;

    [Range(0, 12)]
    public int RefillsAllowed { get; set; }
}

public class VerifyPrescriptionDto
{
    /// <summary>The pharmacist has seen the allergy warnings and verifies anyway.</summary>
    public bool AcknowledgeAllergyWarnings { get; set; }
}

public class RejectPrescriptionDto
{
    [Required, MinLength(3), MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class PrescriptionSummaryDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PrescriberName { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public PrescriptionStatus Status { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public static PrescriptionSummaryDto From(Prescription p) => new()
    {
        Id = p.Id,
        PatientId = p.PatientId,
        PatientName = p.Patient?.FullName ?? string.Empty,
        PrescriberName = p.PrescriberName,
        IssuedOn = p.IssuedOn,
        Status = p.Status,
        ItemCount = p.Items.Count,
        CreatedAt = p.CreatedAt
    };
}

public class PrescriptionDto : PrescriptionSummaryDto
{
    public string? PatientAllergies { get; set; }
    public string PrescriberRegNo { get; set; } = string.Empty;
    public string? PrescriberAddress { get; set; }
    public bool CopyRetained { get; set; }
    public string? RejectReason { get; set; }
    public string? EnteredByName { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? DispensedByName { get; set; }
    public DateTime? DispensedAt { get; set; }
    public bool HasAllergyWarnings => Items.Any(i => i.AllergyWarnings.Count > 0);
    public List<PrescriptionItemDto> Items { get; set; } = new();
}

public class PrescriptionItemDto
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public DrugSchedule Schedule { get; set; }
    public string Dose { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Directions { get; set; } = string.Empty;
    public int RefillsAllowed { get; set; }
    public int RefillsUsed { get; set; }
    public int QuantityDispensed { get; set; }
    public int SellableQuantity { get; set; }

    /// <summary>The patient's recorded allergies that match this medicine's name.</summary>
    public List<string> AllergyWarnings { get; set; } = new();
}

public class DispensedLineDto
{
    public int PrescriptionItemId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public int Quantity { get; set; }
}

public class DispenseResultDto
{
    public PrescriptionDto Prescription { get; set; } = new();
    public bool WasRefill { get; set; }
    public List<DispensedLineDto> Lines { get; set; } = new();
}
