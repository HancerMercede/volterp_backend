using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicInvoiceSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElectronicInvoiceSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    CurrentSequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectronicInvoiceSequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectronicInvoiceSequences_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicInvoiceSequences_CompanyId_DocumentType",
                table: "ElectronicInvoiceSequences",
                columns: new[] { "CompanyId", "DocumentType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElectronicInvoiceSequences");
        }
    }
}
