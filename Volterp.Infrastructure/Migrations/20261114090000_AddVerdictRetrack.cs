using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    public partial class AddVerdictRetrack : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextCheckAt",
                table: "ElectronicInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusCheckCount",
                table: "ElectronicInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // The documents whose verdict check is due are looked up by company
            // and state.
            migrationBuilder.CreateIndex(
                name: "IX_ElectronicInvoices_CompanyId_Status_NextCheckAt",
                table: "ElectronicInvoices",
                columns: new[] { "CompanyId", "Status", "NextCheckAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ElectronicInvoices_CompanyId_Status_NextCheckAt",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "NextCheckAt",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "StatusCheckCount",
                table: "ElectronicInvoices");
        }
    }
}
