# Pharmacy Management App: Design & Planning

_Stage 1 of 3 · drafted 2026-10-03 · updated for India · status: draft for Amar's review_

## 1. Purpose

A web app for running a single community (retail) pharmacy: keeping stock of medicines,
filling prescriptions, knowing who the patients are, and ringing up sales. It is a
self-directed learning project, so it should be realistic enough to teach real-world
concerns (stock by batch and expiry, controlled drugs, audit trails, health-data privacy)
while staying small enough to build one milestone at a time.

**Scope:** one retail pharmacy in **India**, following the Drugs and Cosmetics Act 1940 and
Rules 1945, GST, and the DPDP Act 2023. No insurance claims and no live e-prescription
integration in the first version. See section 6a for the Indian rules the app enforces.

## 2. Users and goals

| Role | Who | What they need to do |
|------|-----|----------------------|
| **Admin / Owner** | Pharmacy owner or manager | Manage staff accounts, see reports, set reorder levels and prices |
| **Pharmacist** | Licensed pharmacist | Verify and dispense prescriptions, check interactions and allergies, handle controlled drugs |
| **Technician / Cashier** | Counter staff | Look up stock, enter prescriptions for review, sell over-the-counter (OTC) items, receive deliveries |
| **Patient** _(later)_ | Customer | See their own prescriptions and refill status (out of MVP) |

Goals in priority order:

1. **Never dispense the wrong thing.** Prescription-only items need a pharmacist to verify; expired batches can't be sold.
2. **Know what's on the shelf.** Stock is tracked per batch with expiry date, so "what expires next month" and "what's below reorder level" are one click.
3. **Fast counter sales.** Search a product, add to cart, take payment, print a receipt.
4. **Keep a trail.** Every stock movement and every dispense is recorded with who did it and when.

## 3. Core features

### 3.1 Medicine catalogue
- Product record: name, generic name, strength, form (tablet, syrup, ...), pack size, manufacturer, barcode
- Drug schedule: OTC, Schedule G, H, H1, X, or NDPS (narcotic / psychotropic)
- HSN code and GST rate (0%, 5%, 12% or 18%), reorder level
- MRP is printed per batch, so it is stored on the batch; the selling price can't exceed it

### 3.2 Inventory
- Stock held per **batch** (batch number, expiry date, MRP, quantity, purchase rate)
- Receive stock from a supplier (goods receipt), adjust stock (damage, count correction) with a reason
- Sell/dispense picks from the earliest-expiring batch first (FEFO)
- Alerts: low stock, expiring within N days, expired

### 3.3 Patients (customers)
- Name, date of birth, phone, address, allergies, notes
- Prescription and purchase history

### 3.4 Prescriptions
- Enter a prescription: patient, prescriber name and registration number (NMC / state medical council), date, line items (drug, dose, quantity, directions, refills allowed)
- Workflow: **Entered → Verified (pharmacist) → Dispensed**, or Rejected with a reason
- Allergy warning when a line item matches a recorded allergy
- Refills tracked against "refills allowed"

### 3.5 Sales / point of sale
- Cart with OTC items and/or dispensed prescription items
- Payment method (cash, card, UPI), discount, GST invoice
- Invoice shows the pharmacy's drug licence numbers and GSTIN, and per line: HSN, batch, expiry, MRP, and CGST + SGST
- Returns/voids by Admin only

### 3.6 Suppliers and purchasing
- Supplier list; purchase orders generated from low-stock items; receipt against a PO

### 3.7 Reports
- Daily sales, GST summary (for GSTR-1 / GSTR-3B filing), stock valuation, expiring stock, Schedule H1 register, Schedule X / NDPS register

### 3.8 Users, roles and audit
- Login, role-based access (Admin, Pharmacist, Technician)
- Audit log of sensitive actions (dispense, stock adjust, patient record view/edit)

## 4. MVP scope

The MVP is the smallest thing a pharmacy could actually use at the counter.

**In:**
- Login with roles (Admin, Pharmacist, Technician)
- Medicine catalogue CRUD
- Batch-level inventory: receive stock, adjust stock, low-stock and expiry alerts
- Patients CRUD with allergies
- Prescriptions: enter, verify, dispense (stock deducted FEFO)
- Simple POS: OTC + dispensed items, cash/card, printable receipt
- Audit log for dispense and stock changes

