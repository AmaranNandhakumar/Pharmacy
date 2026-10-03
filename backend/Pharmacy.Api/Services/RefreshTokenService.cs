using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Services;

public record RotationResult(User? User, string? NewToken, DateTime NewExpiresAt, string? Error);

/// <summary>Issues, rotates and revokes refresh tokens. Changes are added to the unit of work; callers save.</summary>
public class RefreshTokenService
{
    private readonly PharmacyDbContext _db;
    private readonly TimeProvider _time;
    private readonly TimeSpan _lifetime;

    public RefreshTokenService(PharmacyDbContext db, TimeProvider time, IConfiguration config)
    {
        _db = db;
        _time = time;
        _lifetime = TimeSpan.FromDays(double.Parse(config["Jwt:RefreshDays"] ?? "7"));
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public (string token, DateTime expiresAt) Issue(User user)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiresAt = Now.Add(_lifetime);
        _db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = Hash(raw), CreatedAt = Now, ExpiresAt = expiresAt });
        return (raw, expiresAt);
    }

    /// <summary>
    /// Swaps a valid refresh token for a new one. A token that was already used or revoked is treated as
    /// stolen: every session of that user is ended, and they have to log in again.
    /// </summary>
    public async Task<RotationResult> RotateAsync(string raw)
    {
        var token = await _db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == Hash(raw));
        if (token is null) return new(null, null, default, "Invalid refresh token.");

        if (token.RevokedAt is not null)
        {
            if (token.RevokedReason == "Rotated")
                await RevokeAllAsync(token.UserId, "ReuseDetected");
            return new(null, null, default, "Invalid refresh token.");
        }
        if (token.ExpiresAt <= Now) return new(null, null, default, "Session expired. Please log in again.");
        if (!token.User.IsActive) return new(null, null, default, "Invalid refresh token.");

        token.RevokedAt = Now;
        token.RevokedReason = "Rotated";
        var (newRaw, expiresAt) = Issue(token.User);
        return new(token.User, newRaw, expiresAt, null);
    }

    public async Task RevokeAsync(string raw, string reason)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == Hash(raw));
        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = Now;
            token.RevokedReason = reason;
        }
    }

    public async Task<int> RevokeAllAsync(int userId, string reason)
    {
        var active = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync();
        foreach (var t in active)
        {
            t.RevokedAt = Now;
            t.RevokedReason = reason;
        }
        return active.Count;
    }

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
