using System.Net;
using System.Net.Http.Json;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Entities;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class MedicinesTests : IClassFixture<PharmacyApiFactory>
{
    private readonly PharmacyApiFactory _factory;

    public MedicinesTests(PharmacyApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Pharmacist_CreatesMedicine()
    {
        var client = await CreateStaffClientAsync(_factory, "Pharmacist");

        var medicine = await CreateMedicineAsync(client, schedule: "H1", gst: 5m);

        Assert.Equal(DrugSchedule.H1, medicine.Schedule);
        Assert.True(medicine.RequiresPrescription);
        Assert.Equal("3004", medicine.HsnCode);
        Assert.Equal(5m, medicine.GstRatePercent);
        Assert.Equal(0, medicine.SellableQuantity);
        Assert.Empty(medicine.Batches);
    }

    [Fact]
    public async Task OtcMedicine_DoesNotRequirePrescription()
    {
        var client = await CreateAdminClientAsync(_factory);

        var medicine = await CreateMedicineAsync(client, schedule: "Otc", gst: 18m);

        Assert.False(medicine.RequiresPrescription);
    }

    [Fact]
    public async Task Technician_CanReadButNotEditCatalogue()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var existing = await CreateMedicineAsync(admin);
        var tech = await CreateStaffClientAsync(_factory, "Technician");

        Assert.Equal(HttpStatusCode.OK, (await tech.GetAsync("/api/medicines")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tech.GetAsync($"/api/medicines/{existing.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsJsonAsync("/api/medicines",
            new { name = "Nope", form = "Tablet", schedule = "Otc", hsnCode = "3004", gstRatePercent = 5 }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.DeleteAsync($"/api/medicines/{existing.Id}")).StatusCode);
    }

    [Fact]
    public async Task Medicines_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/medicines");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(12)]   // slab removed in the September 2025 GST rationalisation
    [InlineData(28)]
    [InlineData(7.5)]
    public async Task Create_WithUnsupportedGstRate_ReturnsBadRequest(double gst)
    {
        var admin = await CreateAdminClientAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/medicines",
            new { name = UniqueName(), form = "Tablet", schedule = "H", hsnCode = "3004", gstRatePercent = (decimal)gst }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("30045")]
    [InlineData("30AB")]
    public async Task Create_WithBadHsnCode_ReturnsBadRequest(string hsn)
    {
        var admin = await CreateAdminClientAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/medicines",
            new { name = UniqueName(), form = "Tablet", schedule = "H", hsnCode = hsn, gstRatePercent = 5 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownSchedule_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/medicines",
            new { name = UniqueName(), form = "Tablet", schedule = "Z", hsnCode = "3004", gstRatePercent = 5 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateBarcode_ReturnsConflict()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var barcode = Guid.NewGuid().ToString("N")[..13];
        await CreateMedicineAsync(admin, barcode: barcode);

        var response = await admin.PostAsJsonAsync("/api/medicines",
            new { name = UniqueName(), form = "Tablet", schedule = "H", hsnCode = "3004", gstRatePercent = 5, barcode }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Search_MatchesGenericName()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var generic = UniqueName("Generic");
        var created = await CreateMedicineAsync(admin, genericName: generic);

        var results = await admin.GetFromJsonAsync<List<MedicineDto>>($"/api/medicines?search={Uri.EscapeDataString(generic[..15])}", Json);

        Assert.Contains(results!, m => m.Id == created.Id);
    }

    [Fact]
    public async Task Update_ChangesScheduleAndGst()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var created = await CreateMedicineAsync(admin, schedule: "Otc", gst: 18m);

        var response = await admin.PutAsJsonAsync($"/api/medicines/{created.Id}",
            new { name = created.Name, form = "Syrup", schedule = "H", hsnCode = "30049099", gstRatePercent = 5, reorderLevel = 25 }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<MedicineDetailDto>(Json);
        Assert.Equal(DrugSchedule.H, updated!.Schedule);
        Assert.Equal("Syrup", updated.Form);
        Assert.Equal("30049099", updated.HsnCode);
        Assert.Equal(5m, updated.GstRatePercent);
        Assert.Equal(25, updated.ReorderLevel);
    }

    [Fact]
    public async Task Deactivate_HidesFromDefaultList()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var created = await CreateMedicineAsync(admin);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/medicines/{created.Id}")).StatusCode);

        var active = await admin.GetFromJsonAsync<List<MedicineDto>>("/api/medicines", Json);
        var all = await admin.GetFromJsonAsync<List<MedicineDto>>("/api/medicines?includeInactive=true", Json);
        Assert.DoesNotContain(active!, m => m.Id == created.Id);
        Assert.Contains(all!, m => m.Id == created.Id && !m.IsActive);
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNotFound()
    {
        var admin = await CreateAdminClientAsync(_factory);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/medicines/999999")).StatusCode);
    }
}
