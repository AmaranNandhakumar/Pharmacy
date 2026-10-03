using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Api.DTOs;
using Pharmacy.Api.Services;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Controllers;

/// <summary>The append-only audit trail: who did what to which record, and when. Admin only; there is no edit or delete.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AuditController : ControllerBase
{
    private const int MaxPageSize = 200;

    private readonly PharmacyDbContext _db;
    private readonly TimeProvider _time;

    public AuditController(PharmacyDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    [HttpGet]
    public async Task<ActionResult<AuditPageDto>> Get(
        [FromQuery] string? entityType, [FromQuery] string? entityId, [FromQuery] string? action, [FromQuery] int? userId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) return BadRequest(new { message = "page starts at 1." });
        if (pageSize is < 1 or > MaxPageSize) return BadRequest(new { message = $"pageSize must be 1 to {MaxPageSize}." });

        var query = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId)) query = query.Where(a => a.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (userId is not null) query = query.Where(a => a.UserId == userId);
        if (from is not null || to is not null)
        {
            var today = _time.LocalToday();
            var (start, end) = _time.UtcRange(from ?? DateOnly.MinValue.AddDays(1), to ?? today);
            query = query.Where(a => a.CreatedAt >= start && a.CreatedAt < end);
        }

        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var userIds = rows.Where(r => r.UserId != null).Select(r => r.UserId!.Value).Distinct().ToList();
        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

        return Ok(new AuditPageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = rows.Select(a => new AuditLogDto
            {
                Id = a.Id,
                CreatedAt = a.CreatedAt,
                UserId = a.UserId,
                UserName = a.UserId is null ? null : names.GetValueOrDefault(a.UserId.Value),
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details
            }).ToList(),
            // For the filter drop-downs
            Actions = await _db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(),
            EntityTypes = await _db.AuditLogs.Select(a => a.EntityType).Distinct().OrderBy(a => a).ToListAsync()
        });
    }
}
