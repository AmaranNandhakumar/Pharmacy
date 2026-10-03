using System.ComponentModel.DataAnnotations;
using Pharmacy.Core.Entities;

namespace Pharmacy.Api.DTOs;

public class CreateSaleDto
{
    /// <summary>Registered patient, if any. Filled in automatically when billing a prescription.</summary>
    public int? PatientId { get; set; }

    [MaxLength(100)]
    public string? CustomerName { get; set; }

    [Required, EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; set; }

    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }

    /// <summary>OTC and Schedule G medicines picked off the shelf (stock is taken FEFO).</summary>
    [MaxLength(50)]
    public List<SaleItemRequestDto> Items { get; set; } = new();

    /// <summary>Dispensed prescription fills to bill (their stock already left at dispense).</summary>
    [MaxLength(10)]
    public List<int> PrescriptionFillIds { get; set; } = new();
}

public class SaleItemRequestDto
{
    [Range(1, int.MaxValue)]
    public int MedicineId { get; set; }

    [Range(1, 10_000)]
    public int Quantity { get; set; }
}

public class VoidSaleDto
{
    [Required, MinLength(3), MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class SaleSummaryDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int? PatientId { get; set; }
    public string? CustomerName { get; set; }
    public int ItemCount { get; set; }
    public decimal Total { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public SaleStatus Status { get; set; }

    public static SaleSummaryDto From(Sale s) => new()
    {
        Id = s.Id,
        InvoiceNo = s.InvoiceNo,
        CreatedAt = s.CreatedAt,
        PatientId = s.PatientId,
        CustomerName = s.Patient?.FullName ?? s.CustomerName,
        ItemCount = s.Items.Count,
        Total = s.Total,
        PaymentMethod = s.PaymentMethod,
        Status = s.Status
    };
}

public class SaleItemDto
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public int? PrescriptionItemId { get; set; }
    public string HsnCode { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal GstRatePercent { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>Tax per GST rate, as printed at the foot of an Indian invoice.</summary>
public class GstSummaryDto
{
    public decimal GstRatePercent { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
}

public class SaleDto : SaleSummaryDto
{
    public string? PatientAddress { get; set; }
    public string? BilledByName { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public string? VoidedByName { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
    public List<SaleItemDto> Items { get; set; } = new();
    public List<GstSummaryDto> GstSummary { get; set; } = new();

    /// <summary>The pharmacy's details for the invoice header.</summary>
    public PharmacySettingsDto Pharmacy { get; set; } = new();
}

public class BillableFillDto
{
    public int FillId { get; set; }
    public int PrescriptionId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PrescriberName { get; set; } = string.Empty;
    public bool IsRefill { get; set; }
    public DateTime DispensedAt { get; set; }
    public decimal Total { get; set; }
    public List<BillableFillLineDto> Lines { get; set; } = new();
}

public class BillableFillLineDto
{
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ScheduleRegisterEntryDto
{
    public long Id { get; set; }
    public DrugSchedule Schedule { get; set; }
    public DateTime CreatedAt { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string? PatientAddress { get; set; }
    public string PrescriberName { get; set; } = string.Empty;
    public string PrescriberRegNo { get; set; } = string.Empty;
    public string? PrescriberAddress { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? PharmacistName { get; set; }
}

public class PharmacySettingsDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required, RegularExpression(@"^\d{2}$", ErrorMessage = "State code is 2 digits, e.g. 33 for Tamil Nadu.")]
    public string StateCode { get; set; } = string.Empty;

    /// <summary>15 characters: state code, PAN, entity number, Z, check character.</summary>
    [Required, RegularExpression(@"^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", ErrorMessage = "GSTIN must be 15 characters, e.g. 33ABCDE1234F1Z5.")]
    public string Gstin { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string DrugLicence20 { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string DrugLicence21 { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string RegisteredPharmacistName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string RegisteredPharmacistRegNo { get; set; } = string.Empty;

    public static PharmacySettingsDto From(PharmacySettings s) => new()
    {
        Name = s.Name,
        Address = s.Address,
        Phone = s.Phone,
        StateCode = s.StateCode,
        Gstin = s.Gstin,
        DrugLicence20 = s.DrugLicence20,
        DrugLicence21 = s.DrugLicence21,
        RegisteredPharmacistName = s.RegisteredPharmacistName,
        RegisteredPharmacistRegNo = s.RegisteredPharmacistRegNo
    };
}
