using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRangeAnnulment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnnulledAt",
                table: "ElectronicInvoiceSequences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnulmentResponse",
                table: "ElectronicInvoiceSequences",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnulledAt",
                table: "ElectronicInvoiceSequences");

            migrationBuilder.DropColumn(
                name: "AnnulmentResponse",
                table: "ElectronicInvoiceSequences");
        }
    }
}
