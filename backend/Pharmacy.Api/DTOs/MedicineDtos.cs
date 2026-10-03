using System.ComponentModel.DataAnnotations;
using Pharmacy.Core.Entities;

namespace Pharmacy.Api.DTOs;

public class MedicineUpsertDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? GenericName { get; set; }

    [MaxLength(50)]
    public string? Strength { get; set; }

    [Required, MaxLength(50)]
    public string Form { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? PackSize { get; set; }

    [MaxLength(200)]
    public string? Manufacturer { get; set; }

    [MaxLength(50)]
    public string? Barcode { get; set; }

    [Required, EnumDataType(typeof(DrugSchedule))]
    public DrugSchedule Schedule { get; set; }

    /// <summary>4, 6 or 8 digit HSN code; medicines are usually 3003 or 3004.</summary>
    [Required, RegularExpression(@"^\d{4}(\d{2}){0,2}$", ErrorMessage = "HSN code must be 4, 6 or 8 digits.")]
    public string HsnCode { get; set; } = string.Empty;

    public decimal GstRatePercent { get; set; }

    [Range(0, 1_000_000)]
    public int ReorderLevel { get; set; }
}

public class MedicineDto
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
    public bool RequiresPrescription { get; set; }
    public string HsnCode { get; set; } = string.Empty;
    public decimal GstRatePercent { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Units on hand in batches that haven't expired.</summary>
    public int SellableQuantity { get; set; }
    public DateOnly? NearestExpiry { get; set; }

    public static MedicineDto From(Medicine m, DateOnly today)
    {
        var live = m.Batches.Where(b => b.QuantityOnHand > 0 && !b.IsExpired(today)).ToList();
        return new()
        {
            Id = m.Id,
            Name = m.Name,
            GenericName = m.GenericName,
            Strength = m.Strength,
            Form = m.Form,
            PackSize = m.PackSize,
            Manufacturer = m.Manufacturer,
            Barcode = m.Barcode,
            Schedule = m.Schedule,
            RequiresPrescription = m.RequiresPrescription,
            HsnCode = m.HsnCode,
            GstRatePercent = m.GstRatePercent,
            ReorderLevel = m.ReorderLevel,
            IsActive = m.IsActive,
            SellableQuantity = live.Sum(b => b.QuantityOnHand),
            NearestExpiry = live.Count == 0 ? null : live.Min(b => b.ExpiryDate)
        };
    }
}

public class MedicineDetailDto : MedicineDto
{
    public List<BatchDto> Batches { get; set; } = new();
}
