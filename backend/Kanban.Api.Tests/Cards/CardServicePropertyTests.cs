using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Boards;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Columns;
using Kanban.Api.Services.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Cards;

public class CardServicePropertyTests
{
    [Fact]
    public async Task Property_33_CreateWithoutTitleReturnsError()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card create validation project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("", "desc", null)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("   ", "desc", null)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto(null!, "desc", null)));
    }

    [Fact]
    public async Task Property_34_MarkdownDescriptionIsStoredAndReturnedCorrectly()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card markdown project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var markdown = "# Heading\n\n- item 1\n- item 2\n\n**bold** and `code`";

        var created = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Markdown card", markdown, 2.5m));

        var queried = await fixture.CardService.GetByIdAsync(created.Id, ownerId);

        Assert.Equal(markdown, created.Description);
        Assert.Equal(markdown, queried.Description);
    }

    [Fact]
    public async Task Property_38_UpdatePersistsChangesForSubsequentQuery()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card update persistence project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Original", "initial", 1.25m));

        var updated = await fixture.CardService.UpdateAsync(
            card.Id,
            ownerId,
            new UpdateCardDto("Renamed", "updated **markdown**", 4.5m, card.Version));

        var queried = await fixture.CardService.GetByIdAsync(card.Id, ownerId);

        Assert.Equal("Renamed", updated.Title);
        Assert.Equal("updated **markdown**", updated.Description);
        Assert.Equal(4.5m, updated.PlannedDurationHours);
        Assert.Equal("Renamed", queried.Title);
        Assert.Equal("updated **markdown**", queried.Description);
        Assert.Equal(4.5m, queried.PlannedDurationHours);
    }

    [Fact]
    public async Task Property_39_CreateSetsTimestampsAndUpdateChangesUpdatedAt()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card timestamp project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var created = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Timestamp card", "v1", 1m));

        var createdAt = created.CreatedAt;
        var updatedAtBeforeEdit = created.UpdatedAt;

        await Task.Delay(10);

        var updated = await fixture.CardService.UpdateAsync(
            created.Id,
            ownerId,
            new UpdateCardDto("Timestamp card v2", "v2", 2m, created.Version));

        Assert.NotEqual(default, createdAt);
        Assert.NotEqual(default, updatedAtBeforeEdit);
        Assert.Equal(createdAt, updatedAtBeforeEdit);
        Assert.True(updated.UpdatedAt > updatedAtBeforeEdit);
    }

    [Fact]
    public async Task Property_40_ArchiveSetsDeletedAtOnCardAttachmentsAndSubtasks()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card archive cascade project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Archive me", "card", null));

        var now = DateTime.UtcNow;
        fixture.DbContext.Attachments.Add(new Attachment
        {
            Id = Guid.NewGuid(),
            CardId = card.Id,
            Filename = "doc.md",
            FileSize = 128,
            StorageKey = "attachments/doc.md",
            MimeType = "text/markdown",
            UploadedBy = ownerId,
            UploadedAt = now
        });
        fixture.DbContext.Subtasks.Add(new Subtask
        {
            Id = Guid.NewGuid(),
            CardId = card.Id,
            Description = "Subtask 1",
            Completed = false,
            Position = 1000,
            CreatedAt = now,
            UpdatedAt = now
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.CardService.ArchiveAsync(card.Id, ownerId);

        var archivedCard = await fixture.DbContext.Cards
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == card.Id);
        var archivedAttachment = await fixture.DbContext.Attachments
            .IgnoreQueryFilters()
            .SingleAsync(x => x.CardId == card.Id);
        var archivedSubtask = await fixture.DbContext.Subtasks
            .IgnoreQueryFilters()
            .SingleAsync(x => x.CardId == card.Id);

        Assert.NotNull(archivedCard.DeletedAt);
        Assert.NotNull(archivedAttachment.DeletedAt);
        Assert.NotNull(archivedSubtask.DeletedAt);
    }

    [Fact]
    public async Task Property_41_ArchivedCardIsExcludedFromNormalQueries()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card archived filter project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Archived card", "to archive", null));

        await fixture.CardService.ArchiveAsync(card.Id, ownerId);

        Assert.False(await fixture.DbContext.Cards.AnyAsync(x => x.Id == card.Id));

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => fixture.CardService.GetByIdAsync(card.Id, ownerId));
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"card-service-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        var projectService = new ProjectService(dbContext);
        var boardService = new BoardService(dbContext);
        var columnService = new ColumnService(dbContext);
        var cardService = new CardService(dbContext);

        return new TestFixture(projectService, boardService, columnService, cardService, dbContext);
    }

    private sealed record TestFixture(
        ProjectService ProjectService,
        BoardService BoardService,
        ColumnService ColumnService,
        CardService CardService,
        ApplicationDbContext DbContext);
}