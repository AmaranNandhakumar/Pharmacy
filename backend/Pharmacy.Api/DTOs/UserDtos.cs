using System.ComponentModel.DataAnnotations;
using Pharmacy.Core.Entities;

namespace Pharmacy.Api.DTOs;

public class CreateUserDto
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(2), MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, EnumDataType(typeof(UserRole))]
    public UserRole Role { get; set; }
}

public class UpdateUserDto
{
    [Required, MinLength(2), MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EnumDataType(typeof(UserRole))]
    public UserRole Role { get; set; }
}

public class SetActiveDto
{
    public bool IsActive { get; set; }
}
