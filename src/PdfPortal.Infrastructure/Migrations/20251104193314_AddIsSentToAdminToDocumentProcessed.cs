using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PdfPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSentToAdminToDocumentProcessed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSentToAdmin",
                table: "DocumentProcessed",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentToAdminAt",
                table: "DocumentProcessed",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSentToAdmin",
                table: "DocumentProcessed");

            migrationBuilder.DropColumn(
                name: "SentToAdminAt",
                table: "DocumentProcessed");
        }
    }
}
