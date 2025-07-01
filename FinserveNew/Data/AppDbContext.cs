//using Microsoft.EntityFrameworkCore;
//using FinserveNew.Models;

//namespace FinserveNew.Data
//{
//    public class AppDbContext : DbContext
//    {
//        // Constructor to configure the DbContext with options (like connection string)
//        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
//        {
//        }

//        // Tables in the database
//        //public DbSet<EmployeeModel> Employees { get; set; }
//        public DbSet<Employee> Employees { get; set; }
//        public DbSet<Claim> Claims { get; set; }
//        public DbSet<BankInformation> BankInformations { get; set; }
//        public DbSet<EmergencyContact> EmergencyContacts { get; set; }
//        public DbSet<Role> Roles { get; set; }
//        public DbSet<Salary> Salaries { get; set; }
//        public DbSet<Approval> Approvals { get; set; }
//        public DbSet<ClaimDetails> ClaimDetails { get; set; }
//        public DbSet<ClaimType> ClaimTypes { get; set; }
//        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }

//        public DbSet<PayrollBatch> PayrollBatches { get; set; }
//        public DbSet<PayrollRecord> PayrollRecords { get; set; }
//        public DbSet<PayrollComponent> PayrollComponents { get; set; }
//        public DbSet<StatutoryRate> StatutoryRates { get; set; }


//        // Configure the table structures and relationships
//        protected override void OnModelCreating(ModelBuilder modelBuilder)
//        {
//            // Employee table configuration
//            modelBuilder.Entity<Employee>(entity =>
//            {
//                entity.HasKey(e => e.EmployeeID); // Primary key
//                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
//                entity.Property(e => e.Password).IsRequired().HasMaxLength(255);
//                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
//                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
//                entity.Property(e => e.IC).IsRequired().HasMaxLength(20);
//                entity.Property(e => e.Nationality).IsRequired().HasMaxLength(50);
//                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
//                entity.Property(e => e.TelephoneNumber).IsRequired().HasMaxLength(20);
//                entity.Property(e => e.Position).IsRequired().HasMaxLength(100);
//                entity.Property(e => e.ConfirmationStatus).HasMaxLength(20).HasDefaultValue("Pending");

//                // Foreign key relationships
//                entity.HasOne(e => e.BankInformation)
//                    .WithMany(b => b.Employees)
//                    .HasForeignKey(e => e.BankID)
//                    .OnDelete(DeleteBehavior.Restrict); // Prevent delete cascade

//                entity.HasOne(e => e.EmergencyContact)
//                    .WithMany(ec => ec.Employees)
//                    .HasForeignKey(e => e.EmergencyID)
//                    .OnDelete(DeleteBehavior.Restrict);

//                entity.HasOne(e => e.Role)
//                    .WithMany(r => r.Employees)
//                    .HasForeignKey(e => e.RoleID)
//                    .OnDelete(DeleteBehavior.Restrict);
//            });

//            // Claim table configuration
//            modelBuilder.Entity<Claim>(entity =>
//            {
//                entity.HasKey(c => c.Id); // Primary key
//                entity.Property(c => c.ClaimType).IsRequired().HasMaxLength(50);
//                entity.Property(c => c.ClaimAmount).HasPrecision(18, 2).IsRequired();
//                entity.Property(c => c.Status).HasMaxLength(20).HasDefaultValue("Pending");
//                entity.Property(c => c.CreatedDate).IsRequired();
//                entity.Property(c => c.SupportingDocumentPath).HasMaxLength(500);
//                entity.Property(c => c.SupportingDocumentName).HasMaxLength(255);

//                // Relationship to employee (claim owner)
//                entity.HasOne(c => c.Employee)
//                    .WithMany(e => e.Claims)
//                    .HasForeignKey(c => c.EmployeeID)
//                    .OnDelete(DeleteBehavior.Cascade); // Delete claim if employee is deleted

//                // Relationship to approval
//                entity.HasOne(c => c.Approval)
//                    .WithMany()
//                    .HasForeignKey(c => c.ApprovalID)
//                    .OnDelete(DeleteBehavior.SetNull); // Set approval ID to null if approval is deleted
//            });

