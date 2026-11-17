using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContingencyReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReplacesContingencyDocument",
                table: "ElectronicInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SendNotBefore",
                table: "ElectronicInvoices",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacesContingencyDocument",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "SendNotBefore",
                table: "ElectronicInvoices");
        }
    }
}
