using System.ComponentModel.DataAnnotations;
using Pharmacy.Core.Entities;

namespace Pharmacy.Api.DTOs;

public class PatientUpsertDto
{
    [Required, MinLength(2), MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Indian mobile number, optionally with +91.</summary>
    [RegularExpression(@"^(\+91[\- ]?)?[6-9]\d{9}$", ErrorMessage = "Phone must be a 10-digit Indian mobile number.")]
    public string? Phone { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? Allergies { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>The patient agreed to the pharmacy keeping their details. Required when creating a patient.</summary>
    public bool ConsentGiven { get; set; }
}

public class PatientDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public int Age { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Allergies { get; set; }
    public string? Notes { get; set; }
    public DateTime ConsentGivenAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public static int AgeOn(DateOnly dob, DateOnly today)
    {
        var age = today.Year - dob.Year;
        return dob > today.AddYears(-age) ? age - 1 : age;
    }

    public static T Fill<T>(T dto, Patient p, DateOnly today) where T : PatientDto
    {
        dto.Id = p.Id;
        dto.FullName = p.FullName;
        dto.DateOfBirth = p.DateOfBirth;
        dto.Age = AgeOn(p.DateOfBirth, today);
        dto.Phone = p.Phone;
        dto.Address = p.Address;
        dto.Allergies = p.Allergies;
        dto.Notes = p.Notes;
        dto.ConsentGivenAt = p.ConsentGivenAt;
        dto.CreatedAt = p.CreatedAt;
        return dto;
    }

    public static PatientDto From(Patient p, DateOnly today) => Fill(new PatientDto(), p, today);
}

public class PatientDetailDto : PatientDto
{
    public List<PrescriptionSummaryDto> Prescriptions { get; set; } = new();
}
