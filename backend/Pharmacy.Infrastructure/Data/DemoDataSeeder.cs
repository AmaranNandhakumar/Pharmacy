using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Rules;

namespace Pharmacy.Infrastructure.Data;

/// <summary>
/// Fills an empty database with a believable fortnight of a small Indian retail pharmacy: catalogue, stock
/// (some low, some expiring, one batch expired), suppliers and purchase orders, patients, prescriptions
/// in every state, and sales with GST invoices and the H1 register. Everything is fake.
/// Uses the same domain rules as the API (FEFO, stock movements, GST maths) so the numbers add up.
/// </summary>
public class DemoDataSeeder
{
    public const string DemoPharmacistEmail = "pharmacist@demo.local";
    public const string DemoTechnicianEmail = "technician@demo.local";

    private readonly PharmacyDbContext _db;
    private readonly DateTime _now;
    private readonly DateOnly _today;
    private readonly Random _random = new(2026);
    private readonly Dictionary<string, int> _invoiceSeq = new();
    private int _poSeq;

    private User _admin = null!;
    private User _pharmacist = null!;
    private User _technician = null!;

    /// <param name="now">Current UTC time; the demo history is laid out in the 14 days before it.</param>
    public DemoDataSeeder(PharmacyDbContext db, DateTime now)
    {
        _db = db;
        _now = DateTime.SpecifyKind(now, DateTimeKind.Utc);
        _today = DateOnly.FromDateTime(_now);
    }

    /// <summary>Refuses to touch a database that already has medicines, so it never mixes with real data.</summary>
    public async Task<bool> CanSeedAsync() => !await _db.Medicines.AnyAsync();

    /// <param name="adminEmail">The Admin to attach the data to; created if missing.</param>
    /// <param name="demoPasswordHash">BCrypt hash used for the Admin (if created) and the demo staff accounts.</param>
    public async Task SeedAsync(string adminEmail, string demoPasswordHash)
    {
        if (!await CanSeedAsync())
            throw new InvalidOperationException("This database already has medicines. Demo data only goes into an empty database.");

        await SeedStaffAsync(adminEmail, demoPasswordHash);
        SeedSettings();

        var suppliers = SeedSuppliers();
        var m = SeedMedicines();
        SeedStock(m, suppliers);
        await _db.SaveChangesAsync();

        var patients = SeedPatients();
        await _db.SaveChangesAsync();

        SeedSales(m);
        SeedPrescriptions(m, patients);
        SeedPurchaseOrders(m, suppliers);

        _db.AuditLogs.Add(new AuditLog { UserId = _admin.Id, Action = "DemoDataSeeded", EntityType = "Database", CreatedAt = _now });
        await _db.SaveChangesAsync();
    }

    // ---------- staff and settings ----------

    private async Task SeedStaffAsync(string adminEmail, string hash)
    {
        var email = adminEmail.Trim().ToLowerInvariant();
        _admin = await _db.Users.FirstOrDefaultAsync(u => u.Email == email)
            ?? Add(new User { Email = email, FullName = "Amar (Owner)", PasswordHash = hash, Role = UserRole.Admin, CreatedAt = Days(-30) });

        _pharmacist = await _db.Users.FirstOrDefaultAsync(u => u.Email == DemoPharmacistEmail)
            ?? Add(new User { Email = DemoPharmacistEmail, FullName = "Priya Raman", PasswordHash = hash, Role = UserRole.Pharmacist, CreatedAt = Days(-30) });
        _technician = await _db.Users.FirstOrDefaultAsync(u => u.Email == DemoTechnicianEmail)
            ?? Add(new User { Email = DemoTechnicianEmail, FullName = "Karthik S", PasswordHash = hash, Role = UserRole.Technician, CreatedAt = Days(-30) });
        await _db.SaveChangesAsync();
    }

    private void SeedSettings()
    {
        var s = _db.PharmacySettings.Find(PharmacySettings.SingletonId);
        if (s is null)
        {
            s = new PharmacySettings();
            _db.PharmacySettings.Add(s);
        }
        s.Name = "Amaran Pharmacy (Demo)";
        s.Address = "12 Gandhi Road, T. Nagar, Chennai 600017";
        s.Phone = "044 2434 0000";
        s.StateCode = "33";
        s.Gstin = "33AAAAA0000A1Z5";
        s.DrugLicence20 = "TN/CHN/20/0000";
        s.DrugLicence21 = "TN/CHN/21/0000";
        s.RegisteredPharmacistName = "Priya Raman";
        s.RegisteredPharmacistRegNo = "TNPC 00000";
    }

