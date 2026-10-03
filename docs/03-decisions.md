# Pharmacy Management App: Key Decisions

_Stage 3 of 3 · written 2026-10-03 · builds on [01-design-and-planning.md](01-design-and-planning.md) and [02-architecture.md](02-architecture.md)_

Each entry is a short architecture decision record: the problem, what was chosen, what it cost, and
what was rejected. They are the questions a reviewer is most likely to ask about this codebase.

---

## 1. Business rules live in `Pharmacy.Core`, with no EF Core or ASP.NET

**Context.** The hard parts of a pharmacy are rules, not screens: pick the earliest-expiring batch,
never let stock go negative, only a pharmacist verifies, GST is inside the MRP.

**Decision.** Rules are plain static classes in `Pharmacy.Core/Rules` (`StockRules`, `FefoAllocator`,
`PrescriptionRules`, `GstCalculator`, `SaleRules`, `PurchaseOrderRules`, `PasswordRules`) that take
and change entities in memory. Controllers load data, call a rule, and save.

**Consequences.** The rules have fast unit tests with no database or HTTP (`FefoAllocatorTests`,
`GstCalculatorTests`, `PurchaseOrderRulesTests`). The demo-data seeder reuses the same rules, so seeded
data obeys the same invariants as real data (checked by `DemoDataSeederTests`).

**Rejected.** A full Clean Architecture with MediatR, commands and repositories. For one bounded
context and a single database it adds files and indirection without new guarantees; controllers stay
thin enough to read top to bottom. It is the next step if the app splits into services.

## 2. Stock is a ledger: every change is a `StockMovement`

**Context.** "Why does the shelf say 12 when the system says 15?" is the most common pharmacy question,
and the Drugs and Cosmetics Rules expect a trail for Schedule H1 / X stock.

**Decision.** Nothing writes `Batch.QuantityOnHand` directly. `StockRules.Apply` changes it and appends a
movement (Receipt, Sale, Dispense, Adjustment, Return) with who, when, why and a reference (invoice,
`RX-12`, PO number). The batch keeps the running total for fast reads.

**Consequences.** On-hand always equals the sum of movements (asserted in tests); history is never
edited, and a void adds a Return rather than deleting a Sale.

**Rejected.** Full event sourcing (rebuilding state from events). The ledger gives the audit benefit
while reads stay simple SQL.

## 3. Stock is tracked per batch, and picked First-Expiry-First-Out

**Decision.** A medicine has many batches, each with its own expiry date and printed MRP (Indian
packs print MRP per batch). `FefoAllocator` splits a quantity across non-expired batches, earliest
expiry first; expired batches are never picked. The invoice shows the actual batch and expiry.

**Consequences.** Less expired stock written off, correct MRP on every line, and recalls can be traced
to the patients who received a batch.

## 4. Optimistic concurrency on batches

**Context.** Two counters can sell the last strip at the same moment.

**Decision.** `Batch.Version` is a concurrency token replaced on every stock change. A save that loses
the race gets `409 Conflict` ("stock changed, try again") and nothing is written.

**Rejected.** Pessimistic locks (`SELECT ... WITH (UPDLOCK)`): they tie the code to SQL Server and hold
locks across user think time. Collisions are rare in a single pharmacy, so retrying is cheaper.

## 5. Dispensing and billing are separate steps

**Context.** In Indian retail pharmacy the pharmacist hands over the medicine, then the counter bills
it, sometimes along with shelf items, sometimes later. Billing must not take stock a second time.

**Decision.** Dispense creates a `PrescriptionFill` that records exactly which batches went out. The
counter bills unbilled fills (`/api/sales/billable-fills`), copying those batches onto the invoice.
Prescription-only medicines (Schedule H, H1, X) can't be sold off the shelf at all; only OTC and
Schedule G can. NDPS is refused everywhere.

**Consequences.** Stock leaves exactly once; a fill can be billed only once; the H1 register is
written from the bill with the prescriber's registration number and the dispensing pharmacist.

## 6. Money: GST worked backwards from MRP, rounded per line

