using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayableServices.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillVerificationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillIdentityJson",
                table: "Quotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillVerificationJson",
                table: "Quotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingPeriod",
                table: "AmountInquiries",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            // Older inquiry-backed quotes have no authoritative billing period to verify.
            migrationBuilder.Sql("""
                UPDATE Quotes
                SET Status = 7,
                    ExecutionErrorCode = 'BILL_IDENTITY_REQUIRED',
                    ExecutionErrorMessage = 'The quote has no supported bill identity. Consult again.'
                WHERE InquiryId IS NOT NULL AND Status = 0 AND SubmissionId IS NULL AND PaymentId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Quotes WHERE BillVerificationJson IS NOT NULL
                    OR (BillIdentityJson IS NOT NULL AND SubmissionId IS NOT NULL))
                    THROW 51000, 'Cannot remove evidence for submitted bill payments.', 1;
                UPDATE Quotes SET Status = 2 WHERE Status = 7 AND ExecutionErrorCode = 'BILL_IDENTITY_REQUIRED';
                """);

            migrationBuilder.DropColumn(
                name: "BillIdentityJson",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "BillVerificationJson",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "AmountInquiries");
        }
    }
}