    // ---------- catalogue, suppliers, stock ----------

    private List<Supplier> SeedSuppliers()
    {
        var list = new List<Supplier>
        {
            new() { Name = "Sri Murugan Pharma Distributors", ContactPerson = "R. Senthil", Phone = "044 2345 1111", Gstin = "33AAACS1111A1Z1", DrugLicenceNo = "TN-20B-1111", Address = "Parrys, Chennai", CreatedAt = Days(-30) },
            new() { Name = "Medline Wholesale", ContactPerson = "Anita Joseph", Phone = "044 2345 2222", Gstin = "33AAACM2222B1Z2", DrugLicenceNo = "TN-20B-2222", Address = "Vadapalani, Chennai", CreatedAt = Days(-30) },
            new() { Name = "HealthKart Distributors", ContactPerson = "Vikram Rao", Phone = "044 2345 3333", Gstin = "33AAACH3333C1Z3", DrugLicenceNo = "TN-20B-3333", Address = "Guindy, Chennai", CreatedAt = Days(-30) }
        };
        _db.Suppliers.AddRange(list);
        return list;
    }

    private record Med(Medicine Medicine, decimal Mrp, decimal Price, decimal Rate);

    private Dictionary<string, Med> SeedMedicines()
    {
        // Brand-like names are made up; generic names, forms and schedules follow common Indian practice.
        var rows = new (string key, string name, string generic, string strength, string form, string pack, DrugSchedule sch, string hsn, decimal gst, int reorder, decimal mrp, decimal price, decimal rate)[]
        {
            ("para",   "Paracip",      "Paracetamol",               "650 mg",    "Tablet",   "15 tablets", DrugSchedule.G,   "3004", 5,  60, 2.10m, 2.00m, 1.20m),
            ("cetri",  "Cetrinova",        "Cetirizine",                "10 mg",     "Tablet",   "10 tablets", DrugSchedule.Otc, "3004", 5,  40, 1.90m, 1.80m, 0.90m),
            ("ors",    "Electra ORS",      "Oral rehydration salts",    "21.8 g",    "Powder",   "1 sachet",   DrugSchedule.Otc, "3004", 5,  30, 22.00m, 21.00m, 14.00m),
            ("antacid","Digestol",         "Aluminium hydroxide + magnesium hydroxide", "200 ml", "Suspension", "200 ml bottle", DrugSchedule.Otc, "3004", 18, 10, 128.00m, 125.00m, 88.00m),
            ("balm",   "Vapocool Rub",     "Menthol + camphor",         "50 g",      "Ointment", "50 g jar",   DrugSchedule.Otc, "3004", 18, 8,  165.00m, 160.00m, 110.00m),
            ("vitc",   "CeeMax",           "Vitamin C",                 "500 mg",    "Tablet",   "15 tablets", DrugSchedule.Otc, "2936", 18, 30, 1.60m, 1.50m, 0.80m),
            ("cough",  "Koflin Syrup",     "Dextromethorphan + chlorpheniramine", "100 ml", "Syrup", "100 ml bottle", DrugSchedule.G, "3004", 5, 10, 112.00m, 110.00m, 72.00m),
            ("amox",   "Amoxinate",    "Amoxicillin",               "500 mg",    "Capsule",  "10 capsules", DrugSchedule.H,  "3004", 5,  40, 7.80m, 7.50m, 4.60m),
            ("panto",  "Pantozen",      "Pantoprazole",              "40 mg",     "Tablet",   "15 tablets", DrugSchedule.H,   "3004", 5,  45, 9.90m, 9.50m, 5.80m),
            ("metf",   "Glyconorm",    "Metformin",                 "500 mg",    "Tablet",   "20 tablets", DrugSchedule.H,   "3004", 5,  60, 2.20m, 2.10m, 1.10m),
            ("amlo",   "Amlopress",      "Amlodipine",                "5 mg",      "Tablet",   "15 tablets", DrugSchedule.H,   "3004", 5,  45, 3.20m, 3.00m, 1.70m),
            ("atorva", "Atorlip",       "Atorvastatin",              "10 mg",     "Tablet",   "15 tablets", DrugSchedule.H,   "3004", 5,  30, 9.40m, 9.00m, 5.20m),
            ("ibu",    "Ibunova",      "Ibuprofen",                 "400 mg",    "Tablet",   "15 tablets", DrugSchedule.H,   "3004", 5,  30, 1.50m, 1.45m, 0.80m),
            ("azi",    "Azinova",      "Azithromycin",              "500 mg",    "Tablet",   "3 tablets",  DrugSchedule.H1,  "3004", 5,  15, 24.00m, 23.00m, 14.50m),
            ("cefi",   "Cefinova",     "Cefixime",                  "200 mg",    "Tablet",   "10 tablets", DrugSchedule.H1,  "3004", 5,  20, 11.50m, 11.00m, 6.80m),
            ("alpra",  "Alprazen",    "Alprazolam",                "0.25 mg",   "Tablet",   "10 tablets", DrugSchedule.H1,  "3004", 5,  10, 3.80m, 3.60m, 2.10m),
            ("insulin","Glucovin R",       "Human insulin (regular)",   "40 IU/ml",  "Injection","10 ml vial", DrugSchedule.H,   "3004", 5,  4,  165.00m, 160.00m, 120.00m),
            ("tram",   "Tramanova",     "Tramadol",                  "50 mg",     "Capsule",  "10 capsules", DrugSchedule.Ndps, "3004", 5, 5, 6.50m, 6.20m, 3.90m)
        };

        var result = new Dictionary<string, Med>();
        foreach (var r in rows)
        {
            var medicine = new Medicine
            {
                Name = r.name, GenericName = r.generic, Strength = r.strength, Form = r.form, PackSize = r.pack,
                Manufacturer = "Demo Labs Pvt Ltd", Schedule = r.sch, HsnCode = r.hsn, GstRatePercent = r.gst,
                ReorderLevel = r.reorder, CreatedAt = Days(-30)
            };
            _db.Medicines.Add(medicine);
            result[r.key] = new Med(medicine, r.mrp, r.price, r.rate);
        }
        return result;
    }

