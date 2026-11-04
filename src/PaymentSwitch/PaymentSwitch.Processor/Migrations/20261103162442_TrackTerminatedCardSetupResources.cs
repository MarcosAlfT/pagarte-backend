using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSwitch.Processor.Migrations
{
    /// <inheritdoc />
    public partial class TrackTerminatedCardSetupResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CleanupAttempts",
                table: "CardRegistrationSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CleanupState",
                table: "CardRegistrationSessions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.Sql("""
                UPDATE CardRegistrationSessions
                SET CleanupState = 'Pending', NextAttemptAt = COALESCE(LeaseUntil, CreatedAt)
                WHERE State IN (5, 6) AND Attempts > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleanupAttempts",
                table: "CardRegistrationSessions");

            migrationBuilder.DropColumn(
                name: "CleanupState",
                table: "CardRegistrationSessions");
        }
    }
}
