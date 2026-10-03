using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class InventoryTests : IClassFixture<PharmacyApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private readonly PharmacyApiFactory _factory;

    public InventoryTests(PharmacyApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Receive_NewBatch_AddsSellableStock()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);

        var response = await ReceiveAsync(admin, medicine.Id, "ab123", 100, expiry: Today.AddMonths(18), mrp: 60m, sellingPrice: 54m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var batch = await response.Content.ReadFromJsonAsync<BatchDto>(Json);
        Assert.Equal("AB123", batch!.BatchNumber);
        Assert.Equal(100, batch.QuantityOnHand);
        Assert.Equal(60m, batch.Mrp);
        Assert.Equal(54m, batch.SellingPrice);

        var detail = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{medicine.Id}", Json);
        Assert.Equal(100, detail!.SellableQuantity);
        Assert.Equal(Today.AddMonths(18), detail.NearestExpiry);
        Assert.Single(detail.Batches);
    }

    [Fact]
    public async Task Receive_SameBatchAgain_AddsToIt()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var expiry = Today.AddYears(1);
        (await ReceiveAsync(admin, medicine.Id, "B-1", 40, expiry)).EnsureSuccessStatusCode();

        var response = await ReceiveAsync(admin, medicine.Id, "b-1", 25, expiry);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var batch = await response.Content.ReadFromJsonAsync<BatchDto>(Json);
        Assert.Equal(65, batch!.QuantityOnHand);
        var detail = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{medicine.Id}", Json);
        Assert.Single(detail!.Batches);
    }

    [Fact]
    public async Task Receive_SameBatchWithDifferentMrp_ReturnsConflict()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var expiry = Today.AddYears(1);
        (await ReceiveAsync(admin, medicine.Id, "B-2", 10, expiry, mrp: 50m)).EnsureSuccessStatusCode();

        var response = await ReceiveAsync(admin, medicine.Id, "B-2", 10, expiry, mrp: 55m);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Receive_SellingPriceAboveMrp_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);

        var response = await ReceiveAsync(admin, medicine.Id, "B-3", 10, mrp: 50m, sellingPrice: 50.01m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Receive_ExpiredStock_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);

        var response = await ReceiveAsync(admin, medicine.Id, "OLD", 10, expiry: Today.AddDays(-1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Receive_ForDeactivatedMedicine_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        await admin.DeleteAsync($"/api/medicines/{medicine.Id}");

        var response = await ReceiveAsync(admin, medicine.Id, "B-4", 10);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Technician_CanReceiveButNotAdjust()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var tech = await CreateStaffClientAsync(_factory, "Technician");

        var receive = await ReceiveAsync(tech, medicine.Id, "T-1", 5);
        Assert.Equal(HttpStatusCode.OK, receive.StatusCode);
        var batch = await receive.Content.ReadFromJsonAsync<BatchDto>(Json);

        var adjust = await tech.PostAsJsonAsync("/api/inventory/adjustments",
            new { batchId = batch!.Id, quantityChange = -1, reason = "Damaged" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, adjust.StatusCode);
    }

    [Fact]
    public async Task Adjust_RecordsMovementWithReason()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var batch = (await (await ReceiveAsync(admin, medicine.Id, "ADJ-1", 20)).Content.ReadFromJsonAsync<BatchDto>(Json))!;

        var response = await admin.PostAsJsonAsync("/api/inventory/adjustments",
            new { batchId = batch.Id, quantityChange = -3, reason = "Strips damaged in delivery" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var adjusted = await response.Content.ReadFromJsonAsync<BatchDto>(Json);
        Assert.Equal(17, adjusted!.QuantityOnHand);

        var movements = await admin.GetFromJsonAsync<List<StockMovementDto>>($"/api/inventory/batches/{batch.Id}/movements", Json);
        Assert.Collection(movements!,
            m => { Assert.Equal(StockMovementType.Receipt, m.Type); Assert.Equal(20, m.Quantity); },
            m => { Assert.Equal(StockMovementType.Adjustment, m.Type); Assert.Equal(-3, m.Quantity); Assert.Equal("Strips damaged in delivery", m.Reason); });
    }

    [Fact]
    public async Task Adjust_BelowZero_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var batch = (await (await ReceiveAsync(admin, medicine.Id, "ADJ-2", 5)).Content.ReadFromJsonAsync<BatchDto>(Json))!;

        var response = await admin.PostAsJsonAsync("/api/inventory/adjustments",
            new { batchId = batch.Id, quantityChange = -6, reason = "Count correction" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var unchanged = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{medicine.Id}", Json);
        Assert.Equal(5, unchanged!.SellableQuantity);
    }

    [Fact]
    public async Task Adjust_WithoutReason_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var medicine = await CreateMedicineAsync(admin);
        var batch = (await (await ReceiveAsync(admin, medicine.Id, "ADJ-3", 5)).Content.ReadFromJsonAsync<BatchDto>(Json))!;

        var response = await admin.PostAsJsonAsync("/api/inventory/adjustments",
            new { batchId = batch.Id, quantityChange = -1, reason = "" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Alerts_ShowLowStockExpiringAndExpired()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var low = await CreateMedicineAsync(admin, reorderLevel: 50);
        (await ReceiveAsync(admin, low.Id, "LOW-1", 10)).EnsureSuccessStatusCode();

        var expiring = await CreateMedicineAsync(admin, reorderLevel: 0);
        (await ReceiveAsync(admin, expiring.Id, "SOON-1", 30, expiry: Today.AddDays(30))).EnsureSuccessStatusCode();

        // Stock can't be received already expired, so put an expired batch straight into the database
        var expired = await CreateMedicineAsync(admin, reorderLevel: 0);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
            db.Batches.Add(new Batch
            {
                MedicineId = expired.Id,
                BatchNumber = "GONE-1",
                ExpiryDate = Today.AddDays(-5),
                Mrp = 10m,
                SellingPrice = 10m,
                PurchaseRate = 5m,
                QuantityOnHand = 8
            });
            await db.SaveChangesAsync();
        }

        var alerts = await admin.GetFromJsonAsync<StockAlertsDto>("/api/inventory/alerts?expiringWithinDays=90", Json);

        Assert.Contains(alerts!.LowStock, i => i.MedicineId == low.Id && i.SellableQuantity == 10 && i.ReorderLevel == 50);
        Assert.Contains(alerts.ExpiringSoon, b => b.MedicineId == expiring.Id && b.BatchNumber == "SOON-1");
        Assert.Contains(alerts.Expired, b => b.MedicineId == expired.Id && b.IsExpired);
        // Expired stock isn't sellable, so that medicine is out of stock too
        Assert.Contains(alerts.LowStock, i => i.MedicineId == expired.Id && i.SellableQuantity == 0);

        var narrow = await admin.GetFromJsonAsync<StockAlertsDto>("/api/inventory/alerts?expiringWithinDays=10", Json);
        Assert.DoesNotContain(narrow!.ExpiringSoon, b => b.MedicineId == expiring.Id);
    }

    [Fact]
    public async Task Alerts_RejectsOutOfRangeWindow()
    {
        var admin = await CreateAdminClientAsync(_factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/inventory/alerts?expiringWithinDays=0")).StatusCode);
    }
}