    private void SeedStock(Dictionary<string, Med> m, List<Supplier> s)
    {
        var (murugan, medline, healthkart) = (s[0], s[1], s[2]);

        // Two batches for the busy lines, so FEFO has something to choose between
        Receive(m["para"], "PC2401", 4, 300, murugan, -20);
        Receive(m["para"], "PC2507", 20, 300, murugan, -6);
        Receive(m["cetri"], "CT2410", 11, 500, medline, -20);
        Receive(m["ors"], "OR2502", 15, 90, medline, -20);
        Receive(m["antacid"], "DG2412", 9, 70, healthkart, -20);
        Receive(m["balm"], "VR2501", 26, 45, healthkart, -20);
        Receive(m["vitc"], "VC2405", 1, 450, medline, -20);      // expires within the month
        Receive(m["cough"], "KF2411", 8, 60, murugan, -20);
        Receive(m["amox"], "AM2409", 2, 200, murugan, -20);      // expiring soon
        Receive(m["amox"], "AM2506", 18, 200, murugan, -5);
        Receive(m["panto"], "PZ2412", 14, 300, medline, -20);
        Receive(m["metf"], "MF2501", 22, 400, medline, -20);
        Receive(m["amlo"], "AL2502", 19, 300, healthkart, -20);
        Receive(m["atorva"], "AT2411", 12, 150, healthkart, -20);
        Receive(m["ibu"], "IB2410", 10, 150, murugan, -20);
        Receive(m["azi"], "AZ2503", 16, 60, murugan, -20);
        Receive(m["cefi"], "CF2502", 13, 30, medline, -20);       // will end up low
        Receive(m["alpra"], "AP2412", 12, 40, healthkart, -20);
        Receive(m["insulin"], "GV2501", 7, 6, healthkart, -20);   // low stock
        Receive(m["tram"], "TR2502", 15, 20, murugan, -20);       // NDPS: tracked, never sold

        // An expired batch still on the shelf, for the alerts and the write-off report
        var expired = new Batch
        {
            Medicine = m["cough"].Medicine, BatchNumber = "KF2306", ExpiryDate = _today.AddDays(-12),
            Mrp = m["cough"].Mrp, SellingPrice = m["cough"].Price, PurchaseRate = m["cough"].Rate,
            SupplierName = murugan.Name, SupplierInvoiceNo = "SMP/23-24/0912", ReceivedAt = Days(-400)
        };
        _db.Batches.Add(expired);
        Stamp(StockRules.Apply(expired, 6, StockMovementType.Receipt, _admin.Id, referenceId: "SMP/23-24/0912"), Days(-400));
    }

