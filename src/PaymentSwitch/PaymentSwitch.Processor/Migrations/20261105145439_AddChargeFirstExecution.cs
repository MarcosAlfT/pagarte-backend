using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSwitch.Processor.Migrations
{
    /// <inheritdoc />
    public partial class AddChargeFirstExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingPeriod",
                table: "Payments",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChargeFirst",
                table: "Payments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CompanyNotificationEndpoint",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompensationAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompensationKey",
                table: "Payments",
                type: "nvarchar(42)",
                maxLength: 42,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCompensationAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OperatorRefundId",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Payments WHERE ChargeFirst = 1)
                    THROW 51000, 'Cannot remove charge-first execution and refund evidence.', 1;
                """);

            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ChargeFirst",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompanyNotificationEndpoint",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompensationAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompensationKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MaxCompensationAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "OperatorRefundId",
                table: "Payments");
        }
    }
}