//            // BankInformation table configuration
//            modelBuilder.Entity<BankInformation>(entity =>
//            {
//                entity.HasKey(b => b.BankID); // Primary key
//            });

//            // EmergencyContact table configuration
//            modelBuilder.Entity<EmergencyContact>(entity =>
//            {
//                entity.HasKey(e => e.EmergencyID); // Primary key
//            });

//            // Role table configuration
//            modelBuilder.Entity<Role>(entity =>
//            {
//                entity.HasKey(r => r.RoleID); // Primary key
//            });

//            // Salary table configuration
//            modelBuilder.Entity<Salary>(entity =>
//            {
//                entity.HasKey(s => s.SalaryID); // Primary key

//                // Relationship to employee
//                entity.HasOne<Employee>()
//                    .WithMany(e => e.Salaries)
//                    .HasForeignKey(s => s.EmployeeID)
//                    .OnDelete(DeleteBehavior.Cascade);
//            });

//            // Approval table configuration
//            modelBuilder.Entity<Approval>(entity =>
//            {
//                entity.HasKey(a => a.ApprovalID); // Primary key

//                // Relationship to employee (approver)
//                entity.HasOne<Employee>()
//                    .WithMany(e => e.Approvals)
//                    .HasForeignKey(a => a.EmployeeID)
//                    .OnDelete(DeleteBehavior.Cascade);
//            });

//            // ClaimDetails table configuration
//            modelBuilder.Entity<ClaimDetails>(entity =>
//            {
//                entity.HasKey(cd => new { cd.ClaimID, cd.ClaimTypeID }); // Composite primary key

//                entity.Property(cd => cd.Comment).IsRequired().HasMaxLength(500);
//                entity.Property(cd => cd.DocumentPath).IsRequired().HasMaxLength(255);

//                // Relationship to claim
//                entity.HasOne(cd => cd.Claim)
//                    .WithMany(c => c.ClaimDetails)
//                    .HasForeignKey(cd => cd.ClaimID)
//                    .OnDelete(DeleteBehavior.Cascade);

//                // Relationship to claim type
//                entity.HasOne(cd => cd.ClaimType)
//                    .WithMany()
//                    .HasForeignKey(cd => cd.ClaimTypeID)
//                    .OnDelete(DeleteBehavior.Restrict); // Prevent deletion if claim type is in use
//            });

//            // ClaimType table configuration
//            modelBuilder.Entity<ClaimType>(entity =>
//            {
//                entity.HasKey(ct => ct.ClaimTypeID); // Primary key
//            });

//            // EmployeeDocument table configuration
//            modelBuilder.Entity<EmployeeDocument>(entity =>
//            {
//                entity.HasKey(ed => ed.DocumentID); // Primary key

//                // Relationship to employee
//                entity.HasOne(ed => ed.Employee)
//                    .WithMany(e => e.EmployeeDocuments)
//                    .HasForeignKey(ed => ed.EmployeeID)
//                    .OnDelete(DeleteBehavior.Cascade);
//            });

//            // PayrollBatch configuration
//            modelBuilder.Entity<PayrollBatch>(entity =>
//            {
//                entity.HasKey(pb => pb.PayrollBatchId);
//                entity.Property(pb => pb.Status).HasMaxLength(20).HasDefaultValue("Draft");
//                entity.HasMany(pb => pb.PayrollRecords)
//                      .WithOne(pr => pr.PayrollBatch)
//                      .HasForeignKey(pr => pr.PayrollBatchId)
//                      .OnDelete(DeleteBehavior.Cascade);
//            });

//            // PayrollRecord configuration
//            modelBuilder.Entity<PayrollRecord>(entity =>
//            {
//                entity.HasKey(pr => pr.PayrollRecordId);
//                entity.Property(pr => pr.Status).HasMaxLength(20).HasDefaultValue("Draft");
//                entity.HasMany(pr => pr.Components)
//                      .WithOne(pc => pc.PayrollRecord)
//                      .HasForeignKey(pc => pc.PayrollRecordId)
//                      .OnDelete(DeleteBehavior.Cascade);
//                entity.HasOne(pr => pr.Employee)
//                      .WithMany()
//                      .HasForeignKey(pr => pr.EmployeeID)
//                      .OnDelete(DeleteBehavior.Restrict);
//            });

