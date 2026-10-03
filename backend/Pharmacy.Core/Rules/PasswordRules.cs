namespace Pharmacy.Core.Rules;

/// <summary>Minimum password policy for staff accounts (they open patient health records).</summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    /// <summary>Returns an error message, or null when the password is acceptable.</summary>
    public static string? Validate(string password, string? email = null)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return $"Password must be at least {MinLength} characters.";
        if (password.Length > 128)
            return "Password can't be longer than 128 characters.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Password must contain at least one letter and one number.";
        if (email is not null && string.Equals(password, email, StringComparison.OrdinalIgnoreCase))
            return "Password can't be the same as the email address.";
        var local = email?.Split('@')[0];
        if (!string.IsNullOrEmpty(local) && local.Length >= 4 && password.Contains(local, StringComparison.OrdinalIgnoreCase))
            return "Password can't contain your email name.";
        return null;
    }
}
