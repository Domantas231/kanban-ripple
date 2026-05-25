using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueBoardAndProjectNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId_Name",
                table: "Projects",
                columns: new[] { "OwnerId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_ProjectId_Name",
                table: "Boards",
                columns: new[] { "ProjectId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerId_Name",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Boards_ProjectId_Name",
                table: "Boards");
        }
    }
}
