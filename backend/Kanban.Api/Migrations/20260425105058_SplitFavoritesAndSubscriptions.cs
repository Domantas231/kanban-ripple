using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.Api.Migrations
{
    /// <inheritdoc />
    public partial class SplitFavoritesAndSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoardFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardFavorites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardFavorites_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoardSubscriptions_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CardSubscriptions_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ColumnSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ColumnId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ColumnSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ColumnSubscriptions_Columns_ColumnId",
                        column: x => x.ColumnId,
                        principalTable: "Columns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFavorites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectFavorites_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectSubscriptions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardFavorites_BoardId",
                table: "BoardFavorites",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardFavorites_UserId",
                table: "BoardFavorites",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardFavorites_UserId_BoardId",
                table: "BoardFavorites",
                columns: new[] { "UserId", "BoardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoardSubscriptions_BoardId",
                table: "BoardSubscriptions",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardSubscriptions_UserId",
                table: "BoardSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardSubscriptions_UserId_BoardId",
                table: "BoardSubscriptions",
                columns: new[] { "UserId", "BoardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardSubscriptions_CardId",
                table: "CardSubscriptions",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_CardSubscriptions_UserId",
                table: "CardSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CardSubscriptions_UserId_CardId",
                table: "CardSubscriptions",
                columns: new[] { "UserId", "CardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColumnSubscriptions_ColumnId",
                table: "ColumnSubscriptions",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_ColumnSubscriptions_UserId",
                table: "ColumnSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ColumnSubscriptions_UserId_ColumnId",
                table: "ColumnSubscriptions",
                columns: new[] { "UserId", "ColumnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFavorites_ProjectId",
                table: "ProjectFavorites",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFavorites_UserId",
                table: "ProjectFavorites",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFavorites_UserId_ProjectId",
                table: "ProjectFavorites",
                columns: new[] { "UserId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubscriptions_ProjectId",
                table: "ProjectSubscriptions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubscriptions_UserId",
                table: "ProjectSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSubscriptions_UserId_ProjectId",
                table: "ProjectSubscriptions",
                columns: new[] { "UserId", "ProjectId" },
                unique: true);

            // Copy existing polymorphic rows into per-entity tables.
            // EntityType: Card=0, Column=1, Project=2, Board=3.
            // Only copy rows where the target entity actually exists, to satisfy the new FK.
            migrationBuilder.Sql(@"
                INSERT INTO ""BoardFavorites"" (""Id"", ""UserId"", ""BoardId"", ""CreatedAt"")
                SELECT f.""Id"", f.""UserId"", f.""EntityId"", f.""CreatedAt""
                FROM ""Favorites"" f
                INNER JOIN ""Boards"" b ON b.""Id"" = f.""EntityId""
                WHERE f.""EntityType"" = 3;

                INSERT INTO ""ProjectFavorites"" (""Id"", ""UserId"", ""ProjectId"", ""CreatedAt"")
                SELECT f.""Id"", f.""UserId"", f.""EntityId"", f.""CreatedAt""
                FROM ""Favorites"" f
                INNER JOIN ""Projects"" p ON p.""Id"" = f.""EntityId""
                WHERE f.""EntityType"" = 2;

                INSERT INTO ""CardSubscriptions"" (""Id"", ""UserId"", ""CardId"", ""CreatedAt"")
                SELECT s.""Id"", s.""UserId"", s.""EntityId"", s.""CreatedAt""
                FROM ""Subscriptions"" s
                INNER JOIN ""Cards"" c ON c.""Id"" = s.""EntityId""
                WHERE s.""EntityType"" = 0;

                INSERT INTO ""ColumnSubscriptions"" (""Id"", ""UserId"", ""ColumnId"", ""CreatedAt"")
                SELECT s.""Id"", s.""UserId"", s.""EntityId"", s.""CreatedAt""
                FROM ""Subscriptions"" s
                INNER JOIN ""Columns"" c ON c.""Id"" = s.""EntityId""
                WHERE s.""EntityType"" = 1;

                INSERT INTO ""ProjectSubscriptions"" (""Id"", ""UserId"", ""ProjectId"", ""CreatedAt"")
                SELECT s.""Id"", s.""UserId"", s.""EntityId"", s.""CreatedAt""
                FROM ""Subscriptions"" s
                INNER JOIN ""Projects"" p ON p.""Id"" = s.""EntityId""
                WHERE s.""EntityType"" = 2;

                INSERT INTO ""BoardSubscriptions"" (""Id"", ""UserId"", ""BoardId"", ""CreatedAt"")
                SELECT s.""Id"", s.""UserId"", s.""EntityId"", s.""CreatedAt""
                FROM ""Subscriptions"" s
                INNER JOIN ""Boards"" b ON b.""Id"" = s.""EntityId""
                WHERE s.""EntityType"" = 3;
            ");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropTable(
                name: "Subscriptions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardFavorites");

            migrationBuilder.DropTable(
                name: "BoardSubscriptions");

            migrationBuilder.DropTable(
                name: "CardSubscriptions");

            migrationBuilder.DropTable(
                name: "ColumnSubscriptions");

            migrationBuilder.DropTable(
                name: "ProjectFavorites");

            migrationBuilder.DropTable(
                name: "ProjectSubscriptions");

            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Favorites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_UserId",
                table: "Favorites",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_UserId_EntityType_EntityId",
                table: "Favorites",
                columns: new[] { "UserId", "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_EntityType_EntityId",
                table: "Subscriptions",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId_EntityType_EntityId",
                table: "Subscriptions",
                columns: new[] { "UserId", "EntityType", "EntityId" },
                unique: true);
        }
    }
}
