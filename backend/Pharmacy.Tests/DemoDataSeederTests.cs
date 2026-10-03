using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Tests;

/// <summary>The demo data must obey the same invariants as data the app creates.</summary>
public class DemoDataSeederTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private PharmacyDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new PharmacyDbContext(new DbContextOptionsBuilder<PharmacyDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        await new DemoDataSeeder(_db, DateTime.UtcNow).SeedAsync("owner@demo.local", BCrypt.Net.BCrypt.HashPassword("x", 4));
        _db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task FillsEveryArea()
    {
        Assert.True(await _db.Medicines.CountAsync() >= 15);
        Assert.Equal(3, await _db.Suppliers.CountAsync());
        Assert.True(await _db.Patients.CountAsync() >= 5);
        Assert.True(await _db.Sales.CountAsync() >= 30);
        Assert.Equal(3, await _db.Users.CountAsync());
        foreach (var status in Enum.GetValues<PrescriptionStatus>())
            Assert.True(await _db.Prescriptions.AnyAsync(p => p.Status == status), $"No {status} prescription");
        Assert.True(await _db.Sales.AnyAsync(s => s.Status == SaleStatus.Voided));
        Assert.True(await _db.ScheduleRegister.AnyAsync(r => r.Schedule == DrugSchedule.H1));
        Assert.True(await _db.PrescriptionFills.AnyAsync(f => f.SaleId == null), "No fill waiting to be billed");
        Assert.True(await _db.PurchaseOrders.AnyAsync(p => p.Status == PurchaseOrderStatus.Received));
        Assert.True(await _db.PurchaseOrders.AnyAsync(p => p.Status == PurchaseOrderStatus.Draft));
    }

    [Fact]
    public async Task StockOnHand_EqualsSumOfMovements_AndIsNeverNegative()
    {
        var batches = await _db.Batches.Include(b => b.Movements).ToListAsync();

        Assert.All(batches, b =>
        {
            Assert.Equal(b.Movements.Sum(m => m.Quantity), b.QuantityOnHand);
            Assert.True(b.QuantityOnHand >= 0);
        });
        Assert.Contains(batches, b => b.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow) && b.QuantityOnHand > 0);
    }

    [Fact]
    public async Task Invoices_AddUp_AndNumbersAreUnique()
    {
        var sales = await _db.Sales.Include(s => s.Items).ToListAsync();

        Assert.Equal(sales.Count, sales.Select(s => s.InvoiceNo).Distinct().Count());
        Assert.All(sales, s =>
        {
            Assert.Equal(s.Items.Sum(i => i.LineTotal), s.Total);
            Assert.Equal(s.Total, s.TaxableValue + s.Cgst + s.Sgst);
            Assert.True(s.Items.Count > 0);
        });
    }

    [Fact]
    public async Task PrescriptionMedicines_AreOnlySoldThroughPrescriptions()
    {
        var items = await _db.SaleItems.Include(i => i.Medicine).ToListAsync();

        Assert.All(items.Where(i => i.PrescriptionItemId == null),
            i => Assert.True(i.Medicine.Schedule is DrugSchedule.Otc or DrugSchedule.G, $"{i.MedicineName} sold off the shelf"));
        Assert.DoesNotContain(items, i => i.Medicine.Schedule == DrugSchedule.Ndps);
    }

    [Fact]
    public async Task SecondRun_IsRefused()
    {
        var seeder = new DemoDataSeeder(_db, DateTime.UtcNow);

        Assert.False(await seeder.CanSeedAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync("owner@demo.local", "hash"));
    }
}
