using Pharmacy.Core.Entities;

namespace Pharmacy.Api.DTOs;

/// <summary>Money totals shared by the sales reports. Only completed (not voided) bills count.</summary>
public class MoneyTotalsDto
{
    public decimal Gross { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal Total { get; set; }
}

public class DailySalesRowDto : MoneyTotalsDto
{
    public DateOnly Date { get; set; }
    public int Bills { get; set; }
    public int VoidedBills { get; set; }
    public decimal Cash { get; set; }
    public decimal Upi { get; set; }
    public decimal Card { get; set; }
}

public class DailySalesReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<DailySalesRowDto> Days { get; set; } = new();
    public DailySalesRowDto Totals { get; set; } = new();
}

public class GstRateRowDto
{
    public decimal GstRatePercent { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal Total { get; set; }
}

public class HsnRowDto : GstRateRowDto
{
    public string HsnCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>
/// Figures for the GST returns: tax by rate (GSTR-3B, table 3.1) and the HSN-wise summary (GSTR-1, table 12).
/// Retail sales to unregistered customers are B2C, so no customer GSTINs are involved.
/// </summary>
public class GstSummaryReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int Bills { get; set; }
    public string FirstInvoiceNo { get; set; } = string.Empty;
    public string LastInvoiceNo { get; set; } = string.Empty;
    public int VoidedBills { get; set; }
    public List<GstRateRowDto> ByRate { get; set; } = new();
    public List<HsnRowDto> ByHsn { get; set; } = new();
    public GstRateRowDto Totals { get; set; } = new();
}

public class StockValuationRowDto
{
    public int MedicineId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Strength { get; set; }
    public DrugSchedule Schedule { get; set; }
    public int Quantity { get; set; }

    /// <summary>What the sellable stock cost (purchase rate × quantity).</summary>
    public decimal PurchaseValue { get; set; }

    /// <summary>What it would sell for at the selling price.</summary>
    public decimal SalesValue { get; set; }
    public int ExpiredQuantity { get; set; }
    public decimal ExpiredPurchaseValue { get; set; }
}

public class StockValuationReportDto
{
    public DateOnly AsOf { get; set; }
    public List<StockValuationRowDto> Rows { get; set; } = new();
    public int Quantity { get; set; }
    public decimal PurchaseValue { get; set; }
    public decimal SalesValue { get; set; }
    public int ExpiredQuantity { get; set; }
    public decimal ExpiredPurchaseValue { get; set; }
}

public class ExpiringRowDto
{
    public int BatchId { get; set; }
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }

    /// <summary>Negative once the batch has expired.</summary>
    public int DaysLeft { get; set; }
    public int Quantity { get; set; }
    public decimal PurchaseValue { get; set; }
    public string? SupplierName { get; set; }
}

public class ExpiringReportDto
{
    public DateOnly AsOf { get; set; }
    public int WithinDays { get; set; }
    public List<ExpiringRowDto> Rows { get; set; } = new();
    public decimal PurchaseValueAtRisk { get; set; }
}

public class AuditLogDto
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
}

public class AuditPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<AuditLogDto> Items { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public List<string> EntityTypes { get; set; } = new();
}
