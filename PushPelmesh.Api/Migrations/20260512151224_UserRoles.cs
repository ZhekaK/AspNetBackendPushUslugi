using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class UserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "GiveDate",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GivePlace",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sex",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "GiveDate",
                table: "AccessKeys",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GivePlace",
                table: "AccessKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sex",
                table: "AccessKeys",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Post = table.Column<int>(type: "integer", nullable: false),
                    PostName = table.Column<string>(type: "text", nullable: true),
                    GivePlace = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UserSeries = table.Column<string>(type: "text", nullable: true),
                    UserNumbers = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropColumn(
                name: "GiveDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GivePlace",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GiveDate",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "GivePlace",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "AccessKeys");
        }
    }
}
