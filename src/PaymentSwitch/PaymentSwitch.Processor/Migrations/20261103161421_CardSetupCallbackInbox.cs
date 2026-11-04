using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSwitch.Processor.Migrations
{
    /// <inheritdoc />
    public partial class CardSetupCallbackInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardSetupNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardSetupNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardSetupNotifications_IntegrationId_EventId",
                table: "CardSetupNotifications",
                columns: new[] { "IntegrationId", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardSetupNotifications");
        }
    }
}
