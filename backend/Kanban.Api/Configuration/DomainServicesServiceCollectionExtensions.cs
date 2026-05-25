using Kanban.Api.Hubs;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Archive;
using Kanban.Api.Services.Attachments;
using Kanban.Api.Services.Auth;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Boards;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Columns;
using Kanban.Api.Services.Comments;
using Kanban.Api.Services.Favorites;
using Kanban.Api.Services.Google;
using Kanban.Api.Services.Invitations;
using Kanban.Api.Services.Notifications;
using Kanban.Api.Services.Planner;
using Kanban.Api.Services.Projects;
using Kanban.Api.Services.Search;
using Kanban.Api.Services.Subscriptions;
using Kanban.Api.Services.Tags;

namespace Kanban.Api.Configuration;

public static class DomainServicesServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectAccessGuard, ProjectAccessGuard>();
        services.AddScoped<INotificationFanout, NotificationFanout>();
        services.AddScoped<IActivityRecorder, ActivityRecorder>();
        services.AddScoped<IAuthProfileService, AuthProfileService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IProjectSwimlaneService, ProjectSwimlaneService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IBoardArchiveService, BoardArchiveService>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IColumnArchiveService, ColumnArchiveService>();
        services.AddScoped<IColumnService, ColumnService>();
        services.AddScoped<ICardQueryService, CardQueryService>();
        services.AddScoped<ISubtaskService, SubtaskService>();
        services.AddScoped<ICardAssignmentService, CardAssignmentService>();
        services.AddScoped<ICardArchiveService, CardArchiveService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<ICardActivityService, CardActivityService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IPlannerService, PlannerService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IProjectBroadcaster, ProjectBroadcaster>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IArchivePurgeService, ArchivePurgeService>();
        services.AddSingleton<IAccessTokenBlocklist, MemoryAccessTokenBlocklist>();

        services.AddHostedService<InvitationCleanupService>();

        return services;
    }

    public static IServiceCollection AddGoogleIntegrations(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddHttpClient("GoogleDriveApi");
        services.AddHttpClient("GoogleCalendarApi");
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IGoogleDriveApiClient, GoogleDriveApiClient>();
        services.AddScoped<IGoogleDriveLinkService, GoogleDriveLinkService>();
        services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();

        return services;
    }
}
