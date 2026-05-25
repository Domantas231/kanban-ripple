using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCardEstimatedHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EstimatedHours",
                table: "Cards",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "Cards");
        }
    }
}
