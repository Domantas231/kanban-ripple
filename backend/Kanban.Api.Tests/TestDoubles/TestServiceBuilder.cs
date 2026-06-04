using Kanban.Api.Data;
using Kanban.Api.Hubs;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Archive;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Boards;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Columns;
using Kanban.Api.Services.Comments;
using Kanban.Api.Services.Notifications;
using Kanban.Api.Services.Projects;
using Kanban.Api.Services.Subscriptions;
using Kanban.Api.Services.Tags;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kanban.Api.Tests.TestDoubles;

/// <summary>
/// Constructs production service graphs from an <see cref="ApplicationDbContext"/>
/// so tests can build a fully-wired service without listing every dependency by hand.
///
/// Mirrors the wiring registered in <c>DomainServicesServiceCollectionExtensions</c>.
/// </summary>
public static class TestServiceBuilder
{
    public static IProjectAccessGuard AccessGuard(ApplicationDbContext dbContext) =>
        new ProjectAccessGuard(dbContext);

    public static IActivityRecorder ActivityRecorder(ApplicationDbContext dbContext) =>
        new ActivityRecorder(dbContext);

    public static INotificationFanout Fanout(
        ApplicationDbContext dbContext,
        INotificationService? notificationService,
        ISubscriptionService? subscriptionService) =>
        notificationService is not null && subscriptionService is not null
            ? new NotificationFanout(dbContext, notificationService, subscriptionService)
            : new NoOpNotificationFanout();

    public static IArchivePurgeService ArchivePurge(ApplicationDbContext dbContext) =>
        new ArchivePurgeService(dbContext, new NoOpFileStorageService(), NullLogger<ArchivePurgeService>.Instance);

    public static ProjectService BuildProjectService(ApplicationDbContext dbContext)
    {
        var accessGuard = AccessGuard(dbContext);
        var swimlane = new ProjectSwimlaneService(dbContext, accessGuard);
        var recorder = ActivityRecorder(dbContext);
        var purge = ArchivePurge(dbContext);
        return new ProjectService(dbContext, accessGuard, swimlane, recorder, purge, NullLogger<ProjectService>.Instance);
    }

    public static BoardService BuildBoardService(
        ApplicationDbContext dbContext,
        INotificationService? notificationService = null,
        ISubscriptionService? subscriptionService = null,
        IProjectBroadcaster? projectBroadcaster = null)
    {
        var accessGuard = AccessGuard(dbContext);
        var recorder = ActivityRecorder(dbContext);
        var fanout = Fanout(dbContext, notificationService, subscriptionService);
        var broadcaster = projectBroadcaster ?? new NoOpProjectBroadcaster();
        var purge = ArchivePurge(dbContext);
        var archive = new BoardArchiveService(dbContext, accessGuard, recorder, fanout, broadcaster, purge);
        return new BoardService(dbContext, accessGuard, recorder, archive, fanout, broadcaster, NullLogger<BoardService>.Instance);
    }

    public static ColumnService BuildColumnService(
        ApplicationDbContext dbContext,
        INotificationService? notificationService = null,
        ISubscriptionService? subscriptionService = null,
        IProjectBroadcaster? projectBroadcaster = null)
    {
        var accessGuard = AccessGuard(dbContext);
        var recorder = ActivityRecorder(dbContext);
        var fanout = Fanout(dbContext, notificationService, subscriptionService);
        var broadcaster = projectBroadcaster ?? new NoOpProjectBroadcaster();
        var purge = ArchivePurge(dbContext);
        var archive = new ColumnArchiveService(dbContext, accessGuard, recorder, fanout, broadcaster, purge);
        return new ColumnService(dbContext, accessGuard, recorder, archive, fanout, broadcaster, NullLogger<ColumnService>.Instance);
    }

    public static CardService BuildCardService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        ISubscriptionService? subscriptionService = null,
        IProjectBroadcaster? projectBroadcaster = null)
    {
        var accessGuard = AccessGuard(dbContext);
        var recorder = ActivityRecorder(dbContext);
        var fanout = Fanout(dbContext, notificationService, subscriptionService);
        var broadcaster = projectBroadcaster ?? new NoOpProjectBroadcaster();
        var queryService = new CardQueryService(dbContext, accessGuard);
        var subtaskService = new SubtaskService(dbContext, accessGuard, recorder);
        var assignmentService = new CardAssignmentService(dbContext, notificationService, accessGuard, recorder);
        var purge = ArchivePurge(dbContext);
        var archiveService = new CardArchiveService(dbContext, accessGuard, recorder, fanout, broadcaster, purge);
        return new CardService(
            dbContext,
            accessGuard,
            recorder,
            queryService,
            subtaskService,
            assignmentService,
            archiveService,
            fanout,
            broadcaster,
            NullLogger<CardService>.Instance);
    }

    public static CommentService BuildCommentService(
        ApplicationDbContext dbContext,
        IProjectBroadcaster? projectBroadcaster = null)
    {
        var accessGuard = AccessGuard(dbContext);
        var recorder = ActivityRecorder(dbContext);
        var broadcaster = projectBroadcaster ?? new NoOpProjectBroadcaster();
        return new CommentService(dbContext, accessGuard, recorder, broadcaster);
    }

    public static TagService BuildTagService(
        ApplicationDbContext dbContext,
        IProjectBroadcaster? projectBroadcaster = null)
    {
        var accessGuard = AccessGuard(dbContext);
        var broadcaster = projectBroadcaster ?? new NoOpProjectBroadcaster();
        return new TagService(dbContext, accessGuard, broadcaster);
    }

    public static SubscriptionService BuildSubscriptionService(ApplicationDbContext dbContext)
    {
        var accessGuard = AccessGuard(dbContext);
        return new SubscriptionService(dbContext, accessGuard);
    }
}
