using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessDateToClientsAndProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same shape as AddBusinessDate: add nullable, backfill from the UTC
            // instant through the business timezone, then make it mandatory. A
            // non-nullable column with a default would stamp every historical row
            // with that default instead of its real business date.
            migrationBuilder.AddColumn<DateOnly>(
                name: "BusinessDate",
                table: "Clients",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BusinessDate",
                table: "Products",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Clients"
                SET "BusinessDate" = (("CreatedAt" AT TIME ZONE 'America/Santo_Domingo')::date);

                UPDATE "Products"
                SET "BusinessDate" = (("CreatedAt" AT TIME ZONE 'America/Santo_Domingo')::date);
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BusinessDate",
                table: "Clients",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "BusinessDate",
                table: "Products",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CompanyId_BusinessDate",
                table: "Clients",
                columns: new[] { "CompanyId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyId_BusinessDate",
                table: "Products",
                columns: new[] { "CompanyId", "BusinessDate" });

            // The demo clients and products were seeded between June and October, so
            // "clientes nuevos" and "productos nuevos" would come back empty for
            // every window the dashboard offers. They are moved relative to the day
            // this migration runs, the same way the sales and the purchases were.
            //
            // Only the rows seeded by AddDemoSeedData are touched: the ones inside
            // the original seed window and the ones belonging to the five demo
            // companies.
            migrationBuilder.Sql(
                """
                WITH demo AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn,
                        (CURRENT_TIMESTAMP AT TIME ZONE 'America/Santo_Domingo')::date AS today
                    FROM "Clients"
                    WHERE "CompanyId" IN (
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95')
                      AND "CreatedAt" >= TIMESTAMPTZ '2026-07-01 00:00:00+00'
                      AND "CreatedAt" <  TIMESTAMPTZ '2026-11-01 00:00:00+00'
                )
                UPDATE "Clients" c
                SET "CreatedAt" = (
                        (demo.today - CASE demo.rn
                            WHEN 1 THEN 0
                            WHEN 2 THEN 1
                            WHEN 3 THEN 3
                            WHEN 4 THEN 6
                            WHEN 5 THEN 12
                            WHEN 6 THEN 30
                            WHEN 7 THEN 90
                            ELSE 210
                        END)
                        + (c."CreatedAt" AT TIME ZONE 'America/Santo_Domingo')::time
                      ) AT TIME ZONE 'America/Santo_Domingo',
                    "BusinessDate" = demo.today - CASE demo.rn
                        WHEN 1 THEN 0
                        WHEN 2 THEN 1
                        WHEN 3 THEN 3
                        WHEN 4 THEN 6
                        WHEN 5 THEN 12
                        WHEN 6 THEN 30
                        WHEN 7 THEN 90
                        ELSE 210
                    END
                FROM demo
                WHERE c."Id" = demo."Id";
                """);

            migrationBuilder.Sql(
                """
                WITH demo AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn,
                        (CURRENT_TIMESTAMP AT TIME ZONE 'America/Santo_Domingo')::date AS today
                    FROM "Products"
                    WHERE "CompanyId" IN (
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95')
                      AND "CreatedAt" >= TIMESTAMPTZ '2026-06-01 00:00:00+00'
                      AND "CreatedAt" <  TIMESTAMPTZ '2026-11-01 00:00:00+00'
                )
                UPDATE "Products" p
                SET "CreatedAt" = (
                        (demo.today - CASE demo.rn
                            WHEN 1 THEN 0
                            WHEN 2 THEN 1
                            WHEN 3 THEN 3
                            WHEN 4 THEN 6
                            WHEN 5 THEN 12
                            WHEN 6 THEN 30
                            WHEN 7 THEN 60
                            WHEN 8 THEN 90
                            WHEN 9 THEN 150
                            ELSE 210
                        END)
                        + (p."CreatedAt" AT TIME ZONE 'America/Santo_Domingo')::time
                      ) AT TIME ZONE 'America/Santo_Domingo',
                    "BusinessDate" = demo.today - CASE demo.rn
                        WHEN 1 THEN 0
                        WHEN 2 THEN 1
                        WHEN 3 THEN 3
                        WHEN 4 THEN 6
                        WHEN 5 THEN 12
                        WHEN 6 THEN 30
                        WHEN 7 THEN 60
                        WHEN 8 THEN 90
                        WHEN 9 THEN 150
                        ELSE 210
                    END
                FROM demo
                WHERE p."Id" = demo."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_CompanyId_BusinessDate",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Products_CompanyId_BusinessDate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "BusinessDate",
                table: "Products");
        }
    }
}
