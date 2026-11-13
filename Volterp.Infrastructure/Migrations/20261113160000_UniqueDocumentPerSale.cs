using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    public partial class UniqueDocumentPerSale : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One document per sale: two emissions of the same sale would burn
            // two fiscal numbers and leave the client with two documents.
            migrationBuilder.DropIndex(
                name: "IX_ElectronicInvoices_SaleId",
                table: "ElectronicInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicInvoices_SaleId",
                table: "ElectronicInvoices",
                column: "SaleId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ElectronicInvoices_SaleId",
                table: "ElectronicInvoices");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicInvoices_SaleId",
                table: "ElectronicInvoices",
                column: "SaleId");
        }
    }
}
