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
- **M2, patients & prescriptions:** patients with allergies and recorded consent; prescriptions
  entered by any staff member, verified by a pharmacist (with an allergy warning to acknowledge),
  then dispensed from the earliest-expiring batches first (FEFO), with refills tracked.

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
│   └── Pharmacy.Tests/           # Rule unit tests + API integration tests
└── frontend/src/app/
    ├── core/                     # Auth service, guards, interceptor, app shell
    ├── auth/                     # Login page
    ├── dashboard/                # Prescription queue and stock alert tiles
    ├── medicines/                # Catalogue list, add/edit, batches, receive & adjust stock
    ├── inventory/                # Stock alerts page
    ├── patients/                 # Patient search, add/edit, prescription history
    ├── prescriptions/            # Enter, verify/reject, dispense and refill
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

# Create or upgrade the database (migrations are in Pharmacy.Infrastructure/Data/Migrations)
cd ..
dotnet ef database update --project Pharmacy.Infrastructure --startup-project Pharmacy.Api

# After changing an entity, add a migration and apply it
dotnet ef migrations add <Name> --project Pharmacy.Infrastructure --startup-project Pharmacy.Api --output-dir Data/Migrations

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
| GET | `/api/patients?search=` (name or mobile) | Signed in |
| GET | `/api/patients/{id}` (with prescriptions; audited) | Signed in |
| POST / PUT | `/api/patients`, `/api/patients/{id}` | Signed in |
| GET | `/api/prescriptions?status=&patientId=` | Signed in |
| GET | `/api/prescriptions/{id}` | Signed in |
| POST | `/api/prescriptions` | Signed in |
| POST | `/api/prescriptions/{id}/verify` | Pharmacist |
| POST | `/api/prescriptions/{id}/reject` | Pharmacist |
| POST | `/api/prescriptions/{id}/dispense` (first fill or refill) | Pharmacist |

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

### Prescription rules (India)

- A patient is saved only after their consent is recorded (DPDP Act 2023). Opening or editing a
  patient record writes an audit row with the staff member's id, never the patient's details.
- Any staff member can enter a prescription; only a **Pharmacist** can verify, reject or dispense
  it (Pharmacy Act 1948). Admins can't, unless they also hold a Pharmacist account.
- Entered → Verified → Dispensed, or Rejected with a reason. Dispensing again is a refill and only
  covers items with refills left.
- If a recorded allergy matches a medicine's brand or generic name, verifying needs
  `acknowledgeAllergyWarnings: true`, and that override is audited.
- Schedule X needs the pharmacy to keep a copy of the prescription; NDPS drugs are refused.
- Dispensing takes stock from the earliest-expiring unexpired batches first (FEFO) and saves all
  items together or none of them.

## Next: M3, point of sale

Cart with OTC and dispensed items, GST invoice (CGST + SGST) with batch, expiry and MRP per line,
cash/card/UPI payment, and the Schedule H1 register filled in from each sale.
