using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Rules;
using Pharmacy.Infrastructure.Data;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class PasswordRulesTests
{
    [Theory]
    [InlineData("Passw0rd!", null)]
    [InlineData("short1", "Password must be at least 8 characters.")]
    [InlineData("onlyletters", "Password must contain at least one letter and one number.")]
    [InlineData("1234567890", "Password must contain at least one letter and one number.")]
    public void Validate(string password, string? expected)
    {
        Assert.Equal(expected, PasswordRules.Validate(password, "someone@pharmacy.test"));
    }

    [Fact]
    public void Validate_RefusesPasswordsBuiltFromTheEmail()
    {
        Assert.NotNull(PasswordRules.Validate("priya2026", "priya@pharmacy.test"));
    }
}

public class SecurityTests : IClassFixture<PharmacyApiFactory>
{
    private readonly PharmacyApiFactory _factory;

    public SecurityTests(PharmacyApiFactory factory) => _factory = factory;

    private async Task<(UserDto user, AuthResponseDto auth)> NewStaffSessionAsync(string role = "Technician")
    {
        var admin = await CreateAdminClientAsync(_factory);
        var user = await CreateStaffAsync(admin, role);
        var auth = await (await LoginAsync(_factory.CreateClient(), user.Email, StaffPassword)).Content.ReadFromJsonAsync<AuthResponseDto>(Json);
        return (user, auth!);
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, Json);

    private HttpClient ClientWith(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    [Fact]
    public async Task Login_ReturnsShortAccessToken_AndRefreshToken()
    {
        var (_, auth) = await NewStaffSessionAsync();

        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.True(auth.ExpiresAt < DateTime.UtcNow.AddMinutes(16));
        Assert.True(auth.RefreshExpiresAt > DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task Refresh_Rotates_AndReusingTheOldTokenEndsEverySession()
    {
        var (_, first) = await NewStaffSessionAsync();

        var response = await RefreshAsync(first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = (await response.Content.ReadFromJsonAsync<AuthResponseDto>(Json))!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await ClientWith(second.Token).GetAsync("/api/auth/me")).StatusCode);

        // Someone replays the old token: refused, and the new one is revoked too
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(first.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(second.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Refresh_WithGarbage_IsUnauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync("not-a-real-token")).StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var (_, auth) = await NewStaffSessionAsync();

        var logout = await _factory.CreateClient().PostAsJsonAsync("/api/auth/logout", new { refreshToken = auth.RefreshToken }, Json);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ChecksCurrent_EnforcesPolicy_AndEndsOtherSessions()
    {
        var (user, auth) = await NewStaffSessionAsync();
        var other = (await (await LoginAsync(_factory.CreateClient(), user.Email, StaffPassword)).Content.ReadFromJsonAsync<AuthResponseDto>(Json))!;
        var client = ClientWith(auth.Token);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Wrong-Passw0rd!", newPassword = "Brand-N3w-Secret" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = StaffPassword, newPassword = "weak" }, Json)).StatusCode);

        var ok = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = StaffPassword, newPassword = "Brand-N3w-Secret" }, Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var fresh = (await ok.Content.ReadFromJsonAsync<AuthResponseDto>(Json))!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(other.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(fresh.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(_factory.CreateClient(), user.Email, StaffPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(_factory.CreateClient(), user.Email, "Brand-N3w-Secret")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "PasswordChanged" && a.EntityId == user.Id.ToString());
        Assert.Null(audit.Details);
    }

    [Fact]
    public async Task AdminReset_SetsPassword_AndSignsUserOutEverywhere()
    {
        var (user, auth) = await NewStaffSessionAsync("Pharmacist");
        var admin = await CreateAdminClientAsync(_factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await ClientWith(auth.Token).PostAsJsonAsync($"/api/users/{user.Id}/reset-password",
            new { newPassword = "Reset-By-Adm1n" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/users/{user.Id}/reset-password",
            new { newPassword = "nodigits" }, Json)).StatusCode);

        var reset = await admin.PostAsJsonAsync($"/api/users/{user.Id}/reset-password", new { newPassword = "Reset-By-Adm1n" }, Json);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(_factory.CreateClient(), user.Email, "Reset-By-Adm1n")).StatusCode);
    }

    [Fact]
    public async Task Deactivating_EndsRefreshSessions()
    {
        var (user, auth) = await NewStaffSessionAsync();
        var admin = await CreateAdminClientAsync(_factory);

        (await admin.PatchAsJsonAsync($"/api/users/{user.Id}/active", new { isActive = false }, Json)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(auth.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task CreateStaff_WithWeakPassword_IsRefused()
    {
        var admin = await CreateAdminClientAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/users",
            new { email = UniqueEmail(), fullName = "Weak Password", password = "abcdefgh", role = "Technician" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders_AndApiIsNotCached()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}

/// <summary>A host with a tiny login limit, to prove the limiter kicks in.</summary>
public class LowRateLimitFactory : PharmacyApiFactory
{
    protected override int LoginPermitLimit => 3;
}

public class RateLimitTests : IClassFixture<LowRateLimitFactory>
{
    private readonly LowRateLimitFactory _factory;

    public RateLimitTests(LowRateLimitFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_IsLimitedPerClient()
    {
        var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await LoginAsync(client, "nobody@pharmacy.test", "Wrong-Passw0rd!")).StatusCode);

        Assert.Equal(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests }, statuses);
    }
}
