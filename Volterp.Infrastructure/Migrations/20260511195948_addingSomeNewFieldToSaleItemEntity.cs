using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addingSomeNewFieldToSaleItemEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductCategory",
                table: "SaleItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "SaleItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductImageUrl",
                table: "SaleItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductCategory",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ProductImageUrl",
                table: "SaleItems");
        }
    }
}
