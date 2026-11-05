using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSwitch.Processor.Migrations
{
    /// <inheritdoc />
    public partial class AddBillPaymentReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillKey",
                table: "Payments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BillReservationReleased",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "BillVerificationExpiresAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillVerificationJson",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BillKey",
                table: "Payments",
                column: "BillKey",
                unique: true,
                filter: "[BillKey] IS NOT NULL AND [BillReservationReleased] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BillKey_ProcessedAt",
                table: "Payments",
                columns: new[] { "BillKey", "ProcessedAt" },
                filter: "[BillKey] IS NOT NULL AND [BillReservationReleased] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Payments WHERE BillKey IS NOT NULL)
                    THROW 51000, 'Cannot remove bill reservations from recorded payments.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Payments_BillKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_BillKey_ProcessedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BillKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BillReservationReleased",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BillVerificationExpiresAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BillVerificationJson",
                table: "Payments");
        }
    }
}
