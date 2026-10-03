using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;

namespace Pharmacy.Infrastructure.Data;

public static class DbSeeder
{
    /// <summary>
    /// Creates the first Admin when the database has no users yet, so someone can log in
    /// and issue the other staff accounts. Does nothing if any user already exists, unless
    /// <paramref name="addIfEmailMissing"/> is set (development only): then the configured
    /// Admin is added when no account has that email, so a developer can always get in.
    /// </summary>
    public static async Task SeedAdminAsync(PharmacyDbContext db, string? email, string? passwordHash,
        string fullName = "Administrator", bool addIfEmailMissing = false)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(passwordHash)) return;

        var normalized = email.Trim().ToLowerInvariant();
        if (addIfEmailMissing ? await db.Users.AnyAsync(u => u.Email == normalized) : await db.Users.AnyAsync()) return;

        db.Users.Add(new User
        {
            Email = normalized,
            FullName = fullName,
            PasswordHash = passwordHash,
            Role = UserRole.Admin
        });
        await db.SaveChangesAsync();
    }
}
