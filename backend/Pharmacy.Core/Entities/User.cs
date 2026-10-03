namespace Pharmacy.Core.Entities;

/// <summary>A staff account. Accounts are issued by an Admin; there is no self-registration.</summary>
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    // Soft delete: history (sales, dispenses, audit) keeps pointing at deactivated users
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
