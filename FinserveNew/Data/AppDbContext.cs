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

            base.OnModelCreating(modelBuilder);
        }
    }
}