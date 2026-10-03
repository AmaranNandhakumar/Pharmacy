using System.ComponentModel.DataAnnotations;
using Pharmacy.Core.Entities;

namespace Pharmacy.Api.DTOs;

public class SupplierUpsertDto
{
    [Required, MinLength(2), MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContactPerson { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [RegularExpression(@"^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", ErrorMessage = "GSTIN must be 15 characters, e.g. 33ABCDE1234F1Z5.")]
    public string? Gstin { get; set; }

    [MaxLength(100)]
    public string? DrugLicenceNo { get; set; }
}

public class SupplierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Gstin { get; set; }
    public string? DrugLicenceNo { get; set; }
    public bool IsActive { get; set; }
    public int OpenOrders { get; set; }

    public static SupplierDto From(Supplier s, int openOrders = 0) => new()
    {
        Id = s.Id,
        Name = s.Name,
        ContactPerson = s.ContactPerson,
        Phone = s.Phone,
        Email = s.Email,
        Address = s.Address,
        Gstin = s.Gstin,
        DrugLicenceNo = s.DrugLicenceNo,
        IsActive = s.IsActive,
        OpenOrders = openOrders
    };
}

public class PurchaseOrderUpsertDto
{
    [Range(1, int.MaxValue)]
    public int SupplierId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required, MinLength(1), MaxLength(100)]
    public List<PurchaseOrderLineRequestDto> Lines { get; set; } = new();
}

public class PurchaseOrderLineRequestDto
{
    [Range(1, int.MaxValue)]
    public int MedicineId { get; set; }

    [Range(1, 1_000_000)]
    public int Quantity { get; set; }

    [Range(0, 1_000_000)]
    public decimal? ExpectedRate { get; set; }
}

public class ReceiveAgainstPoDto
{
    /// <summary>The supplier's invoice number for this delivery (needed to claim GST input credit).</summary>
    [Required, MaxLength(50)]
    public string SupplierInvoiceNo { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(100)]
    public List<ReceiveLineDto> Lines { get; set; } = new();
}

public class ReceiveLineDto
{
    [Range(1, int.MaxValue)]
    public int LineId { get; set; }

    [Required, MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [Required]
    public DateOnly? ExpiryDate { get; set; }

    public decimal Mrp { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal PurchaseRate { get; set; }

    [Range(1, 1_000_000)]
    public int Quantity { get; set; }
}

public class CloseOrderDto
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class PurchaseOrderSummaryDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public PurchaseOrderStatus Status { get; set; }
    public int LineCount { get; set; }
    public int UnitsOrdered { get; set; }
    public int UnitsReceived { get; set; }
    public decimal? EstimatedValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? OrderedAt { get; set; }

    public static PurchaseOrderSummaryDto From(PurchaseOrder p) => Fill(new PurchaseOrderSummaryDto(), p);

    protected static T Fill<T>(T dto, PurchaseOrder p) where T : PurchaseOrderSummaryDto
    {
        dto.Id = p.Id;
        dto.PoNumber = p.PoNumber;
        dto.SupplierId = p.SupplierId;
        dto.SupplierName = p.Supplier?.Name ?? string.Empty;
        dto.Status = p.Status;
        dto.LineCount = p.Lines.Count;
        dto.UnitsOrdered = p.Lines.Sum(l => l.QuantityOrdered);
        dto.UnitsReceived = p.Lines.Sum(l => l.QuantityReceived);
        dto.EstimatedValue = p.Lines.All(l => l.ExpectedRate is not null) && p.Lines.Count > 0
            ? p.Lines.Sum(l => l.ExpectedRate!.Value * l.QuantityOrdered)
            : null;
        dto.CreatedAt = p.CreatedAt;
        dto.OrderedAt = p.OrderedAt;
        return dto;
    }
}

public class PurchaseOrderDto : PurchaseOrderSummaryDto
{
    public string? Notes { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SupplierDto Supplier { get; set; } = new();
    public List<PurchaseOrderLineDto> Lines { get; set; } = new();

    public static PurchaseOrderDto From(PurchaseOrder p, DateOnly today, string? createdByName)
    {
        var dto = Fill(new PurchaseOrderDto(), p);
        dto.Notes = p.Notes;
        dto.CreatedByName = createdByName;
        dto.CompletedAt = p.CompletedAt;
        dto.Supplier = SupplierDto.From(p.Supplier);
        dto.Lines = p.Lines.OrderBy(l => l.Medicine.Name).Select(l => new PurchaseOrderLineDto
        {
            Id = l.Id,
            MedicineId = l.MedicineId,
            MedicineName = l.Medicine.Name,
            Strength = l.Medicine.Strength,
            Form = l.Medicine.Form,
            PackSize = l.Medicine.PackSize,
            QuantityOrdered = l.QuantityOrdered,
            QuantityReceived = l.QuantityReceived,
            QuantityOutstanding = l.QuantityOutstanding,
            ExpectedRate = l.ExpectedRate,
            SellableQuantity = Pharmacy.Core.Rules.StockRules.SellableQuantity(l.Medicine.Batches, today),
            ReorderLevel = l.Medicine.ReorderLevel
        }).ToList();
        return dto;
    }
}

public class PurchaseOrderLineDto
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string? Strength { get; set; }
    public string Form { get; set; } = string.Empty;
    public string? PackSize { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public int QuantityOutstanding { get; set; }
    public decimal? ExpectedRate { get; set; }
    public int SellableQuantity { get; set; }
    public int ReorderLevel { get; set; }
}

public class ReorderSuggestionDto
{
    public int MedicineId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Strength { get; set; }
    public string Form { get; set; } = string.Empty;
    public int SellableQuantity { get; set; }
    public int ReorderLevel { get; set; }
    public int OnOrder { get; set; }
    public int SuggestedQuantity { get; set; }

    /// <summary>Who supplied the latest batch, if it matches a supplier on file.</summary>
    public int? LastSupplierId { get; set; }
    public string? LastSupplierName { get; set; }
    public decimal? LastPurchaseRate { get; set; }
}
