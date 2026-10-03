using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pharmacy.Infrastructure.Data;

/// <summary>
/// Used only by the <c>dotnet ef</c> tools, so migrations can be created without starting the API
/// (which needs a JWT key and seeds an Admin). Override the connection with the
/// PHARMACY_CONNECTION environment variable; defaults to the local SQL Server Express instance.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PharmacyDbContext>
{
    private const string DefaultConnection =
        "Server=localhost\\SQLEXPRESS;Database=PharmacyDb;Trusted_Connection=True;TrustServerCertificate=True;";

    public PharmacyDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("PHARMACY_CONNECTION") ?? DefaultConnection;
        var options = new DbContextOptionsBuilder<PharmacyDbContext>().UseSqlServer(connection).Options;
        return new PharmacyDbContext(options);
    }
}
