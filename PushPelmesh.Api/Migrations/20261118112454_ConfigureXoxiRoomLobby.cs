using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureXoxiRoomLobby : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OwnerPlaysNoughts",
                table: "XoxiRooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "XoxiRooms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Started",
                table: "XoxiRooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Rooms that already had two players could make moves under the old rules.
            migrationBuilder.Sql("""UPDATE "XoxiRooms" SET "Started" = TRUE WHERE "OpponentUserId" IS NOT NULL""");

            migrationBuilder.CreateTable(
                name: "XoxiRoomAdmissions",
                columns: table => new
                {
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XoxiRoomAdmissions", x => new { x.RoomId, x.UserId });
                    table.ForeignKey(
                        name: "FK_XoxiRoomAdmissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_XoxiRoomAdmissions_XoxiRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "XoxiRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XoxiRoomAdmissions_UserId",
                table: "XoxiRoomAdmissions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XoxiRoomAdmissions");

            migrationBuilder.DropColumn(
                name: "OwnerPlaysNoughts",
                table: "XoxiRooms");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "XoxiRooms");

            migrationBuilder.DropColumn(
                name: "Started",
                table: "XoxiRooms");
        }
    }
}