    private Batch Receive(Med med, string batchNo, int monthsToExpiry, int qty, Supplier supplier, int daysAgo)
    {
        var expiry = new DateOnly(_today.Year, _today.Month, 1).AddMonths(monthsToExpiry + 1).AddDays(-1);
        if (monthsToExpiry <= 1) expiry = _today.AddDays(monthsToExpiry == 1 ? 25 : 40);
        var invoice = $"{supplier.Name.Split(' ')[0].ToUpperInvariant()[..3]}/{_random.Next(1000, 9999)}";
        var batch = new Batch
        {
            Medicine = med.Medicine, BatchNumber = batchNo, ExpiryDate = expiry, Mrp = med.Mrp, SellingPrice = med.Price,
            PurchaseRate = med.Rate, SupplierName = supplier.Name, SupplierInvoiceNo = invoice, ReceivedAt = Days(daysAgo)
        };
        _db.Batches.Add(batch);
        Stamp(StockRules.Apply(batch, qty, StockMovementType.Receipt, _technician.Id, referenceId: invoice), Days(daysAgo));
        return batch;
    }

    // ---------- patients and prescriptions ----------

    private List<Patient> SeedPatients()
    {
        var list = new List<Patient>
        {
            new() { FullName = "Lakshmi Narayanan", DateOfBirth = new DateOnly(1958, 3, 14), Phone = "9840000001", Address = "Mylapore, Chennai", Allergies = "Sulfa" },
            new() { FullName = "Arjun Mehta", DateOfBirth = new DateOnly(1990, 11, 2), Phone = "9840000002", Address = "Adyar, Chennai" },
            new() { FullName = "Fatima Begum", DateOfBirth = new DateOnly(1972, 7, 21), Phone = "9840000003", Address = "Royapettah, Chennai", Allergies = "Amoxicillin, penicillin" },
            new() { FullName = "Ravi Kumar", DateOfBirth = new DateOnly(1965, 1, 30), Phone = "9840000004", Address = "Velachery, Chennai", Notes = "Diabetic; prefers evening pickup" },
            new() { FullName = "Meera Iyer", DateOfBirth = new DateOnly(1999, 5, 9), Phone = "9840000005", Address = "Besant Nagar, Chennai" },
            new() { FullName = "John Peter", DateOfBirth = new DateOnly(1981, 9, 17), Phone = "9840000006", Address = "Anna Nagar, Chennai", Allergies = "Ibuprofen" }
        };
        foreach (var p in list)
        {
            p.ConsentGivenAt = Days(-14);
            p.CreatedAt = Days(-14);
        }
        _db.Patients.AddRange(list);
        return list;
    }

