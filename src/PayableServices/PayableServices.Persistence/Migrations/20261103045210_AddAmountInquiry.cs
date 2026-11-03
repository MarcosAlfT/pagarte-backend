using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayableServices.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAmountInquiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InquiryId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InquiryProvider",
                table: "PaymentRoutes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresInquiry",
                table: "PayableServices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotFingerprint", table: "ExternalCatalogueSources", type: "nvarchar(64)",
                maxLength: 64, nullable: false, defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SnapshotVersion",
                table: "ExternalCatalogueSources",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "AmountInquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RouteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalServiceCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReferenceSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ProviderInquiryId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProviderResultAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmountInquiries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_InquiryId",
                table: "Quotes",
                column: "InquiryId",
                unique: true,
                filter: "[InquiryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalCatalogueSources_Name",
                table: "ExternalCatalogueSources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AmountInquiries_ClientId_IdempotencyKey",
                table: "AmountInquiries",
                columns: new[] { "ClientId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AmountInquiries_Outcome_LeaseUntil",
                table: "AmountInquiries",
                columns: new[] { "Outcome", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_AmountInquiries_ProviderKey",
                table: "AmountInquiries",
                column: "ProviderKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmountInquiries");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_InquiryId",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_ExternalCatalogueSources_Name",
                table: "ExternalCatalogueSources");

            migrationBuilder.DropColumn(
                name: "InquiryId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "InquiryProvider",
                table: "PaymentRoutes");

            migrationBuilder.DropColumn(
                name: "RequiresInquiry",
                table: "PayableServices");

            migrationBuilder.DropColumn(name: "SnapshotFingerprint", table: "ExternalCatalogueSources");

            migrationBuilder.DropColumn(
                name: "SnapshotVersion",
                table: "ExternalCatalogueSources");
        }
    }
}
