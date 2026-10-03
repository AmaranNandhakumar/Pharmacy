using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Entities;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class AuthTests : IClassFixture<PharmacyApiFactory>
{
    private readonly PharmacyApiFactory _factory;

    public AuthTests(PharmacyApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_SeededAdmin_ReturnsTokenAndRole()
    {
        var response = await LoginAsync(_factory.CreateClient(), PharmacyApiFactory.AdminEmail, PharmacyApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(Json);
        Assert.False(string.IsNullOrEmpty(auth!.Token));
        Assert.Equal(UserRole.Admin, auth.User.Role);
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var response = await LoginAsync(_factory.CreateClient(), PharmacyApiFactory.AdminEmail.ToUpperInvariant(), PharmacyApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var response = await LoginAsync(_factory.CreateClient(), PharmacyApiFactory.AdminEmail, "Wrong-Passw0rd!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsUnauthorized()
    {
        var response = await LoginAsync(_factory.CreateClient(), UniqueEmail("ghost"), "Whatever-1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_EndpointDoesNotExist()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = UniqueEmail(), password = "Passw0rd!" }, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsCurrentUser()
    {
        var client = await CreateStaffClientAsync(_factory, "Pharmacist");

        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me", Json);

        Assert.Equal(UserRole.Pharmacist, me!.Role);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_IsAnonymous_ForContainerChecks()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithInvalidToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.real.token");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
