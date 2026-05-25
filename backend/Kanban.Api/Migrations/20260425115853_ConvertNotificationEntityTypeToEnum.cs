using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConvertNotificationEntityTypeToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert text column to int using a CASE that maps the historical
            // lowercase string values to EntityType enum members:
            // Card=0, Column=1, Project=2, Board=3.
            // Unknown values become NULL.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Notifications""
                ALTER COLUMN ""EntityType"" TYPE integer
                USING (
                    CASE LOWER(""EntityType"")
                        WHEN 'card' THEN 0
                        WHEN 'column' THEN 1
                        WHEN 'project' THEN 2
                        WHEN 'board' THEN 3
                        ELSE NULL
                    END
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Notifications""
                ALTER COLUMN ""EntityType"" TYPE text
                USING (
                    CASE ""EntityType""
                        WHEN 0 THEN 'card'
                        WHEN 1 THEN 'column'
                        WHEN 2 THEN 'project'
                        WHEN 3 THEN 'board'
                        ELSE NULL
                    END
                );
            ");
        }
    }
}
