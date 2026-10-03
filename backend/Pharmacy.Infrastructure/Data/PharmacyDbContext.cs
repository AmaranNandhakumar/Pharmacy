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
    }
}
