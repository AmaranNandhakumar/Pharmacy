# Pharmacy Management App: Architecture

_Stage 2 of 3 · drafted 2026-10-03 · updated for India · builds on [01-design-and-planning.md](01-design-and-planning.md)_

## 1. Stack

Same stack as the TaskManager project, so the learning compounds instead of restarting.

| Layer | Choice | Why |
|-------|--------|-----|
| Frontend | Angular 17, standalone components, Reactive Forms, RxJS | Already familiar from TaskManager; strong forms story for data-entry screens |
| Backend | ASP.NET Core 8 Web API (controllers) | Same as TaskManager; mature auth, validation, OpenAPI |
| Data access | EF Core 8, code-first migrations | Transactions and concurrency tokens for stock updates |
| Database | SQL Server (Express in dev) | Relational data with strong integrity; supports Transparent Data Encryption |
| Auth | JWT bearer + BCrypt, **role claims** | Extends TaskManager's auth with roles |
| Tests | xUnit + `WebApplicationFactory` + in-memory SQLite | Same pattern as `TaskManager.Api.Tests` |
| API docs | Swagger / OpenAPI | Explore endpoints during development |

What changes versus TaskManager: one backend project is split into **Api / Core / Infrastructure**
layers because the domain rules (FEFO picking, dispense workflow, stock never negative) are
the interesting part and deserve to be testable without HTTP or a database.

## 2. Solution layout

```
pharmacy/app/
├── backend/
│   ├── Pharmacy.sln
│   ├── Pharmacy.Core/            # Entities, enums, domain rules (no EF, no ASP.NET)
│   │   ├── Entities/
│   │   └── Services/             # e.g. FefoAllocator (pure logic)
│   ├── Pharmacy.Infrastructure/  # EF Core DbContext, configurations, migrations
│   │   └── Data/
│   ├── Pharmacy.Api/             # Controllers, DTOs, auth, Program.cs
│   │   ├── Controllers/
│   │   ├── DTOs/
│   │   └── Services/             # TokenService, AuditService
│   └── Pharmacy.Tests/           # Unit tests (Core) + API integration tests
└── frontend/                     # Angular 17 app
    └── src/app/
        ├── core/                 # auth service, guards, interceptor, layout shell
        ├── medicines/
        ├── inventory/
        ├── patients/
        ├── prescriptions/
        └── pos/
```

Dependency direction: `Api → Infrastructure → Core`. Core depends on nothing.

## 3. Components

```
 Angular SPA ──HTTPS/JSON──▶ ASP.NET Core API ──EF Core──▶ SQL Server
   (role-aware UI)            ├─ Auth (JWT, roles)
                              ├─ Controllers (thin)
                              ├─ Core services (FEFO, dispense workflow)
                              └─ Audit service (writes AuditLog)
```

- **Controllers** validate input, check the role policy, and call a service. No business logic.
- **Core services** hold the rules: allocate quantity across batches by earliest expiry,
  move a prescription through its states, refuse negative stock.
- **Audit service** is called for every sensitive action and writes an append-only row.
- **Frontend** mirrors roles: routes guarded by role, menu items hidden when not permitted
  (the API still enforces everything; the UI hiding is convenience only).

## 4. Data model

```
User (Id, Email, PasswordHash, FullName, Role[Admin|Pharmacist|Technician], IsActive, CreatedAt)

Medicine (Id, Name, GenericName, Strength, Form, PackSize, Manufacturer, Barcode?,
          Schedule[Otc|G|H|H1|X|Ndps], HsnCode, GstRatePercent[0|5|18],
          ReorderLevel, IsActive)
  └─< Batch (Id, MedicineId, BatchNumber, ExpiryDate, Mrp, SellingPrice (<= Mrp),
             PurchaseRate, QuantityOnHand, ReceivedAt, SupplierName?, SupplierInvoiceNo?, RowVersion)
        └─< StockMovement (Id, BatchId, Quantity(+/-), Type[Receipt|Sale|Dispense|Adjustment|Return],
                           Reason?, ReferenceId?, UserId, CreatedAt)

Patient (Id, FullName, DateOfBirth, Phone?, Address?, Allergies?, Notes?, CreatedAt)
  └─< Prescription (Id, PatientId, PrescriberName, PrescriberRegNo, PrescriberAddress?, IssuedOn,
                    ImagePath? (scanned copy; required for Schedule X),
                    Status[Entered|Verified|Dispensed|Rejected], RejectReason?,
                    EnteredById, VerifiedById?, DispensedById?, CreatedAt)
        └─< PrescriptionItem (Id, PrescriptionId, MedicineId, Dose, Quantity,
                              Directions, RefillsAllowed, RefillsUsed)

Sale (Id, InvoiceNo (sequential per financial year, e.g. 2026-27/000123), PatientId?, UserId,
      CreatedAt, TaxableValue, Cgst, Sgst, Discount, Total,
      PaymentMethod[Cash|Card|Upi], Status[Completed|Voided])
  └─< SaleItem (Id, SaleId, MedicineId, BatchId, PrescriptionItemId?, Quantity,
                Mrp, UnitPrice, HsnCode, GstRatePercent, TaxableValue, Cgst, Sgst, LineTotal)

ScheduleRegisterEntry (Id, Schedule[H1|X], SaleItemId, PatientName, PatientAddress,
                       PrescriberName, PrescriberAddress, DrugName, Quantity, PharmacistId, CreatedAt)

PharmacySettings (single row: Name, Address, StateCode, Gstin, DrugLicence20, DrugLicence21,
                  RegisteredPharmacistName, RegisteredPharmacistRegNo)

AuditLog (Id, UserId, Action, EntityType, EntityId, Details(json), CreatedAt)
```

