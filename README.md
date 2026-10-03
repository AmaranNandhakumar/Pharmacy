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
- **M3, point of sale:** a billing counter for shelf (OTC / Schedule G) items and dispensed
  prescriptions on one GST invoice (CGST + SGST, batch, expiry and MRP per line), cash/UPI/card,
  printable A5 invoice, sales history, Admin-only voids, and the Schedule H1 / X register.
- **M4, reports & audit:** daily sales by payment method, GST summary by rate and HSN (for
  GSTR-3B / GSTR-1), stock valuation at cost and selling price, expiring stock with money at risk,
  CSV download for each, and an Admin audit log viewer.
- **M5, purchasing:** suppliers (GSTIN, wholesale drug licence), purchase orders drafted from the
  low-stock list with suggested quantities, a printable PO, and deliveries received against it
  (partial deliveries, several batches per line, close short or cancel).

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
    ├── sales/                    # Counter (POS), invoice, sales list, H1 register, settings
    ├── reports/                  # Reports (CSV export) and audit log
    ├── purchasing/               # Suppliers, purchase orders, receive deliveries
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

If Windows Smart App Control blocks a freshly built DLL ("An Application Control policy has
blocked this file"), or the running API locks the Debug output, build the tests in another
configuration: `dotnet test Pharmacy.Tests/Pharmacy.Tests.csproj -c Release`.

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
| POST | `/api/sales` (shelf items + prescription fills) | Signed in |
| GET | `/api/sales?from=&to=` (local dates, default today) | Signed in |
| GET | `/api/sales/{id}` (full invoice) | Signed in |
| POST | `/api/sales/{id}/void` | Admin |
| GET | `/api/sales/billable-fills?patientId=` | Signed in |
| GET | `/api/sales/register?schedule=H1&from=&to=` | Admin, Pharmacist |
| GET / PUT | `/api/settings` (invoice header: GSTIN, drug licences) | read: all · edit: Admin |
| GET | `/api/reports/daily-sales?from=&to=` (default: this month) | Admin, Pharmacist |
| GET | `/api/reports/gst-summary?from=&to=` | Admin, Pharmacist |
| GET | `/api/reports/stock-valuation` | Admin, Pharmacist |
| GET | `/api/reports/expiring?withinDays=90` | Admin, Pharmacist |
| GET | `/api/audit?entityType=&entityId=&action=&userId=&from=&to=&page=&pageSize=` | Admin |
| GET | `/api/suppliers?search=&includeInactive=`, `/api/suppliers/{id}` | Signed in |
| POST / PUT / DELETE | `/api/suppliers`, `/api/suppliers/{id}` (delete = deactivate) | Admin, Pharmacist |
| GET | `/api/purchase-orders?status=&supplierId=`, `/api/purchase-orders/{id}` | Signed in |
| GET | `/api/purchase-orders/suggestions` (low stock, suggested quantity) | Admin, Pharmacist |
| POST / PUT | `/api/purchase-orders`, `/api/purchase-orders/{id}` (draft only) | Admin, Pharmacist |
| POST | `/api/purchase-orders/{id}/order`, `/api/purchase-orders/{id}/close` | Admin, Pharmacist |
| POST | `/api/purchase-orders/{id}/receive` (a delivery) | Signed in |

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

### Billing rules (India)

- Only OTC and Schedule G medicines are sold straight off the shelf. Schedule H / H1 / X are
  billed only from a **dispensed prescription fill**, so their stock leaves the shelf once (at
  dispense) and the bill shows exactly the batches handed over. A fill can be billed only once.
- Prices are GST-inclusive. Per line: taxable value = total × 100 / (100 + GST rate); the GST is
  split equally into CGST and SGST (intra-state sale). The invoice also totals tax per GST rate.
- Discount is a percentage of the bill, up to 20%, applied before tax is worked out.
- Invoice numbers run per financial year (April to March): `2026-27/000001`, `2026-27/000002`, ...
  Voided invoices keep their number.
- Every Schedule H1 / X line is copied into the register (patient, prescriber with registration
  number, drug, batch, quantity, pharmacist) at the time of sale, so later edits don't change it.
- Only an Admin can void a sale. Voiding needs a reason and puts the stock back on the shelf;
  a voided prescription bill doesn't give the patient their refill back.
- Invoices print the pharmacy's name, GSTIN and drug licence numbers from **Settings**. They start
  as obvious placeholders; an Admin fills in the real ones.
- Prescriptions dispensed before M3 have no fill record, so they can't be billed at the counter.

### Report rules

- Sales reports count only completed bills; voided bills are shown as a count, not as money.
- Days are the pharmacy's local dates (the server's time zone); times are stored in UTC.
- GST summary: tax by rate (GSTR-3B table 3.1a) and the HSN-wise summary (GSTR-1 table 12).
  All sales are treated as B2C and intra-state. It's a learning summary for your accountant,
  not a filing tool.
- Stock valuation counts unexpired stock at purchase rate and at selling price; expired stock
  still on the shelf is shown separately, as a loss to write off or return.
- Ranges are capped at 366 days. Reports are for Admins and Pharmacists; the audit log is Admin only
  and read-only.

### Purchasing rules

- Draft → Ordered → Part received → Received. A draft can be edited; once ordered, its lines are fixed.
  **Close short** stops waiting for what hasn't come (or **cancels** if nothing came).
- Suggested quantity for a low-stock medicine: enough to reach twice its reorder level, less what is
  already on an open order (never less than the reorder level). Medicines already covered by an
  ordered PO drop off the list. The last supplier and purchase rate come from its latest batch.
- A delivery needs the supplier's invoice number. Each line becomes stock in a batch, with the same
  rules as any receipt (MRP, selling price ≤ MRP, not expired, same batch must match expiry and MRP).
  One line can arrive in several batches. Receiving more than was ordered is refused, and the whole
  delivery saves together or not at all.
- Any staff member can receive a delivery; only Admins and Pharmacists raise, change or close orders.
- A supplier with open orders can't be deactivated.

## All planned milestones (M0–M5) are done

Ideas for later: refresh tokens, supplier returns of near-expiry stock, drug-interaction checks,
a patient portal, and deployment (Docker / Azure).
