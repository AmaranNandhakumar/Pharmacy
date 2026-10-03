using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;

namespace Pharmacy.Infrastructure.Data;

public class PharmacyDbContext : DbContext
{
    public PharmacyDbContext(DbContextOptions<PharmacyDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<PrescriptionFill> PrescriptionFills => Set<PrescriptionFill>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<ScheduleRegisterEntry> ScheduleRegister => Set<ScheduleRegisterEntry>();
    public DbSet<PharmacySettings> PharmacySettings => Set<PharmacySettings>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    /// <summary>
    /// Every DateTime is stored in UTC. SQL Server's datetime2 doesn't keep the "kind", so mark values
    /// read back as UTC; otherwise the API sends them without a "Z" and browsers show UTC as local time.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    private class UtcDateTimeConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Action).HasMaxLength(100).IsRequired();
            e.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
            e.Property(a => a.EntityId).HasMaxLength(50);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
            e.HasIndex(a => a.CreatedAt);
        });

        modelBuilder.Entity<Medicine>(e =>
        {
            e.Property(m => m.Name).HasMaxLength(200).IsRequired();
            e.Property(m => m.GenericName).HasMaxLength(200);
            e.Property(m => m.Strength).HasMaxLength(50);
            e.Property(m => m.Form).HasMaxLength(50).IsRequired();
            e.Property(m => m.PackSize).HasMaxLength(50);
            e.Property(m => m.Manufacturer).HasMaxLength(200);
            e.Property(m => m.Barcode).HasMaxLength(50);
            e.Property(m => m.Schedule).HasConversion<string>().HasMaxLength(10);
            e.Property(m => m.HsnCode).HasMaxLength(8).IsRequired();
            e.Property(m => m.GstRatePercent).HasPrecision(5, 2);
            e.Ignore(m => m.RequiresPrescription);
            e.HasIndex(m => m.Name);
            e.HasIndex(m => m.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
        });

        modelBuilder.Entity<Batch>(e =>
        {
            e.Property(b => b.BatchNumber).HasMaxLength(50).IsRequired();
            e.Property(b => b.Mrp).HasPrecision(18, 2);
            e.Property(b => b.SellingPrice).HasPrecision(18, 2);
            e.Property(b => b.PurchaseRate).HasPrecision(18, 2);
            e.Property(b => b.SupplierName).HasMaxLength(200);
            e.Property(b => b.SupplierInvoiceNo).HasMaxLength(50);
            e.Property(b => b.Version).IsConcurrencyToken();
            e.HasOne(b => b.Medicine).WithMany(m => m.Batches).HasForeignKey(b => b.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(b => new { b.MedicineId, b.BatchNumber }).IsUnique();
            e.HasIndex(b => b.ExpiryDate);
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Reason).HasMaxLength(500);
            e.Property(m => m.ReferenceId).HasMaxLength(50);
            e.HasOne(m => m.Batch).WithMany(b => b.Movements).HasForeignKey(m => m.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => m.CreatedAt);
        });

        modelBuilder.Entity<Patient>(e =>
        {
            e.Property(p => p.FullName).HasMaxLength(100).IsRequired();
            e.Property(p => p.Phone).HasMaxLength(20);
            e.Property(p => p.Address).HasMaxLength(300);
            e.Property(p => p.Allergies).HasMaxLength(500);
            e.Property(p => p.Notes).HasMaxLength(1000);
            e.HasIndex(p => p.FullName);
            e.HasIndex(p => p.Phone);
        });

        modelBuilder.Entity<Prescription>(e =>
        {
            e.Property(p => p.PrescriberName).HasMaxLength(100).IsRequired();
            e.Property(p => p.PrescriberRegNo).HasMaxLength(50).IsRequired();
            e.Property(p => p.PrescriberAddress).HasMaxLength(300);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.RejectReason).HasMaxLength(500);
            e.HasOne(p => p.Patient).WithMany(p => p.Prescriptions).HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.EnteredById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.VerifiedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.DispensedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.Status);
        });

        modelBuilder.Entity<PrescriptionItem>(e =>
        {
            e.Property(i => i.Dose).HasMaxLength(100).IsRequired();
            e.Property(i => i.Directions).HasMaxLength(300).IsRequired();
            e.Ignore(i => i.HasRefillsLeft);
            e.HasOne(i => i.Prescription).WithMany(p => p.Items).HasForeignKey(i => i.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Medicine).WithMany().HasForeignKey(i => i.MedicineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PrescriptionFill>(e =>
        {
            e.HasOne(f => f.Prescription).WithMany(p => p.Fills).HasForeignKey(f => f.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(f => f.DispensedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Sale).WithMany().HasForeignKey(f => f.SaleId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(f => f.SaleId);
        });

        modelBuilder.Entity<PrescriptionFillLine>(e =>
        {
            e.HasOne(l => l.Fill).WithMany(f => f.Lines).HasForeignKey(l => l.PrescriptionFillId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.PrescriptionItem).WithMany().HasForeignKey(l => l.PrescriptionItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.Batch).WithMany().HasForeignKey(l => l.BatchId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sale>(e =>
        {
            e.Property(s => s.InvoiceNo).HasMaxLength(20).IsRequired();
            e.HasIndex(s => s.InvoiceNo).IsUnique();
            e.Property(s => s.CustomerName).HasMaxLength(100);
            e.Property(s => s.PaymentMethod).HasConversion<string>().HasMaxLength(10);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(10);
            e.Property(s => s.VoidReason).HasMaxLength(500);
            e.Property(s => s.DiscountPercent).HasPrecision(5, 2);
            foreach (var money in new[] { nameof(Sale.GrossAmount), nameof(Sale.Discount), nameof(Sale.TaxableValue), nameof(Sale.Cgst), nameof(Sale.Sgst), nameof(Sale.Total) })
                e.Property(money).HasPrecision(18, 2);
            e.HasOne(s => s.Patient).WithMany().HasForeignKey(s => s.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(s => s.VoidedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(s => s.CreatedAt);
        });

        modelBuilder.Entity<SaleItem>(e =>
        {
            e.Property(i => i.MedicineName).HasMaxLength(200).IsRequired();
            e.Property(i => i.BatchNumber).HasMaxLength(50).IsRequired();
            e.Property(i => i.HsnCode).HasMaxLength(8).IsRequired();
            e.Property(i => i.GstRatePercent).HasPrecision(5, 2);
            foreach (var money in new[] { nameof(SaleItem.Mrp), nameof(SaleItem.UnitPrice), nameof(SaleItem.GrossAmount), nameof(SaleItem.Discount),
                         nameof(SaleItem.TaxableValue), nameof(SaleItem.Cgst), nameof(SaleItem.Sgst), nameof(SaleItem.LineTotal) })
                e.Property(money).HasPrecision(18, 2);
            e.HasOne(i => i.Sale).WithMany(s => s.Items).HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Medicine).WithMany().HasForeignKey(i => i.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Batch).WithMany().HasForeignKey(i => i.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PrescriptionItem>().WithMany().HasForeignKey(i => i.PrescriptionItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ScheduleRegisterEntry>(e =>
        {
            e.Property(r => r.Schedule).HasConversion<string>().HasMaxLength(10);
            e.Property(r => r.InvoiceNo).HasMaxLength(20).IsRequired();
            e.Property(r => r.PatientName).HasMaxLength(100).IsRequired();
            e.Property(r => r.PatientAddress).HasMaxLength(300);
            e.Property(r => r.PrescriberName).HasMaxLength(100).IsRequired();
            e.Property(r => r.PrescriberRegNo).HasMaxLength(50).IsRequired();
            e.Property(r => r.PrescriberAddress).HasMaxLength(300);
            e.Property(r => r.DrugName).HasMaxLength(200).IsRequired();
            e.Property(r => r.BatchNumber).HasMaxLength(50).IsRequired();
            e.HasOne(r => r.SaleItem).WithMany().HasForeignKey(r => r.SaleItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.PharmacistId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.Schedule, r.CreatedAt });
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.ContactPerson).HasMaxLength(100);
            e.Property(s => s.Phone).HasMaxLength(20);
            e.Property(s => s.Email).HasMaxLength(256);
            e.Property(s => s.Address).HasMaxLength(300);
            e.Property(s => s.Gstin).HasMaxLength(15);
            e.Property(s => s.DrugLicenceNo).HasMaxLength(100);
            e.HasIndex(s => s.Name).IsUnique();
        });

        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.Property(p => p.PoNumber).HasMaxLength(20).IsRequired();
            e.HasIndex(p => p.PoNumber).IsUnique();
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Notes).HasMaxLength(500);
            e.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.Status);
        });

        modelBuilder.Entity<PurchaseOrderLine>(e =>
        {
            e.Property(l => l.ExpectedRate).HasPrecision(18, 2);
            e.Ignore(l => l.QuantityOutstanding);
            e.HasOne(l => l.PurchaseOrder).WithMany(p => p.Lines).HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Medicine).WithMany().HasForeignKey(l => l.MedicineId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => new { l.PurchaseOrderId, l.MedicineId }).IsUnique();
        });

        modelBuilder.Entity<PharmacySettings>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.Address).HasMaxLength(300).IsRequired();
            e.Property(s => s.Phone).HasMaxLength(20);
            e.Property(s => s.StateCode).HasMaxLength(2).IsRequired();
            e.Property(s => s.Gstin).HasMaxLength(15).IsRequired();
            e.Property(s => s.DrugLicence20).HasMaxLength(50).IsRequired();
            e.Property(s => s.DrugLicence21).HasMaxLength(50).IsRequired();
            e.Property(s => s.RegisteredPharmacistName).HasMaxLength(100).IsRequired();
            e.Property(s => s.RegisteredPharmacistRegNo).HasMaxLength(50).IsRequired();

            // Placeholder details (clearly fake) until an Admin fills in the real ones on the Settings page
            e.HasData(new PharmacySettings
            {
                Id = Pharmacy.Core.Entities.PharmacySettings.SingletonId,
                Name = "Your Pharmacy Name",
                Address = "Shop address, City, State, PIN",
                StateCode = "33",
                Gstin = "33AAAAA0000A1Z5",
                DrugLicence20 = "DL-20-XXXX",
                DrugLicence21 = "DL-21-XXXX",
                RegisteredPharmacistName = "Registered Pharmacist",
                RegisteredPharmacistRegNo = "REG-XXXX"
            });
        });
    }
}
