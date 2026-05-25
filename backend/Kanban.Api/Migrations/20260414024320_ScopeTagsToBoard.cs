using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScopeTagsToBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Projects_ProjectId",
                table: "Tags");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "Tags",
                newName: "BoardId");

            migrationBuilder.RenameIndex(
                name: "IX_Tags_ProjectId_Name",
                table: "Tags",
                newName: "IX_Tags_BoardId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Tags_ProjectId",
                table: "Tags",
                newName: "IX_Tags_BoardId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Boards_BoardId",
                table: "Tags",
                column: "BoardId",
                principalTable: "Boards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Boards_BoardId",
                table: "Tags");

            migrationBuilder.RenameColumn(
                name: "BoardId",
                table: "Tags",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Tags_BoardId_Name",
                table: "Tags",
                newName: "IX_Tags_ProjectId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Tags_BoardId",
                table: "Tags",
                newName: "IX_Tags_ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Projects_ProjectId",
                table: "Tags",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
