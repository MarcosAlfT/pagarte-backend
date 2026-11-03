using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientProfiles.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalIdentityMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Issuer",
                table: "Clients",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Clients",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Issuer_Subject",
                table: "Clients",
                columns: new[] { "Issuer", "Subject" },
                unique: true,
                filter: "[Issuer] IS NOT NULL AND [Subject] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_Issuer_Subject",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Issuer",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Clients");
        }
    }
}
