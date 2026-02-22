using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Boards;
using Kanban.Api.Services.Columns;
using Kanban.Api.Services.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Columns;

public class ColumnServicePropertyTests
{
    [Fact]
    public async Task Property_29_CreateWithoutNameReturnsError()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Column create validation project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ColumnService.CreateAsync(board.Id, ownerId, ""));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ColumnService.CreateAsync(board.Id, ownerId, "   "));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ColumnService.CreateAsync(board.Id, ownerId, null!));
    }

    [Fact]
    public async Task Property_30_UpdateNamePersistsAndSubsequentQueryReturnsNewName()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Column update persistence project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Original");

        await fixture.ColumnService.UpdateAsync(column.Id, ownerId, new UpdateColumnDto("Renamed"));

        var queriedById = await fixture.ColumnService.GetByIdAsync(column.Id, ownerId);
        var queriedFromList = await fixture.ColumnService.ListAsync(board.Id, ownerId);

        Assert.Equal("Renamed", queriedById.Name);
        Assert.Contains(queriedFromList, x => x.Id == column.Id && x.Name == "Renamed");
    }

    [Fact]
    public async Task Property_31_ArchiveSetsDeletedAtOnColumnAndAllItsCards()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Column archive property project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "To archive");
        var now = DateTime.UtcNow;

        fixture.DbContext.Cards.AddRange(
            new Card
            {
                Id = Guid.NewGuid(),
                ColumnId = column.Id,
                Title = "Card A",
                Position = 1000,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Card
            {
                Id = Guid.NewGuid(),
                ColumnId = column.Id,
                Title = "Card B",
                Position = 2000,
                CreatedAt = now,
                UpdatedAt = now
            });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.ColumnService.ArchiveAsync(column.Id, ownerId);

        var archivedColumn = await fixture.DbContext.Columns
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == column.Id);
        var archivedCards = await fixture.DbContext.Cards
            .IgnoreQueryFilters()
            .Where(x => x.ColumnId == column.Id)
            .ToListAsync();

        Assert.NotNull(archivedColumn.DeletedAt);
        Assert.NotEmpty(archivedCards);
        Assert.All(archivedCards, card => Assert.NotNull(card.DeletedAt));
    }

    [Fact]
    public async Task Property_32_ReorderPersistsAndSubsequentListReturnsCorrectOrder()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Column reorder persistence project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");

        var first = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "First");
        var second = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Second");
        var third = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Third");

        await fixture.ColumnService.ReorderAsync(
            first.Id,
            ownerId,
            new ReorderColumnDto(second.Id, third.Id));

        var listAfterFirstQuery = await fixture.ColumnService.ListAsync(board.Id, ownerId);
        var listAfterSecondQuery = await fixture.ColumnService.ListAsync(board.Id, ownerId);

        Assert.Collection(
            listAfterFirstQuery,
            column => Assert.Equal(second.Id, column.Id),
            column => Assert.Equal(first.Id, column.Id),
            column => Assert.Equal(third.Id, column.Id));

        Assert.Collection(
            listAfterSecondQuery,
            column => Assert.Equal(second.Id, column.Id),
            column => Assert.Equal(first.Id, column.Id),
            column => Assert.Equal(third.Id, column.Id));
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"column-service-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        var projectService = new ProjectService(dbContext);
        var boardService = new BoardService(dbContext);
        var columnService = new ColumnService(dbContext);

        return new TestFixture(projectService, boardService, columnService, dbContext);
    }

    private sealed record TestFixture(
        ProjectService ProjectService,
        BoardService BoardService,
        ColumnService ColumnService,
        ApplicationDbContext DbContext);
}
