using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayableServices.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleUseQuoteSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy unpaid snapshots cannot acquire invented submission authority.
            // Explicit source-quote status recovery remains available for a remotely accepted payment.
            migrationBuilder.Sql("UPDATE Quotes SET Status = 2 WHERE Status = 0 AND PaymentId IS NULL;");
            migrationBuilder.AddColumn<int>(
                name: "LifecycleVersion",
                table: "Quotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubmissionAttemptCount",
                table: "Quotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionCompletedAt",
                table: "Quotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionFingerprint",
                table: "Quotes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionLeaseId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionLeaseUntil",
                table: "Quotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionNextAttemptAt",
                table: "Quotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionPackageJson",
                table: "Quotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionResultJson",
                table: "Quotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Quotes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedCardId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_SubmissionId",
                table: "Quotes",
                column: "SubmissionId",
                unique: true,
                filter: "[SubmissionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_SubmissionNextAttemptAt_SubmissionLeaseUntil",
                table: "Quotes",
                columns: new[] { "SubmissionNextAttemptAt", "SubmissionLeaseUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM Quotes WHERE SubmissionId IS NOT NULL) THROW 51000, 'Consumed quote history must be preserved; downgrade is blocked.', 1;");
            migrationBuilder.DropIndex(
                name: "IX_Quotes_SubmissionId",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_SubmissionNextAttemptAt_SubmissionLeaseUntil",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "LifecycleVersion",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmissionAttemptCount",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmissionCompletedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmissionFingerprint",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmissionLeaseId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmissionLeaseUntil",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmissionNextAttemptAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmissionPackageJson",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmissionResultJson",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "SubmittedCardId",
                table: "Quotes");
        }
    }
}
