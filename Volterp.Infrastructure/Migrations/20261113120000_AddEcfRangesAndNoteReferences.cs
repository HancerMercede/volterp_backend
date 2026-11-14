using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    public partial class AddEcfRangesAndNoteReferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssuerTaxId",
                table: "ElectronicInvoiceSequences",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "From",
                table: "ElectronicInvoiceSequences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "To",
                table: "ElectronicInvoiceSequences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "ElectronicInvoiceSequences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "Sales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedENCF",
                table: "Sales",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModificationCode",
                table: "Sales",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModificationReason",
                table: "Sales",
                type: "character varying(90)",
                maxLength: 90,
                nullable: true);

            // Existing counters become the first authorized range of their issuer
            // and document type.
            migrationBuilder.Sql(
                """
                UPDATE "ElectronicInvoiceSequences" AS sequence
                SET "IssuerTaxId" = issuer."TaxId"
                FROM "Companies" AS issuer
                WHERE issuer."Id" = sequence."CompanyId";
                """);

            migrationBuilder.Sql(
                """
                UPDATE "ElectronicInvoiceSequences"
                SET "From" = 1,
                    "To" = CASE WHEN "DocumentType" = 32 THEN 50000000 ELSE 10000000 END;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "ElectronicInvoiceSequences"
                SET "ExpiresAt" = TIMESTAMPTZ '2028-12-31 23:59:59.999999+00'
                WHERE "DocumentType" NOT IN (32, 34);
                """);

            migrationBuilder.Sql(
                """
                UPDATE "ElectronicInvoiceSequences" AS sequence
                SET "CurrentSequence" = GREATEST(
                    sequence."CurrentSequence",
                    COALESCE((
                        SELECT MAX(CAST(SUBSTRING(invoice."ENCF" FROM 4) AS integer))
                        FROM "ElectronicInvoices" AS invoice
                        WHERE invoice."CompanyId" = sequence."CompanyId"
                          AND invoice."DocumentType" = sequence."DocumentType"
                    ), 0)
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "IssuerTaxId",
                table: "ElectronicInvoiceSequences",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "From",
                table: "ElectronicInvoiceSequences",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "To",
                table: "ElectronicInvoiceSequences",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_ElectronicInvoiceSequences_CompanyId_DocumentType",
                table: "ElectronicInvoiceSequences");

            migrationBuilder.CreateIndex(
                name: "IX_ElectronicInvoiceSequences_IssuerTaxId_DocumentType_From",
                table: "ElectronicInvoiceSequences",
                columns: new[] { "IssuerTaxId", "DocumentType", "From" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Before this migration the index was unique by company and
            // document type, and the code of that state allocated the sequence
            // with ON CONFLICT (CompanyId, DocumentType), which needs that
            // uniqueness. This migration moved the uniqueness to (issuer, type,
            // range) and the model now allows several ranges per issuer and
            // type: once a second range exists, the old model cannot hold the
            // data, so the rollback refuses with an explanation instead of
            // failing later with a duplicate key.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "ElectronicInvoiceSequences"
                        GROUP BY "CompanyId", "DocumentType"
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'The rollback of AddEcfRangesAndNoteReferences needs one range per company and document type: this database has more than one range for some of them, and the model before this migration cannot hold them.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ElectronicInvoiceSequences_IssuerTaxId_DocumentType_From",
                table: "ElectronicInvoiceSequences");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ModifiedENCF",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ModificationCode",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ModificationReason",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "IssuerTaxId",
                table: "ElectronicInvoiceSequences");

            migrationBuilder.DropColumn(
                name: "From",
                table: "ElectronicInvoiceSequences");

            migrationBuilder.DropColumn(
                name: "To",
                table: "ElectronicInvoiceSequences");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "ElectronicInvoiceSequences");

            // The index that existed before this migration was unique by company
            // and document type. It comes back unique, as the code of that
            // state needs it to be.
            migrationBuilder.CreateIndex(
                name: "IX_ElectronicInvoiceSequences_CompanyId_DocumentType",
                table: "ElectronicInvoiceSequences",
                columns: new[] { "CompanyId", "DocumentType" },
                unique: true);
        }
    }
}
