using Pharmacy.Core.Entities;

namespace Pharmacy.Core.Rules;

/// <summary>The purchase order workflow and the reorder suggestion.</summary>
public static class PurchaseOrderRules
{
    public static bool IsEditable(PurchaseOrder po) => po.Status == PurchaseOrderStatus.Draft;

    public static bool CanReceive(PurchaseOrder po) =>
        po.Status is PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived;

    /// <summary>Sends a draft to the supplier. From here on its lines can't be changed.</summary>
    public static void MarkOrdered(PurchaseOrder po, DateTime now)
    {
        if (po.Status != PurchaseOrderStatus.Draft)
            throw new PurchaseOrderRuleException($"Only a draft can be ordered; this one is {po.Status}.");
        if (po.Lines.Count == 0)
            throw new PurchaseOrderRuleException("Add at least one medicine before ordering.");

        po.Status = PurchaseOrderStatus.Ordered;
        po.OrderedAt = now;
    }

    /// <summary>Counts a delivered quantity against a line. Receiving more than was ordered is refused.</summary>
    public static void RecordReceipt(PurchaseOrder po, PurchaseOrderLine line, int quantity)
    {
        if (!CanReceive(po))
            throw new PurchaseOrderRuleException($"Stock can be received only against an ordered purchase order; this one is {po.Status}.");
        if (quantity <= 0)
            throw new PurchaseOrderRuleException("Quantity must be greater than zero.");
        if (quantity > line.QuantityOutstanding)
            throw new PurchaseOrderRuleException(
                $"Only {line.QuantityOutstanding} more units of {line.Medicine?.Name ?? "this medicine"} were ordered.");

        line.QuantityReceived += quantity;
    }

    /// <summary>After a delivery: Received when every line is complete, otherwise PartiallyReceived.</summary>
    public static void UpdateStatusAfterReceipt(PurchaseOrder po, DateTime now)
    {
        if (po.Lines.All(l => l.QuantityOutstanding == 0))
        {
            po.Status = PurchaseOrderStatus.Received;
            po.CompletedAt = now;
        }
        else if (po.Lines.Any(l => l.QuantityReceived > 0))
        {
            po.Status = PurchaseOrderStatus.PartiallyReceived;
        }
    }

    /// <summary>Gives up on what hasn't arrived: Closed if anything came, Cancelled if nothing did.</summary>
    public static void Close(PurchaseOrder po, DateTime now)
    {
        if (po.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Closed or PurchaseOrderStatus.Cancelled)
            throw new PurchaseOrderRuleException($"This purchase order is already {po.Status}.");

        po.Status = po.Lines.Any(l => l.QuantityReceived > 0) ? PurchaseOrderStatus.Closed : PurchaseOrderStatus.Cancelled;
        po.CompletedAt = now;
    }

    /// <summary>
    /// How much to order for a medicine at or below its reorder level: enough to reach twice the reorder
    /// level, less what is already on order. Never less than the reorder level itself, and at least 1.
    /// Returns 0 when nothing needs ordering.
    /// </summary>
    public static int SuggestQuantity(int sellableQuantity, int reorderLevel, int alreadyOnOrder)
    {
        if (sellableQuantity + alreadyOnOrder > reorderLevel) return 0;

        var target = Math.Max(reorderLevel * 2, 1);
        var needed = target - sellableQuantity - alreadyOnOrder;
        return Math.Max(needed, Math.Max(reorderLevel, 1));
    }

    public static string PoNumber(string financialYear, int sequence) => $"PO/{financialYear}/{sequence:0000}";
}

public class PurchaseOrderRuleException : Exception
{
    public PurchaseOrderRuleException(string message) : base(message) { }
}
