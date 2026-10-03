using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Api.DTOs;
using Pharmacy.Api.Services;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Read-only reports for the owner and pharmacist. Rows are loaded and added up in memory:
/// a single pharmacy's month is small, and SQLite (used by the tests) can't sum decimals in SQL.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.Pharmacist))]
public class ReportsController : ControllerBase
{
    private const int MaxRangeDays = 366;

    private readonly PharmacyDbContext _db;
    private readonly TimeProvider _time;

    public ReportsController(PharmacyDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    /// <summary>Takings per day, split by payment method. Defaults to the current month.</summary>
    [HttpGet("daily-sales")]
    public async Task<ActionResult<DailySalesReportDto>> DailySales([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (start, end, error) = Range(from, to);
        if (error is not null) return error;

        var sales = await SalesBetween(start, end);
        var days = new List<DailySalesRowDto>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var ofDay = sales.Where(s => _time.ToLocalDate(s.CreatedAt) == day).ToList();
            var row = Sum(ofDay.Where(s => s.Status == SaleStatus.Completed));
            row.Date = day;
            row.VoidedBills = ofDay.Count(s => s.Status == SaleStatus.Voided);
            days.Add(row);
        }

        var totals = Sum(sales.Where(s => s.Status == SaleStatus.Completed));
        totals.VoidedBills = sales.Count(s => s.Status == SaleStatus.Voided);

        return Ok(new DailySalesReportDto { From = start, To = end, Days = days, Totals = totals });
    }

    /// <summary>Tax by GST rate and by HSN code, for filing GSTR-3B and GSTR-1. Defaults to the current month.</summary>
    [HttpGet("gst-summary")]
    public async Task<ActionResult<GstSummaryReportDto>> GstSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (start, end, error) = Range(from, to);
        if (error is not null) return error;

        var sales = await SalesBetween(start, end);
        var completed = sales.Where(s => s.Status == SaleStatus.Completed).ToList();
        var lines = completed.SelectMany(s => s.Items).ToList();
        var invoiceNos = sales.Select(s => s.InvoiceNo).Order().ToList();

        return Ok(new GstSummaryReportDto
        {
            From = start,
            To = end,
            Bills = completed.Count,
            VoidedBills = sales.Count - completed.Count,
            FirstInvoiceNo = invoiceNos.FirstOrDefault() ?? string.Empty,
            LastInvoiceNo = invoiceNos.LastOrDefault() ?? string.Empty,
            ByRate = lines.GroupBy(i => i.GstRatePercent).OrderBy(g => g.Key)
                .Select(g => Fill(new GstRateRowDto { GstRatePercent = g.Key }, g)).ToList(),
            ByHsn = lines.GroupBy(i => new { i.HsnCode, i.GstRatePercent }).OrderBy(g => g.Key.HsnCode).ThenBy(g => g.Key.GstRatePercent)
                .Select(g =>
                {
                    var row = Fill(new HsnRowDto { HsnCode = g.Key.HsnCode, GstRatePercent = g.Key.GstRatePercent }, g);
                    row.Quantity = g.Sum(i => i.Quantity);
                    return row;
                }).ToList(),
            Totals = Fill(new GstRateRowDto(), lines)
        });
    }

    /// <summary>What the stock on the shelf is worth today, per medicine. Expired stock is shown separately.</summary>
    [HttpGet("stock-valuation")]
    public async Task<ActionResult<StockValuationReportDto>> StockValuation()
    {
        var today = _time.LocalToday();
        var medicines = await _db.Medicines.Include(m => m.Batches).AsNoTracking().ToListAsync();

        var rows = medicines
            .Select(m =>
            {
                var live = m.Batches.Where(b => b.QuantityOnHand > 0 && !b.IsExpired(today)).ToList();
                var expired = m.Batches.Where(b => b.QuantityOnHand > 0 && b.IsExpired(today)).ToList();
                return new StockValuationRowDto
                {
                    MedicineId = m.Id,
                    Name = m.Name,
                    Strength = m.Strength,
                    Schedule = m.Schedule,
                    Quantity = live.Sum(b => b.QuantityOnHand),
                    PurchaseValue = live.Sum(b => b.PurchaseRate * b.QuantityOnHand),
                    SalesValue = live.Sum(b => b.SellingPrice * b.QuantityOnHand),
                    ExpiredQuantity = expired.Sum(b => b.QuantityOnHand),
                    ExpiredPurchaseValue = expired.Sum(b => b.PurchaseRate * b.QuantityOnHand)
                };
            })
            .Where(r => r.Quantity > 0 || r.ExpiredQuantity > 0)
            .OrderByDescending(r => r.PurchaseValue)
            .ToList();

        return Ok(new StockValuationReportDto
        {
            AsOf = today,
            Rows = rows,
            Quantity = rows.Sum(r => r.Quantity),
            PurchaseValue = rows.Sum(r => r.PurchaseValue),
            SalesValue = rows.Sum(r => r.SalesValue),
            ExpiredQuantity = rows.Sum(r => r.ExpiredQuantity),
            ExpiredPurchaseValue = rows.Sum(r => r.ExpiredPurchaseValue)
        });
    }