**Out (later milestones):**
- Purchase orders and supplier management beyond a name on a goods receipt
- Drug-interaction checks (needs a drug database)
- Insurance / claims, e-prescriptions, barcode scanner hardware
- Patient portal, multi-branch, offline mode

## 5. Milestones

| # | Milestone | Done when |
|---|-----------|-----------|
| **M0** | Scaffold & auth | Solution builds, API + Angular app run, login with roles works, CI runs tests |
| **M1** | Catalogue & inventory | Medicines CRUD, receive stock into batches, adjust stock, low-stock/expiry list |
| **M2** | Patients & prescriptions | Patients CRUD with allergies; prescription enter → verify → dispense with FEFO deduction and allergy warning |
| **M3** | Point of sale | Cart, checkout, receipt, sales history; stock decremented on sale |
| **M4** | Reports & audit | Daily sales, GST summary, stock valuation, expiring stock, Schedule H1 / X registers, audit log viewer |
| **M5** | Purchasing (stretch) | Suppliers, purchase orders from low stock, receive against PO |

Each milestone ends with integration tests for its API endpoints, following the
TaskManager project's `WebApplicationFactory` + SQLite pattern.

## 6. Key rules (domain invariants)

- Stock quantity can never go negative; a sale that would do so is refused.
- Expired batches are never picked for sale or dispense.
- Rx items can only be sold through a **verified** prescription line.
- Only a Pharmacist can verify or dispense; only an Admin can void a sale or manage users.
- Every change to stock writes a `StockMovement` row; quantities on hand are the sum of movements per batch (kept in sync on the batch row for fast reads).

## 6a. Indian regulatory rules

These shape the data model and the dispense flow. They are a learning summary, not legal
advice; check current rules with the State Drugs Control department before real use.

- **Schedule H and H1** drugs are sold only against a prescription from a registered medical practitioner.
- **Schedule H1** sales go in a separate register: patient name and address, prescriber name and address, drug name and quantity. The register is kept for 3 years.
- **Schedule X** drugs need the prescription in duplicate, with one copy retained by the pharmacy, and a separate register.
- **NDPS** (narcotic and psychotropic) drugs follow the NDPS Act; the MVP blocks their sale and only tracks stock.
- **Pharmacist:** prescription-only drugs are dispensed under a registered pharmacist (Pharmacy Act 1948); the app records which pharmacist verified each one.
- **Bills** carry the pharmacy name, address, drug licence numbers (Form 20/21), GSTIN, and for each item its batch number, expiry date and MRP.
- **GST:** each product has an HSN code and a GST rate. Intra-state sales split GST into CGST + SGST; the MVP assumes all retail sales are intra-state. MRP is GST-inclusive, so tax is calculated backwards from the price.
- **MRP:** you can't sell above the printed MRP (Legal Metrology rules).
- **Privacy:** patient data is handled under the DPDP Act 2023 (consent, purpose limitation, deletion on request where the law allows).

## 7. Non-functional goals

- **Privacy:** patient data is health data; least-privilege access, audit of reads/edits, encryption in transit and at rest (details in the architecture doc).
- **Correctness over speed:** stock and dispense operations are transactional.
- **Usability:** counter screens usable with keyboard only; search-as-you-type for products and patients.
- **Testability:** every endpoint covered by integration tests.

## 8. Open questions for Amar

1. ~~Which country's rules?~~ **Decided: India** (section 6a).
2. **Retail pharmacy or hospital pharmacy?** Default: retail/community pharmacy.
3. **Same stack as TaskManager (ASP.NET Core 8 + Angular 17 + SQL Server)?** Default: yes, so the learning builds on what you already have.
4. ~~Where does the code live?~~ **Decided:** Amar's local `Pharmacy` git repo (`D:\MS Project\TaskManager\Pharmacy\Pharmacy`), outside TaskManager.
5. **Receipt format?** Default: currency INR (₹), printable A5 GST invoice in HTML.
6. **Does the pharmacy sell inter-state (online orders)?** Default: no, so CGST + SGST only.