    private void SeedPrescriptions(Dictionary<string, Med> m, List<Patient> p)
    {
        var (lakshmi, arjun, fatima, ravi, meera, john) = (p[0], p[1], p[2], p[3], p[4], p[5]);

        // Dispensed and billed, with refills (chronic medicines)
        var r1 = Rx(ravi, "Dr. S. Venkatesh", "TNMC 54321", -12, (m["metf"], "1 tablet", 60, "Twice a day after food", 2), (m["atorva"], "1 tablet", 30, "At night", 2));
        Verify(r1, -12);
        Bill(Dispense(r1, -12), -12, PaymentMethod.Upi);
        Bill(Dispense(r1, -2), -2, PaymentMethod.Upi);   // first refill

        // Antibiotic course with an H1 drug: goes into the H1 register
        var r2 = Rx(arjun, "Dr. K. Anand", "TNMC 11223", -9, (m["azi"], "1 tablet", 3, "Once a day for 3 days", 0), (m["para"], "1 tablet", 10, "When needed for fever", 0));
        Verify(r2, -9);
        Bill(Dispense(r2, -9), -9, PaymentMethod.Cash);

        var r3 = Rx(meera, "Dr. R. Shalini", "TNMC 99881", -4, (m["cefi"], "1 tablet", 10, "Twice a day for 5 days", 0), (m["panto"], "1 tablet", 5, "Before breakfast", 0));
        Verify(r3, -4);
        Bill(Dispense(r3, -4), -4, PaymentMethod.Card);

        var r4 = Rx(lakshmi, "Dr. S. Venkatesh", "TNMC 54321", -6, (m["amlo"], "1 tablet", 30, "Once a day in the morning", 3));
        Verify(r4, -6);
        Bill(Dispense(r4, -6), -6, PaymentMethod.Cash);

        // Rejected: illegible registration number
        var r5 = Rx(john, "Dr. M. Thomas", "UNKNOWN", -3, (m["ibu"], "1 tablet", 10, "Twice a day after food", 0));
        r5.Status = PrescriptionStatus.Rejected;
        r5.RejectReason = "Prescriber registration number not legible; asked patient to get it confirmed.";

        // Verified, dispensed today, waiting at the counter to be billed
        var r6 = Rx(lakshmi, "Dr. A. Prakash", "TNMC 77665", 0, (m["alpra"], "1 tablet", 10, "At bedtime for 10 days", 0));
        Verify(r6, 0);
        Dispense(r6, 0);

        // Verified, ready to dispense
        var r7 = Rx(ravi, "Dr. S. Venkatesh", "TNMC 54321", 0, (m["insulin"], "As directed", 1, "Store in the fridge", 1));
        Verify(r7, 0);

        // Waiting for a pharmacist, one with an allergy warning to acknowledge
        Rx(fatima, "Dr. K. Anand", "TNMC 11223", 0, (m["amox"], "1 capsule", 15, "Three times a day for 5 days", 0), (m["para"], "1 tablet", 10, "When needed", 0));
        Rx(arjun, "Dr. R. Shalini", "TNMC 99881", 0, (m["cetri"], "1 tablet", 5, "At night", 0));
    }

    private Prescription Rx(Patient patient, string doctor, string regNo, int daysAgo,
        params (Med med, string dose, int qty, string directions, int refills)[] items)
    {
        var rx = new Prescription
        {
            Patient = patient, PrescriberName = doctor, PrescriberRegNo = regNo, PrescriberAddress = "Chennai",
            IssuedOn = _today.AddDays(daysAgo), EnteredById = _technician.Id, CreatedAt = Days(daysAgo, 10),
            Items = items.Select(i => new PrescriptionItem
            {
                Medicine = i.med.Medicine, Dose = i.dose, Quantity = i.qty, Directions = i.directions, RefillsAllowed = i.refills
            }).ToList()
        };
        _db.Prescriptions.Add(rx);
        // Save now so items have ids: invoice lines point at the prescription item they bill
        _db.SaveChanges();
        return rx;
    }

    private void Verify(Prescription rx, int daysAgo) => PrescriptionRules.Verify(rx, _pharmacist.Id, Days(daysAgo, 11));

    private PrescriptionFill Dispense(Prescription rx, int daysAgo)
    {
        var at = Days(daysAgo, 12);
        var items = PrescriptionRules.ItemsToFill(rx);
        var fill = new PrescriptionFill { Prescription = rx, IsRefill = rx.Status == PrescriptionStatus.Dispensed, DispensedById = _pharmacist.Id, DispensedAt = at };
        foreach (var item in items)
        {
            foreach (var (batch, qty) in FefoAllocator.Allocate(item.Medicine.Batches, item.Quantity, _today.AddDays(daysAgo)))
            {
                Stamp(StockRules.Apply(batch, -qty, StockMovementType.Dispense, _pharmacist.Id, referenceId: $"RX-{rx.Id}"), at);
                fill.Lines.Add(new PrescriptionFillLine { PrescriptionItem = item, Batch = batch, Quantity = qty });
            }
        }
        PrescriptionRules.RecordFill(rx, items, _pharmacist.Id, at);
        _db.PrescriptionFills.Add(fill);
        return fill;
    }

