using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;
using Pharmacy.Infrastructure.Data;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class SalesTests : IClassFixture<PharmacyApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private readonly PharmacyApiFactory _factory;

    public SalesTests(PharmacyApiFactory factory) => _factory = factory;

    private static Task<HttpResponseMessage> SellAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync("/api/sales", body, Json);

    private static async Task<SaleDto> SellOkAsync(HttpClient client, object body)
    {
        var response = await SellAsync(client, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SaleDto>(Json))!;
    }

    /// <summary>Enters, verifies and dispenses a prescription; returns the fill to bill.</summary>
    private async Task<(DispenseResultDto result, PatientDetailDto patient)> DispenseAsync(HttpClient admin, int medicineId, int quantity)
    {
        var patient = await CreatePatientAsync(admin);
        var rx = await CreatePrescriptionAsync(admin, patient.Id, (medicineId, quantity, 0));
        var pharmacist = await CreateStaffClientAsync(_factory, "Pharmacist");
        (await pharmacist.PostAsJsonAsync($"/api/prescriptions/{rx.Id}/verify", new { acknowledgeAllergyWarnings = true }, Json)).EnsureSuccessStatusCode();
        var response = await pharmacist.PostAsync($"/api/prescriptions/{rx.Id}/dispense", null);
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<DispenseResultDto>(Json))!, patient);
    }

    [Fact]
    public async Task OtcSale_TakesStockFefo_AndWorksOutGst()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var otc = await CreateMedicineAsync(admin, schedule: "Otc", gst: 5m);
        (await ReceiveAsync(admin, otc.Id, "LATE", 20, expiry: Today.AddMonths(18), mrp: 110m, sellingPrice: 105m)).EnsureSuccessStatusCode();
        (await ReceiveAsync(admin, otc.Id, "EARLY", 1, expiry: Today.AddMonths(2), mrp: 110m, sellingPrice: 105m)).EnsureSuccessStatusCode();
        var tech = await CreateStaffClientAsync(_factory, "Technician");

        var sale = await SellOkAsync(tech, new { paymentMethod = "Upi", discountPercent = 0, items = new[] { new { medicineId = otc.Id, quantity = 2 } } });

        Assert.Matches(@"^\d{4}-\d{2}/\d{6}$", sale.InvoiceNo);
        Assert.StartsWith(GstCalculator.FinancialYear(Today), sale.InvoiceNo);
        Assert.Collection(sale.Items,
            i => { Assert.Equal("EARLY", i.BatchNumber); Assert.Equal(1, i.Quantity); },
            i => { Assert.Equal("LATE", i.BatchNumber); Assert.Equal(1, i.Quantity); });
        Assert.Equal(210m, sale.Total);
        Assert.Equal(200m, sale.TaxableValue);
        Assert.Equal(5m, sale.Cgst);
        Assert.Equal(5m, sale.Sgst);
        Assert.Equal(PaymentMethod.Upi, sale.PaymentMethod);
        Assert.Equal("Test Technician", sale.BilledByName);
        var gst = Assert.Single(sale.GstSummary);
        Assert.Equal(5m, gst.GstRatePercent);
        Assert.False(string.IsNullOrEmpty(sale.Pharmacy.Gstin));

        var medicine = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{otc.Id}", Json);
        Assert.Equal(19, medicine!.SellableQuantity);
    }

    [Fact]
    public async Task InvoiceNumbers_AreSequential()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var otc = await CreateMedicineAsync(admin, schedule: "G");
        (await ReceiveAsync(admin, otc.Id, "SEQ", 10)).EnsureSuccessStatusCode();
        var body = new { paymentMethod = "Cash", items = new[] { new { medicineId = otc.Id, quantity = 1 } } };

        var first = await SellOkAsync(admin, body);
        var second = await SellOkAsync(admin, body);

        var n1 = int.Parse(first.InvoiceNo.Split('/')[1]);
        var n2 = int.Parse(second.InvoiceNo.Split('/')[1]);
        Assert.Equal(n1 + 1, n2);
    }

    [Fact]
    public async Task PrescriptionMedicine_CantBeSoldOffTheShelf()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var rxOnly = await CreateMedicineAsync(admin, schedule: "H");
        (await ReceiveAsync(admin, rxOnly.Id, "RX1", 10)).EnsureSuccessStatusCode();

        var response = await SellAsync(admin, new { paymentMethod = "Cash", items = new[] { new { medicineId = rxOnly.Id, quantity = 1 } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NotEnoughStock_OrTooMuchDiscount_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var otc = await CreateMedicineAsync(admin, schedule: "Otc");
        (await ReceiveAsync(admin, otc.Id, "FEW", 2)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(admin,
            new { paymentMethod = "Cash", items = new[] { new { medicineId = otc.Id, quantity = 3 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(admin,
            new { paymentMethod = "Cash", discountPercent = 25, items = new[] { new { medicineId = otc.Id, quantity = 1 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(admin, new { paymentMethod = "Cash" })).StatusCode);

        var medicine = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{otc.Id}", Json);
        Assert.Equal(2, medicine!.SellableQuantity);
    }

    [Fact]
    public async Task BillingAFill_DoesNotTakeStockTwice_AndWritesH1Register()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var h1 = await CreateMedicineAsync(admin, schedule: "H1", gst: 5m);
        (await ReceiveAsync(admin, h1.Id, "H1-B", 30, mrp: 50m, sellingPrice: 42m)).EnsureSuccessStatusCode();
        var (dispensed, patient) = await DispenseAsync(admin, h1.Id, 10);

        var billable = await admin.GetFromJsonAsync<List<BillableFillDto>>($"/api/sales/billable-fills?patientId={patient.Id}", Json);
        var fill = Assert.Single(billable!);
        Assert.Equal(dispensed.FillId, fill.FillId);
        Assert.Equal(420m, fill.Total);

        var sale = await SellOkAsync(admin, new { paymentMethod = "Card", prescriptionFillIds = new[] { fill.FillId } });

        Assert.Equal(patient.Id, sale.PatientId);
        Assert.Equal(patient.FullName, sale.CustomerName);
        var line = Assert.Single(sale.Items);
        Assert.Equal(10, line.Quantity);
        Assert.NotNull(line.PrescriptionItemId);
        Assert.Equal(420m, sale.Total);

        // Stock left once, at dispense
        var medicine = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{h1.Id}", Json);
        Assert.Equal(20, medicine!.SellableQuantity);

        // Can't bill the same fill twice
        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(admin, new { paymentMethod = "Cash", prescriptionFillIds = new[] { fill.FillId } })).StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<BillableFillDto>>($"/api/sales/billable-fills?patientId={patient.Id}", Json))!);

        var register = await admin.GetFromJsonAsync<List<ScheduleRegisterEntryDto>>("/api/sales/register?schedule=H1", Json);
        var entry = Assert.Single(register!, r => r.InvoiceNo == sale.InvoiceNo);
        Assert.Equal(patient.FullName, entry.PatientName);
        Assert.Equal("Dr. Test Doctor", entry.PrescriberName);
        Assert.Equal("TN-12345", entry.PrescriberRegNo);
        Assert.Equal(10, entry.Quantity);
        Assert.Equal("Test Pharmacist", entry.PharmacistName);
    }

    [Fact]
    public async Task MixedBill_FillPlusOtc_OnOneInvoice()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var rxMed = await CreateMedicineAsync(admin, schedule: "H", gst: 5m);
        var otc = await CreateMedicineAsync(admin, schedule: "Otc", gst: 18m);
        (await ReceiveAsync(admin, rxMed.Id, "MX-RX", 10, sellingPrice: 21m, mrp: 21m)).EnsureSuccessStatusCode();
        (await ReceiveAsync(admin, otc.Id, "MX-OTC", 10, sellingPrice: 59m, mrp: 59m)).EnsureSuccessStatusCode();
        var (dispensed, _) = await DispenseAsync(admin, rxMed.Id, 5);

        var sale = await SellOkAsync(admin, new
        {
            paymentMethod = "Cash",
            prescriptionFillIds = new[] { dispensed.FillId },
            items = new[] { new { medicineId = otc.Id, quantity = 2 } }
        });

        Assert.Equal(2, sale.Items.Count);
        Assert.Equal(105m + 118m, sale.Total);
        Assert.Equal(2, sale.GstSummary.Count);
        Assert.Equal(100m + 100m, sale.TaxableValue);
    }

    [Fact]
    public async Task Void_IsAdminOnly_AndPutsStockBack()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var otc = await CreateMedicineAsync(admin, schedule: "Otc");
        (await ReceiveAsync(admin, otc.Id, "VOID", 10)).EnsureSuccessStatusCode();
        var sale = await SellOkAsync(admin, new { paymentMethod = "Cash", items = new[] { new { medicineId = otc.Id, quantity = 4 } } });

        var pharmacist = await CreateStaffClientAsync(_factory, "Pharmacist");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await pharmacist.PostAsJsonAsync($"/api/sales/{sale.Id}/void", new { reason = "Wrong item" }, Json)).StatusCode);

        var response = await admin.PostAsJsonAsync($"/api/sales/{sale.Id}/void", new { reason = "Customer returned unopened" }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var voided = await response.Content.ReadFromJsonAsync<SaleDto>(Json);
        Assert.Equal(SaleStatus.Voided, voided!.Status);
        Assert.Equal("Customer returned unopened", voided.VoidReason);

        var medicine = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{otc.Id}", Json);
        Assert.Equal(10, medicine!.SellableQuantity);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync($"/api/sales/{sale.Id}/void", new { reason = "Again" }, Json)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "SaleVoided" && a.EntityId == sale.Id.ToString()));
    }

    [Fact]
    public async Task List_ReturnsTodaysSales()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var otc = await CreateMedicineAsync(admin, schedule: "Otc");
        (await ReceiveAsync(admin, otc.Id, "LIST", 5)).EnsureSuccessStatusCode();
        var sale = await SellOkAsync(admin, new { paymentMethod = "Cash", customerName = "Walk-in", items = new[] { new { medicineId = otc.Id, quantity = 1 } } });

        var today = await admin.GetFromJsonAsync<List<SaleSummaryDto>>("/api/sales", Json);
        var yesterday = await admin.GetFromJsonAsync<List<SaleSummaryDto>>($"/api/sales?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today.AddDays(-1):yyyy-MM-dd}", Json);

        Assert.Contains(today!, s => s.Id == sale.Id && s.CustomerName == "Walk-in");
        Assert.DoesNotContain(yesterday!, s => s.Id == sale.Id);
    }

    [Fact]
    public async Task Register_IsForAdminsAndPharmacists()
    {
        var tech = await CreateStaffClientAsync(_factory, "Technician");

        Assert.Equal(HttpStatusCode.Forbidden, (await tech.GetAsync("/api/sales/register?schedule=H1")).StatusCode);
    }

    [Fact]
    public async Task Settings_AdminUpdates_OthersRead()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var tech = await CreateStaffClientAsync(_factory, "Technician");
        var settings = new
        {
            name = "Test Care Pharmacy",
            address = "1 Anna Salai, Chennai 600002",
            phone = "04412345678",
            stateCode = "33",
            gstin = "33ABCDE1234F1Z5",
            drugLicence20 = "TN-01-20-123",
            drugLicence21 = "TN-01-21-123",
            registeredPharmacistName = "A. Pharmacist",
            registeredPharmacistRegNo = "TNPC-999"
        };

        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PutAsJsonAsync("/api/settings", settings, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/settings", settings, Json)).StatusCode);

        var read = await tech.GetFromJsonAsync<PharmacySettingsDto>("/api/settings", Json);
        Assert.Equal("Test Care Pharmacy", read!.Name);
        Assert.Equal("33ABCDE1234F1Z5", read.Gstin);

        var wrongState = await admin.PutAsJsonAsync("/api/settings", new
        {
            settings.name, settings.address, stateCode = "29", settings.gstin, settings.drugLicence20, settings.drugLicence21,
            settings.registeredPharmacistName, settings.registeredPharmacistRegNo
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrongState.StatusCode);
    }
}
