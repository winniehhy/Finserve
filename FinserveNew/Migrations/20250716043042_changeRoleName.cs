using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinserveNew.Migrations
{
    /// <inheritdoc />
    public partial class changeRoleName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ApprovedBy",
                table: "Leaves",
                type: "varchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalDate",
                table: "Leaves",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalRemarks",
                table: "Leaves",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Leaves",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Leaves",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "LeaveDays",
                table: "Leaves",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionDate",
                table: "Leaves",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalDate",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "ApprovalRemarks",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "LeaveDays",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "SubmissionDate",
                table: "Leaves");

            migrationBuilder.AlterColumn<string>(
                name: "ApprovedBy",
                table: "Leaves",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(450)",
                oldMaxLength: 450,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
