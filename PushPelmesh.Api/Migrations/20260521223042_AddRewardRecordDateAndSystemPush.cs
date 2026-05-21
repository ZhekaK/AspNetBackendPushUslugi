using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardRecordDateAndSystemPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "RewardRecords",
                type: "date",
                nullable: false,
                defaultValueSql: "CURRENT_DATE");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRecords_Date",
                table: "RewardRecords",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewardRecords_Date",
                table: "RewardRecords");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "RewardRecords");
        }
    }
}
