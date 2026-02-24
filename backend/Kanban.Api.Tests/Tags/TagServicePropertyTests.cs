using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Boards;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Columns;
using Kanban.Api.Services.Notifications;
using Kanban.Api.Services.Projects;
using Kanban.Api.Services.Tags;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Tags;

public class TagServicePropertyTests
{
    [Fact]
    public async Task Property_59_CreateWithoutNameOrColorReturnsError()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Tag create validation project", ProjectType.Team);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.TagService.CreateAsync(project.Id, ownerId, new CreateTagDto("", "#AABBCC")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.TagService.CreateAsync(project.Id, ownerId, new CreateTagDto("   ", "#AABBCC")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.TagService.CreateAsync(project.Id, ownerId, new CreateTagDto("bug", "")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.TagService.CreateAsync(project.Id, ownerId, new CreateTagDto("bug", "   ")));
    }

    [Fact]
    public async Task Property_60_UpdateNameAndColorIsReflectedOnAllCardsUsingTag()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Tag update propagation project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var firstCard = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card 1", null, null));
        var secondCard = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card 2", null, null));

        var tag = await fixture.TagService.CreateAsync(project.Id, ownerId, new CreateTagDto("Bug", "#112233"));

        await fixture.CardService.AssignTagAsync(firstCard.Id, tag.Id, ownerId);
        await fixture.CardService.AssignTagAsync(secondCard.Id, tag.Id, ownerId);

        var updated = await fixture.TagService.UpdateAsync(tag.Id, ownerId, new UpdateTagDto("Urgent", "#A1B2C3"));
        var queriedTag = await fixture.TagService.GetByIdAsync(tag.Id, ownerId);
        var listedTags = await fixture.TagService.ListAsync(project.Id, ownerId);

        var firstCardAfter = await fixture.CardService.GetByIdAsync(firstCard.Id, ownerId);
        var secondCardAfter = await fixture.CardService.GetByIdAsync(secondCard.Id, ownerId);

        Assert.Equal("Urgent", updated.Name);
        Assert.Equal("#A1B2C3", updated.Color);

        Assert.Equal("Urgent", queriedTag.Name);
        Assert.Equal("#A1B2C3", queriedTag.Color);

        Assert.Contains(listedTags, x => x.Id == tag.Id && x.Name == "Urgent" && x.Color == "#A1B2C3");

        Assert.Contains(firstCardAfter.CardTags, x => x.TagId == tag.Id && x.Tag.Name == "Urgent" && x.Tag.Color == "#A1B2C3");
        Assert.Contains(secondCardAfter.CardTags, x => x.TagId == tag.Id && x.Tag.Name == "Urgent" && x.Tag.Color == "#A1B2C3");
    }

    [Fact]
    public async Task Property_61_DeleteTagRemovesAssociationFromAllCards()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Tag deletion project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");

        var firstCard = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card 1", null, null));
        var secondCard = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card 2", null, null));
        var tag = await fixture.TagService.CreateAsync(project.Id, ownerId, new CreateTagDto("Ops", "#445566"));

        await fixture.CardService.AssignTagAsync(firstCard.Id, tag.Id, ownerId);
        await fixture.CardService.AssignTagAsync(secondCard.Id, tag.Id, ownerId);

        await fixture.TagService.DeleteAsync(tag.Id, ownerId);

        var firstCardAfter = await fixture.CardService.GetByIdAsync(firstCard.Id, ownerId);
        var secondCardAfter = await fixture.CardService.GetByIdAsync(secondCard.Id, ownerId);

        Assert.DoesNotContain(firstCardAfter.CardTags, x => x.TagId == tag.Id);
        Assert.DoesNotContain(secondCardAfter.CardTags, x => x.TagId == tag.Id);
        Assert.DoesNotContain(fixture.DbContext.CardTags, x => x.TagId == tag.Id);
    }

    [Fact]
    public async Task Property_62_ColorIsStoredAndReturnedCorrectlyInQueries()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Tag color property project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Main board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "Todo");
        var card = await fixture.CardService.CreateAsync(column.Id, ownerId, new CreateCardDto("Card", null, null));

        var created = await fixture.TagService.CreateAsync(project.Id, ownerId, new CreateTagDto("Design", "#a1b2c3"));
        await fixture.CardService.AssignTagAsync(card.Id, created.Id, ownerId);

        var queried = await fixture.TagService.GetByIdAsync(created.Id, ownerId);
        var listed = await fixture.TagService.ListAsync(project.Id, ownerId);
        var cardAfter = await fixture.CardService.GetByIdAsync(card.Id, ownerId);

        Assert.Equal("#A1B2C3", created.Color);
        Assert.Equal("#A1B2C3", queried.Color);
        Assert.Contains(listed, x => x.Id == created.Id && x.Color == "#A1B2C3");
        Assert.Contains(cardAfter.CardTags, x => x.TagId == created.Id && x.Tag.Color == "#A1B2C3");
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tag-service-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        var projectService = new ProjectService(dbContext);
        var boardService = new BoardService(dbContext);
        var columnService = new ColumnService(dbContext);
        var notificationService = new NotificationService(dbContext);
        var cardService = new CardService(dbContext, notificationService);
        var tagService = new TagService(dbContext);

        return new TestFixture(projectService, boardService, columnService, cardService, tagService, dbContext);
    }

    private sealed record TestFixture(
        ProjectService ProjectService,
        BoardService BoardService,
        ColumnService ColumnService,
        CardService CardService,
        TagService TagService,
        ApplicationDbContext DbContext);
}