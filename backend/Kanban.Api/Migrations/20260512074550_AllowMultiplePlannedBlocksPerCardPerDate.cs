using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultiplePlannedBlocksPerCardPerDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlannedBlocks_UserId_CardId_Date",
                table: "PlannedBlocks");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedBlocks_UserId_CardId_Date",
                table: "PlannedBlocks",
                columns: new[] { "UserId", "CardId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlannedBlocks_UserId_CardId_Date",
                table: "PlannedBlocks");

            migrationBuilder.CreateIndex(
                name: "IX_PlannedBlocks_UserId_CardId_Date",
                table: "PlannedBlocks",
                columns: new[] { "UserId", "CardId", "Date" },
                unique: true);
        }
    }
}
