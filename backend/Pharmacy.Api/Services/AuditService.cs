using System.Security.Claims;
using System.Text.Json;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Services;

public interface IAuditService
{
    /// <summary>Adds an audit row to the current unit of work; it is saved with the caller's SaveChanges.</summary>
    void Record(string action, string entityType, object? entityId, object? details = null);
}

public class AuditService : IAuditService
{
    private readonly PharmacyDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(PharmacyDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public void Record(string action, string entityType, object? entityId, object? details = null)
    {
        var sub = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = int.TryParse(sub, out var id) ? id : null,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Details = details is null ? null : JsonSerializer.Serialize(details)
        });
    }
}
