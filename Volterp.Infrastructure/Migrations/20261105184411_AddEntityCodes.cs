using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The model no longer declares a single-column CompanyId index on
            // Categories, Clients and Users: the unique (CompanyId, Code) index
            // created at the end of this migration covers those lookups with
            // CompanyId as its leading column. The three indexes are dropped here
            // so the database ends up exactly like the model says.
            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CompanyId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CompanyId",
                table: "Categories");

            // 1. The table that holds the sequence of every coded entity. It
            //    starts empty on purpose: the application creates the row the
            //    first time it allocates a number for a company and a type.
            migrationBuilder.CreateTable(
                name: "CodeSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    CurrentValue = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeSequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeSequences_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodeSequences_CompanyId_EntityType",
                table: "CodeSequences",
                columns: new[] { "CompanyId", "EntityType" },
                unique: true);

            // 2. The column arrives empty and nullable. It cannot be NOT NULL
            //    yet: until every row has its own code, two rows of the same
            //    company would share the empty value and the unique index at the
            //    end of this migration would explode. The column is filled first
            //    and tightened afterwards.
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Categories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Sales",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Purchases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "AccountingTransactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // 3. The backfill. Every existing row gets a code, numbered per
            //    company so each company has its own series, in a deterministic
            //    order (CreatedAt, then Id) so the same database always yields
            //    the same codes. The format is the one the application uses:
            //    prefix + '-' + the number padded to three digits.

            // Products: Prod-001
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Products"
                )
                UPDATE "Products" p
                SET "Code" = 'Prod-' || lpad(n.rn::text, 3, '0')
                FROM numbered n
                WHERE p."Id" = n."Id";
                """);

            // Categories: Cat-001
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Categories"
                )
                UPDATE "Categories" c
                SET "Code" = 'Cat-' || lpad(n.rn::text, 3, '0')
                FROM numbered n
                WHERE c."Id" = n."Id";
                """);

            // Clients: Cli-001
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Clients"
                )
                UPDATE "Clients" c
                SET "Code" = 'Cli-' || lpad(n.rn::text, 3, '0')
                FROM numbered n
                WHERE c."Id" = n."Id";
                """);

            // Suppliers: Prov-001
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Suppliers"
                )
                UPDATE "Suppliers" s
                SET "Code" = 'Prov-' || lpad(n.rn::text, 3, '0')
                FROM numbered n
                WHERE s."Id" = n."Id";
                """);

            // Sales: Vent-001
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Sales"
                )
                UPDATE "Sales" s
                SET "Code" = 'Vent-' || lpad(n.rn::text, 3, '0')
                FROM numbered n
                WHERE s."Id" = n."Id";
                """);

            // Purchases: Comp-001
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Purchases"
                )
                UPDATE "Purchases" p
                SET "Code" = 'Comp-' || lpad(n.rn::text, 3, '0')
                FROM numbered n
                WHERE p."Id" = n."Id";
                """);

            // Employees: Empl-001
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "Employees"
                )
                UPDATE "Employees" e
                SET "Code" = 'Empl-' || lpad(n.rn::text, 3, '0')
                FROM numbered n
                WHERE e."Id" = n."Id";
                """);

            // Accounting transactions: Asi-001
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "AccountingTransactions"
                )
                UPDATE "AccountingTransactions" t
                SET "Code" = 'Asi-' || lpad(n.rn::text, 3, '0')
                FROM numbered n
                WHERE t."Id" = n."Id";
                """);

            // Users: Usu-001. The seeded admin is pinned to the first slot of its
            // company so it always gets "Usu-001", the same code that
            // UserConfiguration declares for it. EF re-emits that seeded row as
            // an UpdateData further down, and an empty code there would wipe
            // the one assigned here.
            migrationBuilder.Sql(
                """
                WITH numbered AS (
                    SELECT "Id",
                           row_number() OVER (
                               PARTITION BY "CompanyId"
                               ORDER BY CASE WHEN "Id" = '00000002-0000-0000-0000-000000000001' THEN 0 ELSE 1 END,
                                        "CreatedAt",
                                        "Id"
                           ) AS rn
                    FROM "Users"
                )
                UPDATE "Users" u
                SET "Code" = 'Usu-' || lpad(n.rn::text, 3, '0')
                FROM numbered n
                WHERE u."Id" = n."Id";
                """);

            // 4. The sequence of every company and type that has rows, starting
            //    where the backfill left it, so the next allocation continues
            //    with the next free number. A company with no rows of a type
            //    gets no row: the sequence is created by the first allocation.
            //    The identifiers come from the database (gen_random_uuid) because
            //    PostgreSQL 17 has no uuidv7(); nothing orders this table by
            //    identifier, so the loss of locality does not matter.
            migrationBuilder.Sql(
                """
                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                SELECT gen_random_uuid(), "CompanyId", 1, count(*)::int, now() FROM "Products" GROUP BY "CompanyId";

                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                SELECT gen_random_uuid(), "CompanyId", 2, count(*)::int, now() FROM "Categories" GROUP BY "CompanyId";

                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                SELECT gen_random_uuid(), "CompanyId", 3, count(*)::int, now() FROM "Clients" GROUP BY "CompanyId";

                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                SELECT gen_random_uuid(), "CompanyId", 4, count(*)::int, now() FROM "Suppliers" GROUP BY "CompanyId";

                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                SELECT gen_random_uuid(), "CompanyId", 5, count(*)::int, now() FROM "Sales" GROUP BY "CompanyId";

                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                SELECT gen_random_uuid(), "CompanyId", 6, count(*)::int, now() FROM "Purchases" GROUP BY "CompanyId";

                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                SELECT gen_random_uuid(), "CompanyId", 7, count(*)::int, now() FROM "Employees" GROUP BY "CompanyId";

                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                SELECT gen_random_uuid(), "CompanyId", 8, count(*)::int, now() FROM "AccountingTransactions" GROUP BY "CompanyId";

                INSERT INTO "CodeSequences" ("Id", "CompanyId", "EntityType", "CurrentValue", "CreatedAt")
                SELECT gen_random_uuid(), "CompanyId", 9, count(*)::int, now() FROM "Users" GROUP BY "CompanyId";
                """);

            // 5. The lines of a sale and a purchase carry a snapshot of the code
            //    of the product they sold. The old seed left "CS-01" there while
            //    the product is now "Prod-001", so the lines are refreshed from
            //    the product to stop the demo data from contradicting itself.
            migrationBuilder.Sql(
                """
                UPDATE "SaleItems" si SET "ProductCode" = p."Code"
                FROM "Products" p
                WHERE si."ProductId" = p."Id";

                UPDATE "PurchaseItems" pi SET "ProductCode" = p."Code"
                FROM "Products" p
                WHERE pi."ProductId" = p."Id";
                """);

            // 6. The seeded admin gets the code its configuration declares, which
            //    is the same one the backfill assigned to it. The statement is
            //    idempotent with the backfill: both write "Usu-001".
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000002-0000-0000-0000-000000000001"),
                column: "Code",
                value: "Usu-001");

            // 7. The unique index. It can only be created now that every row of
            //    a company carries a code of its own.
            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyId_Code",
                table: "Products",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyId_Code",
                table: "Categories",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CompanyId_Code",
                table: "Clients",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CompanyId_Code",
                table: "Suppliers",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CompanyId_Code",
                table: "Sales",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_CompanyId_Code",
                table: "Purchases",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_Code",
                table: "Employees",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingTransactions_CompanyId_Code",
                table: "AccountingTransactions",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId_Code",
                table: "Users",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            // 8. No row is left without a code, so the column can be tightened.
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Categories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Clients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Sales",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Purchases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "AccountingTransactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The lines go back to the code they carried before the backfill, so
            // a rollback does not leave them pointing at a code that no longer
            // exists. The list is the snapshot of the seeded demo catalogue.
            migrationBuilder.Sql(
                """
                UPDATE "SaleItems" si SET "ProductCode" = m."code"
                FROM (VALUES
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0a', 'DC-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0b', 'DC-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0c', 'DC-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0d', 'DC-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0f', 'DC-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e10', 'DC-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e11', 'DC-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e12', 'DC-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e13', 'DC-10'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e6f', 'FV-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e70', 'FV-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e71', 'FV-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e72', 'FV-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e74', 'FV-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e75', 'FV-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e76', 'FV-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e77', 'FV-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e78', 'FV-10'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed4', 'CS-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed5', 'CS-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed6', 'CS-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed7', 'CS-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed9', 'CS-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5eda', 'CS-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5edb', 'CS-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5edc', 'CS-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5edd', 'CS-10'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f39', 'TS-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3a', 'TS-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3b', 'TS-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3c', 'TS-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3e', 'TS-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3f', 'TS-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f40', 'TS-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f41', 'TS-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f42', 'TS-10'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f9e', 'PT-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f9f', 'PT-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa0', 'PT-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa1', 'PT-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa3', 'PT-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa4', 'PT-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa5', 'PT-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa6', 'PT-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa7', 'PT-10')
                ) AS m("id", "code")
                WHERE si."ProductId" = m."id"::uuid;

                UPDATE "PurchaseItems" pi SET "ProductCode" = m."code"
                FROM (VALUES
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0a', 'DC-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0b', 'DC-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0c', 'DC-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0d', 'DC-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e0f', 'DC-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e10', 'DC-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e11', 'DC-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e12', 'DC-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e13', 'DC-10'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e6f', 'FV-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e70', 'FV-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e71', 'FV-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e72', 'FV-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e74', 'FV-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e75', 'FV-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e76', 'FV-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e77', 'FV-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e78', 'FV-10'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed4', 'CS-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed5', 'CS-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed6', 'CS-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed7', 'CS-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ed9', 'CS-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5eda', 'CS-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5edb', 'CS-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5edc', 'CS-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5edd', 'CS-10'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f39', 'TS-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3a', 'TS-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3b', 'TS-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3c', 'TS-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3e', 'TS-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f3f', 'TS-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f40', 'TS-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f41', 'TS-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f42', 'TS-10'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f9e', 'PT-01'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f9f', 'PT-02'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa0', 'PT-03'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa1', 'PT-04'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa3', 'PT-06'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa4', 'PT-07'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa5', 'PT-08'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa6', 'PT-09'),
                    ('0192b9c7-b6a3-7def-8c5e-1f2a3b4c5fa7', 'PT-10')
                ) AS m("id", "code")
                WHERE pi."ProductId" = m."id"::uuid;
                """);

            migrationBuilder.DropTable(
                name: "CodeSequences");

            migrationBuilder.DropIndex(
                name: "IX_Products_CompanyId_Code",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_CompanyId_Code",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CompanyId_Code",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CompanyId_Code",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CompanyId_Code",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_CompanyId_Code",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CompanyId_Code",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_AccountingTransactions_CompanyId_Code",
                table: "AccountingTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId_Code",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "AccountingTransactions");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId",
                table: "Users",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CompanyId",
                table: "Clients",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompanyId",
                table: "Categories",
                column: "CompanyId");
        }
    }
}
