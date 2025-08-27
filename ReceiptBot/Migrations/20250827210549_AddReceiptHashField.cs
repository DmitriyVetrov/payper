using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptBot.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptHashField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptHash",
                table: "Receipts",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptHash",
                table: "Receipts");
        }
    }
}
