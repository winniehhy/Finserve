using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace FinserveNew;

public partial class FinserveNewDbContext : DbContext
{
    public FinserveNewDbContext()
    {
    }

    public FinserveNewDbContext(DbContextOptions<FinserveNewDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Claim> Claims { get; set; }

    public virtual DbSet<Efmigrationshistory> Efmigrationshistories { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySql("server=localhost;port=3306;database=FinserveNewDB;user=root;password=mysql123456", Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.42-mysql"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Claim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("claims");

            entity.HasIndex(e => e.UserId, "IX_Claims_UserId");

            entity.Property(e => e.ClaimAmount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedDate).HasMaxLength(6);
            entity.Property(e => e.Status).HasDefaultValueSql("_utf8mb4\\'Pending\\'");

            entity.HasOne(d => d.User).WithMany(p => p.Claims)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Claims_Users_UserId");
        });

        modelBuilder.Entity<Efmigrationshistory>(entity =>
        {
            entity.HasKey(e => e.MigrationId).HasName("PRIMARY");

            entity.ToTable("__efmigrationshistory");

            entity.Property(e => e.MigrationId).HasMaxLength(150);
            entity.Property(e => e.ProductVersion).HasMaxLength(32);
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
            entity.Property(e => e.TelephoneNumber).HasMaxLength(20);
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
