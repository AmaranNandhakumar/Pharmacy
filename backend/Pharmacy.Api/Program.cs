using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pharmacy.Api.Services;
using Pharmacy.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// .NET only reads user-secrets in Development, so starting the API without the launch profile
// (running the dll, an IDE without launchSettings, dotnet run --no-launch-profile) lost the JWT key.
// Read them whenever they exist on this machine; environment variables still override them,
// and the integration tests ("Testing") supply their own settings.
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
    // Re-add these so they keep the highest priority, as in the default setup
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);
}

// Controllers + Swagger (enums as strings to match the Angular client's string unions)
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Pharmacy API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database (migrations live in Pharmacy.Infrastructure, next to the DbContext)
builder.Services.AddDbContext<PharmacyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Auth
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"];
if ((string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32) && builder.Environment.IsDevelopment())
{
    // Development only: rather than refuse to start, sign tokens with a random key for this run.
    // Everyone has to log in again after a restart; set Jwt:Key in user-secrets to avoid that.
    jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
    builder.Configuration["Jwt:Key"] = jwtKey;
    Console.WriteLine("warn: Jwt:Key not found in user-secrets; using a temporary key for this run.");
}
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. In development set it with " +
        "'dotnet user-secrets set \"Jwt:Key\" \"<secret>\"'; in production use the Jwt__Key environment variable.");
}
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// CORS for Angular dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// First Admin comes from configuration (user secrets in dev), never from a public endpoint
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
    var seedPassword = app.Configuration["SeedAdmin:Password"];
    await DbSeeder.SeedAdminAsync(db,
        app.Configuration["SeedAdmin:Email"],
        string.IsNullOrWhiteSpace(seedPassword) ? null : BCrypt.Net.BCrypt.HashPassword(seedPassword),
        addIfEmailMissing: app.Environment.IsDevelopment());
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularDev");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposes the implicit Program class to WebApplicationFactory in the integration tests
public partial class Program { }
