using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSwitch.Processor.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRecoveryAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentRecoveryActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpectedVersion = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecoveryActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRecoveryActions_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status_LastUpdatedAt_Id",
                table: "Payments",
                columns: new[] { "Status", "LastUpdatedAt", "Id" },
                filter: "[ChargeFirst] = 1 AND [NextAttemptAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecoveryActions_Actor_RequestKey",
                table: "PaymentRecoveryActions",
                columns: new[] { "Actor", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecoveryActions_PaymentId",
                table: "PaymentRecoveryActions",
                column: "PaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM PaymentRecoveryActions)
                    THROW 51000, 'Cannot discard recorded payment recovery audits.', 1;
                """);

            migrationBuilder.DropTable(
                name: "PaymentRecoveryActions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Status_LastUpdatedAt_Id",
                table: "Payments");
        }
    }
}
