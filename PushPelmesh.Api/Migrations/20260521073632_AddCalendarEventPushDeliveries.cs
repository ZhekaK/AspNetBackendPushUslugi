using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PushPelmesh.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEventPushDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEventPushDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalendarEventId = table.Column<int>(type: "integer", nullable: false),
                    PushNotificationSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    SentForDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventPushDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEventPushDeliveries_CalendarEvents_CalendarEventId",
                        column: x => x.CalendarEventId,
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarEventPushDeliveries_PushNotificationSubscriptions_P~",
                        column: x => x.PushNotificationSubscriptionId,
                        principalTable: "PushNotificationSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventPushDeliveries_CalendarEventId_PushNotificatio~",
                table: "CalendarEventPushDeliveries",
                columns: new[] { "CalendarEventId", "PushNotificationSubscriptionId", "SentForDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventPushDeliveries_PushNotificationSubscriptionId",
                table: "CalendarEventPushDeliveries",
                column: "PushNotificationSubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEventPushDeliveries");
        }
    }
}
