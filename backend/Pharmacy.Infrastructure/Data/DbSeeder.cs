using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;

namespace Pharmacy.Infrastructure.Data;

public static class DbSeeder
{
    /// <summary>
    /// Creates the first Admin when the database has no users yet, so someone can log in
    /// and issue the other staff accounts. Does nothing if any user already exists.
    /// </summary>
    public static async Task SeedAdminAsync(PharmacyDbContext db, string? email, string? passwordHash, string fullName = "Administrator")
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(passwordHash)) return;
        if (await db.Users.AnyAsync()) return;

        db.Users.Add(new User
        {
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName,
            PasswordHash = passwordHash,
            Role = UserRole.Admin
        });
        await db.SaveChangesAsync();
    }
}
