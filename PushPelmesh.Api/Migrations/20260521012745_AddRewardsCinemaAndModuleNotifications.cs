using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardsCinemaAndModuleNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CinemaMovieRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Rating = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    WatchedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CinemaMovieRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CinemaMovieRatings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RewardRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    FullName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    EventName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Place = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserModuleNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ModuleKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModuleNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserModuleNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CinemaMovieRatings_CreatedByUserId",
                table: "CinemaMovieRatings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRecords_Kind",
                table: "RewardRecords",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRecords_UserId",
                table: "RewardRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserModuleNotifications_UserId_ModuleKey_SourceKey",
                table: "UserModuleNotifications",
                columns: new[] { "UserId", "ModuleKey", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserModuleNotifications_UserId_ReadAt",
                table: "UserModuleNotifications",
                columns: new[] { "UserId", "ReadAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CinemaMovieRatings");

            migrationBuilder.DropTable(
                name: "RewardRecords");

            migrationBuilder.DropTable(
                name: "UserModuleNotifications");
        }
    }
}
