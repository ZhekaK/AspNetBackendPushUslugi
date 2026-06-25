using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDurakModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DurakRoomPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DurakRoomId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TurnOrder = table.Column<int>(type: "integer", nullable: false),
                    HandJson = table.Column<string>(type: "jsonb", nullable: false),
                    HasPassed = table.Column<bool>(type: "boolean", nullable: false),
                    IsOut = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DurakRoomPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DurakRoomPlayers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DurakRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: false),
                    CardCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AttackerPlayerId = table.Column<int>(type: "integer", nullable: true),
                    DefenderPlayerId = table.Column<int>(type: "integer", nullable: true),
                    WinnerPlayerId = table.Column<int>(type: "integer", nullable: true),
                    TrumpSuit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TrumpCardCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    DeckJson = table.Column<string>(type: "jsonb", nullable: false),
                    TableJson = table.Column<string>(type: "jsonb", nullable: false),
                    DiscardJson = table.Column<string>(type: "jsonb", nullable: false),
                    FirstBout = table.Column<bool>(type: "boolean", nullable: false),
                    LastMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DurakRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DurakRooms_DurakRoomPlayers_AttackerPlayerId",
                        column: x => x.AttackerPlayerId,
                        principalTable: "DurakRoomPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DurakRooms_DurakRoomPlayers_DefenderPlayerId",
                        column: x => x.DefenderPlayerId,
                        principalTable: "DurakRoomPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DurakRooms_DurakRoomPlayers_WinnerPlayerId",
                        column: x => x.WinnerPlayerId,
                        principalTable: "DurakRoomPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DurakRooms_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DurakRoomPlayers_DurakRoomId_TurnOrder",
                table: "DurakRoomPlayers",
                columns: new[] { "DurakRoomId", "TurnOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DurakRoomPlayers_DurakRoomId_UserId",
                table: "DurakRoomPlayers",
                columns: new[] { "DurakRoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DurakRoomPlayers_UserId",
                table: "DurakRoomPlayers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DurakRooms_AttackerPlayerId",
                table: "DurakRooms",
                column: "AttackerPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DurakRooms_CreatedByUserId",
                table: "DurakRooms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DurakRooms_DefenderPlayerId",
                table: "DurakRooms",
                column: "DefenderPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DurakRooms_Status",
                table: "DurakRooms",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DurakRooms_UpdatedAt",
                table: "DurakRooms",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DurakRooms_WinnerPlayerId",
                table: "DurakRooms",
                column: "WinnerPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_DurakRoomPlayers_DurakRooms_DurakRoomId",
                table: "DurakRoomPlayers",
                column: "DurakRoomId",
                principalTable: "DurakRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DurakRoomPlayers_DurakRooms_DurakRoomId",
                table: "DurakRoomPlayers");

            migrationBuilder.DropTable(
                name: "DurakRooms");

            migrationBuilder.DropTable(
                name: "DurakRoomPlayers");
        }
    }
}
