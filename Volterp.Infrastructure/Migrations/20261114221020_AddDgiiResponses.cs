using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDgiiResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "ElectronicInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawResponse",
                table: "ElectronicInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "ElectronicInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SequenceConsumed",
                table: "ElectronicInvoices",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastError",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "RawResponse",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "SequenceConsumed",
                table: "ElectronicInvoices");
        }
    }
}