//            // PayrollComponent configuration
//            modelBuilder.Entity<PayrollComponent>(entity =>
//            {
//                entity.HasKey(pc => pc.PayrollComponentId);
//                entity.Property(pc => pc.Type).HasMaxLength(20);
//                entity.Property(pc => pc.Name).HasMaxLength(100);
//            });

//            // StatutoryRate configuration
//            modelBuilder.Entity<StatutoryRate>(entity =>
//            {
//                entity.HasKey(sr => sr.StatutoryRateId);
//                entity.Property(sr => sr.Name).HasMaxLength(50);
//                entity.Property(sr => sr.Description).HasMaxLength(255);
//            });

//            // Call the base method
//            base.OnModelCreating(modelBuilder);
//        }
//    }
//}
using Microsoft.EntityFrameworkCore;
using FinserveNew.Models;

namespace FinserveNew.Data
{
    public class AppDbContext : DbContext
    {
        // Constructor to configure the DbContext with options (like connection string)
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Tables in the database
        //public DbSet<EmployeeModel> Employees { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Claim> Claims { get; set; }
        public DbSet<BankInformation> BankInformations { get; set; }
        public DbSet<EmergencyContact> EmergencyContacts { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Salary> Salaries { get; set; }
        public DbSet<Approval> Approvals { get; set; }
        public DbSet<ClaimDetails> ClaimDetails { get; set; }
        public DbSet<ClaimType> ClaimTypes { get; set; }
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }

        public DbSet<PayrollBatch> PayrollBatches { get; set; }
        public DbSet<PayrollRecord> PayrollRecords { get; set; }
        public DbSet<PayrollComponent> PayrollComponents { get; set; }
        public DbSet<StatutoryRate> StatutoryRates { get; set; }

        public DbSet<LeaveModel> Leaves { get; set; }
        public DbSet<LeaveTypeModel> LeaveTypes { get; set; }