    // ---------- sales ----------

    private void SeedSales(Dictionary<string, Med> m)
    {
        var shelf = new[] { "para", "cetri", "ors", "antacid", "balm", "vitc", "cough" };
        var payments = new[] { PaymentMethod.Cash, PaymentMethod.Cash, PaymentMethod.Upi, PaymentMethod.Upi, PaymentMethod.Upi, PaymentMethod.Card };

        for (var daysAgo = -13; daysAgo <= 0; daysAgo++)
        {
            var bills = _random.Next(3, 8);
            for (var b = 0; b < bills; b++)
            {
                var picks = shelf.OrderBy(_ => _random.Next()).Take(_random.Next(1, 4))
                    .Select(k => (m[k], k is "para" or "cetri" or "vitc" ? _random.Next(5, 21) : _random.Next(1, 3)))
                    .ToList();
                var discount = _random.Next(10) == 0 ? 5m : 0m;
                var sale = Sell(Days(daysAgo, 9 + b), payments[_random.Next(payments.Length)], discount, picks, b % 2 == 0 ? _technician : _admin);

                // One bill in the period was voided (wrong item scanned)
                if (daysAgo == -7 && b == 0) Void(sale, Days(daysAgo, 10), "Wrong item billed; customer returned it unopened.");
            }
        }
    }

    private Sale Sell(DateTime at, PaymentMethod payment, decimal discountPercent, List<(Med med, int qty)> lines, User user)
    {
        var sale = NewSale(at, payment, discountPercent, user);
        var day = DateOnly.FromDateTime(at);
        foreach (var (med, qty) in lines)
        {
            foreach (var (batch, take) in FefoAllocator.Allocate(med.Medicine.Batches, qty, day))
            {
                AddLine(sale, med.Medicine, batch, take, null);
                Stamp(StockRules.Apply(batch, -take, StockMovementType.Sale, user.Id, referenceId: sale.InvoiceNo), at);
            }
        }
        return Total(sale);
    }

    private void Bill(PrescriptionFill fill, int daysAgo, PaymentMethod payment)
    {
        var at = Days(daysAgo, 13);
        var sale = NewSale(at, payment, 0m, _technician);
        sale.Patient = fill.Prescription.Patient;
        fill.Sale = sale;
        foreach (var line in fill.Lines)
        {
            var item = AddLine(sale, line.PrescriptionItem.Medicine, line.Batch, line.Quantity, line.PrescriptionItem);
            if (SaleRules.NeedsRegisterEntry(line.PrescriptionItem.Medicine.Schedule))
            {
                var rx = fill.Prescription;
                _db.ScheduleRegister.Add(new ScheduleRegisterEntry
                {
                    Schedule = line.PrescriptionItem.Medicine.Schedule, SaleItem = item, InvoiceNo = sale.InvoiceNo,
                    PatientName = rx.Patient.FullName, PatientAddress = rx.Patient.Address, PrescriberName = rx.PrescriberName,
                    PrescriberRegNo = rx.PrescriberRegNo, PrescriberAddress = rx.PrescriberAddress, DrugName = item.MedicineName,
                    BatchNumber = item.BatchNumber, Quantity = item.Quantity, PharmacistId = fill.DispensedById, CreatedAt = at
                });
            }
        }
        Total(sale);
    }

    private Sale NewSale(DateTime at, PaymentMethod payment, decimal discountPercent, User user)
    {
        var fy = GstCalculator.FinancialYear(DateOnly.FromDateTime(at));
        _invoiceSeq[fy] = _invoiceSeq.GetValueOrDefault(fy) + 1;
        var sale = new Sale
        {
            InvoiceNo = GstCalculator.InvoiceNo(fy, _invoiceSeq[fy]), CreatedAt = at, PaymentMethod = payment,
            DiscountPercent = discountPercent, UserId = user.Id
        };
        _db.Sales.Add(sale);
        return sale;
    }

