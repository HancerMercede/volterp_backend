using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addingSuperAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert string roles to integers using CASE
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" 
                ALTER COLUMN ""Role"" TYPE integer 
                USING CASE 
                    WHEN ""Role"" = 'superadmin' THEN 1
                    WHEN ""Role"" = 'admin' THEN 2
                    WHEN ""Role"" = 'ventas' THEN 3
                    WHEN ""Role"" = 'inventario' THEN 4
                    WHEN ""Role"" = 'contabilidad' THEN 5
                    WHEN ""Role"" = 'rrhh' THEN 6
                    ELSE 3
                END
            ");

            // Set admin user (id=1) to SuperAdmin (value 1)
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "Role",
                value: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert integer roles back to strings
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users"" 
                ALTER COLUMN ""Role"" TYPE varchar(30)
                USING CASE 
                    WHEN ""Role"" = 1 THEN 'superadmin'
                    WHEN ""Role"" = 2 THEN 'admin'
                    WHEN ""Role"" = 3 THEN 'ventas'
                    WHEN ""Role"" = 4 THEN 'inventario'
                    WHEN ""Role"" = 5 THEN 'contabilidad'
                    WHEN ""Role"" = 6 THEN 'rrhh'
                    ELSE 'ventas'
                END
            ");
        }
    }
}
