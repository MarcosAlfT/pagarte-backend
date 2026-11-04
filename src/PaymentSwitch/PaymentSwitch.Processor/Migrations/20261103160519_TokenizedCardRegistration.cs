using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSwitch.Processor.Migrations
{
    /// <inheritdoc />
    public partial class TokenizedCardRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Inspect populated local databases before removing the raw-card column.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM CreditCards WHERE LEN(ClientId) > 100)
                    THROW 51000, 'Card owner identifiers longer than 100 characters require explicit remediation.', 1;
                IF EXISTS (SELECT ClientId FROM CreditCards WHERE IsDefault = 1 AND IsDeleted = 0 GROUP BY ClientId HAVING COUNT(*) > 1)
                    THROW 51000, 'Resolve duplicate active card defaults before applying TokenizedCardRegistration.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_CreditCards_ClientId",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "CardNumber",
                table: "CreditCards");

            migrationBuilder.AlterColumn<string>(
                name: "ClientId",
                table: "CreditCards",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "IntegrationVersion",
                table: "CreditCards",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderAccount",
                table: "CreditCards",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderEnvironment",
                table: "CreditCards",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderReferenceDigest",
                table: "CreditCards",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SetupVerified",
                table: "CreditCards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CardRegistrationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreationFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CompletionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, collation: "Latin1_General_100_BIN2"),
                    TokenDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProtectedToken = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                    IntegrationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderAccount = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderEnvironment = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IntegrationVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CardHolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MakeDefault = table.Column<bool>(type: "bit", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NextAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardRegistrationSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_ClientId_ProviderReferenceDigest",
                table: "CreditCards",
                columns: new[] { "ClientId", "ProviderReferenceDigest" },
                unique: true,
                filter: "[ProviderReferenceDigest] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_CreditCards_ActiveDefault",
                table: "CreditCards",
                column: "ClientId",
                unique: true,
                filter: "[IsDefault] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CardRegistrationSessions_ClientId_CompletionKey",
                table: "CardRegistrationSessions",
                columns: new[] { "ClientId", "CompletionKey" },
                unique: true,
                filter: "[CompletionKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CardRegistrationSessions_ClientId_CreationKey",
                table: "CardRegistrationSessions",
                columns: new[] { "ClientId", "CreationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardRegistrationSessions_NextAttemptAt",
                table: "CardRegistrationSessions",
                column: "NextAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_CardRegistrationSessions_TokenDigest",
                table: "CardRegistrationSessions",
                column: "TokenDigest",
                unique: true,
                filter: "[TokenDigest] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardRegistrationSessions");

            migrationBuilder.DropIndex(
                name: "IX_CreditCards_ClientId_ProviderReferenceDigest",
                table: "CreditCards");

            migrationBuilder.DropIndex(
                name: "UX_CreditCards_ActiveDefault",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "IntegrationVersion",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "ProviderAccount",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "ProviderEnvironment",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "ProviderReferenceDigest",
                table: "CreditCards");

            migrationBuilder.DropColumn(
                name: "SetupVerified",
                table: "CreditCards");

            migrationBuilder.AlterColumn<string>(
                name: "ClientId",
                table: "CreditCards",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "CardNumber",
                table: "CreditCards",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_ClientId",
                table: "CreditCards",
                column: "ClientId");
        }
    }
}
