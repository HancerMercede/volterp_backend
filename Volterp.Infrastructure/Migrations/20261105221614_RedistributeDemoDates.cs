using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedistributeDemoDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The demo dataset was seeded with fixed dates between 2026-08-09 and
            // 2026-10-28. Once the dashboard reads a real time window, "today",
            // "this week" and "this month" would come back empty and the dashboard
            // would look broken in front of a client.
            //
            // The seeded rows are moved relative to the day this migration runs, so
            // the demo always covers every window the dashboard offers. The local
            // time of day is preserved and the business date follows the new date.
            //
            // Only the rows seeded by AddDemoSeedData are touched: they are the ones
            // inside the original seed window and the ones belonging to the five demo
            // companies. Any row a real user created is left alone.
            migrationBuilder.Sql(
                """
                WITH demo AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn,
                        (CURRENT_TIMESTAMP AT TIME ZONE 'America/Santo_Domingo')::date AS today
                    FROM "Sales"
                    WHERE "CompanyId" IN (
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95')
                      AND "CreatedAt" >= TIMESTAMPTZ '2026-08-01 00:00:00+00'
                      AND "CreatedAt" <  TIMESTAMPTZ '2026-11-01 00:00:00+00'
                )
                UPDATE "Sales" s
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
                        + (s."CreatedAt" AT TIME ZONE 'America/Santo_Domingo')::time
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
                WHERE s."Id" = demo."Id";
                """);

            migrationBuilder.Sql(
                """
                WITH demo AS (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (PARTITION BY "CompanyId" ORDER BY "CreatedAt", "Id") AS rn,
                        (CURRENT_TIMESTAMP AT TIME ZONE 'America/Santo_Domingo')::date AS today
                    FROM "Purchases"
                    WHERE "CompanyId" IN (
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e01',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5e66',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5ecb',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f30',
                        '0192b9c7-b6a3-7def-8c5e-1f2a3b4c5f95')
                      AND "CreatedAt" >= TIMESTAMPTZ '2026-08-01 00:00:00+00'
                      AND "CreatedAt" <  TIMESTAMPTZ '2026-11-01 00:00:00+00'
                )
                UPDATE "Purchases" p
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
                        + (p."CreatedAt" AT TIME ZONE 'America/Santo_Domingo')::time
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
                WHERE p."Id" = demo."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original dates are not restored: they are not recoverable from the
            // redistributed rows and the demo dataset carries no contractual value
            // on its own.
        }
    }
}
