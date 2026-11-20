using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volterp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_CompanyId",
                table: "UserRoles",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_Role",
                table: "UserRoles",
                columns: new[] { "UserId", "Role" },
                unique: true);

            // Every user keeps the role it had: the assignment is born out of
            // the column that is about to go away. Without this the user would
            // be left without a role, and a user without roles has no
            // permissions and no way to get them back.
            migrationBuilder.Sql(
                """
                INSERT INTO "UserRoles" ("Id", "UserId", "Role", "CompanyId", "CreatedAt")
                SELECT gen_random_uuid(), u."Id", u."Role", u."CompanyId", u."CreatedAt"
                FROM "Users" u;
                """);

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Users",
                type: "integer",
                maxLength: 30,
                nullable: true);

            // The column carries one role and the user may hold several: the
            // lowest of them is the one that goes back to the column.
            migrationBuilder.Sql(
                """
                UPDATE "Users" u
                SET "Role" = r."Role"
                FROM (
                    SELECT DISTINCT ON ("UserId") "UserId", "Role"
                    FROM "UserRoles"
                    ORDER BY "UserId", "Role"
                ) r
                WHERE r."UserId" = u."Id";
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "Users",
                type: "integer",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "UserRoles");
        }
    }
}
