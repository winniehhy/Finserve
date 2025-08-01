﻿// <auto-generated />
using System;
using FinserveNew.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace FinserveNew.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250718092743_AddPayrollPaymentStatus")]
    partial class AddPayrollPaymentStatus
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.17")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("FinserveNew.Models.ApplicationUser", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("varchar(255)");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("int");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("longtext");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("EmployeeID")
                        .HasColumnType("longtext");

                    b.Property<string>("EmployeeID1")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("longtext");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("longtext");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("longtext");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("UserName")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("EmployeeID1");

                    b.HasIndex("NormalizedEmail")
                        .HasDatabaseName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasDatabaseName("UserNameIndex");

                    b.ToTable("AspNetUsers", (string)null);
                });

            modelBuilder.Entity("FinserveNew.Models.Approval", b =>
                {
                    b.Property<int>("ApprovalID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("ApprovalID"));

                    b.Property<DateTime>("ApprovalDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("ApprovedBy")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("EmployeeID")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<string>("Purpose")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("varchar(500)");

                    b.HasKey("ApprovalID");

                    b.HasIndex("EmployeeID");

                    b.ToTable("Approvals");
                });

            modelBuilder.Entity("FinserveNew.Models.BankInformation", b =>
                {
                    b.Property<int>("BankID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("BankID"));

                    b.Property<string>("BankAccountNumber")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<string>("BankName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("BankType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.HasKey("BankID");

                    b.ToTable("BankInformations");
                });

            modelBuilder.Entity("FinserveNew.Models.Claim", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<DateTime?>("ApprovalDate")
                        .HasColumnType("datetime(6)");

                    b.Property<int?>("ApprovalID")
                        .HasColumnType("int");

                    b.Property<decimal>("ClaimAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("ClaimType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("varchar(1000)");

                    b.Property<string>("EmployeeID")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("varchar(20)")
                        .HasDefaultValue("Pending");

                    b.Property<DateTime?>("SubmissionDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("SupportingDocumentName")
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.Property<string>("SupportingDocumentPath")
                        .HasMaxLength(500)
                        .HasColumnType("varchar(500)");

                    b.Property<decimal?>("TotalAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.HasKey("Id");

                    b.HasIndex("ApprovalID");

                    b.HasIndex("EmployeeID");

                    b.ToTable("Claims");
                });

            modelBuilder.Entity("FinserveNew.Models.ClaimDetails", b =>
                {
                    b.Property<int>("ClaimID")
                        .HasColumnType("int");

                    b.Property<int>("ClaimTypeID")
                        .HasColumnType("int");

                    b.Property<string>("Comment")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("varchar(500)");

                    b.Property<string>("DocumentPath")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.HasKey("ClaimID", "ClaimTypeID");

                    b.HasIndex("ClaimTypeID");

                    b.ToTable("ClaimDetails");
                });

            modelBuilder.Entity("FinserveNew.Models.ClaimType", b =>
                {
                    b.Property<int>("ClaimTypeID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("ClaimTypeID"));

                    b.Property<decimal>("MaxAmount")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("TypeName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.HasKey("ClaimTypeID");

                    b.ToTable("ClaimTypes");
                });

            modelBuilder.Entity("FinserveNew.Models.EmergencyContact", b =>
                {
                    b.Property<int>("EmergencyID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("EmergencyID"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("Relationship")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<string>("TelephoneNumber")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("varchar(20)");

                    b.HasKey("EmergencyID");

                    b.ToTable("EmergencyContacts");
                });

            modelBuilder.Entity("FinserveNew.Models.Employee", b =>
                {
                    b.Property<string>("EmployeeID")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("ApplicationUserId")
                        .HasColumnType("varchar(255)");

                    b.Property<int>("BankID")
                        .HasColumnType("int");

                    b.Property<string>("ConfirmationStatus")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("varchar(20)")
                        .HasDefaultValue("Pending");

                    b.Property<DateOnly>("DateOfBirth")
                        .HasColumnType("date");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<int>("EmergencyID")
                        .HasColumnType("int");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("IC")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("varchar(20)");

                    b.Property<DateOnly>("JoinDate")
                        .HasColumnType("date");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("Nationality")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.Property<string>("Position")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<DateOnly?>("ResignationDate")
                        .HasColumnType("date");

                    b.Property<int>("RoleID")
                        .HasColumnType("int");

                    b.Property<string>("TelephoneNumber")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("varchar(20)");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.HasKey("EmployeeID");

                    b.HasIndex("ApplicationUserId")
                        .IsUnique();

                    b.HasIndex("BankID");

                    b.HasIndex("EmergencyID");

                    b.HasIndex("RoleID");

                    b.ToTable("Employees");
                });

            modelBuilder.Entity("FinserveNew.Models.EmployeeDocument", b =>
                {
                    b.Property<int>("DocumentID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("DocumentID"));

                    b.Property<string>("DocumentType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("EmployeeID")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("varchar(500)");

                    b.HasKey("DocumentID");

                    b.HasIndex("EmployeeID");

                    b.ToTable("EmployeeDocuments");
                });

            modelBuilder.Entity("FinserveNew.Models.Invoice", b =>
                {
                    b.Property<int>("InvoiceID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("InvoiceID"));

                    b.Property<string>("Currency")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(3)
                        .HasColumnType("varchar(3)")
                        .HasDefaultValue("MYR");

                    b.Property<DateTime>("DueDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("EmployeeID")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.Property<string>("InvoiceNumber")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<DateTime>("IssueDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Remark")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("varchar(500)");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("varchar(20)")
                        .HasDefaultValue("Pending");

                    b.Property<decimal>("TotalAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("Year")
                        .HasColumnType("int");

                    b.HasKey("InvoiceID");

                    b.HasIndex("EmployeeID");

                    b.ToTable("Invoices");
                });

            modelBuilder.Entity("FinserveNew.Models.LeaveModel", b =>
                {
                    b.Property<int>("LeaveID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("LeaveID"));

                    b.Property<string>("ApprovedBy")
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("EmployeeID")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<DateOnly>("EndDate")
                        .HasColumnType("date");

                    b.Property<int>("LeaveTypeID")
                        .HasColumnType("int");

                    b.Property<string>("Reason")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("varchar(500)");

                    b.Property<DateOnly>("StartDate")
                        .HasColumnType("date");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("varchar(20)");

                    b.HasKey("LeaveID");

                    b.HasIndex("EmployeeID");

                    b.HasIndex("LeaveTypeID");

                    b.ToTable("Leaves");
                });

            modelBuilder.Entity("FinserveNew.Models.LeaveTypeModel", b =>
                {
                    b.Property<int>("LeaveTypeID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("LeaveTypeID"));

                    b.Property<int>("DefaultDaysPerYear")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("varchar(500)");

                    b.Property<string>("TypeName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.HasKey("LeaveTypeID");

                    b.ToTable("LeaveTypes");
                });

            modelBuilder.Entity("FinserveNew.Models.Payroll", b =>
                {
                    b.Property<int>("PayrollID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("PayrollID"));

                    b.Property<decimal>("BasicSalary")
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("datetime(6)");

                    b.Property<decimal>("EmployeeEis")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("EmployeeEpf")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("EmployeeID")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<decimal>("EmployeeSocso")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("EmployeeTax")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("EmployerEis")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("EmployerEpf")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("EmployerOtherContributions")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("EmployerSocso")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("EmployerTax")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("Month")
                        .HasColumnType("int");

                    b.Property<string>("PaymentStatus")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("varchar(30)");

                    b.Property<string>("ProjectName")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<decimal>("TotalEmployerCost")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("TotalWages")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("Year")
                        .HasColumnType("int");

                    b.HasKey("PayrollID");

                    b.HasIndex("EmployeeID");

                    b.ToTable("Payrolls");
                });

            modelBuilder.Entity("FinserveNew.Models.JobRole", b =>
                {
                    b.Property<int>("RoleID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("RoleID"));

                    b.Property<string>("Description")
                        .HasMaxLength(200)
                        .HasColumnType("varchar(200)");

                    b.Property<string>("RoleName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.HasKey("RoleID");

                    b.ToTable("Roles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("longtext");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasDatabaseName("RoleNameIndex");

                    b.ToTable("AspNetRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasColumnType("longtext");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("longtext");

                    b.Property<string>("RoleId")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasColumnType("longtext");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("longtext");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("RoleId")
                        .HasColumnType("varchar(255)");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles", (string)null);
                });

            modelBuilder.Entity("FinserveNew.Models.ApplicationUser", b =>
                {
                    b.HasOne("FinserveNew.Models.Employee", "Employee")
                        .WithMany()
                        .HasForeignKey("EmployeeID1");

                    b.Navigation("Employee");
                });

            modelBuilder.Entity("FinserveNew.Models.Approval", b =>
                {
                    b.HasOne("FinserveNew.Models.Employee", "Employee")
                        .WithMany("Approvals")
                        .HasForeignKey("EmployeeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Employee");
                });

            modelBuilder.Entity("FinserveNew.Models.Claim", b =>
                {
                    b.HasOne("FinserveNew.Models.Approval", null)
                        .WithMany("Claims")
                        .HasForeignKey("ApprovalID");

                    b.HasOne("FinserveNew.Models.Employee", null)
                        .WithMany("Claims")
                        .HasForeignKey("EmployeeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("FinserveNew.Models.ClaimDetails", b =>
                {
                    b.HasOne("FinserveNew.Models.Claim", "Claim")
                        .WithMany()
                        .HasForeignKey("ClaimID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.ClaimType", "ClaimType")
                        .WithMany("ClaimDetails")
                        .HasForeignKey("ClaimTypeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Claim");

                    b.Navigation("ClaimType");
                });

            modelBuilder.Entity("FinserveNew.Models.Employee", b =>
                {
                    b.HasOne("FinserveNew.Models.ApplicationUser", "ApplicationUser")
                        .WithOne()
                        .HasForeignKey("FinserveNew.Models.Employee", "ApplicationUserId");

                    b.HasOne("FinserveNew.Models.BankInformation", "BankInformation")
                        .WithMany("Employees")
                        .HasForeignKey("BankID")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.EmergencyContact", "EmergencyContact")
                        .WithMany("Employees")
                        .HasForeignKey("EmergencyID")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.JobRole", "Role")
                        .WithMany("Employees")
                        .HasForeignKey("RoleID")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("ApplicationUser");

                    b.Navigation("BankInformation");

                    b.Navigation("EmergencyContact");

                    b.Navigation("Role");
                });

            modelBuilder.Entity("FinserveNew.Models.EmployeeDocument", b =>
                {
                    b.HasOne("FinserveNew.Models.Employee", "Employee")
                        .WithMany("EmployeeDocuments")
                        .HasForeignKey("EmployeeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Employee");
                });

            modelBuilder.Entity("FinserveNew.Models.Invoice", b =>
                {
                    b.HasOne("FinserveNew.Models.Employee", "Employee")
                        .WithMany()
                        .HasForeignKey("EmployeeID")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Employee");
                });

            modelBuilder.Entity("FinserveNew.Models.LeaveModel", b =>
                {
                    b.HasOne("FinserveNew.Models.Employee", "Employee")
                        .WithMany()
                        .HasForeignKey("EmployeeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.LeaveTypeModel", "LeaveType")
                        .WithMany("Leaves")
                        .HasForeignKey("LeaveTypeID")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Employee");

                    b.Navigation("LeaveType");
                });

            modelBuilder.Entity("FinserveNew.Models.Payroll", b =>
                {
                    b.HasOne("FinserveNew.Models.Employee", "Employee")
                        .WithMany("Payrolls")
                        .HasForeignKey("EmployeeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Employee");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("FinserveNew.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("FinserveNew.Models.Approval", b =>
                {
                    b.Navigation("Claims");
                });

            modelBuilder.Entity("FinserveNew.Models.BankInformation", b =>
                {
                    b.Navigation("Employees");
                });

            modelBuilder.Entity("FinserveNew.Models.ClaimType", b =>
                {
                    b.Navigation("ClaimDetails");
                });

            modelBuilder.Entity("FinserveNew.Models.EmergencyContact", b =>
                {
                    b.Navigation("Employees");
                });

            modelBuilder.Entity("FinserveNew.Models.Employee", b =>
                {
                    b.Navigation("Approvals");

                    b.Navigation("Claims");

                    b.Navigation("EmployeeDocuments");

                    b.Navigation("Payrolls");
                });

            modelBuilder.Entity("FinserveNew.Models.LeaveTypeModel", b =>
                {
                    b.Navigation("Leaves");
                });

            modelBuilder.Entity("FinserveNew.Models.JobRole", b =>
                {
                    b.Navigation("Employees");
                });
#pragma warning restore 612, 618
        }
    }
}
