using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDurationWithDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedDurationHours",
                table: "Cards");

            migrationBuilder.RenameColumn(
                name: "EndDate",
                table: "Cards",
                newName: "DueDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DueDate",
                table: "Cards",
                newName: "EndDate");

            migrationBuilder.AddColumn<decimal>(
                name: "PlannedDurationHours",
                table: "Cards",
                type: "numeric",
                nullable: true);
        }
    }
}
