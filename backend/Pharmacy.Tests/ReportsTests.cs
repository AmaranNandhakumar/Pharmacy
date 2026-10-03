using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class ReportsTests : IClassFixture<PharmacyApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private readonly PharmacyApiFactory _factory;

    public ReportsTests(PharmacyApiFactory factory) => _factory = factory;

    private static async Task<SaleDto> SellAsync(HttpClient client, int medicineId, int quantity, string payment = "Cash", decimal discount = 0)
    {
        var response = await client.PostAsJsonAsync("/api/sales",
            new { paymentMethod = payment, discountPercent = discount, items = new[] { new { medicineId, quantity } } }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SaleDto>(Json))!;
    }

    private static string Range(DateOnly from, DateOnly to) => $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

    [Fact]
    public async Task DailySales_AddsUpCompletedBills_ByPaymentMethod()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var otc = await CreateMedicineAsync(admin, schedule: "Otc", gst: 5m);
        (await ReceiveAsync(admin, otc.Id, "DS-1", 50, mrp: 21m, sellingPrice: 21m)).EnsureSuccessStatusCode();
        var before = await admin.GetFromJsonAsync<DailySalesReportDto>($"/api/reports/daily-sales?{Range(Today, Today)}", Json);

        await SellAsync(admin, otc.Id, 5, "Cash");    // 105
        await SellAsync(admin, otc.Id, 10, "Upi");    // 210
        var voided = await SellAsync(admin, otc.Id, 1, "Card");
        (await admin.PostAsJsonAsync($"/api/sales/{voided.Id}/void", new { reason = "Test void" }, Json)).EnsureSuccessStatusCode();

        var after = await admin.GetFromJsonAsync<DailySalesReportDto>($"/api/reports/daily-sales?{Range(Today, Today)}", Json);
        var day = Assert.Single(after!.Days);
        var was = before!.Days.Single();

        Assert.Equal(Today, day.Date);
        Assert.Equal(was.Bills + 2, day.Bills);
        Assert.Equal(was.VoidedBills + 1, day.VoidedBills);
        Assert.Equal(was.Total + 315m, day.Total);
        Assert.Equal(was.Cash + 105m, day.Cash);
        Assert.Equal(was.Upi + 210m, day.Upi);
        Assert.Equal(was.Card, day.Card);
        Assert.Equal(was.TaxableValue + 300m, day.TaxableValue);
        Assert.Equal(day.Total, after.Totals.Total);
    }

    [Fact]
    public async Task DailySales_ListsEveryDayInTheRange()
    {
        var admin = await CreateAdminClientAsync(_factory);

        var report = await admin.GetFromJsonAsync<DailySalesReportDto>($"/api/reports/daily-sales?{Range(Today.AddDays(-6), Today)}", Json);

        Assert.Equal(7, report!.Days.Count);
        Assert.Equal(Today.AddDays(-6), report.Days[0].Date);
    }

    [Fact]
    public async Task GstSummary_GroupsByRateAndHsn()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var five = await CreateMedicineAsync(admin, schedule: "Otc", gst: 5m);
        var eighteen = await CreateMedicineAsync(admin, schedule: "G", gst: 18m);
        (await ReceiveAsync(admin, five.Id, "G5", 50, mrp: 21m, sellingPrice: 21m)).EnsureSuccessStatusCode();
        (await ReceiveAsync(admin, eighteen.Id, "G18", 50, mrp: 59m, sellingPrice: 59m)).EnsureSuccessStatusCode();
        var before = await admin.GetFromJsonAsync<GstSummaryReportDto>($"/api/reports/gst-summary?{Range(Today, Today)}", Json);

        var sale = await SellAsync(admin, five.Id, 5);   // taxable 100, GST 5
        await SellAsync(admin, eighteen.Id, 2);          // taxable 100, GST 18

        var after = await admin.GetFromJsonAsync<GstSummaryReportDto>($"/api/reports/gst-summary?{Range(Today, Today)}", Json);
        decimal Taxable(GstSummaryReportDto r, decimal rate) => r.ByRate.SingleOrDefault(x => x.GstRatePercent == rate)?.TaxableValue ?? 0;
        decimal Gst(GstSummaryReportDto r, decimal rate) => r.ByRate.Where(x => x.GstRatePercent == rate).Sum(x => x.Cgst + x.Sgst);

        Assert.Equal(Taxable(before!, 5m) + 100m, Taxable(after!, 5m));
        Assert.Equal(Gst(before!, 5m) + 5m, Gst(after!, 5m));
        Assert.Equal(Taxable(before!, 18m) + 100m, Taxable(after!, 18m));
        Assert.Equal(Gst(before!, 18m) + 18m, Gst(after!, 18m));
        Assert.Contains(after!.ByHsn, h => h.HsnCode == "3004" && h.GstRatePercent == 18m);
        Assert.Equal(after.ByRate.Sum(r => r.Total), after.Totals.Total);
        Assert.Equal(after.Totals.Total, after.Totals.TaxableValue + after.Totals.Cgst + after.Totals.Sgst);
        Assert.Equal(before!.Bills + 2, after.Bills);
        Assert.False(string.IsNullOrEmpty(after.FirstInvoiceNo));
        Assert.True(string.CompareOrdinal(after.LastInvoiceNo, sale.InvoiceNo) >= 0);
    }

    [Fact]
    public async Task StockValuation_ValuesSellableStock_AndSeparatesExpired()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin, schedule: "Otc");
        (await ReceiveAsync(admin, medicine.Id, "SV-1", 10, mrp: 50m, sellingPrice: 45m, purchaseRate: 30m)).EnsureSuccessStatusCode();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
            db.Batches.Add(new Batch
            {
                MedicineId = medicine.Id, BatchNumber = "SV-OLD", ExpiryDate = Today.AddDays(-5),
                Mrp = 50, SellingPrice = 45, PurchaseRate = 20, QuantityOnHand = 4
            });
            await db.SaveChangesAsync();
        }

        var report = await admin.GetFromJsonAsync<StockValuationReportDto>("/api/reports/stock-valuation", Json);

        var row = Assert.Single(report!.Rows, r => r.MedicineId == medicine.Id);
        Assert.Equal(10, row.Quantity);
        Assert.Equal(300m, row.PurchaseValue);
        Assert.Equal(450m, row.SalesValue);
        Assert.Equal(4, row.ExpiredQuantity);
        Assert.Equal(80m, row.ExpiredPurchaseValue);
        Assert.Equal(report.Rows.Sum(r => r.PurchaseValue), report.PurchaseValue);
    }

    [Fact]
    public async Task Expiring_ListsBatchesInWindow_WithDaysLeft()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin, schedule: "Otc");
        (await ReceiveAsync(admin, medicine.Id, "EX-SOON", 6, expiry: Today.AddDays(20), purchaseRate: 10m)).EnsureSuccessStatusCode();
        (await ReceiveAsync(admin, medicine.Id, "EX-LATER", 6, expiry: Today.AddDays(200))).EnsureSuccessStatusCode();

        var report = await admin.GetFromJsonAsync<ExpiringReportDto>("/api/reports/expiring?withinDays=30", Json);

        var soon = Assert.Single(report!.Rows, r => r.BatchNumber == "EX-SOON");
        Assert.Equal(20, soon.DaysLeft);
        Assert.Equal(60m, soon.PurchaseValue);
        Assert.DoesNotContain(report.Rows, r => r.BatchNumber == "EX-LATER");
    }

    [Theory]
    [InlineData("/api/reports/daily-sales?from=2026-10-10&to=2026-10-01")]
    [InlineData("/api/reports/daily-sales?from=2024-01-01&to=2026-01-01")]
    [InlineData("/api/reports/expiring?withinDays=0")]
    public async Task BadRanges_ReturnBadRequest(string url)
    {
        var admin = await CreateAdminClientAsync(_factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Reports_AreForAdminsAndPharmacists_AuditForAdminsOnly()
    {
        var tech = await CreateStaffClientAsync(_factory, "Technician");
        var pharmacist = await CreateStaffClientAsync(_factory, "Pharmacist");

        Assert.Equal(HttpStatusCode.Forbidden, (await tech.GetAsync("/api/reports/daily-sales")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.GetAsync("/api/reports/stock-valuation")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await pharmacist.GetAsync("/api/reports/gst-summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pharmacist.GetAsync("/api/audit")).StatusCode);
    }

    [Fact]
    public async Task Audit_FiltersAndNamesTheUser()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin, schedule: "Otc");

        var page = await admin.GetFromJsonAsync<AuditPageDto>(
            $"/api/audit?entityType=Medicine&entityId={medicine.Id}&from={Today:yyyy-MM-dd}", Json);

        var entry = Assert.Single(page!.Items);
        Assert.Equal("MedicineCreated", entry.Action);
        Assert.Equal("Administrator", entry.UserName);
        Assert.Contains("MedicineCreated", page.Actions);
        Assert.Contains("Medicine", page.EntityTypes);

        await CreateMedicineAsync(admin, schedule: "Otc");
        var paged = await admin.GetFromJsonAsync<AuditPageDto>("/api/audit?pageSize=1&page=2", Json);
        Assert.Single(paged!.Items);
        Assert.True(paged.TotalCount >= 2);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/audit?pageSize=500")).StatusCode);
    }
}
