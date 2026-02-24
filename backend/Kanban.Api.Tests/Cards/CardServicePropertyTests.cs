using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Boards;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Columns;
using Kanban.Api.Services.Projects;
using Microsoft.Data.Sqlite;
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
    public async Task Property_35_TagAssignmentSupportsZeroOneAndManyTags()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card tag assignment project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Taggable card", "desc", null));

        var tag1 = new Tag
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "bug",
            Color = "#ff0000",
            CreatedAt = DateTime.UtcNow
        };
        var tag2 = new Tag
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "feature",
            Color = "#00ff00",
            CreatedAt = DateTime.UtcNow
        };
        var tag3 = new Tag
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "urgent",
            Color = "#0000ff",
            CreatedAt = DateTime.UtcNow
        };

        fixture.DbContext.Tags.AddRange(tag1, tag2, tag3);
        await fixture.DbContext.SaveChangesAsync();

        var withZeroTags = await fixture.CardService.GetByIdAsync(card.Id, ownerId);
        Assert.Empty(withZeroTags.CardTags);

        await fixture.CardService.AssignTagAsync(card.Id, tag1.Id, ownerId);
        var withOneTag = await fixture.CardService.GetByIdAsync(card.Id, ownerId);
        Assert.Single(withOneTag.CardTags);

        await fixture.CardService.AssignTagAsync(card.Id, tag2.Id, ownerId);
        await fixture.CardService.AssignTagAsync(card.Id, tag3.Id, ownerId);
        var withManyTags = await fixture.CardService.GetByIdAsync(card.Id, ownerId);

        Assert.Equal(3, withManyTags.CardTags.Count);
        Assert.Contains(withManyTags.CardTags, x => x.TagId == tag1.Id);
        Assert.Contains(withManyTags.CardTags, x => x.TagId == tag2.Id);
        Assert.Contains(withManyTags.CardTags, x => x.TagId == tag3.Id);
    }

    [Fact]
    public async Task Property_36_UserAssignmentSupportsZeroOneAndManyUsers()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        fixture.DbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = ownerId,
                Email = "owner@example.test",
                UserName = "owner@example.test",
                NormalizedEmail = "OWNER@EXAMPLE.TEST",
                NormalizedUserName = "OWNER@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = user1Id,
                Email = "user1@example.test",
                UserName = "user1@example.test",
                NormalizedEmail = "USER1@EXAMPLE.TEST",
                NormalizedUserName = "USER1@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = user2Id,
                Email = "user2@example.test",
                UserName = "user2@example.test",
                NormalizedEmail = "USER2@EXAMPLE.TEST",
                NormalizedUserName = "USER2@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card user assignment project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Assignable card", "desc", null));

        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = user1Id,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = user2Id,
                Role = ProjectRole.Viewer,
                JoinedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var withZeroAssignments = await fixture.CardService.GetByIdAsync(card.Id, ownerId);
        Assert.Empty(withZeroAssignments.Assignments);

        await fixture.CardService.AssignUserAsync(card.Id, user1Id, ownerId);
        var withOneAssignment = await fixture.CardService.GetByIdAsync(card.Id, ownerId);
        Assert.Single(withOneAssignment.Assignments);

        await fixture.CardService.AssignUserAsync(card.Id, user2Id, ownerId);
        await fixture.CardService.AssignUserAsync(card.Id, ownerId, ownerId);
        var withManyAssignments = await fixture.CardService.GetByIdAsync(card.Id, ownerId);

        Assert.Equal(3, withManyAssignments.Assignments.Count);
        Assert.Contains(withManyAssignments.Assignments, x => x.UserId == ownerId);
        Assert.Contains(withManyAssignments.Assignments, x => x.UserId == user1Id);
        Assert.Contains(withManyAssignments.Assignments, x => x.UserId == user2Id);
    }

    [Fact]
    public async Task Property_37_PlannedDurationCanBeSetUpdatedAndCleared()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card duration property project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var created = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Duration card", "desc", null));
        Assert.Null(created.PlannedDurationHours);

        var withSetDuration = await fixture.CardService.UpdateAsync(
            created.Id,
            ownerId,
            new UpdateCardDto(created.Title, created.Description, 2.5m, created.Version));
        Assert.Equal(2.5m, withSetDuration.PlannedDurationHours);

        var withUpdatedDuration = await fixture.CardService.UpdateAsync(
            withSetDuration.Id,
            ownerId,
            new UpdateCardDto(withSetDuration.Title, withSetDuration.Description, 6.75m, withSetDuration.Version));
        Assert.Equal(6.75m, withUpdatedDuration.PlannedDurationHours);

        var withClearedDuration = await fixture.CardService.UpdateAsync(
            withUpdatedDuration.Id,
            ownerId,
            new UpdateCardDto(withUpdatedDuration.Title, withUpdatedDuration.Description, null, withUpdatedDuration.Version));
        Assert.Null(withClearedDuration.PlannedDurationHours);

        var queried = await fixture.CardService.GetByIdAsync(created.Id, ownerId);
        Assert.Null(queried.PlannedDurationHours);
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

    [Fact]
    public async Task Property_42_MoveToDifferentColumnUpdatesCardColumnId()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card movement project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var sourceColumn = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var targetColumn = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Doing");

        var firstInTarget = await fixture.CardService.CreateAsync(
            targetColumn.Id,
            ownerId,
            new CreateCardDto("Existing target card", "desc", null));
        var movable = await fixture.CardService.CreateAsync(
            sourceColumn.Id,
            ownerId,
            new CreateCardDto("Movable", "desc", null));

        var moved = await fixture.CardService.MoveAsync(
            movable.Id,
            ownerId,
            new MoveCardDto(targetColumn.Id, 1));

        var persisted = await fixture.CardService.GetByIdAsync(movable.Id, ownerId);

        Assert.Equal(targetColumn.Id, moved.ColumnId);
        Assert.Equal(targetColumn.Id, persisted.ColumnId);
        Assert.True(moved.UpdatedAt >= movable.UpdatedAt);
        Assert.True(moved.Position > firstInTarget.Position);
    }

    [Fact]
    public async Task Property_43_ReorderWithinColumnUsesGapBasedMidpoint()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card reorder midpoint project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var first = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("First", null, null));
        var second = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Second", null, null));
        var third = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Third", null, null));

        var moved = await fixture.CardService.MoveAsync(
            third.Id,
            ownerId,
            new MoveCardDto(column.Id, 1));

        var expectedPosition = (first.Position + second.Position) / 2;
        var ordered = await fixture.DbContext.Cards
            .Where(x => x.ColumnId == column.Id)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(expectedPosition, moved.Position);
        Assert.Collection(
            ordered,
            card => Assert.Equal(first.Id, card.Id),
            card => Assert.Equal(third.Id, card.Id),
            card => Assert.Equal(second.Id, card.Id));
    }

    [Fact]
    public async Task Property_43_Integration_CollisionTriggersRenumberWithStableOrdering()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var projectService = new ProjectService(dbContext);
        var boardService = new BoardService(dbContext);
        var columnService = new ColumnService(dbContext);
        var cardService = new CardService(dbContext);

        Assert.True(dbContext.Database.IsRelational());

        var ownerId = Guid.NewGuid();
        dbContext.Users.Add(new ApplicationUser
        {
            Id = ownerId,
            Email = "owner@example.test",
            UserName = "owner@example.test",
            NormalizedEmail = "OWNER@EXAMPLE.TEST",
            NormalizedUserName = "OWNER@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var project = await projectService.CreateAsync(ownerId, "Card reorder collision project", ProjectType.Team);
        var board = await boardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await columnService.CreateAsync(board.Id, ownerId, "Todo");

        var now = DateTime.UtcNow;
        var before = new Card
        {
            Id = Guid.NewGuid(),
            ColumnId = column.Id,
            Title = "Before",
            Position = 1000,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = ownerId
        };
        var after = new Card
        {
            Id = Guid.NewGuid(),
            ColumnId = column.Id,
            Title = "After",
            Position = 1001,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = ownerId
        };
        var movable = new Card
        {
            Id = Guid.NewGuid(),
            ColumnId = column.Id,
            Title = "Movable",
            Position = 9000,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = ownerId
        };

        dbContext.Cards.AddRange(before, after, movable);
        await dbContext.SaveChangesAsync();

        var moved = await cardService.MoveAsync(
            movable.Id,
            ownerId,
            new MoveCardDto(column.Id, 1));

        var ordered = await dbContext.Cards
            .Where(x => x.ColumnId == column.Id)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync();

        Assert.Collection(
            ordered,
            card =>
            {
                Assert.Equal(before.Id, card.Id);
                Assert.Equal(1000, card.Position);
            },
            card =>
            {
                Assert.Equal(movable.Id, card.Id);
                Assert.Equal(2000, card.Position);
            },
            card =>
            {
                Assert.Equal(after.Id, card.Id);
                Assert.Equal(3000, card.Position);
            });

        Assert.Equal(2000, moved.Position);
    }

    [Fact]
    public async Task Property_48_CreateSubtaskWithDescriptionPersistsAssociation()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Subtask creation project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card", null, null));

        var created = await fixture.CardService.CreateSubtaskAsync(
            card.Id,
            ownerId,
            new CreateSubtaskDto("  Write tests  "));

        var persisted = await fixture.DbContext.Subtasks
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(card.Id, created.CardId);
        Assert.Equal("Write tests", created.Description);
        Assert.Equal(card.Id, persisted.CardId);
        Assert.False(created.Completed);
    }

    [Fact]
    public async Task Property_49_ToggleCompletedUpdatesAndCanRevert()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Subtask update project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card", null, null));

        var created = await fixture.CardService.CreateSubtaskAsync(
            card.Id,
            ownerId,
            new CreateSubtaskDto("Draft doc"));
        var createdUpdatedAt = created.UpdatedAt;

        await Task.Delay(10);

        var toggledOn = await fixture.CardService.UpdateSubtaskAsync(
            created.Id,
            ownerId,
            new UpdateSubtaskDto("Final doc", true));

        Assert.Equal("Final doc", toggledOn.Description);
        Assert.True(toggledOn.Completed);
        Assert.True(toggledOn.UpdatedAt > createdUpdatedAt);
        var toggledOnUpdatedAt = toggledOn.UpdatedAt;

        await Task.Delay(10);

        var toggledOff = await fixture.CardService.UpdateSubtaskAsync(
            created.Id,
            ownerId,
            new UpdateSubtaskDto(null, false));

        var persisted = await fixture.DbContext.Subtasks
            .SingleAsync(x => x.Id == created.Id);

        Assert.False(toggledOff.Completed);
        Assert.True(toggledOff.UpdatedAt > toggledOnUpdatedAt);
        Assert.Equal("Final doc", persisted.Description);
        Assert.False(persisted.Completed);
    }

    [Fact]
    public async Task Property_50_DeleteSubtaskRemovesItFromQueries()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Subtask delete project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card", null, null));

        var created = await fixture.CardService.CreateSubtaskAsync(
            card.Id,
            ownerId,
            new CreateSubtaskDto("Delete me"));

        var beforeDelete = await fixture.CardService.GetByIdAsync(card.Id, ownerId);
        Assert.Contains(beforeDelete.Subtasks, x => x.Id == created.Id);

        await fixture.CardService.DeleteSubtaskAsync(created.Id, ownerId);

        var afterDelete = await fixture.CardService.GetByIdAsync(card.Id, ownerId);
        var counts = await fixture.CardService.GetSubtaskCountsAsync(card.Id, ownerId);

        Assert.False(await fixture.DbContext.Subtasks.IgnoreQueryFilters().AnyAsync(x => x.Id == created.Id));
        Assert.DoesNotContain(afterDelete.Subtasks, x => x.Id == created.Id);
        Assert.Equal(0, counts.Completed);
        Assert.Equal(0, counts.Total);
    }

    [Fact]
    public async Task Property_51_GetSubtaskCountsReturnsCompletedAndTotal()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Subtask count project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card", null, null));

        var first = await fixture.CardService.CreateSubtaskAsync(card.Id, ownerId, new CreateSubtaskDto("One"));
        var second = await fixture.CardService.CreateSubtaskAsync(card.Id, ownerId, new CreateSubtaskDto("Two"));
        await fixture.CardService.CreateSubtaskAsync(card.Id, ownerId, new CreateSubtaskDto("Three"));

        await fixture.CardService.UpdateSubtaskAsync(first.Id, ownerId, new UpdateSubtaskDto(null, true));
        await fixture.CardService.UpdateSubtaskAsync(second.Id, ownerId, new UpdateSubtaskDto(null, true));

        var counts = await fixture.CardService.GetSubtaskCountsAsync(card.Id, ownerId);

        Assert.Equal(2, counts.Completed);
        Assert.Equal(3, counts.Total);
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