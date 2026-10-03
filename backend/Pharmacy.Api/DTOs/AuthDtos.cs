using System.ComponentModel.DataAnnotations;
using Pharmacy.Core.Entities;

namespace Pharmacy.Api.DTOs;

public class LoginDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    /// <summary>Short-lived access token (minutes) sent as "Authorization: Bearer ...".</summary>
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Single-use token for POST /api/auth/refresh; valid for days.</summary>
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}

public class RefreshRequestDto
{
    [Required, MaxLength(200)]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required, MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public static UserDto From(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        FullName = u.FullName,
        Role = u.Role,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };
}
