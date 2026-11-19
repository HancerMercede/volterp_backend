using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIssuerUniqueRnc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyIssuers_CompanyId_TaxId",
                table: "CompanyIssuers");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyIssuers_TaxId",
                table: "CompanyIssuers",
                column: "TaxId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyIssuers_TaxId",
                table: "CompanyIssuers");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyIssuers_CompanyId_TaxId",
                table: "CompanyIssuers",
                columns: new[] { "CompanyId", "TaxId" },
                unique: true);
        }
    }
}
