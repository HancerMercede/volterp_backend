using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleFiscalBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "Sales",
                type: "integer",
                nullable: false,
                defaultValue: 32);

            migrationBuilder.AddColumn<decimal>(
                name: "Itbis",
                table: "Sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReceptorTaxId",
                table: "Sales",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TaxCode",
                table: "SaleItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Sales created before the fiscal breakdown have no ITBIS: their total
            // is the sum of the lines, so it becomes their subtotal.
            migrationBuilder.Sql(@"UPDATE ""Sales"" SET ""Subtotal"" = ""Total"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Itbis",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ReceptorTaxId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "SaleItems");
        }
    }
}
