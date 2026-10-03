using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Rules;

/// <summary>Domain rules for stock that don't depend on EF or HTTP, so they can be unit tested on their own.</summary>
public static class StockRules
{
    /// <summary>
    /// GST rates a medicine can carry. The 12% slab was folded into 5% by the GST rate
    /// rationalisation of September 2025, so most medicines are now 5% and some are nil-rated.
    /// </summary>
    public static readonly IReadOnlySet<decimal> AllowedGstRates = new HashSet<decimal> { 0m, 5m, 18m };

    /// <summary>Returns an error message, or null when the batch prices are valid.</summary>
    public static string? ValidatePrices(decimal mrp, decimal sellingPrice, decimal purchaseRate)
    {
        if (mrp <= 0) return "MRP must be greater than zero.";
        if (sellingPrice <= 0) return "Selling price must be greater than zero.";
        if (sellingPrice > mrp) return "Selling price can't be above the MRP.";
        if (purchaseRate < 0) return "Purchase rate can't be negative.";
        return null;
    }

    /// <summary>Applies a quantity change to a batch, refusing anything that would make stock negative.</summary>
    public static StockMovement Apply(Batch batch, int quantity, StockMovementType type, int userId, string? reason = null, string? referenceId = null)
    {
        if (quantity == 0)
            throw new StockRuleException("Quantity change can't be zero.");
        if (batch.QuantityOnHand + quantity < 0)
            throw new StockRuleException($"Only {batch.QuantityOnHand} units of batch {batch.BatchNumber} are in stock.");

        batch.QuantityOnHand += quantity;
        batch.Version = Guid.NewGuid();

        var movement = new StockMovement
        {
            Batch = batch,
            Quantity = quantity,
            Type = type,
            Reason = reason,
            ReferenceId = referenceId,
            UserId = userId
        };
        batch.Movements.Add(movement);
        return movement;
    }

    /// <summary>Units that can still be sold: on hand in batches that haven't expired.</summary>
    public static int SellableQuantity(IEnumerable<Batch> batches, DateOnly today) =>
        batches.Where(b => !b.IsExpired(today)).Sum(b => b.QuantityOnHand);
}

public class StockRuleException : Exception
{
    public StockRuleException(string message) : base(message) { }
}
