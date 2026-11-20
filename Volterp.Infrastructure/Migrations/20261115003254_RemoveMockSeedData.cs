using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMockSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The mock of the model leaves: the company "HM Software Solutions",
            // whose RNC was fake, and its administrator, whose password was
            // published in the repository. Nothing else is touched: the
            // tenants of the demo data and everything that belongs to them
            // stay where they are.
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000001"));

            // Everything the mock seeded goes with it. The rows the model
            // seeded for the fake tenant (its clients, its products, its
            // sales, its purchases, its employees, its suppliers, its
            // categories, its accounting transactions, its invoice ranges and
            // its certificates) still point at the company with a restrictive
            // foreign key, so the company cannot be deleted while they are
            // there. Children first, then parents.
            migrationBuilder.Sql(
                """
                DELETE FROM "SaleItems" WHERE "SaleId" IN (SELECT "Id" FROM "Sales" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001');
                DELETE FROM "PurchaseItems" WHERE "PurchaseId" IN (SELECT "Id" FROM "Purchases" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001');
                DELETE FROM "ElectronicInvoices" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "AccountingTransactions" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "Sales" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "Purchases" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "Products" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "Clients" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "Suppliers" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "Employees" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "Categories" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "ElectronicInvoiceSequences" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                DELETE FROM "CompanyCertificates" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                """);

            // The backfill of AddEntityCodes opened a sequence of codes for
            // the mock company, because the mock administrator was its only
            // user. That row is the mock's own, and the foreign key of the
            // company is restrictive: the company cannot be deleted while the
            // sequence is there.
            migrationBuilder.Sql(
                """
                DELETE FROM "CodeSequences" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                """);

            // The model of this project had accounts and a journal before:
            // those tables are not in the model anymore, but the databases
            // created by that version still have them, and their rows point
            // at the mock company with a restrictive foreign key. The
            // company cannot be deleted while they are there, so the mock's
            // rows go with it. The tables themselves are left alone: a
            // database that never had them does not miss them.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('"Accounts"') IS NOT NULL THEN
                        DELETE FROM "Accounts" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                    END IF;
                    IF to_regclass('"JournalLines"') IS NOT NULL THEN
                        DELETE FROM "JournalLines" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                    END IF;
                    IF to_regclass('"JournalEntries"') IS NOT NULL THEN
                        DELETE FROM "JournalEntries" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                    END IF;
                END $$;
                """);

            migrationBuilder.DeleteData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000001"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The mock tenant comes back with its administrator and its code
            // sequence. The rows it seeded (its clients, its products, its
            // sales...) do NOT come back: they were the data of a company
            // whose RNC never existed, and the model does not seed them
            // anymore. This asymmetry is deliberate and is the only way to
            // keep the rollback honest.
            migrationBuilder.InsertData(
                table: "Companies",
                columns: new[] { "Id", "Address", "CreatedAt", "CreatedBy", "Email", "IsActive", "LegalName", "LogoUrl", "Name", "Phone", "TaxId", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("00000001-0000-0000-0000-000000000001"), "", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "", true, "", null, "HM Software Solutions", "", "HM123456789", null, null });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Code", "CompanyId", "CreatedAt", "CreatedBy", "Email", "FullName", "IsActive", "LastLoginAt", "PasswordHash", "Role", "UpdatedAt", "UpdatedBy", "Username" },
                values: new object[] { new Guid("00000002-0000-0000-0000-000000000001"), "Usu-001", new Guid("00000001-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "admin@hm.com", "Administrador", true, null, "$2a$12$XLgan9fXGIGfO.UcBuUweegkVWrYJqDgOZY48h4/udZJyNASNol3O", 1, null, null, "admin" });

            // The sequence of the codes of the mock company comes back with it:
            // the backfill of AddEntityCodes had left it at one, the number of
            // users the mock had. Without it, the next user of that company
            // would take the code of the mock administrator.
            migrationBuilder.Sql(
                """
                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                VALUES (gen_random_uuid(), '00000001-0000-0000-0000-000000000001', 9, 1, now());
                """);
        }
    }
}
