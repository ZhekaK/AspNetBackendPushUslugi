using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddZongModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZongRoomPlayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ZongRoomId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    InitialRoll = table.Column<int>(type: "integer", nullable: false),
                    TurnOrder = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZongRoomPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZongRoomPlayers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZongRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: false),
                    TargetScore = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CurrentTurnPlayerId = table.Column<int>(type: "integer", nullable: true),
                    WinnerPlayerId = table.Column<int>(type: "integer", nullable: true),
                    CurrentTurnScore = table.Column<int>(type: "integer", nullable: false),
                    RemainingDice = table.Column<int>(type: "integer", nullable: false),
                    LastDice = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastRollScore = table.Column<int>(type: "integer", nullable: false),
                    LastRollMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZongRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZongRooms_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZongRooms_ZongRoomPlayers_CurrentTurnPlayerId",
                        column: x => x.CurrentTurnPlayerId,
                        principalTable: "ZongRoomPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ZongRooms_ZongRoomPlayers_WinnerPlayerId",
                        column: x => x.WinnerPlayerId,
                        principalTable: "ZongRoomPlayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZongRoomPlayers_UserId",
                table: "ZongRoomPlayers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ZongRoomPlayers_ZongRoomId_TurnOrder",
                table: "ZongRoomPlayers",
                columns: new[] { "ZongRoomId", "TurnOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ZongRoomPlayers_ZongRoomId_UserId",
                table: "ZongRoomPlayers",
                columns: new[] { "ZongRoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZongRooms_CreatedByUserId",
                table: "ZongRooms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ZongRooms_CurrentTurnPlayerId",
                table: "ZongRooms",
                column: "CurrentTurnPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ZongRooms_Status",
                table: "ZongRooms",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ZongRooms_UpdatedAt",
                table: "ZongRooms",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ZongRooms_WinnerPlayerId",
                table: "ZongRooms",
                column: "WinnerPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ZongRoomPlayers_ZongRooms_ZongRoomId",
                table: "ZongRoomPlayers",
                column: "ZongRoomId",
                principalTable: "ZongRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ZongRoomPlayers_ZongRooms_ZongRoomId",
                table: "ZongRoomPlayers");

            migrationBuilder.DropTable(
                name: "ZongRooms");

            migrationBuilder.DropTable(
                name: "ZongRoomPlayers");
        }
    }
}
