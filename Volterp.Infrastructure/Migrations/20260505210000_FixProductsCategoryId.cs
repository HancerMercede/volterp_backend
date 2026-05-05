using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations;

public partial class FixProductsCategoryId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""CategoryId"" integer;
            ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""UpdatedAt"" timestamp with time zone;
            ALTER TABLE ""Products"" ALTER COLUMN ""Code"" TYPE character varying(50);
            ALTER TABLE ""Products"" ALTER COLUMN ""Name"" TYPE character varying(100);
            ALTER TABLE ""Products"" ALTER COLUMN ""Description"" TYPE character varying(500);
            ALTER TABLE ""Products"" ALTER COLUMN ""IsActive"" SET NOT NULL;
            ALTER TABLE ""Products"" ALTER COLUMN ""IsActive"" SET DEFAULT true;
            ALTER TABLE ""Products"" ALTER COLUMN ""Stock"" SET DEFAULT 0;
        ");

        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Products_Categories_CategoryId') THEN
                    ALTER TABLE ""Products"" ADD CONSTRAINT ""FK_Products_Categories_CategoryId"" FOREIGN KEY (""CategoryId"") REFERENCES ""Categories"" (""Id"") ON DELETE SET NULL;
                END IF;
            END $$;
        ");

        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints WHERE constraint_name = 'FK_Products_Companies_CompanyId') THEN
                    ALTER TABLE ""Products"" ADD CONSTRAINT ""FK_Products_Companies_CompanyId"" FOREIGN KEY (""CompanyId"") REFERENCES ""Companies"" (""Id"") ON DELETE RESTRICT;
                END IF;
            END $$;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_Products_CategoryId"" ON ""Products"" (""CategoryId"") WHERE ""CategoryId"" IS NOT NULL;
        ");

        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Products_Code_CompanyId') THEN
                    CREATE UNIQUE INDEX ""IX_Products_Code_CompanyId"" ON ""Products"" (""Code"", ""CompanyId"");
                END IF;
            END $$;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE ""Products"" DROP COLUMN IF EXISTS ""CategoryId"";");
        migrationBuilder.Sql(@"ALTER TABLE ""Products"" DROP COLUMN IF EXISTS ""UpdatedAt"";");
    }
}