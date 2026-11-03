using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientTaxId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "Clients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "Clients");
        }
    }
}
