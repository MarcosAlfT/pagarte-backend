using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSwitch.Processor.Migrations
{
    /// <inheritdoc />
    public partial class ImplementQuoteExecutionSaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM Payments
                    WHERE Status IN (0, 1, 2, 3, 6, 9)
                )
                    THROW 51000, 'Drain all legacy in-flight payments before applying the quote execution saga migration.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM OutboxMessages
                    WHERE PublishedAt IS NULL
                      AND (RoutingKey = 'payment.request'
                           OR MessageType LIKE '%PaymentRequestMessage%')
                )
                    THROW 51001, 'Drain all legacy payment.request outbox messages before applying the quote execution saga migration.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentQuotes_QuoteId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Services_ServiceId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_QuoteId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ServiceId",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "ServiceId",
                table: "Payments",
                newName: "SourceServiceId");

            migrationBuilder.RenameColumn(
                name: "RetryCount",
                table: "Payments",
                newName: "RefundRetryCount");

            migrationBuilder.RenameColumn(
                name: "QuoteId",
                table: "Payments",
                newName: "SourceQuoteId");

            migrationBuilder.RenameColumn(
                name: "NextRetryAt",
                table: "Payments",
                newName: "NextRefundRetryAt");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "Payments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardChargeIdempotencyKey",
                table: "Payments",
                type: "nvarchar(42)",
                maxLength: 42,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CardCheckAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChargeAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RollbackAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCredentialReference",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CompanyPaymentAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CompanyPaymentEndpoint",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyPaymentIdempotencyKey",
                table: "Payments",
                type: "nvarchar(42)",
                maxLength: 42,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyRollbackEndpoint",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyRollbackIdempotencyKey",
                table: "Payments",
                type: "nvarchar(42)",
                maxLength: 42,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyStatusEndpoint",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Payments",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CountryId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "CurrencyScale",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalServiceCode",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalSourceCode",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCardCheckAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxChargeAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxCompanyPaymentAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRollbackAttempts",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageFingerprint",
                table: "Payments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentNetworkId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuoteCreatedAt",
                table: "Payments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "QuoteExpiresAt",
                table: "Payments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RetryIntervalSeconds",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RoundingMode",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RouteId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RouteType",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServiceName",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "TotalMinorUnits",
                table: "Payments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Payments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "PaymentDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ComponentType",
                table: "PaymentDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "PaymentDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisibleToClient",
                table: "PaymentDetails",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MinorUnits",
                table: "PaymentDetails",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "PaymentDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE pd
                SET pd.ComponentType = CASE pd.Type
                        WHEN 0 THEN 'ServiceAmount'
                        WHEN 1 THEN 'PaymentOperatorFee'
                        WHEN 2 THEN 'CompanyFee'
                        WHEN 3 THEN 'PlatformFee'
                        WHEN 4 THEN 'Tax'
                        ELSE 'LegacyAmount'
                    END,
                    pd.DisplayOrder = pd.Type + 1,
                    pd.IsVisibleToClient = 1,
                    pd.Source = 'LegacyProcessor',
                    pd.MinorUnits = CONVERT(bigint, ROUND(pd.Amount *
                        CASE
                            WHEN pd.Currency IN ('JPY', 'CLP', 'PYG') THEN 1
                            WHEN pd.Currency IN ('BHD', 'KWD', 'OMR', 'TND') THEN 1000
                            ELSE 100
                        END, 0))
                FROM PaymentDetails pd;

                UPDATE p
                SET p.SourceQuoteId = COALESCE(p.SourceQuoteId, p.Id),
                    p.PackageFingerprint = LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varchar(36), p.Id)), 2)),
                    p.ServiceName = COALESCE(s.Name, 'Legacy payment'),
                    p.CountryCode = 'ZZ',
                    p.RouteType = 'LegacyProcessor',
                    p.ExternalServiceCode = CONVERT(varchar(36), p.SourceServiceId),
                    p.CompanyPaymentEndpoint = COALESCE(c.ApiEndpoint, 'https://legacy.invalid/payments'),
                    p.CompanyRollbackEndpoint = 'https://legacy.invalid/payments/rollback',
                    p.CompanyCredentialReference = 'LEGACY',
                    p.CurrencyScale = CASE
                        WHEN p.Currency IN ('JPY', 'CLP', 'PYG') THEN 0
                        WHEN p.Currency IN ('BHD', 'KWD', 'OMR', 'TND') THEN 3
                        ELSE 2
                    END,
                    p.RoundingMode = 'ToEven',
                    p.QuoteCreatedAt = p.CreatedAt,
                    p.QuoteExpiresAt = DATEADD(hour, 1, p.CreatedAt),
                    p.TotalMinorUnits = COALESCE(details.TotalMinorUnits, 0),
                    p.CardChargeIdempotencyKey = LEFT('charge-' + REPLACE(CONVERT(varchar(36), p.Id), '-', ''), 42),
                    p.CompanyPaymentIdempotencyKey = LEFT('company-' + REPLACE(CONVERT(varchar(36), p.Id), '-', ''), 42),
                    p.CompanyRollbackIdempotencyKey = LEFT('rollback-' + REPLACE(CONVERT(varchar(36), p.Id), '-', ''), 42),
                    p.MaxCardCheckAttempts = 3,
                    p.MaxCompanyPaymentAttempts = 3,
                    p.MaxChargeAttempts = 3,
                    p.MaxRollbackAttempts = 10,
                    p.RetryIntervalSeconds = 300
                FROM Payments p
                LEFT JOIN Services s ON s.Id = p.SourceServiceId
                LEFT JOIN Companies c ON c.Id = s.CompanyId
                OUTER APPLY (
                    SELECT SUM(pd.MinorUnits) AS TotalMinorUnits
                    FROM PaymentDetails pd
                    WHERE pd.PaymentId = p.Id
                ) details;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceQuoteId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "PaymentDetails");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "PaymentDetails");

            migrationBuilder.DropTable(
                name: "FeeConfigurations");

            migrationBuilder.DropTable(
                name: "PaymentQuoteDetails");

            migrationBuilder.DropTable(
                name: "PaymentQuotes");

            migrationBuilder.CreateTable(
                name: "PaymentReferenceValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReferenceValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentReferenceValues_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_NextAttemptAt_Status",
                table: "Payments",
                columns: new[] { "NextAttemptAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SourceQuoteId",
                table: "Payments",
                column: "SourceQuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReferenceValues_PaymentId_Key",
                table: "PaymentReferenceValues",
                columns: new[] { "PaymentId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentReferenceValues");

            migrationBuilder.DropIndex(
                name: "IX_Payments_NextAttemptAt_Status",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_SourceQuoteId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CardChargeIdempotencyKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CardCheckAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ChargeAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompanyCredentialReference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompanyPaymentAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompanyPaymentEndpoint",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompanyPaymentIdempotencyKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompanyRollbackEndpoint",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompanyRollbackIdempotencyKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompanyStatusEndpoint",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CurrencyScale",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ExternalServiceCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ExternalSourceCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MaxCardCheckAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MaxChargeAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MaxCompanyPaymentAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MaxRollbackAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PackageFingerprint",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentNetworkId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "QuoteCreatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "QuoteExpiresAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RetryIntervalSeconds",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RoundingMode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RouteId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RouteType",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ServiceName",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TotalMinorUnits",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ComponentType",
                table: "PaymentDetails");

            migrationBuilder.DropColumn(
                name: "IsVisibleToClient",
                table: "PaymentDetails");

            migrationBuilder.DropColumn(
                name: "MinorUnits",
                table: "PaymentDetails");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "PaymentDetails");

            migrationBuilder.DropColumn(
                name: "RollbackAttempts",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "PaymentDetails");

            migrationBuilder.RenameColumn(
                name: "SourceServiceId",
                table: "Payments",
                newName: "ServiceId");

			migrationBuilder.RenameColumn(
				name: "RefundRetryCount",
				table: "Payments",
				newName: "RetryCount");

            migrationBuilder.RenameColumn(
                name: "NextRefundRetryAt",
                table: "Payments",
                newName: "NextRetryAt");


			migrationBuilder.AlterColumn<Guid>(
				name: "SourceQuoteId",
				table: "Payments",
				type: "uniqueidentifier",
				nullable: true,
				oldClrType: typeof(Guid),
				oldType: "uniqueidentifier");

			migrationBuilder.RenameColumn(
				name: "SourceQuoteId",
				table: "Payments",
				newName: "QuoteId");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "PaymentDetails",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "PaymentDetails",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

			migrationBuilder.AddColumn<int>(
				name: "Type",
				table: "PaymentDetails",
				type: "int",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.Sql("UPDATE Payments SET QuoteId = NULL;");

            migrationBuilder.CreateTable(
                name: "FeeConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentQuotes_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentQuoteDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentQuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentQuoteDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentQuoteDetails_PaymentQuotes_PaymentQuoteId",
                        column: x => x.PaymentQuoteId,
                        principalTable: "PaymentQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_QuoteId",
                table: "Payments",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ServiceId",
                table: "Payments",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentQuoteDetails_PaymentQuoteId",
                table: "PaymentQuoteDetails",
                column: "PaymentQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentQuotes_ClientId",
                table: "PaymentQuotes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentQuotes_ExpiresAt",
                table: "PaymentQuotes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentQuotes_ServiceId",
                table: "PaymentQuotes",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentQuotes_Status",
                table: "PaymentQuotes",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentQuotes_QuoteId",
                table: "Payments",
                column: "QuoteId",
                principalTable: "PaymentQuotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Services_ServiceId",
                table: "Payments",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
