using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinserveNew.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedClaimColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "IsOCRProcessed",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OCRAmountVerified",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OCRConfidence",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OCRDetectedAmount",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OCRDetectedCurrency",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OCRPriceAnalysis",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OCRProcessedDate",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OCRRawText",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OriginalAmount",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OriginalCurrency",
                table: "Claims");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Claims",
                type: "decimal(10,6)",
                precision: 10,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOCRProcessed",
                table: "Claims",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OCRAmountVerified",
                table: "Claims",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OCRConfidence",
                table: "Claims",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OCRDetectedAmount",
                table: "Claims",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OCRDetectedCurrency",
                table: "Claims",
                type: "varchar(3)",
                maxLength: 3,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OCRPriceAnalysis",
                table: "Claims",
                type: "TEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "OCRProcessedDate",
                table: "Claims",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OCRRawText",
                table: "Claims",
                type: "LONGTEXT",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                table: "Claims",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalCurrency",
                table: "Claims",
                type: "varchar(3)",
                maxLength: 3,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
