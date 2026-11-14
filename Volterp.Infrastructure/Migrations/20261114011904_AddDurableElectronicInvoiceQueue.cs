using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableElectronicInvoiceQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "ElectronicInvoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LeaseOwner",
                table: "ElectronicInvoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseUntil",
                table: "ElectronicInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "ElectronicInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalXml",
                table: "ElectronicInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresReview",
                table: "ElectronicInvoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicInvoiceSequences_CompanyId",
                table: "ElectronicInvoiceSequences",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicInvoices_CompanyId_Status_NextAttemptAt_LeaseUntil",
                table: "ElectronicInvoices",
                columns: new[] { "CompanyId", "Status", "NextAttemptAt", "LeaseUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ElectronicInvoiceSequences_CompanyId",
                table: "ElectronicInvoiceSequences");

            migrationBuilder.DropIndex(
                name: "IX_ElectronicInvoices_CompanyId_Status_NextAttemptAt_LeaseUntil",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "LeaseUntil",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "OriginalXml",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "RequiresReview",
                table: "ElectronicInvoices");
        }
    }
}
