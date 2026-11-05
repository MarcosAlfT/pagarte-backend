using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayableServices.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistUnavailableQuoteRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SubmissionRouteUnavailable",
                table: "Quotes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Quotes WHERE SubmissionRouteUnavailable = 1)
                    THROW 51000, 'Cannot discard recorded quote route rejections.', 1;
                """);

            migrationBuilder.DropColumn(
                name: "SubmissionRouteUnavailable",
                table: "Quotes");
        }
    }
}
