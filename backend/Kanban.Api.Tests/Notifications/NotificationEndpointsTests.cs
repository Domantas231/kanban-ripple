using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Notifications;
using Kanban.Api.Services.Projects;
using Kanban.Api.Tests.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.Api.Tests.Notifications;

public sealed class NotificationEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public NotificationEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Assignment_CreatesNotificationForAssignee()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("notifications-assignment-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Notifications Assignment Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Main Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Assigned Card");

        var assigneeUserId = await _factory.CreateUserAsync(UniqueEmail("notifications-assignee"));
        using var assigneeClient = CreateClient(assigneeUserId);

        await AddProjectMemberAsync(project.Id, assigneeUserId, ProjectRole.Member);

        await InvokeCardServiceAsync(service => service.AssignUserAsync(card.Id, assigneeUserId, ownerUserId));

        var response = await assigneeClient.GetAsync("/api/notifications?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PaginatedResponse<Notification>>();
        Assert.NotNull(payload);
        var created = Assert.Single(payload!.Items);
        Assert.Equal(1, payload.TotalCount);
        Assert.Equal(NotificationType.CardAssigned, created.Type);
        Assert.Equal(assigneeUserId, created.UserId);
        Assert.Equal(card.Id, created.EntityId);
    }

    [Fact]
    public async Task SelfAssignment_DoesNotCreateNotification()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("notifications-self-assignment"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Notifications Self Assignment Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Self Assigned Card");

        await InvokeCardServiceAsync(service => service.AssignUserAsync(card.Id, userId, userId));

        var response = await client.GetAsync("/api/notifications?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PaginatedResponse<Notification>>();
        Assert.NotNull(payload);
        Assert.Empty(payload!.Items);
        Assert.Equal(0, payload.TotalCount);
    }

    [Fact]
    public async Task MarkAsRead_ReturnsOk()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("notifications-mark-read"));
        using var client = CreateClient(userId);

        var notification = await CreateNotificationAsync(userId, NotificationType.CardUpdated, "Needs reading", "Please read");

        var response = await client.PutAsync($"/api/notifications/{notification.Id}/read", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var persisted = await db.Notifications.SingleAsync(x => x.Id == notification.Id);
            Assert.True(persisted.IsRead);
        });
    }

    [Fact]
    public async Task MarkAllAsRead_UpdatesAllUnread()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("notifications-mark-all"));
        var otherUserId = await _factory.CreateUserAsync(UniqueEmail("notifications-mark-all-other"));
        using var client = CreateClient(userId);

        var firstUnread = await CreateNotificationAsync(userId, NotificationType.CardCreated, "U1", "U1");
        _ = await CreateNotificationAsync(userId, NotificationType.CardUpdated, "U2", "U2");
        var alreadyRead = await CreateNotificationAsync(userId, NotificationType.CardMoved, "Read", "Read");
        var otherUnread = await CreateNotificationAsync(otherUserId, NotificationType.CardDeleted, "Other", "Other");

        await _factory.WithDbContextAsync(async db =>
        {
            var persisted = await db.Notifications.SingleAsync(x => x.Id == alreadyRead.Id);
            persisted.IsRead = true;
            await db.SaveChangesAsync();
        });

        var response = await client.PutAsync("/api/notifications/read-all", content: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var userNotifications = await db.Notifications
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .ToListAsync();

            Assert.NotEmpty(userNotifications);
            Assert.All(userNotifications, x => Assert.True(x.IsRead));

            var otherNotification = await db.Notifications
                .AsNoTracking()
                .SingleAsync(x => x.Id == otherUnread.Id);
            Assert.False(otherNotification.IsRead);

            var first = await db.Notifications
                .AsNoTracking()
                .SingleAsync(x => x.Id == firstUnread.Id);
            Assert.True(first.IsRead);
        });
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("notifications-delete"));
        using var client = CreateClient(userId);

        var notification = await CreateNotificationAsync(userId, NotificationType.CardCreated, "Delete me", "Delete me");

        var response = await client.DeleteAsync($"/api/notifications/{notification.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var exists = await db.Notifications.AnyAsync(x => x.Id == notification.Id);
            Assert.False(exists);
        });
    }

    [Fact]
    public async Task AccessingOtherUsersNotification_ReturnsNotFound()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("notifications-owner"));
        var otherUserId = await _factory.CreateUserAsync(UniqueEmail("notifications-other"));
        using var ownerClient = CreateClient(ownerUserId);

        var otherNotification = await CreateNotificationAsync(otherUserId, NotificationType.CardUpdated, "Other", "Other");

        var markReadResponse = await ownerClient.PutAsync($"/api/notifications/{otherNotification.Id}/read", content: null);
        var deleteResponse = await ownerClient.DeleteAsync($"/api/notifications/{otherNotification.Id}");

        Assert.Equal(HttpStatusCode.NotFound, markReadResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsReverseChronologicalOrder_AndPagination()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("notifications-list"));
        using var client = CreateClient(userId);

        var now = DateTime.UtcNow;
        var created = new List<Notification>();

        await _factory.WithDbContextAsync(async db =>
        {
            for (var index = 0; index < 5; index++)
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Type = NotificationType.CardUpdated,
                    Title = $"N{index}",
                    Message = $"M{index}",
                    IsRead = false,
                    CreatedAt = now.AddMinutes(index)
                };

                db.Notifications.Add(notification);
                created.Add(notification);
            }

            await db.SaveChangesAsync();
        });

        var newestFirst = created
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Id)
            .ToArray();

        var page1 = await client.GetFromJsonAsync<PaginatedResponse<Notification>>("/api/notifications?page=1&pageSize=2");
        var page2 = await client.GetFromJsonAsync<PaginatedResponse<Notification>>("/api/notifications?page=2&pageSize=2");

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(5, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2!.Items.Count);

        Assert.Equal(newestFirst[0], page1.Items[0].Id);
        Assert.Equal(newestFirst[1], page1.Items[1].Id);
        Assert.Equal(newestFirst[2], page2.Items[0].Id);
        Assert.Equal(newestFirst[3], page2.Items[1].Id);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());
        return client;
    }

    private async Task AddProjectMemberAsync(Guid projectId, Guid userId, ProjectRole role)
    {
        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = userId,
                Role = role,
                JoinedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });
    }

    private async Task<Notification> CreateNotificationAsync(Guid userId, NotificationType type, string title, string message)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
        return await service.CreateAsync(userId, type, title, message);
    }

    private async Task InvokeCardServiceAsync(Func<ICardService, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICardService>();
        await action(service);
    }

    private static string UniqueEmail(string prefix)
    {
        return $"{prefix}.{Guid.NewGuid():N}@example.com";
    }

    private static async Task<Project> CreateProjectAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name });
        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);
        return project!;
    }

    private static async Task<Board> CreateBoardAsync(HttpClient client, Guid projectId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/boards", new { name });
        response.EnsureSuccessStatusCode();

        var board = await response.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(board);
        return board!;
    }

    private static async Task<Column> CreateColumnAsync(HttpClient client, Guid boardId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/columns", new { name });
        response.EnsureSuccessStatusCode();

        var column = await response.Content.ReadFromJsonAsync<Column>();
        Assert.NotNull(column);
        return column!;
    }

    private static async Task<Card> CreateCardAsync(HttpClient client, Guid columnId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/columns/{columnId}/cards", new
        {
            title,
            description = "desc",
            plannedDurationHours = 1.0m
        });
        response.EnsureSuccessStatusCode();

        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        return card!;
    }
}
