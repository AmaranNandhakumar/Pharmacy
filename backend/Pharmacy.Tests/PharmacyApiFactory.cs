using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Tests;

/// <summary>
/// Hosts the real API in memory, swapping SQL Server for a private in-memory SQLite
/// database so tests need no external services and never touch real data.
/// </summary>
public class PharmacyApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@pharmacy.test";
    public const string AdminPassword = "Admin-Passw0rd!";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-at-least-32-characters-long");
        builder.UseSetting("SeedAdmin:Email", AdminEmail);
        builder.UseSetting("SeedAdmin:Password", AdminPassword);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PharmacyDbContext>>();

            // The in-memory database lives as long as this connection stays open
            _connection.Open();
            services.AddDbContext<PharmacyDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<PharmacyDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
