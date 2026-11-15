using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivedElectronicInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceivedElectronicInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuerTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReceiverTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ENCF = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    SignedXml = table.Column<string>(type: "text", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    ApprovalDecidedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovalDecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivedElectronicInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivedElectronicInvoices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedElectronicInvoices_CompanyId",
                table: "ReceivedElectronicInvoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivedElectronicInvoices_IssuerTaxId_ENCF",
                table: "ReceivedElectronicInvoices",
                columns: new[] { "IssuerTaxId", "ENCF" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceivedElectronicInvoices");
        }
    }
}
