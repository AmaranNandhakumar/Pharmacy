using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Api.DTOs;
using Pharmacy.Api.Services;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly PharmacyDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly RefreshTokenService _refresh;
    private readonly IAuditService _audit;

    public AuthController(PharmacyDbContext db, ITokenService tokenService, RefreshTokenService refresh, IAuditService audit)
    {
        _db = db;
        _tokenService = tokenService;
        _refresh = refresh;
        _audit = audit;
    }

    /// <summary>Rate limited per client address, so passwords can't be guessed at speed.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Same message for unknown, wrong password and deactivated, so accounts can't be probed
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var response = Respond(user);
        await _db.SaveChangesAsync();
        return Ok(response);
    }

    /// <summary>Swaps a refresh token for a new access token and a new refresh token.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ActionResult<AuthResponseDto>> Refresh(RefreshRequestDto dto)
    {
        var result = await _refresh.RotateAsync(dto.RefreshToken);
        if (result.User is null)
        {
            await _db.SaveChangesAsync(); // keeps a reuse-detection revocation
            return Unauthorized(new { message = result.Error });
        }

        var (token, expiresAt) = _tokenService.GenerateToken(result.User);
        await _db.SaveChangesAsync();
        return Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            RefreshToken = result.NewToken!,
            RefreshExpiresAt = result.NewExpiresAt,
            User = UserDto.From(result.User)
        });
    }

    /// <summary>Ends this session. The access token still works until it expires (minutes); the refresh token stops now.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequestDto dto)
    {
        await _refresh.RevokeAsync(dto.RefreshToken, "Logout");
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Ends every session of the signed-in user, on every device.</summary>
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var count = await _refresh.RevokeAllAsync(User.GetUserId(), "LogoutAll");
        _audit.Record("LoggedOutEverywhere", nameof(User), User.GetUserId(), new { Sessions = count });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user is null || !user.IsActive) return Unauthorized();
        return Ok(UserDto.From(user));
    }

    /// <summary>Any staff member changes their own password. Signs out their other sessions.</summary>
    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ActionResult<AuthResponseDto>> ChangePassword(ChangePasswordDto dto)
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user is null || !user.IsActive) return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is wrong." });
        if (dto.NewPassword == dto.CurrentPassword)
            return BadRequest(new { message = "Choose a password different from the current one." });
        var error = PasswordRules.Validate(dto.NewPassword, user.Email);
        if (error is not null) return BadRequest(new { message = error });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _refresh.RevokeAllAsync(user.Id, "PasswordChanged");
        _audit.Record("PasswordChanged", nameof(User), user.Id);

        // A fresh session for this device, so the user stays signed in here
        var response = Respond(user);
        await _db.SaveChangesAsync();
        return Ok(response);
    }

    private AuthResponseDto Respond(User user)
    {
        var (token, expiresAt) = _tokenService.GenerateToken(user);
        var (refresh, refreshExpiresAt) = _refresh.Issue(user);
        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            RefreshToken = refresh,
            RefreshExpiresAt = refreshExpiresAt,
            User = UserDto.From(user)
        };
    }
}
