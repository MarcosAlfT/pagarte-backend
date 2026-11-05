using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSwitch.Processor.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteSubmissionOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuoteSubmissionOutcomes",
                columns: table => new
                {
                    SourceQuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PackageFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteSubmissionOutcomes", x => x.SourceQuoteId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteSubmissionOutcomes_SubmissionId",
                table: "QuoteSubmissionOutcomes",
                column: "SubmissionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM QuoteSubmissionOutcomes) THROW 51000, 'Submission outcomes protect accepted and rejected requests; downgrade is blocked.', 1;");
            migrationBuilder.DropTable(
                name: "QuoteSubmissionOutcomes");
        }
    }
}
