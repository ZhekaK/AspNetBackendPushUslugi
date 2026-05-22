using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVoteModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VotePolls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AudienceGroups = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotePolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VotePolls_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoteOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VotePollId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoteOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoteOptions_VotePolls_VotePollId",
                        column: x => x.VotePollId,
                        principalTable: "VotePolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VoteBallots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VotePollId = table.Column<int>(type: "integer", nullable: false),
                    VoteOptionId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoteBallots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoteBallots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoteBallots_VoteOptions_VoteOptionId",
                        column: x => x.VoteOptionId,
                        principalTable: "VoteOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VoteBallots_VotePolls_VotePollId",
                        column: x => x.VotePollId,
                        principalTable: "VotePolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoteBallots_UserId",
                table: "VoteBallots",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VoteBallots_VoteOptionId",
                table: "VoteBallots",
                column: "VoteOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_VoteBallots_VotePollId_UserId",
                table: "VoteBallots",
                columns: new[] { "VotePollId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoteOptions_VotePollId_SortOrder",
                table: "VoteOptions",
                columns: new[] { "VotePollId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_VotePolls_AudienceGroups",
                table: "VotePolls",
                column: "AudienceGroups");

            migrationBuilder.CreateIndex(
                name: "IX_VotePolls_CreatedByUserId",
                table: "VotePolls",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VotePolls_EndDate",
                table: "VotePolls",
                column: "EndDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoteBallots");

            migrationBuilder.DropTable(
                name: "VoteOptions");

            migrationBuilder.DropTable(
                name: "VotePolls");
        }
    }
}