    private static SaleItem AddLine(Sale sale, Medicine medicine, Batch batch, int qty, PrescriptionItem? rxItem)
    {
        var money = GstCalculator.Calculate(batch.SellingPrice, qty, medicine.GstRatePercent, sale.DiscountPercent);
        var item = new SaleItem
        {
            Medicine = medicine, Batch = batch, Quantity = qty, MedicineName = $"{medicine.Name} {medicine.Strength}".Trim(),
            BatchNumber = batch.BatchNumber, ExpiryDate = batch.ExpiryDate, Mrp = batch.Mrp, UnitPrice = batch.SellingPrice,
            HsnCode = medicine.HsnCode, GstRatePercent = medicine.GstRatePercent, GrossAmount = money.Gross, Discount = money.Discount,
            TaxableValue = money.TaxableValue, Cgst = money.Cgst, Sgst = money.Sgst, LineTotal = money.Total
        };
        if (rxItem is not null) item.PrescriptionItemId = rxItem.Id == 0 ? null : rxItem.Id;
        sale.Items.Add(item);
        return item;
    }

    private static Sale Total(Sale sale)
    {
        sale.GrossAmount = sale.Items.Sum(i => i.GrossAmount);
        sale.Discount = sale.Items.Sum(i => i.Discount);
        sale.TaxableValue = sale.Items.Sum(i => i.TaxableValue);
        sale.Cgst = sale.Items.Sum(i => i.Cgst);
        sale.Sgst = sale.Items.Sum(i => i.Sgst);
        sale.Total = sale.Items.Sum(i => i.LineTotal);
        return sale;
    }

    private void Void(Sale sale, DateTime at, string reason)
    {
        foreach (var item in sale.Items)
            Stamp(StockRules.Apply(item.Batch, item.Quantity, StockMovementType.Return, _admin.Id, reason: $"Void: {reason}", referenceId: sale.InvoiceNo), at);
        sale.Status = SaleStatus.Voided;
        sale.VoidedById = _admin.Id;
        sale.VoidedAt = at;
        sale.VoidReason = reason;
    }

    // ---------- purchasing ----------

    private void SeedPurchaseOrders(Dictionary<string, Med> m, List<Supplier> s)
    {
        // Received in full last week
        var received = Po(s[1], -8, (m["ors"], 60), (m["cetri"], 100));
        PurchaseOrderRules.MarkOrdered(received, Days(-8));
        foreach (var line in received.Lines)
        {
            PurchaseOrderRules.RecordReceipt(received, line, line.QuantityOrdered);
            var med = m.Values.First(x => x.Medicine == line.Medicine);
            Receive(med, $"PO{line.Medicine.Name[..2].ToUpperInvariant()}{_random.Next(10, 99)}", 18, line.QuantityOrdered, s[1], -5);
        }
        PurchaseOrderRules.UpdateStatusAfterReceipt(received, Days(-5));

        // Ordered, on its way
        var ordered = Po(s[2], -1, (m["insulin"], 10), (m["alpra"], 30));
        PurchaseOrderRules.MarkOrdered(ordered, Days(-1));

        // Draft, not sent yet
        Po(s[0], 0, (m["cefi"], 40), (m["vitc"], 150));
    }

    private PurchaseOrder Po(Supplier supplier, int daysAgo, params (Med med, int qty)[] lines)
    {
        _poSeq++;
        var po = new PurchaseOrder
        {
            PoNumber = PurchaseOrderRules.PoNumber(GstCalculator.FinancialYear(_today.AddDays(daysAgo)), _poSeq),
            Supplier = supplier, CreatedById = _pharmacist.Id, CreatedAt = Days(daysAgo, 9),
            Lines = lines.Select(l => new PurchaseOrderLine { Medicine = l.med.Medicine, QuantityOrdered = l.qty, ExpectedRate = l.med.Rate }).ToList()
        };
        _db.PurchaseOrders.Add(po);
        return po;
    }

    // ---------- helpers ----------

    private T Add<T>(T entity) where T : class
    {
        _db.Add(entity);
        return entity;
    }

    /// <summary>A UTC time on a day relative to today; <paramref name="hour"/> is roughly the hour in India (UTC+5:30).</summary>
    private DateTime Days(int offset, int hour = 10) =>
        DateTime.SpecifyKind(_today.AddDays(offset).ToDateTime(new TimeOnly(Math.Max(hour - 5, 0), 30)), DateTimeKind.Utc);

    private static void Stamp(StockMovement movement, DateTime at) => movement.CreatedAt = at;
}
