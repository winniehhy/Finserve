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
    [Migration("20250617200335_AddEmployeeDocumentTable")]
    partial class AddEmployeeDocumentTable
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.17")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

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

                    b.Property<string>("EmployeeID1")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<string>("Purpose")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("varchar(500)");

                    b.HasKey("ApprovalID");

                    b.HasIndex("EmployeeID");

                    b.HasIndex("EmployeeID1");

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

                    b.Property<int?>("ApprovalID1")
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
                        .HasColumnType("decimal(18,2)");

                    b.HasKey("Id");

                    b.HasIndex("ApprovalID");

                    b.HasIndex("ApprovalID1");

                    b.HasIndex("EmployeeID");

                    b.ToTable("Claims");
                });

            modelBuilder.Entity("FinserveNew.Models.ClaimDetails", b =>
                {
                    b.Property<int>("ClaimID")
                        .HasColumnType("int");

                    b.Property<int>("ClaimTypeID")
                        .HasColumnType("int");

                    b.Property<int?>("ClaimTypeID1")
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

                    b.HasIndex("ClaimTypeID1");

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

            modelBuilder.Entity("FinserveNew.Models.Role", b =>
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

            modelBuilder.Entity("FinserveNew.Models.Salary", b =>
                {
                    b.Property<int>("SalaryID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("SalaryID"));

                    b.Property<decimal>("Allowance")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("ApprovalID")
                        .HasColumnType("int");

                    b.Property<decimal>("BasicSalary")
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("Deduction")
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("EPFNumber")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<string>("EmployeeID")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<string>("EmployeeID1")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<string>("IncomeTaxNumber")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("varchar(50)");

                    b.Property<int>("Month")
                        .HasColumnType("int");

                    b.Property<decimal>("NetSalary")
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTime?>("PaymentDate")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("PaymentStatus")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("varchar(20)");

                    b.Property<string>("ProjectName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<int>("Year")
                        .HasColumnType("int");

                    b.HasKey("SalaryID");

                    b.HasIndex("ApprovalID");

                    b.HasIndex("EmployeeID");

                    b.HasIndex("EmployeeID1");

                    b.ToTable("Salaries");
                });

            modelBuilder.Entity("FinserveNew.Models.Approval", b =>
                {
                    b.HasOne("FinserveNew.Models.Employee", null)
                        .WithMany("Approvals")
                        .HasForeignKey("EmployeeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.Employee", "Employee")
                        .WithMany()
                        .HasForeignKey("EmployeeID1")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Employee");
                });

            modelBuilder.Entity("FinserveNew.Models.Claim", b =>
                {
                    b.HasOne("FinserveNew.Models.Approval", "Approval")
                        .WithMany()
                        .HasForeignKey("ApprovalID")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("FinserveNew.Models.Approval", null)
                        .WithMany("Claims")
                        .HasForeignKey("ApprovalID1");

                    b.HasOne("FinserveNew.Models.Employee", "Employee")
                        .WithMany("Claims")
                        .HasForeignKey("EmployeeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Approval");

                    b.Navigation("Employee");
                });

            modelBuilder.Entity("FinserveNew.Models.ClaimDetails", b =>
                {
                    b.HasOne("FinserveNew.Models.Claim", "Claim")
                        .WithMany("ClaimDetails")
                        .HasForeignKey("ClaimID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.ClaimType", "ClaimType")
                        .WithMany()
                        .HasForeignKey("ClaimTypeID")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.ClaimType", null)
                        .WithMany("ClaimDetails")
                        .HasForeignKey("ClaimTypeID1");

                    b.Navigation("Claim");

                    b.Navigation("ClaimType");
                });

            modelBuilder.Entity("FinserveNew.Models.Employee", b =>
                {
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

                    b.HasOne("FinserveNew.Models.Role", "Role")
                        .WithMany("Employees")
                        .HasForeignKey("RoleID")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

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

            modelBuilder.Entity("FinserveNew.Models.Salary", b =>
                {
                    b.HasOne("FinserveNew.Models.Approval", "Approval")
                        .WithMany("Salaries")
                        .HasForeignKey("ApprovalID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.Employee", null)
                        .WithMany("Salaries")
                        .HasForeignKey("EmployeeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("FinserveNew.Models.Employee", "Employee")
                        .WithMany()
                        .HasForeignKey("EmployeeID1")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Approval");

                    b.Navigation("Employee");
                });

            modelBuilder.Entity("FinserveNew.Models.Approval", b =>
                {
                    b.Navigation("Claims");

                    b.Navigation("Salaries");
                });

            modelBuilder.Entity("FinserveNew.Models.BankInformation", b =>
                {
                    b.Navigation("Employees");
                });

            modelBuilder.Entity("FinserveNew.Models.Claim", b =>
                {
                    b.Navigation("ClaimDetails");
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

                    b.Navigation("Salaries");
                });

            modelBuilder.Entity("FinserveNew.Models.Role", b =>
                {
                    b.Navigation("Employees");
                });
#pragma warning restore 612, 618
        }
    }
}
