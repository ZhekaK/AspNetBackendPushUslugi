using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PushPelmesh.Api.Data;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260515120000_AddXoxiSaves")]
    public partial class AddXoxiSaves : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "xoxi_saves",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    save_data = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xoxi_saves", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_xoxi_saves_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "xoxi_saves");
        }
    }
}
