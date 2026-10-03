using System.Net;
using System.Net.Http.Json;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class PurchaseOrderRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);

    private static PurchaseOrder NewPo(params int[] quantities) => new()
    {
        Lines = quantities.Select(q => new PurchaseOrderLine { QuantityOrdered = q, Medicine = new Medicine { Name = "Drug" } }).ToList()
    };

    [Theory]
    [InlineData(3, 10, 0, 17)]   // up to twice the reorder level
    [InlineData(10, 10, 0, 10)]  // at the level: at least the reorder level
    [InlineData(11, 10, 0, 0)]   // above the level: nothing
    [InlineData(2, 10, 9, 0)]    // enough already on order
    [InlineData(2, 10, 5, 13)]
    [InlineData(0, 0, 0, 1)]     // reorder level 0 and out of stock: order 1
    public void SuggestQuantity(int sellable, int reorderLevel, int onOrder, int expected)
    {
        Assert.Equal(expected, PurchaseOrderRules.SuggestQuantity(sellable, reorderLevel, onOrder));
    }

    [Fact]
    public void Draft_MustBeOrderedBeforeReceiving()
    {
        var po = NewPo(10);

        Assert.Throws<PurchaseOrderRuleException>(() => PurchaseOrderRules.RecordReceipt(po, po.Lines.First(), 5));

        PurchaseOrderRules.MarkOrdered(po, Now);
        PurchaseOrderRules.RecordReceipt(po, po.Lines.First(), 5);
        Assert.Equal(5, po.Lines.First().QuantityReceived);
    }

    [Fact]
    public void Receipts_MovePartiallyThenFullyReceived()
    {
        var po = NewPo(10, 4);
        PurchaseOrderRules.MarkOrdered(po, Now);
        var (a, b) = (po.Lines.First(), po.Lines.Last());

        PurchaseOrderRules.RecordReceipt(po, a, 10);
        PurchaseOrderRules.UpdateStatusAfterReceipt(po, Now);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, po.Status);

        PurchaseOrderRules.RecordReceipt(po, b, 4);
        PurchaseOrderRules.UpdateStatusAfterReceipt(po, Now);
        Assert.Equal(PurchaseOrderStatus.Received, po.Status);
        Assert.Equal(Now, po.CompletedAt);
    }

    [Fact]
    public void Receiving_MoreThanOrdered_Throws()
    {
        var po = NewPo(10);
        PurchaseOrderRules.MarkOrdered(po, Now);

        Assert.Throws<PurchaseOrderRuleException>(() => PurchaseOrderRules.RecordReceipt(po, po.Lines.First(), 11));
    }

    [Fact]
    public void Close_IsClosedIfSomethingArrived_CancelledIfNothingDid()
    {
        var nothing = NewPo(10);
        PurchaseOrderRules.Close(nothing, Now);
        Assert.Equal(PurchaseOrderStatus.Cancelled, nothing.Status);

        var some = NewPo(10);
        PurchaseOrderRules.MarkOrdered(some, Now);
        PurchaseOrderRules.RecordReceipt(some, some.Lines.First(), 3);
        PurchaseOrderRules.Close(some, Now);
        Assert.Equal(PurchaseOrderStatus.Closed, some.Status);

        Assert.Throws<PurchaseOrderRuleException>(() => PurchaseOrderRules.Close(some, Now));
    }

    [Fact]
    public void EmptyDraft_CantBeOrdered()
    {
        Assert.Throws<PurchaseOrderRuleException>(() => PurchaseOrderRules.MarkOrdered(NewPo(), Now));
    }
}

