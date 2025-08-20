using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinserveNew.Migrations
{
    /// <inheritdoc />
    public partial class AddStatutoryInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EPFNumber",
                table: "Employees",
                type: "varchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IncomeTaxNumber",
                table: "Employees",
                type: "varchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EPFNumber",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IncomeTaxNumber",
                table: "Employees");
        }
    }
}
