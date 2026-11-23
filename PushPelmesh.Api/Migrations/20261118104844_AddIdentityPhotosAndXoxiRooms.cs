using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityPhotosAndXoxiRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdentityPhotos",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Jpeg = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityPhotos", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_IdentityPhotos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XoxiRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    OpponentUserId = table.Column<int>(type: "integer", nullable: true),
                    Cells = table.Column<int[]>(type: "integer[]", nullable: false),
                    Moves = table.Column<int>(type: "integer", nullable: false),
                    LastCell = table.Column<int>(type: "integer", nullable: true),
                    Winner = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XoxiRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XoxiRooms_Users_OpponentUserId",
                        column: x => x.OpponentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_XoxiRooms_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XoxiRooms_OpponentUserId",
                table: "XoxiRooms",
                column: "OpponentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_XoxiRooms_OwnerUserId",
                table: "XoxiRooms",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_XoxiRooms_UpdatedAt",
                table: "XoxiRooms",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentityPhotos");

            migrationBuilder.DropTable(
                name: "XoxiRooms");
        }
    }
}
