using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PdfPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRfcToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Rfc",
                table: "Users",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rfc",
                table: "Users");
        }
    }
}
