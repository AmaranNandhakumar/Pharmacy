using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class PrescriptionsTests : IClassFixture<PharmacyApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private readonly PharmacyApiFactory _factory;

    public PrescriptionsTests(PharmacyApiFactory factory) => _factory = factory;

    private Task<HttpClient> Pharmacist() => CreateStaffClientAsync(_factory, "Pharmacist");

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, int id, bool acknowledge = false) =>
        client.PostAsJsonAsync($"/api/prescriptions/{id}/verify", new { acknowledgeAllergyWarnings = acknowledge }, Json);

    private static Task<HttpResponseMessage> DispenseAsync(HttpClient client, int id) =>
        client.PostAsync($"/api/prescriptions/{id}/dispense", null);

    [Fact]
    public async Task EnterVerifyDispense_DeductsStockFefo()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        (await ReceiveAsync(admin, medicine.Id, "LATE", 50, expiry: Today.AddMonths(18))).EnsureSuccessStatusCode();
        (await ReceiveAsync(admin, medicine.Id, "EARLY", 6, expiry: Today.AddMonths(3))).EnsureSuccessStatusCode();
        var patient = await CreatePatientAsync(admin);

        var tech = await CreateStaffClientAsync(_factory, "Technician");
        var rx = await CreatePrescriptionAsync(tech, patient.Id, (medicine.Id, 10, 0));
        Assert.Equal(PrescriptionStatus.Entered, rx.Status);
        Assert.Equal("Test Technician", rx.EnteredByName);
        Assert.Equal(56, rx.Items.Single().SellableQuantity);

        var pharmacist = await Pharmacist();
        var verify = await VerifyAsync(pharmacist, rx.Id);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.Equal("Test Pharmacist", (await verify.Content.ReadFromJsonAsync<PrescriptionDto>(Json))!.VerifiedByName);

        var dispense = await DispenseAsync(pharmacist, rx.Id);
        Assert.Equal(HttpStatusCode.OK, dispense.StatusCode);
        var result = await dispense.Content.ReadFromJsonAsync<DispenseResultDto>(Json);

        Assert.False(result!.WasRefill);
        Assert.Equal(PrescriptionStatus.Dispensed, result.Prescription.Status);
        Assert.Collection(result.Lines,
            l => { Assert.Equal("EARLY", l.BatchNumber); Assert.Equal(6, l.Quantity); },
            l => { Assert.Equal("LATE", l.BatchNumber); Assert.Equal(4, l.Quantity); });
        Assert.Equal(46, result.Prescription.Items.Single().SellableQuantity);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var movements = await db.StockMovements.Where(m => m.ReferenceId == $"RX-{rx.Id}").ToListAsync();
        Assert.Equal(-10, movements.Sum(m => m.Quantity));
        Assert.All(movements, m => Assert.Equal(StockMovementType.Dispense, m.Type));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "PrescriptionDispensed" && a.EntityId == rx.Id.ToString()));
    }

    [Fact]
    public async Task Dispense_NeverUsesExpiredStock()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        (await ReceiveAsync(admin, medicine.Id, "GOOD", 3)).EnsureSuccessStatusCode();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
            db.Batches.Add(new Batch
            {
                MedicineId = medicine.Id, BatchNumber = "EXPIRED", ExpiryDate = Today.AddDays(-1),
                Mrp = 50, SellingPrice = 45, PurchaseRate = 30, QuantityOnHand = 100
            });
            await db.SaveChangesAsync();
        }
        var patient = await CreatePatientAsync(admin);
        var rx = await CreatePrescriptionAsync(admin, patient.Id, (medicine.Id, 5, 0));
        var pharmacist = await Pharmacist();
        (await VerifyAsync(pharmacist, rx.Id)).EnsureSuccessStatusCode();

        var response = await DispenseAsync(pharmacist, rx.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var after = await admin.GetFromJsonAsync<PrescriptionDto>($"/api/prescriptions/{rx.Id}", Json);
        Assert.Equal(PrescriptionStatus.Verified, after!.Status);
        Assert.Equal(3, after.Items.Single().SellableQuantity);
    }

    [Fact]
    public async Task Dispense_IsAllOrNothing_AcrossItems()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var stocked = await CreateMedicineAsync(admin);
        var scarce = await CreateMedicineAsync(admin);
        (await ReceiveAsync(admin, stocked.Id, "S1", 20)).EnsureSuccessStatusCode();
        (await ReceiveAsync(admin, scarce.Id, "S2", 1)).EnsureSuccessStatusCode();
        var patient = await CreatePatientAsync(admin);
        var rx = await CreatePrescriptionAsync(admin, patient.Id, (stocked.Id, 5, 0), (scarce.Id, 5, 0));
        var pharmacist = await Pharmacist();
        (await VerifyAsync(pharmacist, rx.Id)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.BadRequest, (await DispenseAsync(pharmacist, rx.Id)).StatusCode);

        var medicine = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{stocked.Id}", Json);
        Assert.Equal(20, medicine!.SellableQuantity);
    }

    [Fact]
    public async Task Refills_AreTrackedUntilUsedUp()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        (await ReceiveAsync(admin, medicine.Id, "R1", 100)).EnsureSuccessStatusCode();
        var patient = await CreatePatientAsync(admin);
        var rx = await CreatePrescriptionAsync(admin, patient.Id, (medicine.Id, 10, 1));
        var pharmacist = await Pharmacist();
        (await VerifyAsync(pharmacist, rx.Id)).EnsureSuccessStatusCode();
        (await DispenseAsync(pharmacist, rx.Id)).EnsureSuccessStatusCode();

        var refill = await DispenseAsync(pharmacist, rx.Id);
        Assert.Equal(HttpStatusCode.OK, refill.StatusCode);
        var result = await refill.Content.ReadFromJsonAsync<DispenseResultDto>(Json);
        Assert.True(result!.WasRefill);
        Assert.Equal(1, result.Prescription.Items.Single().RefillsUsed);
        Assert.Equal(20, result.Prescription.Items.Single().QuantityDispensed);
        Assert.Equal(80, result.Prescription.Items.Single().SellableQuantity);

        Assert.Equal(HttpStatusCode.BadRequest, (await DispenseAsync(pharmacist, rx.Id)).StatusCode);
    }

    [Fact]
    public async Task Verify_WithAllergyMatch_NeedsAcknowledgement_AndAuditsOverride()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin, genericName: "Amoxicillin");
        var patient = await CreatePatientAsync(admin, allergies: "amoxicillin; dust");
        var rx = await CreatePrescriptionAsync(admin, patient.Id, (medicine.Id, 10, 0));
        Assert.True(rx.HasAllergyWarnings);
        Assert.Equal(new[] { "amoxicillin" }, rx.Items.Single().AllergyWarnings);

        var pharmacist = await Pharmacist();
        var blocked = await VerifyAsync(pharmacist, rx.Id);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var body = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("allergyWarnings").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(pharmacist, rx.Id, acknowledge: true)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "PrescriptionVerified" && a.EntityId == rx.Id.ToString());
        Assert.Contains("AllergyWarningsOverridden", audit.Details);
    }

    [Theory]
    [InlineData("Technician")]
    [InlineData("Admin")]
    public async Task OnlyPharmacists_CanVerifyRejectOrDispense(string role)
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var patient = await CreatePatientAsync(admin);
        var rx = await CreatePrescriptionAsync(admin, patient.Id, (medicine.Id, 1, 0));
        var client = role == "Admin" ? admin : await CreateStaffClientAsync(_factory, role);

        Assert.Equal(HttpStatusCode.Forbidden, (await VerifyAsync(client, rx.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/prescriptions/{rx.Id}/reject", new { reason = "No" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await DispenseAsync(client, rx.Id)).StatusCode);
    }

    [Fact]
    public async Task Dispense_BeforeVerify_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        (await ReceiveAsync(admin, medicine.Id, "NV1", 10)).EnsureSuccessStatusCode();
        var patient = await CreatePatientAsync(admin);
        var rx = await CreatePrescriptionAsync(admin, patient.Id, (medicine.Id, 1, 0));

        Assert.Equal(HttpStatusCode.BadRequest, (await DispenseAsync(await Pharmacist(), rx.Id)).StatusCode);
    }

    [Fact]
    public async Task Reject_RecordsReason_AndBlocksDispense()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var patient = await CreatePatientAsync(admin);
        var rx = await CreatePrescriptionAsync(admin, patient.Id, (medicine.Id, 1, 0));
        var pharmacist = await Pharmacist();

        var response = await pharmacist.PostAsJsonAsync($"/api/prescriptions/{rx.Id}/reject", new { reason = "Prescriber registration not valid" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = await response.Content.ReadFromJsonAsync<PrescriptionDto>(Json);
        Assert.Equal(PrescriptionStatus.Rejected, rejected!.Status);
        Assert.Equal("Prescriber registration not valid", rejected.RejectReason);
        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(pharmacist, rx.Id)).StatusCode);
    }

    [Fact]
    public async Task ScheduleX_NeedsRetainedCopyToVerify()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin, schedule: "X");
        var patient = await CreatePatientAsync(admin);
        var pharmacist = await Pharmacist();

        var withoutCopy = await CreatePrescriptionAsync(admin, patient.Id, (medicine.Id, 1, 0));
        Assert.Equal(HttpStatusCode.BadRequest, (await VerifyAsync(pharmacist, withoutCopy.Id)).StatusCode);

        var withCopy = await (await PostPrescriptionAsync(admin, patient.Id, true, (medicine.Id, 1, 0)))
            .Content.ReadFromJsonAsync<PrescriptionDto>(Json);
        Assert.Equal(HttpStatusCode.OK, (await VerifyAsync(pharmacist, withCopy!.Id)).StatusCode);
    }

    [Fact]
    public async Task Create_NdpsMedicine_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin, schedule: "Ndps");
        var patient = await CreatePatientAsync(admin);

        Assert.Equal(HttpStatusCode.BadRequest, (await PostPrescriptionAsync(admin, patient.Id, false, (medicine.Id, 1, 0))).StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateMedicineOrNoItems_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var patient = await CreatePatientAsync(admin);

        Assert.Equal(HttpStatusCode.BadRequest, (await PostPrescriptionAsync(admin, patient.Id, false, (medicine.Id, 1, 0), (medicine.Id, 2, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostPrescriptionAsync(admin, patient.Id, false)).StatusCode);
    }

    [Fact]
    public async Task List_FiltersByStatusAndPatient()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var patient = await CreatePatientAsync(admin);
        var rx = await CreatePrescriptionAsync(admin, patient.Id, (medicine.Id, 1, 0));

        var entered = await admin.GetFromJsonAsync<List<PrescriptionSummaryDto>>("/api/prescriptions?status=Entered", Json);
        var forPatient = await admin.GetFromJsonAsync<List<PrescriptionSummaryDto>>($"/api/prescriptions?patientId={patient.Id}", Json);
        var verified = await admin.GetFromJsonAsync<List<PrescriptionSummaryDto>>("/api/prescriptions?status=Verified", Json);

        Assert.Contains(entered!, p => p.Id == rx.Id && p.PatientName == patient.FullName);
        Assert.Equal(rx.Id, Assert.Single(forPatient!).Id);
        Assert.DoesNotContain(verified!, p => p.Id == rx.Id);

        var detail = await admin.GetFromJsonAsync<PatientDetailDto>($"/api/patients/{patient.Id}", Json);
        Assert.Equal(rx.Id, Assert.Single(detail!.Prescriptions).Id);
    }
}
