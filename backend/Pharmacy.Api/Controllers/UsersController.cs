using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Api.DTOs;
using Pharmacy.Api.Services;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class UsersController : ControllerBase
{
    private readonly PharmacyDbContext _db;
    private readonly IAuditService _audit;

    public UsersController(PharmacyDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
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

        _audit.Record(dto.IsActive ? "UserActivated" : "UserDeactivated", nameof(User), user.Id);
        await _db.SaveChangesAsync();

        return Ok(UserDto.From(user));
    }

    private async Task<bool> IsLastActiveAdmin(User user) =>
        user.IsActive && !await _db.Users.AnyAsync(u => u.Id != user.Id && u.Role == UserRole.Admin && u.IsActive);
}
