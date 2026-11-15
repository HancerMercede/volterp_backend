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
            // published in the repository. Both rows are deleted by their
            // identifier and nothing else is touched: the tenants of the demo
            // data and everything that belongs to them stay where they are.
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000001"));

            // The backfill of AddEntityCodes opened a sequence of codes for
            // the mock company, because the mock administrator was its only
            // user. That row is the mock's own, and the foreign key of the
            // company is restrictive: the company cannot be deleted while the
            // sequence is there.
            migrationBuilder.Sql(
                """
                DELETE FROM "CodeSequences" WHERE "CompanyId" = '00000001-0000-0000-0000-000000000001';
                """);

            migrationBuilder.DeleteData(
                table: "Companies",
                keyColumn: "Id",
                keyValue: new Guid("00000001-0000-0000-0000-000000000001"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
