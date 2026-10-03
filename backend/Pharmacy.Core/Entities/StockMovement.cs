namespace Pharmacy.Core.Entities;

public enum StockMovementType
{
    Receipt,
    Sale,
    Dispense,
    Adjustment,
    Return
}

/// <summary>Append-only change to a batch's quantity (positive in, negative out).</summary>
public class StockMovement
{
    public long Id { get; set; }
    public int BatchId { get; set; }
    public Batch Batch { get; set; } = null!;

    public int Quantity { get; set; }
    public StockMovementType Type { get; set; }
    public string? Reason { get; set; }

    /// <summary>Id of the sale, prescription or receipt that caused the movement, when there is one.</summary>
    public string? ReferenceId { get; set; }

    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
