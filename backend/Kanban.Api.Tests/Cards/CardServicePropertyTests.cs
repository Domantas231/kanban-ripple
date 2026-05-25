using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Boards;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Columns;
using Kanban.Api.Services.Notifications;
using Kanban.Api.Services.Projects;
using Kanban.Api.Tests.TestDoubles;
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
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card create validation project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("", "desc", null, null, null)));

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("   ", "desc", null, null, null)));

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto(null!, "desc", null, null, null)));
    }

    [Fact]
    public async Task Property_34_MarkdownDescriptionIsStoredAndReturnedCorrectly()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card markdown project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var markdown = "# Heading\n\n- item 1\n- item 2\n\n**bold** and `code`";

        var created = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Markdown card", markdown, null, null, null));

        var queried = await fixture.CardService.GetByIdAsync(created.Id, ownerId);

        Assert.Equal(markdown, created.Description);
        Assert.Equal(markdown, queried.Description);
    }

    [Fact]
    public async Task Property_35_TagAssignmentSupportsZeroOneAndManyTags()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card tag assignment project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Taggable card", "desc", null, null, null));

        var tag1 = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "bug",
            Color = "#ff0000",
            CreatedAt = DateTime.UtcNow
        };
        var tag2 = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "feature",
            Color = "#00ff00",
            CreatedAt = DateTime.UtcNow
        };
        var tag3 = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
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

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card user assignment project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Assignable card", "desc", null, null, null));

        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = board.ProjectId,
                UserId = user1Id,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = board.ProjectId,
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
    public async Task Property_63_AssigningUserCreatesNotificationAndSelfAssignmentDoesNot()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();

        fixture.DbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = ownerId,
                Email = "owner-notify@example.test",
                UserName = "owner-notify@example.test",
                NormalizedEmail = "OWNER-NOTIFY@EXAMPLE.TEST",
                NormalizedUserName = "OWNER-NOTIFY@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = assigneeId,
                Email = "assignee-notify@example.test",
                UserName = "assignee-notify@example.test",
                NormalizedEmail = "ASSIGNEE-NOTIFY@EXAMPLE.TEST",
                NormalizedUserName = "ASSIGNEE-NOTIFY@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card notification assignment project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Notify card", "desc", null, null, null));

        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = board.ProjectId,
            UserId = assigneeId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.CardService.AssignUserAsync(card.Id, assigneeId, ownerId);
        await fixture.CardService.AssignUserAsync(card.Id, ownerId, ownerId);

        var assigneeNotifications = await fixture.DbContext.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == assigneeId)
            .ToListAsync();
        var ownerNotifications = await fixture.DbContext.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == ownerId)
            .ToListAsync();

        var notification = Assert.Single(assigneeNotifications);
        Assert.Equal(NotificationType.Assigned, notification.Type);
        Assert.Equal(card.Id, notification.EntityId);
        Assert.Equal(EntityType.Card, notification.EntityType);
        Assert.Equal(ownerId, notification.CreatedBy);
        Assert.Contains(card.Title, notification.Title);
        Assert.Contains("owner-notify@example.test", notification.Message);
        Assert.Contains(card.Title, notification.Message);
        Assert.Empty(ownerNotifications);
    }

    [Fact]
    public async Task Property_37_StartDateAndDueDateCanBeSetUpdatedAndCleared()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card dates property project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var created = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Dates card", "desc", null, null, null));
        Assert.Null(created.StartDate);
        Assert.Null(created.DueDate);

        var startDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

        var withSetDates = await fixture.CardService.UpdateAsync(
            created.Id,
            ownerId,
            new UpdateCardDto(created.Title, created.Description, startDate, dueDate, null, created.Version));
        Assert.Equal(startDate, withSetDates.StartDate);
        Assert.Equal(dueDate, withSetDates.DueDate);

        var newDueDate = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        var withUpdatedDates = await fixture.CardService.UpdateAsync(
            withSetDates.Id,
            ownerId,
            new UpdateCardDto(withSetDates.Title, withSetDates.Description, startDate, newDueDate, null, withSetDates.Version));
        Assert.Equal(newDueDate, withUpdatedDates.DueDate);

        var withClearedDates = await fixture.CardService.UpdateAsync(
            withUpdatedDates.Id,
            ownerId,
            new UpdateCardDto(withUpdatedDates.Title, withUpdatedDates.Description, null, null, null, withUpdatedDates.Version));
        Assert.Null(withClearedDates.StartDate);
        Assert.Null(withClearedDates.DueDate);

        var queried = await fixture.CardService.GetByIdAsync(created.Id, ownerId);
        Assert.Null(queried.StartDate);
        Assert.Null(queried.DueDate);
    }

    [Fact]
    public async Task Property_38_UpdatePersistsChangesForSubsequentQuery()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card update persistence project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var startDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Original", "initial", startDate, dueDate, null));

        var newDueDate = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
        var updated = await fixture.CardService.UpdateAsync(
            card.Id,
            ownerId,
            new UpdateCardDto("Renamed", "updated **markdown**", startDate, newDueDate, null, card.Version));

        var queried = await fixture.CardService.GetByIdAsync(card.Id, ownerId);

        Assert.Equal("Renamed", updated.Title);
        Assert.Equal("updated **markdown**", updated.Description);
        Assert.Equal(newDueDate, updated.DueDate);
        Assert.Equal("Renamed", queried.Title);
        Assert.Equal("updated **markdown**", queried.Description);
        Assert.Equal(newDueDate, queried.DueDate);
    }

    [Fact]
    public async Task Property_39_CreateSetsTimestampsAndUpdateChangesUpdatedAt()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card timestamp project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var created = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Timestamp card", "v1", null, null, null));

        var createdAt = created.CreatedAt;
        var updatedAtBeforeEdit = created.UpdatedAt;

        await Task.Delay(10);

        var updated = await fixture.CardService.UpdateAsync(
            created.Id,
            ownerId,
            new UpdateCardDto("Timestamp card v2", "v2", null, null, null, created.Version));

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
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card archive cascade project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Archive me", "card", null, null, null));

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
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card archived filter project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto("Archived card", "to archive", null, null, null));

        await fixture.CardService.ArchiveAsync(card.Id, ownerId);

        Assert.False(await fixture.DbContext.Cards.AnyAsync(x => x.Id == card.Id));

        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.CardService.GetByIdAsync(card.Id, ownerId));
    }

    [Fact]
    public async Task Property_42_MoveToDifferentColumnUpdatesCardColumnId()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card movement project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var sourceColumn = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var targetColumn = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Doing");

        var firstInTarget = await fixture.CardService.CreateAsync(
            targetColumn.Id,
            ownerId,
            new CreateCardDto("Existing target card", "desc", null, null, null));
        var movable = await fixture.CardService.CreateAsync(
            sourceColumn.Id,
            ownerId,
            new CreateCardDto("Movable", "desc", null, null, null));

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
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Card reorder midpoint project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var first = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("First", null, null, null, null));
        var second = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Second", null, null, null, null));
        var third = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Third", null, null, null, null));

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

        var projectService = TestServiceBuilder.BuildProjectService(dbContext);
        var boardService = TestServiceBuilder.BuildBoardService(dbContext);
        var columnService = TestServiceBuilder.BuildColumnService(dbContext);
        var notificationService = new NotificationService(dbContext, new NoOpProjectBroadcaster());
        var cardService = TestServiceBuilder.BuildCardService(dbContext, notificationService);

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

        var project = await projectService.CreateAsync(ownerId, "Card reorder collision project");
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
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Subtask creation project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card", null, null, null, null));

        var created = await fixture.CardService.CreateSubtaskAsync(
            card.Id,
            ownerId,
            new CreateSubtaskDto("  Write tests  ", null));

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
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Subtask update project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card", null, null, null, null));

        var created = await fixture.CardService.CreateSubtaskAsync(
            card.Id,
            ownerId,
            new CreateSubtaskDto("Draft doc", null));
        var createdUpdatedAt = created.UpdatedAt;

        await Task.Delay(10);

        var toggledOn = await fixture.CardService.UpdateSubtaskAsync(
            created.Id,
            ownerId,
            new UpdateSubtaskDto("Final doc", true, null));

        Assert.Equal("Final doc", toggledOn.Description);
        Assert.True(toggledOn.Completed);
        Assert.True(toggledOn.UpdatedAt > createdUpdatedAt);
        var toggledOnUpdatedAt = toggledOn.UpdatedAt;

        await Task.Delay(10);

        var toggledOff = await fixture.CardService.UpdateSubtaskAsync(
            created.Id,
            ownerId,
            new UpdateSubtaskDto(null, false, null));

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
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Subtask delete project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card", null, null, null, null));

        var created = await fixture.CardService.CreateSubtaskAsync(
            card.Id,
            ownerId,
            new CreateSubtaskDto("Delete me", null));

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
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Subtask count project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card", null, null, null, null));

        var first = await fixture.CardService.CreateSubtaskAsync(card.Id, ownerId, new CreateSubtaskDto("One", null));
        var second = await fixture.CardService.CreateSubtaskAsync(card.Id, ownerId, new CreateSubtaskDto("Two", null));
        await fixture.CardService.CreateSubtaskAsync(card.Id, ownerId, new CreateSubtaskDto("Three", null));

        await fixture.CardService.UpdateSubtaskAsync(first.Id, ownerId, new UpdateSubtaskDto(null, true, null));
        await fixture.CardService.UpdateSubtaskAsync(second.Id, ownerId, new UpdateSubtaskDto(null, true, null));

        var counts = await fixture.CardService.GetSubtaskCountsAsync(card.Id, ownerId);

        Assert.Equal(2, counts.Completed);
        Assert.Equal(3, counts.Total);
    }

    [Fact]
    public async Task Property_54_FilterByTagsReturnsOnlyCardsWithAtLeastOneMatchingTag()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Filter tags project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var tagA = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "A",
            Color = "#111111",
            CreatedAt = DateTime.UtcNow
        };
        var tagB = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "B",
            Color = "#222222",
            CreatedAt = DateTime.UtcNow
        };
        var tagC = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "C",
            Color = "#333333",
            CreatedAt = DateTime.UtcNow
        };

        fixture.DbContext.Tags.AddRange(tagA, tagB, tagC);
        await fixture.DbContext.SaveChangesAsync();

        var matchA = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Match A", null, null, null, null));
        await fixture.CardService.AssignTagAsync(matchA.Id, tagA.Id, ownerId);

        var matchB = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Match B", null, null, null, null));
        await fixture.CardService.AssignTagAsync(matchB.Id, tagB.Id, ownerId);

        var matchAny = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Match any", null, null, null, null));
        await fixture.CardService.AssignTagAsync(matchAny.Id, tagA.Id, ownerId);
        await fixture.CardService.AssignTagAsync(matchAny.Id, tagC.Id, ownerId);

        var nonMatch = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Non-match", null, null, null, null));
        await fixture.CardService.AssignTagAsync(nonMatch.Id, tagC.Id, ownerId);

        _ = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("No tags", null, null, null, null));

        var filtered = await fixture.CardService.FilterAsync(
            board.Id,
            ownerId,
            new FilterCriteria(new[] { tagA.Id, tagB.Id }, Array.Empty<Guid>(), Array.Empty<Guid>()));

        var resultIds = filtered.Select(x => x.Id).ToHashSet();
        Assert.Equal(3, filtered.Count);
        Assert.Contains(matchA.Id, resultIds);
        Assert.Contains(matchB.Id, resultIds);
        Assert.Contains(matchAny.Id, resultIds);
        Assert.DoesNotContain(nonMatch.Id, resultIds);
    }

    [Fact]
    public async Task Property_55_FilterByUsersReturnsOnlyCardsAssignedToAtLeastOneMatchingUser()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var assigneeXId = Guid.NewGuid();
        var assigneeYId = Guid.NewGuid();
        var assigneeZId = Guid.NewGuid();

        fixture.DbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = ownerId,
                Email = "filter-owner@example.test",
                UserName = "filter-owner@example.test",
                NormalizedEmail = "FILTER-OWNER@EXAMPLE.TEST",
                NormalizedUserName = "FILTER-OWNER@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = assigneeXId,
                Email = "filter-x@example.test",
                UserName = "filter-x@example.test",
                NormalizedEmail = "FILTER-X@EXAMPLE.TEST",
                NormalizedUserName = "FILTER-X@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = assigneeYId,
                Email = "filter-y@example.test",
                UserName = "filter-y@example.test",
                NormalizedEmail = "FILTER-Y@EXAMPLE.TEST",
                NormalizedUserName = "FILTER-Y@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = assigneeZId,
                Email = "filter-z@example.test",
                UserName = "filter-z@example.test",
                NormalizedEmail = "FILTER-Z@EXAMPLE.TEST",
                NormalizedUserName = "FILTER-Z@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Filter users project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = board.ProjectId,
                UserId = assigneeXId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = board.ProjectId,
                UserId = assigneeYId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = board.ProjectId,
                UserId = assigneeZId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var matchX = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Match X", null, null, null, null));
        await fixture.CardService.AssignUserAsync(matchX.Id, assigneeXId, ownerId);

        var matchY = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Match Y", null, null, null, null));
        await fixture.CardService.AssignUserAsync(matchY.Id, assigneeYId, ownerId);

        var matchAny = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Match any user", null, null, null, null));
        await fixture.CardService.AssignUserAsync(matchAny.Id, assigneeXId, ownerId);
        await fixture.CardService.AssignUserAsync(matchAny.Id, assigneeYId, ownerId);

        var nonMatch = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Non-match", null, null, null, null));
        await fixture.CardService.AssignUserAsync(nonMatch.Id, assigneeZId, ownerId);

        _ = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Unassigned", null, null, null, null));

        var filtered = await fixture.CardService.FilterAsync(
            board.Id,
            ownerId,
            new FilterCriteria(Array.Empty<Guid>(), new[] { assigneeXId, assigneeYId }, Array.Empty<Guid>()));

        var resultIds = filtered.Select(x => x.Id).ToHashSet();
        Assert.Equal(3, filtered.Count);
        Assert.Contains(matchX.Id, resultIds);
        Assert.Contains(matchY.Id, resultIds);
        Assert.Contains(matchAny.Id, resultIds);
        Assert.DoesNotContain(nonMatch.Id, resultIds);
    }

    [Fact]
    public async Task Property_56_FilterByColumnsReturnsOnlyCardsInMatchingColumns()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Filter columns project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");

        var todo = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var doing = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Doing");
        var done = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Done");

        var matchTodo = await fixture.CardService.CreateAsync(todo.Id, ownerId, new CreateCardDto("In todo", null, null, null, null));
        var matchDoing = await fixture.CardService.CreateAsync(doing.Id, ownerId, new CreateCardDto("In doing", null, null, null, null));
        var nonMatch = await fixture.CardService.CreateAsync(done.Id, ownerId, new CreateCardDto("In done", null, null, null, null));

        var filtered = await fixture.CardService.FilterAsync(
            board.Id,
            ownerId,
            new FilterCriteria(Array.Empty<Guid>(), Array.Empty<Guid>(), new[] { todo.Id, doing.Id }));

        var resultIds = filtered.Select(x => x.Id).ToHashSet();
        Assert.Equal(2, filtered.Count);
        Assert.Contains(matchTodo.Id, resultIds);
        Assert.Contains(matchDoing.Id, resultIds);
        Assert.DoesNotContain(nonMatch.Id, resultIds);
    }

    [Fact]
    public async Task Property_57_FilterCombinedUsesAndAcrossFilterTypes()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var assigneeXId = Guid.NewGuid();
        var assigneeYId = Guid.NewGuid();

        fixture.DbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = ownerId,
                Email = "filter-owner@example.test",
                UserName = "filter-owner@example.test",
                NormalizedEmail = "FILTER-OWNER@EXAMPLE.TEST",
                NormalizedUserName = "FILTER-OWNER@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = assigneeXId,
                Email = "filter-x@example.test",
                UserName = "filter-x@example.test",
                NormalizedEmail = "FILTER-X@EXAMPLE.TEST",
                NormalizedUserName = "FILTER-X@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = assigneeYId,
                Email = "filter-y@example.test",
                UserName = "filter-y@example.test",
                NormalizedEmail = "FILTER-Y@EXAMPLE.TEST",
                NormalizedUserName = "FILTER-Y@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Filter combined project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var columnTodo = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var columnDoing = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Doing");

        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = board.ProjectId,
                UserId = assigneeXId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = board.ProjectId,
                UserId = assigneeYId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var tagA = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "A",
            Color = "#111111",
            CreatedAt = DateTime.UtcNow
        };
        var tagB = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "B",
            Color = "#222222",
            CreatedAt = DateTime.UtcNow
        };
        var tagC = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "C",
            Color = "#333333",
            CreatedAt = DateTime.UtcNow
        };

        fixture.DbContext.Tags.AddRange(tagA, tagB, tagC);
        await fixture.DbContext.SaveChangesAsync();

        var matchByTagA = await fixture.CardService.CreateAsync(columnTodo.Id, ownerId, new CreateCardDto("Match A", null, null, null, null));
        await fixture.CardService.AssignTagAsync(matchByTagA.Id, tagA.Id, ownerId);
        await fixture.CardService.AssignUserAsync(matchByTagA.Id, assigneeXId, ownerId);

        var matchByTagB = await fixture.CardService.CreateAsync(columnTodo.Id, ownerId, new CreateCardDto("Match B", null, null, null, null));
        await fixture.CardService.AssignTagAsync(matchByTagB.Id, tagB.Id, ownerId);
        await fixture.CardService.AssignUserAsync(matchByTagB.Id, assigneeXId, ownerId);

        var wrongUser = await fixture.CardService.CreateAsync(columnTodo.Id, ownerId, new CreateCardDto("Wrong user", null, null, null, null));
        await fixture.CardService.AssignTagAsync(wrongUser.Id, tagA.Id, ownerId);
        await fixture.CardService.AssignUserAsync(wrongUser.Id, assigneeYId, ownerId);

        var wrongTag = await fixture.CardService.CreateAsync(columnTodo.Id, ownerId, new CreateCardDto("Wrong tag", null, null, null, null));
        await fixture.CardService.AssignTagAsync(wrongTag.Id, tagC.Id, ownerId);
        await fixture.CardService.AssignUserAsync(wrongTag.Id, assigneeXId, ownerId);

        var wrongColumn = await fixture.CardService.CreateAsync(columnDoing.Id, ownerId, new CreateCardDto("Wrong column", null, null, null, null));
        await fixture.CardService.AssignTagAsync(wrongColumn.Id, tagA.Id, ownerId);
        await fixture.CardService.AssignUserAsync(wrongColumn.Id, assigneeXId, ownerId);

        var filtered = await fixture.CardService.FilterAsync(
            board.Id,
            ownerId,
            new FilterCriteria(
                new[] { tagA.Id, tagB.Id },
                new[] { assigneeXId },
                new[] { columnTodo.Id }));

        var resultIds = filtered.Select(x => x.Id).ToHashSet();

        Assert.Equal(2, filtered.Count);
        Assert.Contains(matchByTagA.Id, resultIds);
        Assert.Contains(matchByTagB.Id, resultIds);
        Assert.DoesNotContain(wrongUser.Id, resultIds);
        Assert.DoesNotContain(wrongTag.Id, resultIds);
        Assert.DoesNotContain(wrongColumn.Id, resultIds);
    }

    [Fact]
    public async Task Property_58_ClearFiltersReturnsAllBoardCardsRoundTrip()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();

        fixture.DbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = ownerId,
                Email = "clear-owner@example.test",
                UserName = "clear-owner@example.test",
                NormalizedEmail = "CLEAR-OWNER@EXAMPLE.TEST",
                NormalizedUserName = "CLEAR-OWNER@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationUser
            {
                Id = assigneeId,
                Email = "clear-assignee@example.test",
                UserName = "clear-assignee@example.test",
                NormalizedEmail = "CLEAR-ASSIGNEE@EXAMPLE.TEST",
                NormalizedUserName = "CLEAR-ASSIGNEE@EXAMPLE.TEST",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Filter clear project");
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var otherBoard = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Other board");
        var todo = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var doing = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Doing");
        var otherColumn = await fixture.ColumnService.CreateAsync(otherBoard.Id, ownerId, "Other");

        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = board.ProjectId,
            UserId = assigneeId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        });

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "filter-tag",
            Color = "#123456",
            CreatedAt = DateTime.UtcNow
        };
        fixture.DbContext.Tags.Add(tag);
        await fixture.DbContext.SaveChangesAsync();

        var match = await fixture.CardService.CreateAsync(todo.Id, ownerId, new CreateCardDto("Match", null, null, null, null));
        await fixture.CardService.AssignTagAsync(match.Id, tag.Id, ownerId);
        await fixture.CardService.AssignUserAsync(match.Id, assigneeId, ownerId);

        var boardOnlyA = await fixture.CardService.CreateAsync(todo.Id, ownerId, new CreateCardDto("Board only A", null, null, null, null));
        var boardOnlyB = await fixture.CardService.CreateAsync(doing.Id, ownerId, new CreateCardDto("Board only B", null, null, null, null));
        _ = await fixture.CardService.CreateAsync(otherColumn.Id, ownerId, new CreateCardDto("Outside board", null, null, null, null));

        var filtered = await fixture.CardService.FilterAsync(
            board.Id,
            ownerId,
            new FilterCriteria(new[] { tag.Id }, new[] { assigneeId }, new[] { todo.Id }));

        var cleared = await fixture.CardService.FilterAsync(
            board.Id,
            ownerId,
            new FilterCriteria(Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>()));

        var filteredIds = filtered.Select(x => x.Id).ToHashSet();
        var clearedIds = cleared.Select(x => x.Id).ToHashSet();

        Assert.Single(filtered);
        Assert.Contains(match.Id, filteredIds);

        Assert.Equal(3, cleared.Count);
        Assert.Contains(match.Id, clearedIds);
        Assert.Contains(boardOnlyA.Id, clearedIds);
        Assert.Contains(boardOnlyB.Id, clearedIds);
        Assert.True(filteredIds.IsSubsetOf(clearedIds));
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"card-service-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        var projectService = TestServiceBuilder.BuildProjectService(dbContext);
        var boardService = TestServiceBuilder.BuildBoardService(dbContext);
        var columnService = TestServiceBuilder.BuildColumnService(dbContext);
        var notificationService = new NotificationService(dbContext, new NoOpProjectBroadcaster());
        var cardService = TestServiceBuilder.BuildCardService(dbContext, notificationService);

        return new TestFixture(projectService, boardService, columnService, cardService, dbContext);
    }

    private sealed record TestFixture(
        ProjectService ProjectService,
        BoardService BoardService,
        ColumnService ColumnService,
        CardService CardService,
        ApplicationDbContext DbContext);
}