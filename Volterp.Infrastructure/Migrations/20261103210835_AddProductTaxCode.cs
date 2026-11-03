using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTaxCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TaxCode",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "Products");
        }
    }
}
