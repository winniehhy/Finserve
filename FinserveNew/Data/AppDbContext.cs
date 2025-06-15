using Microsoft.EntityFrameworkCore;
using FinserveNew.Models;

namespace FinserveNew.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSets - Updated to use EmployeeModel
        public DbSet<EmployeeModel> Employees { get; set; } 
        public DbSet<Claim> Claims { get; set; }

        // Add other DbSets as needed
        public DbSet<BankInformation> BankInformations { get; set; }
        public DbSet<EmergencyContact> EmergencyContacts { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Salary> Salaries { get; set; }
        public DbSet<Approval> Approvals { get; set; }
        public DbSet<ClaimDetails> ClaimDetails { get; set; }
        public DbSet<ClaimType> ClaimTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure EmployeeModel entity
            modelBuilder.Entity<EmployeeModel>(entity =>
            {
                entity.HasKey(e => e.EmployeeID);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Password).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IC).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Nationality).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Position).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ConfirmationStatus).HasMaxLength(20).HasDefaultValue("Pending");

                // Configure relationships
                entity.HasOne(e => e.BankInformation)
                    .WithMany()
                    .HasForeignKey(e => e.BankID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.EmergencyContact)
                    .WithMany()
                    .HasForeignKey(e => e.EmergencyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Role)
                    .WithMany()
                    .HasForeignKey(e => e.RoleID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Claim entity
            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.ClaimType).IsRequired().HasMaxLength(50);
                entity.Property(c => c.ClaimAmount)
                    .HasPrecision(18, 2)
                    .IsRequired();
                entity.Property(c => c.Status).HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(c => c.CreatedDate).IsRequired();
                entity.Property(c => c.SupportingDocumentPath).HasMaxLength(500);
                entity.Property(c => c.SupportingDocumentName).HasMaxLength(255);

                // Configure the relationship with EmployeeModel
                entity.HasOne(c => c.Employee)
                    .WithMany(e => e.Claims)
                    .HasForeignKey(c => c.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Optional: Configure relationship with Approval if needed
                entity.HasOne(c => c.Approval)
                    .WithMany()
                    .HasForeignKey(c => c.ApprovalID)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure other entities as needed
            modelBuilder.Entity<BankInformation>(entity =>
            {
                entity.HasKey(b => b.BankID);
                // Add other BankInformation configurations
            });

            modelBuilder.Entity<EmergencyContact>(entity =>
            {
                entity.HasKey(e => e.EmergencyID);
                // Add other EmergencyContact configurations
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.RoleID);
                // Add other Role configurations
            });

            modelBuilder.Entity<Salary>(entity =>
            {
                entity.HasKey(s => s.SalaryID);
                // Configure relationship with Employee
                entity.HasOne<EmployeeModel>()
                    .WithMany(e => e.Salaries)
                    .HasForeignKey("EmployeeID")
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Approval>(entity =>
            {
                entity.HasKey(a => a.ApprovalID);
                // Configure relationship with Employee
                entity.HasOne<EmployeeModel>()
                    .WithMany(e => e.Approvals)
                    .HasForeignKey("EmployeeID")
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ClaimDetails>(entity =>
            {
                // Configure composite key
                entity.HasKey(cd => new { cd.ClaimID, cd.ClaimTypeID });

                entity.Property(cd => cd.Comment).IsRequired().HasMaxLength(500);
                entity.Property(cd => cd.DocumentPath).IsRequired().HasMaxLength(255);

                // Configure relationship with Claim
                entity.HasOne(cd => cd.Claim)
                    .WithMany(c => c.ClaimDetails)
                    .HasForeignKey(cd => cd.ClaimID)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure relationship with ClaimType
                entity.HasOne(cd => cd.ClaimType)
                    .WithMany()
                    .HasForeignKey(cd => cd.ClaimTypeID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ClaimType>(entity =>
            {
                entity.HasKey(ct => ct.ClaimTypeID);
                // Add other ClaimType configurations as needed
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}