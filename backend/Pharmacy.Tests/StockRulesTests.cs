using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;

namespace Pharmacy.Tests;

public class StockRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 3);

    [Theory]
    [InlineData(50, 45, 30, null)]
    [InlineData(50, 50, 30, null)]
    [InlineData(50, 51, 30, "Selling price can't be above the MRP.")]
    [InlineData(0, 0, 0, "MRP must be greater than zero.")]
    [InlineData(50, 45, -1, "Purchase rate can't be negative.")]
    public void ValidatePrices(double mrp, double selling, double purchase, string? expected)
    {
        Assert.Equal(expected, StockRules.ValidatePrices((decimal)mrp, (decimal)selling, (decimal)purchase));
    }

    [Fact]
    public void Apply_UpdatesQuantityVersionAndAddsMovement()
    {
        var batch = new Batch { BatchNumber = "X1", QuantityOnHand = 10 };
        var before = batch.Version;

        var movement = StockRules.Apply(batch, -4, StockMovementType.Adjustment, userId: 7, reason: "Damaged");

        Assert.Equal(6, batch.QuantityOnHand);
        Assert.NotEqual(before, batch.Version);
        Assert.Same(movement, Assert.Single(batch.Movements));
        Assert.Equal(-4, movement.Quantity);
        Assert.Equal(7, movement.UserId);
    }

    [Fact]
    public void Apply_RefusesNegativeStock()
    {
        var batch = new Batch { BatchNumber = "X2", QuantityOnHand = 3 };

        Assert.Throws<StockRuleException>(() => StockRules.Apply(batch, -4, StockMovementType.Sale, userId: 1));
        Assert.Equal(3, batch.QuantityOnHand);
        Assert.Empty(batch.Movements);
    }

    [Fact]
    public void Apply_RefusesZeroChange()
    {
        Assert.Throws<StockRuleException>(() =>
            StockRules.Apply(new Batch { QuantityOnHand = 3 }, 0, StockMovementType.Adjustment, userId: 1));
    }

    [Fact]
    public void SellableQuantity_IgnoresExpiredBatches()
    {
        var batches = new[]
        {
            new Batch { ExpiryDate = Today, QuantityOnHand = 5 },            // expires today: still sellable
            new Batch { ExpiryDate = Today.AddDays(-1), QuantityOnHand = 7 }, // expired
            new Batch { ExpiryDate = Today.AddYears(1), QuantityOnHand = 11 }
        };

        Assert.Equal(16, StockRules.SellableQuantity(batches, Today));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(18, true)]
    [InlineData(12, false)]
    [InlineData(28, false)]
    public void AllowedGstRates(double rate, bool allowed)
    {
        Assert.Equal(allowed, StockRules.AllowedGstRates.Contains((decimal)rate));
    }
}
