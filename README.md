# Pharmacy

A pharmacy management web app for a retail pharmacy in India (inventory, prescriptions,
patients, GST billing), built as a self-directed learning project. Plan and design: [`docs/01-design-and-planning.md`](docs/01-design-and-planning.md) ·
architecture: [`docs/02-architecture.md`](docs/02-architecture.md).

**Done so far**
- **M0, scaffold & auth:** staff log in with a role (Admin, Pharmacist, Technician); an Admin
  manages staff accounts. There is no public sign-up.
- **M1, catalogue & inventory:** medicines with Indian drug schedule, HSN code and GST rate;
  stock received in batches with expiry date and MRP; stock adjustments with a reason; alerts
  for expired, expiring-soon and low stock.

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
    ├── dashboard/                # Stock alert tiles
    ├── medicines/                # Catalogue list, add/edit, batches, receive & adjust stock
    ├── inventory/                # Stock alerts page
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

## API

| Method | Endpoint | Who |
|--------|----------|-----|
| POST | `/api/auth/login` | Anyone |
| GET | `/api/auth/me` | Signed in |
| GET | `/api/users` | Admin |
| GET | `/api/users/{id}` | Admin |
| POST | `/api/users` | Admin |
| PUT | `/api/users/{id}` | Admin (name, role) |
| PATCH | `/api/users/{id}/active` | Admin |
| GET | `/api/medicines?search=&includeInactive=` | Signed in |
| GET | `/api/medicines/{id}` (with batches) | Signed in |
| POST / PUT | `/api/medicines`, `/api/medicines/{id}` | Admin, Pharmacist |
| DELETE | `/api/medicines/{id}` (soft delete) | Admin, Pharmacist |
| GET | `/api/inventory/batches?medicineId=&includeEmpty=` | Signed in |
| GET | `/api/inventory/batches/{id}/movements` | Admin, Pharmacist |
| POST | `/api/inventory/receipts` | Signed in |
| POST | `/api/inventory/adjustments` | Admin, Pharmacist |
| GET | `/api/inventory/alerts?expiringWithinDays=90` | Signed in |

User changes, medicine changes, stock receipts and adjustments are written to the `AuditLogs`
table. The last active Admin can't be demoted or deactivated.

### Stock rules (India)

- GST rate must be 0, 5 or 18% (the 12% slab was merged into 5% in September 2025).
- HSN code is 4, 6 or 8 digits (medicines are usually 3003 / 3004).
- Selling price can never be above the batch's printed MRP; prices are GST-inclusive.
- Expired stock can't be received, and expired batches don't count as sellable.
- Receiving a batch number that's already on the shelf adds to it only if expiry and MRP match.
- Stock can never go negative; every change writes a `StockMovements` row.
- Expiry is entered as month/year, as printed on Indian packs, and stored as the last day of that month.

## Next: M2, patients & prescriptions

Patients with allergies; prescriptions entered with the doctor's registration number, verified
by a pharmacist, and dispensed from the earliest-expiring batch first (FEFO), with the
Schedule H1 register filled in automatically.
