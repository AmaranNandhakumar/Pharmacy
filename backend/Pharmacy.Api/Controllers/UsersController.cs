using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Api.DTOs;
using Pharmacy.Api.Services;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class UsersController : ControllerBase
{
    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;
    private readonly RefreshTokenService _refresh;

    public UsersController(PharmacyDbContext db, IAuditService audit, RefreshTokenService refresh)
    {
        _db = db;
        _audit = audit;
        _refresh = refresh;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        var users = await _db.Users.OrderBy(u => u.FullName).ToListAsync();
        return Ok(users.Select(UserDto.From).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
    {
        var user = await _db.Users.FindAsync(id);
        return user is null ? NotFound() : Ok(UserDto.From(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var passwordError = PasswordRules.Validate(dto.Password, email);
        if (passwordError is not null) return BadRequest(new { message = passwordError });
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "A user with this email already exists." });

        var user = new User
        {
            Email = email,
            FullName = dto.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _audit.Record("UserCreated", nameof(User), user.Id, new { user.Email, Role = user.Role.ToString() });
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, UserDto.From(user));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (user.Role == UserRole.Admin && dto.Role != UserRole.Admin && await IsLastActiveAdmin(user))
            return BadRequest(new { message = "The last active Admin can't be demoted." });

        var oldRole = user.Role;
        user.FullName = dto.FullName.Trim();
        user.Role = dto.Role;

        _audit.Record("UserUpdated", nameof(User), user.Id,
            new { user.FullName, OldRole = oldRole.ToString(), NewRole = user.Role.ToString() });
        await _db.SaveChangesAsync();

        return Ok(UserDto.From(user));
    }

    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<UserDto>> SetActive(int id, SetActiveDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (!dto.IsActive && user.Role == UserRole.Admin && await IsLastActiveAdmin(user))
            return BadRequest(new { message = "The last active Admin can't be deactivated." });

        user.IsActive = dto.IsActive;
        // A deactivated account must not keep refreshing its way back in
        if (!dto.IsActive) await _refresh.RevokeAllAsync(user.Id, "Deactivated");

        _audit.Record(dto.IsActive ? "UserActivated" : "UserDeactivated", nameof(User), user.Id);
        await _db.SaveChangesAsync();

        return Ok(UserDto.From(user));
    }

    /// <summary>Admin sets a new password for a staff member who forgot theirs, and signs them out everywhere.</summary>
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        var error = PasswordRules.Validate(dto.NewPassword, user.Email);
        if (error is not null) return BadRequest(new { message = error });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        var sessions = await _refresh.RevokeAllAsync(user.Id, "PasswordReset");
        _audit.Record("PasswordReset", nameof(User), user.Id, new { SessionsEnded = sessions });
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<bool> IsLastActiveAdmin(User user) =>
        user.IsActive && !await _db.Users.AnyAsync(u => u.Id != user.Id && u.Role == UserRole.Admin && u.IsActive);
}
