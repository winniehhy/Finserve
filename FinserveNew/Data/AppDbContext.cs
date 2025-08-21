using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Models;

namespace FinserveNew.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Tables in the database
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Claim> Claims { get; set; }
        public DbSet<BankInformation> BankInformations { get; set; }
        public DbSet<EmergencyContact> EmergencyContacts { get; set; }
        public DbSet<JobRole> Roles { get; set; }
        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<Approval> Approvals { get; set; }
        public DbSet<ClaimDetails> ClaimDetails { get; set; }
        public DbSet<ClaimType> ClaimTypes { get; set; }
        public DbSet<EmployeeDocument> EmployeeDocuments { get; set; }

        //public DbSet<PayrollBatch> PayrollBatches { get; set; }
        //public DbSet<PayrollRecord> PayrollRecords { get; set; }
        //public DbSet<PayrollComponent> PayrollComponents { get; set; }
        //public DbSet<StatutoryRate> StatutoryRates { get; set; }

        public DbSet<LeaveModel> Leaves { get; set; }
        public DbSet<LeaveTypeModel> LeaveTypes { get; set; }

        public DbSet<LeaveDetailsModel> LeaveDetails { get; set; }

        public DbSet<UnpaidLeaveRequestModel> UnpaidLeaveRequests { get; set; }

        // Add the missing Invoice DbSet
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        
        public DbSet<ProcessOCRSubmissionModel> ProcessOCRSubmissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // IGNORE UNUSED IDENTITY TABLES 
            //modelBuilder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>();
            //modelBuilder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>();
            //modelBuilder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>();
            //modelBuilder.Ignore<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>();

            // Configure Employee table
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.EmployeeID);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Password).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IC).HasMaxLength(12);
                entity.Property(e => e.PassportNumber).HasMaxLength(20);
                entity.Property(e => e.Nationality).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TelephoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Position).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ConfirmationStatus).HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(e => e.IncomeTaxNumber).IsRequired().HasMaxLength(15);
                entity.Property(e => e.EPFNumber).IsRequired().HasMaxLength(15);

                // Foreign key relationships
                entity.HasOne(e => e.BankInformation)
                    .WithMany(b => b.Employees)
                    .HasForeignKey(e => e.BankID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.EmergencyContact)
                    .WithMany(ec => ec.Employees)
                    .HasForeignKey(e => e.EmergencyID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Role)
                    .WithMany(r => r.Employees)
                    .HasForeignKey(e => e.RoleID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure the relationship between ApplicationUser and Employee
            //modelBuilder.Entity<Employee>()
            //    .HasOne(e => e.ApplicationUser)
            //    .WithOne()
            //    .HasForeignKey<Employee>(e => e.ApplicationUserId);

            // Configure the Claim table (updated for RBAC approach)
            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.ClaimType).IsRequired().HasMaxLength(50);
                entity.Property(c => c.ClaimAmount).HasPrecision(18, 2).IsRequired();
                entity.Property(c => c.Status).HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(c => c.CreatedDate).IsRequired();
                entity.Property(c => c.SupportingDocumentPath).HasMaxLength(500);
                entity.Property(c => c.SupportingDocumentName).HasMaxLength(255);
                entity.Property(c => c.EmployeeID).IsRequired().HasMaxLength(255);
                entity.Property(c => c.TotalAmount).HasPrecision(18, 2);
                entity.Property(c => c.ApprovedBy).HasMaxLength(255);
                entity.Property(c => c.ApprovalRemarks).HasMaxLength(1000);

                // NEW: Configure currency-related fields
                entity.Property(c => c.Currency).HasMaxLength(3).HasDefaultValue("MYR");
                entity.Property(c => c.OriginalAmount).HasPrecision(18, 2);
                entity.Property(c => c.OriginalCurrency).HasMaxLength(3);
                entity.Property(c => c.ExchangeRate).HasPrecision(10, 6);
                entity.Property(c => c.ClaimDate).IsRequired();
                entity.Property(c => c.Description).HasMaxLength(1000);
            });

            // Configure the Invoice table
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasKey(i => i.InvoiceID);
                entity.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
                entity.Property(i => i.IssueDate).IsRequired();
                entity.Property(i => i.DueDate).IsRequired();
                entity.Property(i => i.TotalAmount).HasPrecision(18, 2).IsRequired();
                entity.Property(i => i.Currency).HasMaxLength(3).HasDefaultValue("MYR");
                entity.Property(i => i.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(i => i.Remark).HasMaxLength(500);
                entity.Property(i => i.FilePath).HasMaxLength(255);
            });

            modelBuilder.Entity<ClaimDetails>(entity =>
            {
                entity.HasKey(cd => cd.Id); // Use single primary key instead of composite

                entity.Property(cd => cd.Comment).IsRequired().HasMaxLength(500);
                entity.Property(cd => cd.DocumentPath).IsRequired().HasMaxLength(500);
                entity.Property(cd => cd.OriginalFileName).HasMaxLength(255);
                entity.Property(cd => cd.UploadDate).IsRequired();

                // Configure relationships
                entity.HasOne(cd => cd.Claim)
                      .WithMany(c => c.ClaimDetails)
                      .HasForeignKey(cd => cd.ClaimID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cd => cd.ClaimType)
                      .WithMany(ct => ct.ClaimDetails)
                      .HasForeignKey(cd => cd.ClaimTypeID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ClaimType>(entity =>
            {
                entity.HasKey(ct => ct.Id);
                entity.Property(ct => ct.Name).IsRequired().HasMaxLength(50);
                entity.Property(ct => ct.Description).HasMaxLength(500);
                entity.Property(ct => ct.MaxAmount).HasPrecision(18, 2);
                entity.Property(ct => ct.RequiresApproval).HasDefaultValue(true);
                entity.Property(ct => ct.IsActive).HasDefaultValue(true);
                entity.Property(ct => ct.CreatedDate).IsRequired();
            });

            // Configure remaining tables
            modelBuilder.Entity<BankInformation>(entity =>
            {
                entity.HasKey(b => b.BankID);
            });

            modelBuilder.Entity<EmergencyContact>(entity =>
            {
                entity.HasKey(e => e.EmergencyID);
            });

            modelBuilder.Entity<JobRole>(entity =>
            {
                entity.HasKey(r => r.RoleID);
            });

            //modelBuilder.Entity<Payroll>(entity =>
            //{
            //    entity.HasKey(s => s.PayrollID);
            //    entity.HasOne<Employee>()
            //        .WithMany(e => e.Payrolls)
            //        .HasForeignKey(s => s.EmployeeID)
            //        .OnDelete(DeleteBehavior.Cascade);
            //});

            // Configure Approval table and its relationships
            modelBuilder.Entity<Approval>(entity =>
            {
                entity.HasKey(a => a.ApprovalID);
                entity.Property(a => a.Action).IsRequired().HasMaxLength(500);
                entity.Property(a => a.ActionBy).IsRequired().HasMaxLength(100);
                entity.Property(a => a.Status).HasMaxLength(30);
                entity.Property(a => a.Remarks).HasMaxLength(1000);

                entity.HasOne(a => a.Employee)
                    .WithMany()
                    .HasForeignKey(a => a.EmployeeID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Payroll)
                    .WithMany(p => p.Approvals)
                    .HasForeignKey(a => a.PayrollID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            //modelBuilder.Entity<PayrollBatch>(entity =>
            //{
            //    entity.HasKey(pb => pb.PayrollBatchId);
            //    entity.Property(pb => pb.Status).HasMaxLength(20).HasDefaultValue("Draft");
            //});

            modelBuilder.Entity<LeaveModel>(entity =>
            {
                entity.HasKey(l => l.LeaveID);
                entity.HasOne(l => l.Employee)
                    .WithMany()
                    .HasForeignKey(l => l.EmployeeID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(l => l.LeaveType)
                    .WithMany(lt => lt.Leaves)
                    .HasForeignKey(l => l.LeaveTypeID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UnpaidLeaveRequestModel>(entity =>
            {
                entity.HasKey(u => u.UnpaidLeaveRequestID);

                // Configure properties
                entity.Property(u => u.EmployeeID).IsRequired().HasMaxLength(255);
                entity.Property(u => u.StartDate).IsRequired();
                entity.Property(u => u.EndDate).IsRequired();
                entity.Property(u => u.RequestedDays).IsRequired().HasPrecision(4, 1); // Support decimal values
                entity.Property(u => u.ExcessDays).IsRequired().HasPrecision(4, 1); // Support decimal values
                entity.Property(u => u.Reason).IsRequired().HasMaxLength(500);
                entity.Property(u => u.JustificationReason).IsRequired().HasMaxLength(1000);
                entity.Property(u => u.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(u => u.SubmissionDate).IsRequired();
                entity.Property(u => u.ApprovedBy).HasMaxLength(450); // Standard length for Identity UserId
                entity.Property(u => u.ApprovalRemarks).HasMaxLength(1000);
                entity.Property(u => u.CreatedDate).IsRequired();

                // Configure relationships
                entity.HasOne(u => u.Employee)
                    .WithMany()
                    .HasForeignKey(u => u.EmployeeID)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(u => u.LeaveType)
                    .WithMany()
                    .HasForeignKey(u => u.LeaveTypeID)
                    .OnDelete(DeleteBehavior.Restrict);

               
            });
        }
    }
}