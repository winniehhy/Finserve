using Microsoft.EntityFrameworkCore;
using FinserveNew.Models;

namespace FinserveNew.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Claim> Claims { get; set; }
        public DbSet<Employee> Employees { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Name).IsRequired();
                entity.Property(u => u.Role).IsRequired();
            });

            // Configure Claim entity
            modelBuilder.Entity<Claim>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.ClaimType).IsRequired();
                entity.Property(c => c.Description).IsRequired();
                entity.Property(c => c.ClaimAmount)
                    .HasPrecision(18, 2)
                    .IsRequired();
                entity.Property(c => c.Status).HasDefaultValue("Pending");
                entity.Property(c => c.CreatedDate).IsRequired();

                // Configure the relationship explicitly
                entity.HasOne(c => c.User)
                    .WithMany(u => u.Claims)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.EmployeeId).HasName("PRIMARY");

                entity.ToTable("employees");

                entity.Property(e => e.EmployeeId)
                    .HasMaxLength(10)
                    .HasColumnName("EmployeeID");
                entity.Property(e => e.ConfirmationStatus).HasMaxLength(30);
                entity.Property(e => e.Email).HasMaxLength(50);
                entity.Property(e => e.Epfnumber)
                    .HasMaxLength(30)
                    .HasColumnName("EPFNumber");
                entity.Property(e => e.FirstName).HasMaxLength(45);
                entity.Property(e => e.Ic)
                    .HasMaxLength(15)
                    .HasColumnName("IC");
                entity.Property(e => e.IncomeTaxNumber).HasMaxLength(30);
                entity.Property(e => e.LastName).HasMaxLength(45);
                entity.Property(e => e.Nationality).HasMaxLength(50);
                entity.Property(e => e.Password).HasMaxLength(255);
                entity.Property(e => e.Position).HasMaxLength(30);
                entity.Property(e => e.TelephoneNumber).HasMaxLength(20);
                entity.Property(e => e.Username).HasMaxLength(50);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}