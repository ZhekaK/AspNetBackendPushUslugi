using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVoteMultipleChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoteBallots_VotePollId_UserId",
                table: "VoteBallots");

            migrationBuilder.AddColumn<bool>(
                name: "IsMultipleChoice",
                table: "VotePolls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_VoteBallots_VotePollId_UserId_VoteOptionId",
                table: "VoteBallots",
                columns: new[] { "VotePollId", "UserId", "VoteOptionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoteBallots_VotePollId_UserId_VoteOptionId",
                table: "VoteBallots");

            migrationBuilder.DropColumn(
                name: "IsMultipleChoice",
                table: "VotePolls");

            migrationBuilder.CreateIndex(
                name: "IX_VoteBallots_VotePollId_UserId",
                table: "VoteBallots",
                columns: new[] { "VotePollId", "UserId" },
                unique: true);
        }
    }
}
