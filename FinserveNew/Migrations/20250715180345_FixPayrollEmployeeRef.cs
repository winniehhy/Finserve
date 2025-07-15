using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinserveNew.Migrations
{
    /// <inheritdoc />
    public partial class FixPayrollEmployeeRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payrolls_Employees_EmployeeID1",
                table: "Payrolls");

            migrationBuilder.DropIndex(
                name: "IX_Payrolls_EmployeeID1",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "EmployeeID1",
                table: "Payrolls");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeID1",
                table: "Payrolls",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Payrolls_EmployeeID1",
                table: "Payrolls",
                column: "EmployeeID1");

            migrationBuilder.AddForeignKey(
                name: "FK_Payrolls_Employees_EmployeeID1",
                table: "Payrolls",
                column: "EmployeeID1",
                principalTable: "Employees",
                principalColumn: "EmployeeID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
