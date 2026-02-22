using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Boards;
using Kanban.Api.Services.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Boards;

public class BoardServicePropertyTests
{
    [Fact]
    public async Task Property_26_CreateWithoutNameReturnsError()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Board name validation project", ProjectType.Team);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.BoardService.CreateAsync(project.Id, ownerId, ""));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.BoardService.CreateAsync(project.Id, ownerId, "   "));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.BoardService.CreateAsync(project.Id, ownerId, null!));
    }

    [Fact]
    public async Task Property_27_UpdateNamePersistsAndSubsequentQueryReturnsNewName()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Board update persistence project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Original");

        await fixture.BoardService.UpdateAsync(board.Id, ownerId, new UpdateBoardDto("Renamed", board.Position));

        var queriedById = await fixture.BoardService.GetByIdAsync(board.Id, ownerId);
        var queriedFromList = await fixture.BoardService.ListAsync(project.Id, ownerId);

        Assert.Equal("Renamed", queriedById.Name);
        Assert.Contains(queriedFromList, x => x.Id == board.Id && x.Name == "Renamed");
    }

    [Fact]
    public async Task Property_28_ArchiveSoftDeletesBoardColumnsAndCardsAndExcludesThemFromNormalQueries()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Board archive property project", ProjectType.Team);
        var now = DateTime.UtcNow;

        var board = new Board
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Archive board",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        var column = new Column
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "Archive column",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        var card = new Card
        {
            Id = Guid.NewGuid(),
            ColumnId = column.Id,
            Title = "Archive card",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        fixture.DbContext.Boards.Add(board);
        fixture.DbContext.Columns.Add(column);
        fixture.DbContext.Cards.Add(card);
        await fixture.DbContext.SaveChangesAsync();

        await fixture.BoardService.ArchiveAsync(board.Id, ownerId);

        var archivedBoard = await fixture.DbContext.Boards
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == board.Id);
        var archivedColumn = await fixture.DbContext.Columns
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == column.Id);
        var archivedCard = await fixture.DbContext.Cards
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == card.Id);

        Assert.NotNull(archivedBoard.DeletedAt);
        Assert.NotNull(archivedColumn.DeletedAt);
        Assert.NotNull(archivedCard.DeletedAt);

        Assert.False(await fixture.DbContext.Boards.AnyAsync(x => x.Id == board.Id));
        Assert.False(await fixture.DbContext.Columns.AnyAsync(x => x.Id == column.Id));
        Assert.False(await fixture.DbContext.Cards.AnyAsync(x => x.Id == card.Id));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.BoardService.GetByIdAsync(board.Id, ownerId));

        var boards = await fixture.BoardService.ListAsync(project.Id, ownerId);
        Assert.DoesNotContain(boards, x => x.Id == board.Id);
    }

    [Fact]
    public async Task CreateAsync_AssociatesBoardWithProject_AndRequiresMemberPlus()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Board create project", ProjectType.Team);
        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = memberId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = viewerId,
                Role = ProjectRole.Viewer,
                JoinedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var created = await fixture.BoardService.CreateAsync(project.Id, memberId, "Backlog");

        Assert.Equal(project.Id, created.ProjectId);
        Assert.Equal("Backlog", created.Name);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.BoardService.CreateAsync(project.Id, viewerId, "Viewer board"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.BoardService.CreateAsync(project.Id, outsiderId, "Outsider board"));
    }

    [Fact]
    public async Task GetByIdAsync_AllowsViewerPlus_AndDeniesOutsider()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Board read project", ProjectType.Team);
        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = viewerId,
            Role = ProjectRole.Viewer,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Roadmap");

        var found = await fixture.BoardService.GetByIdAsync(board.Id, viewerId);
        Assert.Equal(board.Id, found.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.BoardService.GetByIdAsync(board.Id, outsiderId));
    }

    [Fact]
    public async Task ListAsync_ReturnsProjectBoardsOrderedByPosition()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Board list project", ProjectType.Simple);
        var now = DateTime.UtcNow;

        fixture.DbContext.Boards.AddRange(
            new Board
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "Third",
                Position = 30,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Board
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "First",
                Position = 10,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Board
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "Second",
                Position = 20,
                CreatedAt = now,
                UpdatedAt = now
            });
        await fixture.DbContext.SaveChangesAsync();

        var boards = await fixture.BoardService.ListAsync(project.Id, ownerId);

        Assert.Equal(3, boards.Count);
        Assert.Collection(
            boards,
            board => Assert.Equal("First", board.Name),
            board => Assert.Equal("Second", board.Name),
            board => Assert.Equal("Third", board.Name));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesNameAndPosition_AndRequiresMemberPlus()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Board update project", ProjectType.Team);
        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = viewerId,
            Role = ProjectRole.Viewer,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Initial");

        var updated = await fixture.BoardService.UpdateAsync(board.Id, ownerId, new UpdateBoardDto("Renamed", 99));
        Assert.Equal("Renamed", updated.Name);
        Assert.Equal(99, updated.Position);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.BoardService.UpdateAsync(board.Id, viewerId, new UpdateBoardDto("Nope", 5)));
    }

    [Fact]
    public async Task ArchiveAsync_SoftDeletesBoardColumnsAndCards()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Board archive project", ProjectType.Team);
        var now = DateTime.UtcNow;

        var board = new Board
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Archive board",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        var column = new Column
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "Archive column",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        var card = new Card
        {
            Id = Guid.NewGuid(),
            ColumnId = column.Id,
            Title = "Archive card",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        fixture.DbContext.Boards.Add(board);
        fixture.DbContext.Columns.Add(column);
        fixture.DbContext.Cards.Add(card);
        await fixture.DbContext.SaveChangesAsync();

        await fixture.BoardService.ArchiveAsync(board.Id, ownerId);

        var archivedBoard = await fixture.DbContext.Boards
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == board.Id);
        var archivedColumn = await fixture.DbContext.Columns
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == column.Id);
        var archivedCard = await fixture.DbContext.Cards
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == card.Id);

        Assert.NotNull(archivedBoard.DeletedAt);
        Assert.NotNull(archivedColumn.DeletedAt);
        Assert.NotNull(archivedCard.DeletedAt);

        Assert.False(await fixture.DbContext.Boards.AnyAsync(x => x.Id == board.Id));
        Assert.False(await fixture.DbContext.Columns.AnyAsync(x => x.Id == column.Id));
        Assert.False(await fixture.DbContext.Cards.AnyAsync(x => x.Id == card.Id));
    }

    [Fact]
    public async Task RestoreAsync_ClearsDeletedAtForBoardColumnsAndCards()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Board restore project", ProjectType.Team);
        var now = DateTime.UtcNow;

        var board = new Board
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Restore board",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        var column = new Column
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "Restore column",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        var card = new Card
        {
            Id = Guid.NewGuid(),
            ColumnId = column.Id,
            Title = "Restore card",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        fixture.DbContext.Boards.Add(board);
        fixture.DbContext.Columns.Add(column);
        fixture.DbContext.Cards.Add(card);
        await fixture.DbContext.SaveChangesAsync();

        await fixture.BoardService.ArchiveAsync(board.Id, ownerId);
        await fixture.BoardService.RestoreAsync(board.Id, ownerId);

        var restoredBoard = await fixture.DbContext.Boards.SingleAsync(x => x.Id == board.Id);
        var restoredColumn = await fixture.DbContext.Columns.SingleAsync(x => x.Id == column.Id);
        var restoredCard = await fixture.DbContext.Cards.SingleAsync(x => x.Id == card.Id);

        Assert.Null(restoredBoard.DeletedAt);
        Assert.Null(restoredColumn.DeletedAt);
        Assert.Null(restoredCard.DeletedAt);
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"board-service-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        var projectService = new ProjectService(dbContext);
        var boardService = new BoardService(dbContext);

        return new TestFixture(projectService, boardService, dbContext);
    }

    private sealed record TestFixture(ProjectService ProjectService, BoardService BoardService, ApplicationDbContext DbContext);
}
