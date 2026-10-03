using System.ComponentModel.DataAnnotations;
using Pharmacy.Core.Entities;

namespace Pharmacy.Api.DTOs;

public class BatchDto
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public decimal Mrp { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal PurchaseRate { get; set; }
    public int QuantityOnHand { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierInvoiceNo { get; set; }
    public DateTime ReceivedAt { get; set; }
    public bool IsExpired { get; set; }

    public static BatchDto From(Batch b, DateOnly today) => new()
    {
        Id = b.Id,
        MedicineId = b.MedicineId,
        MedicineName = b.Medicine?.Name ?? string.Empty,
        BatchNumber = b.BatchNumber,
        ExpiryDate = b.ExpiryDate,
        Mrp = b.Mrp,
        SellingPrice = b.SellingPrice,
        PurchaseRate = b.PurchaseRate,
        QuantityOnHand = b.QuantityOnHand,
        SupplierName = b.SupplierName,
        SupplierInvoiceNo = b.SupplierInvoiceNo,
        ReceivedAt = b.ReceivedAt,
        IsExpired = b.IsExpired(today)
    };
}

public class ReceiveStockDto
{
    [Range(1, int.MaxValue)]
    public int MedicineId { get; set; }

    [Required, MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [Required]
    public DateOnly? ExpiryDate { get; set; }

    public decimal Mrp { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal PurchaseRate { get; set; }

    [Range(1, 1_000_000)]
    public int Quantity { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    [MaxLength(50)]
    public string? SupplierInvoiceNo { get; set; }
}

public class AdjustStockDto
{
    [Range(1, int.MaxValue)]
    public int BatchId { get; set; }

    /// <summary>Positive to add units (e.g. count correction), negative to remove (damage, loss).</summary>
    [Range(-1_000_000, 1_000_000)]
    public int QuantityChange { get; set; }

    [Required, MinLength(3), MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class StockMovementDto
{
    public long Id { get; set; }
    public int BatchId { get; set; }
    public int Quantity { get; set; }
    public StockMovementType Type { get; set; }
    public string? Reason { get; set; }
    public string? ReferenceId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LowStockItemDto
{
    public int MedicineId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Strength { get; set; }
    public string Form { get; set; } = string.Empty;
    public int SellableQuantity { get; set; }
    public int ReorderLevel { get; set; }
}

public class StockAlertsDto
{
    public int ExpiringWithinDays { get; set; }
    public List<LowStockItemDto> LowStock { get; set; } = new();
    public List<BatchDto> ExpiringSoon { get; set; } = new();
    public List<BatchDto> Expired { get; set; } = new();
}
