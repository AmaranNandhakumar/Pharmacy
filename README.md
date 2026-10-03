# Pharmacy

A pharmacy management web app for a retail pharmacy in India (inventory, prescriptions,
patients, GST billing), built as a self-directed learning project. Plan and design: [`docs/01-design-and-planning.md`](docs/01-design-and-planning.md) ·
architecture: [`docs/02-architecture.md`](docs/02-architecture.md).

**Current milestone: M0, scaffold & auth.** Staff log in with a role (Admin, Pharmacist,
Technician); an Admin manages staff accounts. There is no public sign-up.

| Layer    | Technology |
|----------|------------|
| Frontend | Angular 17 (standalone components, signals), Reactive Forms |
| Backend  | ASP.NET Core 8 Web API, EF Core 8 |
| Database | SQL Server |
| Auth     | JWT bearer with role claims, BCrypt |
| Tests    | xUnit + WebApplicationFactory + in-memory SQLite |

## Structure

```
Pharmacy/
├── docs/                         # Design & planning, architecture
├── backend/
│   ├── Pharmacy.sln
│   ├── Pharmacy.Core/            # Entities and domain rules (no EF, no ASP.NET)
│   ├── Pharmacy.Infrastructure/  # EF Core DbContext, seeding, migrations
│   ├── Pharmacy.Api/             # Controllers, DTOs, JWT + audit services
│   └── Pharmacy.Tests/           # API integration tests
└── frontend/src/app/
    ├── core/                     # Auth service, guards, interceptor, app shell
    ├── auth/                     # Login page
    ├── dashboard/
    └── users/                    # Staff management (Admin only)
```

## Getting started

Prerequisites are the same as TaskManager: .NET SDK 8+, Node LTS, SQL Server Express, `dotnet-ef`.

### Backend

```bash
cd backend/Pharmacy.Api
dotnet restore

# Secrets live in user-secrets, never in appsettings.json
dotnet user-secrets set "Jwt:Key" "<random string of 32+ characters>"
dotnet user-secrets set "SeedAdmin:Email" "you@example.com"
dotnet user-secrets set "SeedAdmin:Password" "<a strong password>"

# First run only: create the initial migration, then the database
cd ..
dotnet ef migrations add InitialCreate --project Pharmacy.Infrastructure --startup-project Pharmacy.Api
dotnet ef database update --project Pharmacy.Infrastructure --startup-project Pharmacy.Api

cd Pharmacy.Api
dotnet run
```

On startup, if the database has no users, the API creates an Admin from `SeedAdmin:Email` /
`SeedAdmin:Password`. Log in with that account and add the rest of the staff from the **Staff** page.

The API runs on `https://localhost:5101` (Swagger at `/swagger`). Ports differ from TaskManager
(5001) so both can run side by side.

### Frontend

```bash
cd frontend
npm install
npm start
```

Open `http://localhost:4200`.

### Tests

```bash
cd backend
dotnet test
```

## API (M0)

| Method | Endpoint | Who |
|--------|----------|-----|
| POST | `/api/auth/login` | Anyone |
| GET | `/api/auth/me` | Signed in |
| GET | `/api/users` | Admin |
| GET | `/api/users/{id}` | Admin |
| POST | `/api/users` | Admin |
| PUT | `/api/users/{id}` | Admin (name, role) |
| PATCH | `/api/users/{id}/active` | Admin |

User creation, role changes and (de)activation are written to the `AuditLogs` table.
The last active Admin can't be demoted or deactivated.

## Next: M1, catalogue & inventory

Medicines CRUD, receiving stock into batches with expiry dates, stock adjustments with a reason,
and a low-stock / expiring-soon list.