        // Configure the table structures and relationships
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Employee table configuration
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.EmployeeID); // Primary key
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Password).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IC).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Nationality).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TelephoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Position).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ConfirmationStatus).HasMaxLength(20).HasDefaultValue("Pending");

                // Foreign key relationships
                entity.HasOne(e => e.BankInformation)
                    .WithMany(b => b.Employees)
                    .HasForeignKey(e => e.BankID)
                    .OnDelete(DeleteBehavior.Restrict); // Prevent delete cascade

                entity.HasOne(e => e.EmergencyContact)
                    .WithMany(ec => ec.Employees)
                    .HasForeignKey(e => e.EmergencyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Role)
                    .WithMany(r => r.Employees)
                    .HasForeignKey(e => e.RoleID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Claim table configuration - SIMPLIFIED (no navigation properties)
            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasKey(c => c.Id); // Primary key
                entity.Property(c => c.ClaimType).IsRequired().HasMaxLength(50);
                entity.Property(c => c.ClaimAmount).HasPrecision(18, 2).IsRequired();
                entity.Property(c => c.Status).HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(c => c.CreatedDate).IsRequired();
                entity.Property(c => c.SupportingDocumentPath).HasMaxLength(500);
                entity.Property(c => c.SupportingDocumentName).HasMaxLength(255);
                entity.Property(c => c.EmployeeID).IsRequired().HasMaxLength(255);
                entity.Property(c => c.TotalAmount).HasPrecision(18, 2);

                // No relationship configurations since navigation properties are removed
            });

            // BankInformation table configuration
            modelBuilder.Entity<BankInformation>(entity =>
            {
                entity.HasKey(b => b.BankID); // Primary key
            });

            // EmergencyContact table configuration
            modelBuilder.Entity<EmergencyContact>(entity =>
            {
                entity.HasKey(e => e.EmergencyID); // Primary key
            });

            // Role table configuration
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.RoleID); // Primary key
            });

            // Salary table configuration
            modelBuilder.Entity<Salary>(entity =>
            {
                entity.HasKey(s => s.SalaryID); // Primary key

                // Relationship to employee
                entity.HasOne<Employee>()
                    .WithMany(e => e.Salaries)
                    .HasForeignKey(s => s.EmployeeID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Approval table configuration
            modelBuilder.Entity<Approval>(entity =>
            {
                entity.HasKey(a => a.ApprovalID); // Primary key

                // Relationship to employee (approver)
                entity.HasOne<Employee>()
                    .WithMany(e => e.Approvals)
                    .HasForeignKey(a => a.EmployeeID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ClaimDetails table configuration - MODIFIED to work without Claim navigation property
            modelBuilder.Entity<ClaimDetails>(entity =>
            {
                entity.HasKey(cd => new { cd.ClaimID, cd.ClaimTypeID }); // Composite primary key

                entity.Property(cd => cd.Comment).IsRequired().HasMaxLength(500);
                entity.Property(cd => cd.DocumentPath).IsRequired().HasMaxLength(255);

                // Remove relationship to claim since Claim doesn't have ClaimDetails navigation property
                // You can still have a foreign key constraint at the database level if needed

                // Relationship to claim type (if ClaimType has navigation properties)
                entity.HasOne(cd => cd.ClaimType)
                    .WithMany()
                    .HasForeignKey(cd => cd.ClaimTypeID)
                    .OnDelete(DeleteBehavior.Restrict); // Prevent deletion if claim type is in use
            });

            // ClaimType table configuration
            modelBuilder.Entity<ClaimType>(entity =>
            {
                entity.HasKey(ct => ct.ClaimTypeID); // Primary key
            });

            // EmployeeDocument table configuration
            modelBuilder.Entity<EmployeeDocument>(entity =>
            {
                entity.HasKey(ed => ed.DocumentID); // Primary key

                // Relationship to employee
                entity.HasOne(ed => ed.Employee)
                    .WithMany(e => e.EmployeeDocuments)
                    .HasForeignKey(ed => ed.EmployeeID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // PayrollBatch configuration
            modelBuilder.Entity<PayrollBatch>(entity =>
            {
                entity.HasKey(pb => pb.PayrollBatchId);
                entity.Property(pb => pb.Status).HasMaxLength(20).HasDefaultValue("Draft");
                entity.HasMany(pb => pb.PayrollRecords)
                      .WithOne(pr => pr.PayrollBatch)
                      .HasForeignKey(pr => pr.PayrollBatchId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // PayrollRecord configuration
            modelBuilder.Entity<PayrollRecord>(entity =>
            {
                entity.HasKey(pr => pr.PayrollRecordId);
                entity.Property(pr => pr.Status).HasMaxLength(20).HasDefaultValue("Draft");
                entity.HasMany(pr => pr.Components)
                      .WithOne(pc => pc.PayrollRecord)
                      .HasForeignKey(pc => pc.PayrollRecordId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(pr => pr.Employee)
                      .WithMany()
                      .HasForeignKey(pr => pr.EmployeeID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // PayrollComponent configuration
            modelBuilder.Entity<PayrollComponent>(entity =>
            {
                entity.HasKey(pc => pc.PayrollComponentId);
                entity.Property(pc => pc.Type).HasMaxLength(20);
                entity.Property(pc => pc.Name).HasMaxLength(100);
            });

            // StatutoryRate configuration
            modelBuilder.Entity<StatutoryRate>(entity =>
            {
                entity.HasKey(sr => sr.StatutoryRateId);
                entity.Property(sr => sr.Name).HasMaxLength(50);
                entity.Property(sr => sr.Description).HasMaxLength(255);
            });

            // LeaveModel configuration
            modelBuilder.Entity<LeaveModel>(entity =>
            {
                entity.HasKey(l => l.LeaveID); // Primary key

                // Relationships
                entity.HasOne(l => l.Employee)
                    .WithMany()
                    .HasForeignKey(l => l.EmployeeID)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.LeaveType)
                    .WithMany(lt => lt.Leaves)
                    .HasForeignKey(l => l.LeaveTypeID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // LeaveTypeModel configuration
            modelBuilder.Entity<LeaveTypeModel>(entity =>
            {
                entity.HasKey(lt => lt.LeaveTypeID); // Primary key
            });

            // Call the base method
            base.OnModelCreating(modelBuilder);
        }
    }
}