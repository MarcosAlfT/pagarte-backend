using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSwitch.Processor.Migrations
{
    /// <inheritdoc />
    public partial class RecordCardConsentAndDeferredRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdapterProvider",
                table: "CreditCards",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DetachmentAttempts",
                table: "CreditCards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "DetachmentLeaseId",
                table: "CreditCards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DetachmentLeaseUntil",
                table: "CreditCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DetachmentNextAttemptAt",
                table: "CreditCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetachmentState",
                table: "CreditCards",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentAcceptedAt",
                table: "CardRegistrationSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentVersion",
                table: "CardRegistrationSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Only recorded successful setup supplies adapter identity. Legacy rows stay unidentified.
            migrationBuilder.Sql("""
                UPDATE c SET AdapterProvider = s.Provider
                FROM CreditCards c INNER JOIN CardRegistrationSessions s ON s.CardId = c.Id
                WHERE c.SetupVerified = 1 AND s.IntegrationId = c.OperatorProvider
                  AND s.ProviderAccount = c.ProviderAccount AND s.ProviderEnvironment = c.ProviderEnvironment
                  AND s.IntegrationVersion = c.IntegrationVersion;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_DetachmentNextAttemptAt",
                table: "CreditCards",
                column: "DetachmentNextAttemptAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CreditCards_DetachmentNextAttemptAt",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "AdapterProvider",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "DetachmentAttempts",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "DetachmentLeaseId",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "DetachmentLeaseUntil",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "DetachmentNextAttemptAt",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "DetachmentState",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "ConsentAcceptedAt",
                table: "CardRegistrationSessions");

            migrationBuilder.DropColumn(
                name: "ConsentVersion",
                table: "CardRegistrationSessions");
        }
    }
}