**Decision.** Prices are GST-inclusive. Per line: taxable value = total × 100 / (100 + rate), GST =
total − taxable, split into CGST and SGST with the odd paisa on SGST. Rounding is per line, so the parts
always add up to the line total and the invoice total (property-tested in `GstCalculatorTests`).
Invoice numbers run per financial year (April to March), and a unique index plus retry handles two
counters numbering at once. Invoices snapshot names, HSN, batch and price, so later catalogue edits
never change a printed bill.

## 7. Health data: consent, least privilege, audit without personal data

**Decision.**
- A patient can't be saved without recorded consent (DPDP Act 2023).
- Every endpoint has a role policy; the Angular route guards are convenience only, and integration
  tests check the 403s (for example a Technician verifying a prescription).
- Opening or editing a patient record writes an `AuditLog` row with the user and record id, never the
  patient's details (tested).
- API responses carry `Cache-Control: no-store`, so patient data doesn't linger in caches.

## 8. Authentication: short access tokens, rotating refresh tokens

**Context.** Staff sit at a shared counter for a whole shift; a stolen token must not be useful for long.

**Decision.**
- JWT access tokens last 15 minutes. A refresh token (7 days) gets a new pair; it is single use and
  rotated on every refresh.
- Refresh tokens are stored as SHA-256 hashes, so a database leak doesn't hand out sessions.
- Presenting an already-rotated token is treated as theft: every session of that user is revoked.
- Changing a password, an Admin reset, or deactivating an account revokes all of the user's sessions.
- Login, refresh and password change are rate limited per client address (10 a minute), returning 429.
- Passwords need 8+ characters with a letter and a number, and can't contain the email name.

**Trade-off.** The refresh token is kept in `sessionStorage` and sent in the request body, not in an
httpOnly cookie. A cookie would hide it from scripts, but needs cross-site cookie and CSRF handling in
development (Angular on :4200, API on :5101). The mitigation is a strict Content Security Policy
(`script-src 'self'`, no inline script), so injected script can't run to read it, plus rotation and
reuse detection. Moving to an httpOnly `SameSite=Strict` cookie is straightforward now that production
serves the site and API from one origin.

## 9. One origin in production

**Decision.** On Azure the API serves the Angular build from `wwwroot`; in Docker, nginx serves the
build and proxies `/api`. Either way the browser sees one origin.

**Consequences.** No CORS in production, one free App Service instead of two resources, and a strict
CSP (`connect-src 'self'`). Routing runs after static files, and the SPA fallback skips `/api/...`, so
unknown API paths still return 404 instead of the Angular page.

## 10. Free-tier hosting by design

**Decision.** App Service F1 (Free), Azure SQL Database free offer with *auto-pause when the free limit
is used up*, and a budget alert on any spend. The API applies migrations on start (with retries while
SQL wakes) and EF Core retries transient SQL errors.

**Trade-off.** F1 sleeps when idle and has a daily CPU quota, so the first request is slow. Acceptable
for a demo; production would use a Basic plan with Always On, a managed identity for SQL instead of a
password, and Key Vault for secrets.

## 11. Testing strategy

- **Rules:** unit tests on Core, no I/O.
- **API:** integration tests through `WebApplicationFactory` with in-memory SQLite: real routing, auth,
  validation and EF mapping, one isolated database per test class.
- **Frontend:** Karma + Jasmine component tests for the risky logic: token refresh and the interceptor,
  the billing counter, receiving against a purchase order, the allergy acknowledgement.
- **Whole system:** CI builds the Docker images, runs SQL Server + API + nginx, logs in through nginx
  and reads the demo data. Only then does `main` deploy to Azure.

**Known gap.** SQLite in tests isn't SQL Server (no `RowVersion` behaviour, different decimal handling),
so concurrency is covered by the design rather than by a test against SQL Server. Testcontainers with
SQL Server would close that gap.

## 12. What I'd do next

- Move the refresh token to an httpOnly cookie (see 8).
- Managed identity and Key Vault on Azure (see 10).
- Application Insights for errors and slow requests.
- Testcontainers for SQL Server-specific behaviour (see 11).
- Supplier returns for near-expiry stock, and a drug-interaction check.
