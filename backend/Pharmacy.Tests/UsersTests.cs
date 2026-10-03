using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class UsersTests : IClassFixture<PharmacyApiFactory>
{
    private readonly PharmacyApiFactory _factory;

    public UsersTests(PharmacyApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_CreatesStaff_WhoCanLogIn()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var email = UniqueEmail("tech");

        var staff = await CreateStaffAsync(admin, "Technician", email);

        Assert.Equal(UserRole.Technician, staff.Role);
        Assert.True(staff.IsActive);
        var login = await LoginAsync(_factory.CreateClient(), email, StaffPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateEmail_ReturnsConflict()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var email = UniqueEmail();
        await CreateStaffAsync(admin, "Technician", email);

        var response = await admin.PostAsJsonAsync("/api/users",
            new { email = email.ToUpperInvariant(), fullName = "Someone Else", password = StaffPassword, role = "Pharmacist" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "Valid Name", "Passw0rd!", "Technician")]
    [InlineData("valid@pharmacy.test", "Valid Name", "short", "Technician")]
    [InlineData("valid@pharmacy.test", "Valid Name", "Passw0rd!", "Janitor")]
    public async Task Create_InvalidInput_ReturnsBadRequest(string email, string fullName, string password, string role)
    {
        var admin = await CreateAdminClientAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/users", new { email, fullName, password, role }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Pharmacist")]
    [InlineData("Technician")]
    public async Task NonAdmins_CannotManageUsers(string role)
    {
        var client = await CreateStaffClientAsync(_factory, role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users",
            new { email = UniqueEmail(), fullName = "Sneaky Admin", password = StaffPassword, role = "Admin" }, Json)).StatusCode);
    }

    [Fact]
    public async Task Update_ChangesRole_AndWritesAudit()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var staff = await CreateStaffAsync(admin, "Technician");

        var response = await admin.PutAsJsonAsync($"/api/users/{staff.Id}",
            new { fullName = "Promoted Person", role = "Pharmacist" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.Equal(UserRole.Pharmacist, updated!.Role);
        Assert.Equal("Promoted Person", updated.FullName);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var actions = await db.AuditLogs
            .Where(a => a.EntityType == nameof(User) && a.EntityId == staff.Id.ToString())
            .Select(a => a.Action)
            .ToListAsync();
        Assert.Contains("UserCreated", actions);
        Assert.Contains("UserUpdated", actions);
    }

    [Fact]
    public async Task DeactivatedUser_CannotLogIn()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var staff = await CreateStaffAsync(admin, "Technician");

        var response = await admin.PatchAsJsonAsync($"/api/users/{staff.Id}/active", new { isActive = false }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await LoginAsync(_factory.CreateClient(), staff.Email, StaffPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task LastActiveAdmin_CannotBeDeactivatedOrDemoted()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/api/users", Json);
        var activeAdmins = users!.Where(u => u.Role == UserRole.Admin && u.IsActive).ToList();

        // Other tests in this class never create Admins, so the seeded one is the only one
        var seeded = Assert.Single(activeAdmins);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/users/{seeded.Id}/active",
            new { isActive = false }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{seeded.Id}",
            new { fullName = seeded.FullName, role = "Technician" }, Json)).StatusCode);
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNotFound()
    {
        var admin = await CreateAdminClientAsync(_factory);

        var response = await admin.GetAsync("/api/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