Notes:
- Money is `decimal(18,2)` in INR; quantities are integers in the smallest dispensable unit.
- Prices are GST-inclusive (MRP), so `TaxableValue = UnitPrice × Qty × 100 / (100 + GstRate)` and the
  GST is split equally into CGST and SGST (intra-state only in the MVP).
- `ScheduleRegisterEntry` is a snapshot copied at sale time, so the register stays correct even if
  the patient or prescriber record is edited later.
- `Batch.RowVersion` is an EF concurrency token so two counters can't oversell the same batch.
- A sale line that is an Rx item must reference a `PrescriptionItem` in `Verified` state.
- Soft delete (`IsActive`) for medicines and users, because history references them.

## 5. API outline

All routes are under `/api`, JSON, JWT required except login.

| Area | Endpoints | Roles |
|------|-----------|-------|
| Auth | `POST /auth/login`, `GET /auth/me` | anyone / signed in |
| Users | `GET/POST /users`, `PUT /users/{id}`, `PATCH /users/{id}/active` | Admin |
| Medicines | `GET /medicines?search=`, `GET /medicines/{id}`, `POST`, `PUT /{id}`, `DELETE /{id}` (soft) | read: all · write: Admin, Pharmacist |
| Inventory | `GET /inventory/batches?medicineId=`, `POST /inventory/receipts`, `POST /inventory/adjustments`, `GET /inventory/alerts?expiringWithinDays=30` | all · adjustments: Admin, Pharmacist |
| Patients | `GET /patients?search=`, `GET /patients/{id}`, `POST`, `PUT /{id}` | all staff (reads audited) |
| Prescriptions | `GET /prescriptions?status=`, `GET /{id}`, `POST`, `POST /{id}/verify`, `POST /{id}/reject`, `POST /{id}/dispense` | enter: all · verify/reject/dispense: Pharmacist |
| Sales | `POST /sales`, `GET /sales?from=&to=`, `GET /sales/{id}`, `POST /sales/{id}/void` | all · void: Admin |
| Reports | `GET /reports/daily-sales`, `/stock-valuation`, `/expiring`, `/gst-summary`, `/schedule-register?schedule=H1` | Admin, Pharmacist |
| Audit | `GET /audit?entityType=&entityId=` | Admin |

Users are created by an Admin; there is no public self-registration (unlike TaskManager),
because staff accounts in a pharmacy are issued, not signed up for.

## 6. Security notes for health data

Patient records and prescriptions are sensitive health data under India's Digital Personal
Data Protection (DPDP) Act 2023: process only with consent and for a stated purpose, keep it no
longer than needed (but the H1 register must be kept for 3 years), and support correction and erasure requests. Even as a learning project, build the habits:

- **Least privilege:** role-based policies on every endpoint (`[Authorize(Roles = ...)]`),
  tested in integration tests. No anonymous endpoints except login.
- **No self-registration.** First Admin is seeded from configuration; Admins create staff.
- **Secrets out of the repo:** JWT key and connection strings in user-secrets (dev) and
  environment variables (prod), exactly as TaskManager already does.
- **Short-lived tokens:** access token around 30–60 minutes; add refresh tokens in a later milestone.
- **Encryption:** HTTPS only (HSTS in production); SQL Server TDE or encrypted disk at rest;
  consider column encryption for `Patient.Allergies` / `Notes` later.
- **Audit trail:** append-only `AuditLog` for dispense, stock adjustments, sale voids,
  user changes, and patient record views/edits. Never log full patient details in app logs.
- **Input validation:** data annotations / FluentValidation on DTOs; never bind entities directly.
- **Concurrency:** `RowVersion` on batches and transactions around dispense/sale so stock is never oversold.
- **Data minimisation:** collect only fields the pharmacy needs; no national ID numbers in the MVP.
- **Seed data is fake.** Never load real patient data into a dev database.

## 7. Testing strategy

- **Core unit tests:** FEFO allocation, dispense state machine, stock-never-negative.
- **API integration tests:** each endpoint's happy path plus the role check (e.g. a Technician
  gets 403 on `/prescriptions/{id}/verify`), using `WebApplicationFactory` and SQLite in memory.
- **Frontend:** component tests for forms with validation; added once screens exist.

## 8. Decisions to confirm

1. Keep the 3-project backend split (Api / Core / Infrastructure) instead of TaskManager's single project. **Recommended:** yes.
2. ~~Where the code lives.~~ **Decided:** Amar's local `Pharmacy` git repo, separate from TaskManager.
