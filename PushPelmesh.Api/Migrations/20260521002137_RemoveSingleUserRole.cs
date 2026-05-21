using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSingleUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO ""UserRoles"" (
                    ""Number"",
                    ""Post"",
                    ""PostName"",
                    ""GivePlace"",
                    ""StartDate"",
                    ""UserSeries"",
                    ""UserNumbers"")
                SELECT
                    ('9' || lpad(u.""Id""::text, 6, '0')),
                    u.""Role"",
                    CASE u.""Role""
                        WHEN 1 THEN 'Minister'
                        WHEN 2 THEN 'Governor'
                        WHEN 3 THEN 'President'
                        ELSE 'None'
                    END,
                    u.""GivePlace"",
                    CURRENT_DATE,
                    u.""UserSeries"",
                    u.""UserNumber""
                FROM ""Users"" u
                WHERE u.""Role"" IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM ""UserRoles"" ur
                    WHERE ur.""UserSeries"" = u.""UserSeries""
                      AND ur.""UserNumbers"" = u.""UserNumber""
                      AND ur.""Post"" = u.""Role""
                  );
            ");

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
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""Users"" u
                SET ""Role"" = latest.""Post""
                FROM (
                    SELECT DISTINCT ON (""UserSeries"", ""UserNumbers"")
                        ""UserSeries"",
                        ""UserNumbers"",
                        ""Post""
                    FROM ""UserRoles""
                    ORDER BY ""UserSeries"", ""UserNumbers"", ""StartDate"" DESC, ""Id"" DESC
                ) latest
                WHERE latest.""UserSeries"" = u.""UserSeries""
                  AND latest.""UserNumbers"" = u.""UserNumber"";
            ");
        }
    }
}