public class PurchasingTests : IClassFixture<PharmacyApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private readonly PharmacyApiFactory _factory;

    public PurchasingTests(PharmacyApiFactory factory) => _factory = factory;

    private static async Task<SupplierDto> CreateSupplierAsync(HttpClient client, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/suppliers", new
        {
            name = name ?? UniqueName("Distributor"),
            contactPerson = "Test Contact",
            phone = "04423456789",
            gstin = "33ABCDE1234F1Z5",
            drugLicenceNo = "TN-20B-0001"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SupplierDto>(Json))!;
    }

    private static async Task<PurchaseOrderDto> CreatePoAsync(HttpClient client, int supplierId, params (int medicineId, int qty)[] lines)
    {
        var response = await client.PostAsJsonAsync("/api/purchase-orders", new
        {
            supplierId,
            notes = "Test order",
            lines = lines.Select(l => new { medicineId = l.medicineId, quantity = l.qty, expectedRate = 30m })
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseOrderDto>(Json))!;
    }

    private static Task<HttpResponseMessage> ReceiveAsync(HttpClient client, int poId, string invoice, params (int lineId, string batch, int qty)[] lines) =>
        client.PostAsJsonAsync($"/api/purchase-orders/{poId}/receive", new
        {
            supplierInvoiceNo = invoice,
            lines = lines.Select(l => new
            {
                lineId = l.lineId,
                batchNumber = l.batch,
                expiryDate = Today.AddYears(2),
                mrp = 50m,
                sellingPrice = 45m,
                purchaseRate = 30m,
                quantity = l.qty
            })
        }, Json);

    [Fact]
    public async Task FullFlow_DraftOrderReceiveInTwoDeliveries()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var supplier = await CreateSupplierAsync(admin);
        var a = await CreateMedicineAsync(admin, schedule: "Otc");
        var b = await CreateMedicineAsync(admin, schedule: "H");

        var po = await CreatePoAsync(admin, supplier.Id, (a.Id, 100), (b.Id, 20));
        Assert.Equal(PurchaseOrderStatus.Draft, po.Status);
        Assert.Matches(@"^PO/\d{4}-\d{2}/\d{4}$", po.PoNumber);
        Assert.Equal(3600m, po.EstimatedValue);
        var lineA = po.Lines.Single(l => l.MedicineId == a.Id);
        var lineB = po.Lines.Single(l => l.MedicineId == b.Id);

        // Can't receive a draft
        Assert.Equal(HttpStatusCode.BadRequest, (await ReceiveAsync(admin, po.Id, "INV-1", (lineA.Id, "A1", 10))).StatusCode);

        var ordered = await (await admin.PostAsync($"/api/purchase-orders/{po.Id}/order", null)).Content.ReadFromJsonAsync<PurchaseOrderDto>(Json);
        Assert.Equal(PurchaseOrderStatus.Ordered, ordered!.Status);

        // A technician receives the first delivery
        var tech = await CreateStaffClientAsync(_factory, "Technician");
        var first = await ReceiveAsync(tech, po.Id, "INV-1", (lineA.Id, "a1", 60), (lineA.Id, "A2", 40), (lineB.Id, "B1", 5));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var afterFirst = await first.Content.ReadFromJsonAsync<PurchaseOrderDto>(Json);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, afterFirst!.Status);
        Assert.Equal(0, afterFirst.Lines.Single(l => l.Id == lineA.Id).QuantityOutstanding);
        Assert.Equal(15, afterFirst.Lines.Single(l => l.Id == lineB.Id).QuantityOutstanding);

        var medA = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{a.Id}", Json);
        Assert.Equal(100, medA!.SellableQuantity);
        Assert.All(medA.Batches, x => Assert.Equal(supplier.Name, x.SupplierName));

        var second = await ReceiveAsync(tech, po.Id, "INV-2", (lineB.Id, "B2", 15));
        var done = await second.Content.ReadFromJsonAsync<PurchaseOrderDto>(Json);
        Assert.Equal(PurchaseOrderStatus.Received, done!.Status);
        Assert.NotNull(done.CompletedAt);
    }

    [Fact]
    public async Task Receive_MoreThanOrdered_SavesNothing()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var supplier = await CreateSupplierAsync(admin);
        var med = await CreateMedicineAsync(admin, schedule: "Otc");
        var other = await CreateMedicineAsync(admin, schedule: "Otc");
        var po = await CreatePoAsync(admin, supplier.Id, (med.Id, 10), (other.Id, 10));
        (await admin.PostAsync($"/api/purchase-orders/{po.Id}/order", null)).EnsureSuccessStatusCode();
        var line = po.Lines.Single(l => l.MedicineId == med.Id);
        var otherLine = po.Lines.Single(l => l.MedicineId == other.Id);

        var response = await ReceiveAsync(admin, po.Id, "INV-X", (otherLine.Id, "OK1", 5), (line.Id, "OVER", 11));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var otherMed = await admin.GetFromJsonAsync<MedicineDetailDto>($"/api/medicines/{other.Id}", Json);
        Assert.Equal(0, otherMed!.SellableQuantity);
    }

    [Fact]
    public async Task Draft_CanBeEdited_OrderedCant()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var supplier = await CreateSupplierAsync(admin);
        var med = await CreateMedicineAsync(admin, schedule: "Otc");
        var po = await CreatePoAsync(admin, supplier.Id, (med.Id, 10));
        var edit = new { supplierId = supplier.Id, lines = new[] { new { medicineId = med.Id, quantity = 25, expectedRate = (decimal?)null } } };

        var edited = await admin.PutAsJsonAsync($"/api/purchase-orders/{po.Id}", edit, Json);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(25, (await edited.Content.ReadFromJsonAsync<PurchaseOrderDto>(Json))!.Lines.Single().QuantityOrdered);

        (await admin.PostAsync($"/api/purchase-orders/{po.Id}/order", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/purchase-orders/{po.Id}", edit, Json)).StatusCode);
    }

    [Fact]
    public async Task Close_WithNothingReceived_Cancels()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var supplier = await CreateSupplierAsync(admin);
        var med = await CreateMedicineAsync(admin, schedule: "Otc");
        var po = await CreatePoAsync(admin, supplier.Id, (med.Id, 10));

        var response = await admin.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/close", new { reason = "Ordered by mistake" }, Json);

        Assert.Equal(PurchaseOrderStatus.Cancelled, (await response.Content.ReadFromJsonAsync<PurchaseOrderDto>(Json))!.Status);
    }

    [Fact]
    public async Task Suggestions_ListLowStock_WithLastSupplier_AndCountWhatsOnOrder()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var supplier = await CreateSupplierAsync(admin);
        var low = await CreateMedicineAsync(admin, schedule: "Otc", reorderLevel: 20);
        var fine = await CreateMedicineAsync(admin, schedule: "Otc", reorderLevel: 5);
        (await admin.PostAsJsonAsync("/api/inventory/receipts", new
        {
            medicineId = low.Id, batchNumber = "LOW", expiryDate = Today.AddYears(1), mrp = 10m, sellingPrice = 9m,
            purchaseRate = 6m, quantity = 5, supplierName = supplier.Name, supplierInvoiceNo = "I-1"
        }, Json)).EnsureSuccessStatusCode();
        (await ApiClient.ReceiveAsync(admin, fine.Id, "FINE", 50)).EnsureSuccessStatusCode();

        var suggestions = await admin.GetFromJsonAsync<List<ReorderSuggestionDto>>("/api/purchase-orders/suggestions", Json);

        var s = Assert.Single(suggestions!, x => x.MedicineId == low.Id);
        Assert.Equal(35, s.SuggestedQuantity);
        Assert.Equal(supplier.Id, s.LastSupplierId);
        Assert.Equal(6m, s.LastPurchaseRate);
        Assert.DoesNotContain(suggestions!, x => x.MedicineId == fine.Id);

        // Once it's on an ordered PO, it drops off the list
        var po = await CreatePoAsync(admin, supplier.Id, (low.Id, 35));
        (await admin.PostAsync($"/api/purchase-orders/{po.Id}/order", null)).EnsureSuccessStatusCode();
        var after = await admin.GetFromJsonAsync<List<ReorderSuggestionDto>>("/api/purchase-orders/suggestions", Json);
        Assert.DoesNotContain(after!, x => x.MedicineId == low.Id);
    }

    [Fact]
    public async Task Technicians_CanReceive_ButNotRaiseOrders()
    {
        var tech = await CreateStaffClientAsync(_factory, "Technician");

        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsJsonAsync("/api/suppliers", new { name = "Nope Pharma" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.PostAsJsonAsync("/api/purchase-orders",
            new { supplierId = 1, lines = new[] { new { medicineId = 1, quantity = 1 } } }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tech.GetAsync("/api/purchase-orders/suggestions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tech.GetAsync("/api/purchase-orders")).StatusCode);
    }

    [Fact]
    public async Task Supplier_DuplicateName_Conflicts_AndOpenOrdersBlockDeactivation()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var supplier = await CreateSupplierAsync(admin);
        var med = await CreateMedicineAsync(admin, schedule: "Otc");

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/suppliers", new { name = supplier.Name }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/suppliers", new { name = "Bad GSTIN Co", gstin = "123" }, Json)).StatusCode);

        var po = await CreatePoAsync(admin, supplier.Id, (med.Id, 5));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/suppliers/{supplier.Id}")).StatusCode);

        (await admin.PostAsJsonAsync($"/api/purchase-orders/{po.Id}/close", new { }, Json)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/suppliers/{supplier.Id}")).StatusCode);
    }
}
