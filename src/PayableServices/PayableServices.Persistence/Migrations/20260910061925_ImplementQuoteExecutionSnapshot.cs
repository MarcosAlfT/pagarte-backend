using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayableServices.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImplementQuoteExecutionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayableServiceId",
                table: "QuoteItems");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Quotes",
                type: "decimal(38,9)",
                precision: 38,
                scale: 9,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCredentialReference",
                table: "Quotes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyPaymentEndpoint",
                table: "Quotes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyRollbackEndpoint",
                table: "Quotes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyStatusEndpoint",
                table: "Quotes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Quotes",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CurrencyScale",
                table: "Quotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionErrorCode",
                table: "Quotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionErrorMessage",
                table: "Quotes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalServiceCode",
                table: "Quotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalSourceCode",
                table: "Quotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentNetworkId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoundingMode",
                table: "Quotes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RouteId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RouteType",
                table: "Quotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "TotalMinorUnits",
                table: "Quotes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "QuoteItems",
                type: "decimal(38,9)",
                precision: 38,
                scale: 9,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "ComponentType",
                table: "QuoteItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "QuoteItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisibleToClient",
                table: "QuoteItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MinorUnits",
                table: "QuoteItems",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "QuoteItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyCredentialReference",
                table: "PaymentRoutes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyPaymentEndpoint",
                table: "PaymentRoutes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyRollbackEndpoint",
                table: "PaymentRoutes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyStatusEndpoint",
                table: "PaymentRoutes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "AmountComponents",
                type: "decimal(38,9)",
                precision: 38,
                scale: 9,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.Sql("""
                UPDATE PaymentRoutes
                SET CompanyPaymentEndpoint = 'https://localhost:9443/payments',
                    CompanyRollbackEndpoint = 'https://localhost:9443/payments/rollback',
                    CompanyStatusEndpoint = 'https://localhost:9443/payments/{reference}',
                    CompanyCredentialReference = 'QEC'
                WHERE ExternalServiceCode = 'EEQ001'
                    AND CompanyPaymentEndpoint = '';

                UPDATE qi
                SET qi.ComponentType = 'LegacyAmount',
                    qi.Source = 'LegacyQuote',
                    qi.IsVisibleToClient = 1,
                    qi.DisplayOrder = 1,
                    qi.MinorUnits = CONVERT(bigint, ROUND(qi.Amount *
                        CASE
                            WHEN qi.Currency IN ('JPY', 'CLP', 'PYG') THEN 1
                            WHEN qi.Currency IN ('BHD', 'KWD', 'OMR', 'TND') THEN 1000
                            ELSE 100
                        END, 0))
                FROM QuoteItems qi;

                UPDATE q
                SET q.CountryCode = COALESCE(c.Code, 'ZZ'),
                    q.CurrencyScale = CASE
                        WHEN q.Currency IN ('JPY', 'CLP', 'PYG') THEN 0
                        WHEN q.Currency IN ('BHD', 'KWD', 'OMR', 'TND') THEN 3
                        ELSE 2
                    END,
                    q.RoundingMode = 'ToEven',
                    q.RouteId = COALESCE(r.Id, '00000000-0000-0000-0000-000000000000'),
                    q.RouteType = COALESCE(r.RouteType, 'LegacyQuote'),
                    q.PaymentNetworkId = r.PaymentNetworkId,
                    q.ProviderId = r.ProviderId,
                    q.ExternalSourceCode = r.ExternalSourceCode,
                    q.ExternalServiceCode = COALESCE(r.ExternalServiceCode, ''),
                    q.CompanyPaymentEndpoint = COALESCE(r.CompanyPaymentEndpoint, ''),
                    q.CompanyRollbackEndpoint = COALESCE(r.CompanyRollbackEndpoint, ''),
                    q.CompanyStatusEndpoint = r.CompanyStatusEndpoint,
                    q.CompanyCredentialReference = COALESCE(r.CompanyCredentialReference, ''),
                    q.TotalMinorUnits = CONVERT(bigint, ROUND(q.TotalAmount *
                        CASE
                            WHEN q.Currency IN ('JPY', 'CLP', 'PYG') THEN 1
                            WHEN q.Currency IN ('BHD', 'KWD', 'OMR', 'TND') THEN 1000
                            ELSE 100
                        END, 0)),
                    q.Status = CASE WHEN q.Status = 1 THEN 1 ELSE 4 END,
                    q.ExecutionErrorCode = CASE WHEN q.Status = 1 THEN NULL ELSE 'LEGACY_QUOTE_REQUIRES_RECREATE' END,
                    q.ExecutionErrorMessage = CASE WHEN q.Status = 1 THEN NULL ELSE 'Create a new quote before payment.' END
                FROM Quotes q
                LEFT JOIN Countries c ON c.Id = q.CountryId
                OUTER APPLY (
                    SELECT TOP (1) pr.*
                    FROM PaymentRoutes pr
                    WHERE pr.PayableServiceId = q.ServiceId AND pr.IsActive = 1
                    ORDER BY pr.Id
                ) r;
                """);

            migrationBuilder.CreateTable(
                name: "QuoteReferenceValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteReferenceValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteReferenceValues_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteReferenceValues_QuoteId_Key",
                table: "QuoteReferenceValues",
                columns: new[] { "QuoteId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuoteReferenceValues");

            migrationBuilder.DropColumn(
                name: "CompanyCredentialReference",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CompanyPaymentEndpoint",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CompanyRollbackEndpoint",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CompanyStatusEndpoint",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CurrencyScale",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ExecutionErrorCode",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ExecutionErrorMessage",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ExternalServiceCode",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ExternalSourceCode",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "PaymentNetworkId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RoundingMode",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RouteId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "RouteType",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "TotalMinorUnits",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "ComponentType",
                table: "QuoteItems");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "QuoteItems");

            migrationBuilder.DropColumn(
                name: "IsVisibleToClient",
                table: "QuoteItems");

            migrationBuilder.DropColumn(
                name: "MinorUnits",
                table: "QuoteItems");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "QuoteItems");

            migrationBuilder.DropColumn(
                name: "CompanyCredentialReference",
                table: "PaymentRoutes");

            migrationBuilder.DropColumn(
                name: "CompanyPaymentEndpoint",
                table: "PaymentRoutes");

            migrationBuilder.DropColumn(
                name: "CompanyRollbackEndpoint",
                table: "PaymentRoutes");

            migrationBuilder.DropColumn(
                name: "CompanyStatusEndpoint",
                table: "PaymentRoutes");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Quotes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(38,9)",
                oldPrecision: 38,
                oldScale: 9);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "QuoteItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(38,9)",
                oldPrecision: 38,
                oldScale: 9);

            migrationBuilder.AddColumn<Guid>(
                name: "PayableServiceId",
                table: "QuoteItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "AmountComponents",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(38,9)",
                oldPrecision: 38,
                oldScale: 9);
        }
    }
}
