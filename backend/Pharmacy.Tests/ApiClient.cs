using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pharmacy.Api.DTOs;

namespace Pharmacy.Tests;

/// <summary>Shared helpers for talking to the API the same way the Angular client does.</summary>
public static class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public const string StaffPassword = "Staff-Passw0rd!";

    public static string UniqueEmail(string prefix = "staff") => $"{prefix}-{Guid.NewGuid():N}@pharmacy.test";

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, Json);

    public static async Task<HttpClient> CreateClientAsAsync(PharmacyApiFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        var response = await LoginAsync(client, email, password);
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    public static Task<HttpClient> CreateAdminClientAsync(PharmacyApiFactory factory) =>
        CreateClientAsAsync(factory, PharmacyApiFactory.AdminEmail, PharmacyApiFactory.AdminPassword);

    /// <summary>Has the seeded Admin issue a new staff account and returns it.</summary>
    public static async Task<UserDto> CreateStaffAsync(HttpClient admin, string role, string? email = null)
    {
        var response = await admin.PostAsJsonAsync("/api/users",
            new { email = email ?? UniqueEmail(role.ToLowerInvariant()), fullName = $"Test {role}", password = StaffPassword, role }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>(Json))!;
    }

    /// <summary>Creates a staff account with the given role and returns a client logged in as them.</summary>
    public static async Task<HttpClient> CreateStaffClientAsync(PharmacyApiFactory factory, string role)
    {
        var admin = await CreateAdminClientAsync(factory);
        var staff = await CreateStaffAsync(admin, role);
        return await CreateClientAsAsync(factory, staff.Email, StaffPassword);
    }
}
