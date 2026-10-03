using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Rules;

/// <summary>Picks stock First-Expiry-First-Out: the batch that expires soonest is used first.</summary>
public static class FefoAllocator
{
    /// <summary>
    /// Splits <paramref name="quantity"/> across the given batches, earliest expiry first, skipping expired
    /// and empty batches. Throws <see cref="StockRuleException"/> when there isn't enough sellable stock.
    /// Doesn't change the batches; apply the result with <see cref="StockRules.Apply"/>.
    /// </summary>
    public static IReadOnlyList<(Batch Batch, int Quantity)> Allocate(IEnumerable<Batch> batches, int quantity, DateOnly today)
    {
        if (quantity <= 0)
            throw new StockRuleException("Quantity must be greater than zero.");

        var candidates = batches
            .Where(b => b.QuantityOnHand > 0 && !b.IsExpired(today))
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.Id)
            .ToList();

        var available = candidates.Sum(b => b.QuantityOnHand);
        if (available < quantity)
            throw new StockRuleException($"Only {available} units in sellable stock; {quantity} needed.");

        var picks = new List<(Batch, int)>();
        var remaining = quantity;
        foreach (var batch in candidates)
        {
            var take = Math.Min(batch.QuantityOnHand, remaining);
            picks.Add((batch, take));
            remaining -= take;
            if (remaining == 0) break;
        }
        return picks;
    }
}
