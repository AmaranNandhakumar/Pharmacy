namespace Pharmacy.Core.Entities;

/// <summary>
/// A long-lived token that buys a new short-lived access token. Only its SHA-256 hash is stored, so a
/// database leak doesn't hand out sessions. Each use replaces it with a new one (rotation); presenting
/// an already-used token means it was stolen, so the whole family for that user is revoked.
/// </summary>
public class RefreshToken
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    /// <summary>Why it stopped working: "Rotated", "Logout", "PasswordChanged", "ReuseDetected", ...</summary>
    public string? RevokedReason { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
