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

            // Claim table configuration
            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasKey(c => c.Id); // Primary key
                entity.Property(c => c.ClaimType).IsRequired().HasMaxLength(50);
                entity.Property(c => c.ClaimAmount).HasPrecision(18, 2).IsRequired();
                entity.Property(c => c.Status).HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(c => c.CreatedDate).IsRequired();
                entity.Property(c => c.SupportingDocumentPath).HasMaxLength(500);
                entity.Property(c => c.SupportingDocumentName).HasMaxLength(255);

                // Relationship to employee (claim owner)
                entity.HasOne(c => c.Employee)
                    .WithMany(e => e.Claims)
                    .HasForeignKey(c => c.EmployeeID)
                    .OnDelete(DeleteBehavior.Cascade); // Delete claim if employee is deleted

                // Relationship to approval
                entity.HasOne(c => c.Approval)
                    .WithMany()
                    .HasForeignKey(c => c.ApprovalID)
                    .OnDelete(DeleteBehavior.SetNull); // Set approval ID to null if approval is deleted
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

            // ClaimDetails table configuration
            modelBuilder.Entity<ClaimDetails>(entity =>
            {
                entity.HasKey(cd => new { cd.ClaimID, cd.ClaimTypeID }); // Composite primary key

                entity.Property(cd => cd.Comment).IsRequired().HasMaxLength(500);
                entity.Property(cd => cd.DocumentPath).IsRequired().HasMaxLength(255);

                // Relationship to claim
                entity.HasOne(cd => cd.Claim)
                    .WithMany(c => c.ClaimDetails)
                    .HasForeignKey(cd => cd.ClaimID)
                    .OnDelete(DeleteBehavior.Cascade);

                // Relationship to claim type
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

            // Call the base method
            base.OnModelCreating(modelBuilder);
        }
    }
}