    /// <summary>Batches already expired or expiring within the window, with the money at risk.</summary>
    [HttpGet("expiring")]
    public async Task<ActionResult<ExpiringReportDto>> Expiring([FromQuery] int withinDays = 90)
    {
        if (withinDays is < 1 or > 365) return BadRequest(new { message = "withinDays must be between 1 and 365." });

        var today = _time.LocalToday();
        var horizon = today.AddDays(withinDays);
        var batches = await _db.Batches.Include(b => b.Medicine).AsNoTracking()
            .Where(b => b.QuantityOnHand > 0 && b.ExpiryDate <= horizon)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync();

        var rows = batches.Select(b => new ExpiringRowDto
        {
            BatchId = b.Id,
            MedicineId = b.MedicineId,
            MedicineName = $"{b.Medicine.Name} {b.Medicine.Strength}".Trim(),
            BatchNumber = b.BatchNumber,
            ExpiryDate = b.ExpiryDate,
            DaysLeft = b.ExpiryDate.DayNumber - today.DayNumber,
            Quantity = b.QuantityOnHand,
            PurchaseValue = b.PurchaseRate * b.QuantityOnHand,
            SupplierName = b.SupplierName
        }).ToList();

        return Ok(new ExpiringReportDto
        {
            AsOf = today,
            WithinDays = withinDays,
            Rows = rows,
            PurchaseValueAtRisk = rows.Sum(r => r.PurchaseValue)
        });
    }

    private (DateOnly start, DateOnly end, ActionResult? error) Range(DateOnly? from, DateOnly? to)
    {
        var today = _time.LocalToday();
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (end < start) return (start, end, BadRequest(new { message = "'from' must be on or before 'to'." }));
        if (end.DayNumber - start.DayNumber >= MaxRangeDays)
            return (start, end, BadRequest(new { message = $"Pick a range of at most {MaxRangeDays} days." }));
        return (start, end, null);
    }

    private Task<List<Sale>> SalesBetween(DateOnly start, DateOnly end)
    {
        var (utcStart, utcEnd) = _time.UtcRange(start, end);
        return _db.Sales.Include(s => s.Items).AsNoTracking()
            .Where(s => s.CreatedAt >= utcStart && s.CreatedAt < utcEnd)
            .ToListAsync();
    }

    private static DailySalesRowDto Sum(IEnumerable<Sale> sales)
    {
        var list = sales.ToList();
        decimal By(PaymentMethod m) => list.Where(s => s.PaymentMethod == m).Sum(s => s.Total);
        return new DailySalesRowDto
        {
            Bills = list.Count,
            Gross = list.Sum(s => s.GrossAmount),
            Discount = list.Sum(s => s.Discount),
            TaxableValue = list.Sum(s => s.TaxableValue),
            Cgst = list.Sum(s => s.Cgst),
            Sgst = list.Sum(s => s.Sgst),
            Total = list.Sum(s => s.Total),
            Cash = By(PaymentMethod.Cash),
            Upi = By(PaymentMethod.Upi),
            Card = By(PaymentMethod.Card)
        };
    }

    private static T Fill<T>(T row, IEnumerable<SaleItem> items) where T : GstRateRowDto
    {
        var list = items.ToList();
        row.TaxableValue = list.Sum(i => i.TaxableValue);
        row.Cgst = list.Sum(i => i.Cgst);
        row.Sgst = list.Sum(i => i.Sgst);
        row.Total = list.Sum(i => i.LineTotal);
        return row;
    }
}
