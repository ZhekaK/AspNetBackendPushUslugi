using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "Users",
                newName: "MiddleName");

            migrationBuilder.RenameColumn(
                name: "AccountName",
                table: "AccessKeys",
                newName: "AccountNameMiddle");

            migrationBuilder.AddColumn<DateTime>(
                name: "BirthDate",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountNameFirst",
                table: "AccessKeys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountNameLast",
                table: "AccessKeys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "BirthDate",
                table: "AccessKeys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccountNameFirst",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "AccountNameLast",
                table: "AccessKeys");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "AccessKeys");

            migrationBuilder.RenameColumn(
                name: "MiddleName",
                table: "Users",
                newName: "DisplayName");

            migrationBuilder.RenameColumn(
                name: "AccountNameMiddle",
                table: "AccessKeys",
                newName: "AccountName");
        }
    }
}
