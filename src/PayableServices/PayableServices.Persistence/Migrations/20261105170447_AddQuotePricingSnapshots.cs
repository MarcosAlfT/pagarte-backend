using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayableServices.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotePricingSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PricingSnapshotJson",
                table: "Quotes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingDefinitionJson",
                table: "AmountCompositions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Quotes WHERE PricingSnapshotJson IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM AmountCompositions WHERE PricingDefinitionJson IS NOT NULL)
                    THROW 51000, 'Cannot discard quote pricing definitions or snapshots.', 1;
                """);

            migrationBuilder.DropColumn(
                name: "PricingSnapshotJson",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "PricingDefinitionJson",
                table: "AmountCompositions");
        }
    }
}
