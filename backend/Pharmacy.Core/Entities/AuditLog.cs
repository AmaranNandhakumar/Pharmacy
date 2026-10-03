namespace Pharmacy.Core.Entities;

/// <summary>Append-only record of a sensitive action (who did what to which record, and when).</summary>
public class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }

    // JSON with the changed fields; never full patient details
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
