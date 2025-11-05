using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PdfPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGptFieldsToDocumentProcessed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TemplateRuleSetId",
                table: "DocumentProcessed",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "GptContactInformation",
                table: "DocumentProcessed",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GptSummary",
                table: "DocumentProcessed",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GptTitle",
                table: "DocumentProcessed",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GptContactInformation",
                table: "DocumentProcessed");

            migrationBuilder.DropColumn(
                name: "GptSummary",
                table: "DocumentProcessed");

            migrationBuilder.DropColumn(
                name: "GptTitle",
                table: "DocumentProcessed");

            migrationBuilder.AlterColumn<int>(
                name: "TemplateRuleSetId",
                table: "DocumentProcessed",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
