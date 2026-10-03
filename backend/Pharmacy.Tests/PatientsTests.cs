using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharmacy.Api.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Infrastructure.Data;
using static Pharmacy.Tests.ApiClient;

namespace Pharmacy.Tests;

public class PatientsTests : IClassFixture<PharmacyApiFactory>
{
    private readonly PharmacyApiFactory _factory;

    public PatientsTests(PharmacyApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Technician_CreatesPatient_WithConsentAndAge()
    {
        var tech = await CreateStaffClientAsync(_factory, "Technician");

        var patient = await CreatePatientAsync(tech, allergies: "Penicillin");

        Assert.True(patient.Id > 0);
        Assert.Equal("Penicillin", patient.Allergies);
        Assert.True(patient.Age >= 40);
        Assert.True(patient.ConsentGivenAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Create_WithoutConsent_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/patients",
            new { fullName = "No Consent", dateOfBirth = new DateOnly(1990, 1, 1), consentGiven = false }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("5876543210")]
    public async Task Create_InvalidPhone_ReturnsBadRequest(string phone)
    {
        var admin = await CreateAdminClientAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/patients",
            new { fullName = "Bad Phone", dateOfBirth = new DateOnly(1990, 1, 1), phone, consentGiven = true }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_FutureDateOfBirth_ReturnsBadRequest()
    {
        var admin = await CreateAdminClientAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/patients",
            new { fullName = "Not Born", dateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddDays(1), consentGiven = true }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_ByNameOrPhone()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var name = UniqueName("Searchable");
        var patient = await CreatePatientAsync(admin, name: name);

        var byName = await admin.GetFromJsonAsync<List<PatientDto>>($"/api/patients?search={Uri.EscapeDataString(name[..15])}", Json);
        var byPhone = await admin.GetFromJsonAsync<List<PatientDto>>("/api/patients?search=98765", Json);

        Assert.Contains(byName!, p => p.Id == patient.Id);
        Assert.Contains(byPhone!, p => p.Id == patient.Id);
    }

    [Fact]
    public async Task Update_ChangesAllergies_AndAuditsViewAndEdit()
    {
        var admin = await CreateAdminClientAsync(_factory);
        var patient = await CreatePatientAsync(admin);

        var response = await admin.PutAsJsonAsync($"/api/patients/{patient.Id}", new
        {
            fullName = patient.FullName,
            dateOfBirth = patient.DateOfBirth,
            phone = "+91 9876543210",
            allergies = "Sulfa",
            consentGiven = true
        }, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<PatientDetailDto>(Json);
        Assert.Equal("Sulfa", updated!.Allergies);
        Assert.Equal("+919876543210", updated.Phone);

        (await admin.GetAsync($"/api/patients/{patient.Id}")).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var audits = await db.AuditLogs
            .Where(a => a.EntityType == nameof(Patient) && a.EntityId == patient.Id.ToString())
            .ToListAsync();
        Assert.Contains(audits, a => a.Action == "PatientCreated");
        Assert.Contains(audits, a => a.Action == "PatientUpdated" && a.Details!.Contains("\"AllergiesChanged\":true"));
        Assert.Contains(audits, a => a.Action == "PatientViewed" && a.UserId != null);
        // Audit rows never carry the patient's personal details
        Assert.DoesNotContain(audits, a => a.Details != null && a.Details.Contains(patient.FullName));
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNotFound()
    {
        var admin = await CreateAdminClientAsync(_factory);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/patients/999999")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotReadPatients()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/patients")).StatusCode);
    }
}
