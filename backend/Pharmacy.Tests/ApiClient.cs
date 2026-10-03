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

    public static string UniqueName(string prefix = "Med") => $"{prefix} {Guid.NewGuid():N}"[..24];

    /// <summary>Creates a medicine (as whoever <paramref name="client"/> is signed in as) and returns it.</summary>
    public static async Task<MedicineDetailDto> CreateMedicineAsync(HttpClient client, string? name = null,
        string schedule = "H", decimal gst = 5m, int reorderLevel = 10, string? barcode = null, string? genericName = null)
    {
        var response = await client.PostAsJsonAsync("/api/medicines", new
        {
            name = name ?? UniqueName(),
            genericName,
            strength = "500 mg",
            form = "Tablet",
            packSize = "10 tablets",
            manufacturer = "Test Pharma",
            barcode,
            schedule,
            hsnCode = "3004",
            gstRatePercent = gst,
            reorderLevel
        }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MedicineDetailDto>(Json))!;
    }

    public static Task<HttpResponseMessage> ReceiveAsync(HttpClient client, int medicineId, string batchNumber, int quantity,
        DateOnly? expiry = null, decimal mrp = 50m, decimal sellingPrice = 45m, decimal purchaseRate = 30m) =>
        client.PostAsJsonAsync("/api/inventory/receipts", new
        {
            medicineId,
            batchNumber,
            expiryDate = expiry ?? DateOnly.FromDateTime(DateTime.Today).AddYears(1),
            mrp,
            sellingPrice,
            purchaseRate,
            quantity,
            supplierName = "Test Distributor",
            supplierInvoiceNo = "INV-001"
        }, Json);

    /// <summary>Creates a fake patient (never real data) and returns it.</summary>
    public static async Task<PatientDetailDto> CreatePatientAsync(HttpClient client, string? allergies = null, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/api/patients", new
        {
            fullName = name ?? UniqueName("Patient"),
            dateOfBirth = new DateOnly(1985, 6, 15),
            phone = "9876543210",
            address = "12 Test Street, Chennai",
            allergies,
            consentGiven = true
        }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientDetailDto>(Json))!;
    }

    public static Task<HttpResponseMessage> PostPrescriptionAsync(HttpClient client, int patientId, bool copyRetained = false,
        params (int medicineId, int quantity, int refills)[] items) =>
        client.PostAsJsonAsync("/api/prescriptions", new
        {
            patientId,
            prescriberName = "Dr. Test Doctor",
            prescriberRegNo = "TN-12345",
            prescriberAddress = "City Clinic, Chennai",
            issuedOn = DateOnly.FromDateTime(DateTime.Today),
            copyRetained,
            items = items.Select(i => new
            {
                medicineId = i.medicineId,
                dose = "1 tablet",
                quantity = i.quantity,
                directions = "Twice a day after food",
                refillsAllowed = i.refills
            })
        }, Json);

    public static async Task<PrescriptionDto> CreatePrescriptionAsync(HttpClient client, int patientId,
        params (int medicineId, int quantity, int refills)[] items)
    {
        var response = await PostPrescriptionAsync(client, patientId, false, items);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PrescriptionDto>(Json))!;
    }
}
