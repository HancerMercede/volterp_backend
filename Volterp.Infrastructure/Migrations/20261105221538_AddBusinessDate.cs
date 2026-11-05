using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The column is added nullable, backfilled from the UTC instant through the
            // business timezone, and only then made mandatory. A non-nullable column
            // with a default would silently stamp every historical row with that
            // default instead of its real business date.
            migrationBuilder.AddColumn<DateOnly>(
                name: "BusinessDate",
                table: "Sales",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BusinessDate",
                table: "Purchases",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Sales"
                SET "BusinessDate" = (("CreatedAt" AT TIME ZONE 'America/Santo_Domingo')::date);

                UPDATE "Purchases"
                SET "BusinessDate" = (("CreatedAt" AT TIME ZONE 'America/Santo_Domingo')::date);
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BusinessDate",
                table: "Sales",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BusinessDate",
                table: "Purchases",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CompanyId_BusinessDate",
                table: "Sales",
                columns: new[] { "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_CompanyId_BusinessDate",
                table: "Purchases",
                columns: new[] { "CompanyId", "BusinessDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_CompanyId_BusinessDate",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_CompanyId_BusinessDate",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                table: "Purchases");
        }
    }
}
