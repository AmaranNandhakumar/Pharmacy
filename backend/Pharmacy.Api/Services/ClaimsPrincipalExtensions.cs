using System.Security.Claims;

namespace Pharmacy.Api.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in staff member's id, from the token's "sub" claim.</summary>
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
