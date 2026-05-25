using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Services.Invitations;

public sealed class InvitationCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvitationCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval;

    public InvitationCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<InvitationCleanupService> logger,
        IOptions<InvitationOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cleanupInterval = TimeSpan.FromHours(options.Value.CleanupIntervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_cleanupInterval);

        await DeleteExpiredInvitationsAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DeleteExpiredInvitationsAsync(stoppingToken);
        }
    }

    private async Task DeleteExpiredInvitationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;
            var expiredUnacceptedInvitations = await dbContext.Invitations
                .Where(x => x.ExpiresAt < now && x.AcceptedAt == null)
                .ToListAsync(cancellationToken);

            if (expiredUnacceptedInvitations.Count == 0)
            {
                return;
            }

            dbContext.Invitations.RemoveRange(expiredUnacceptedInvitations);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Invitation cleanup deleted {DeletedCount} expired pending invitations.",
                expiredUnacceptedInvitations.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Invitation cleanup service stopping due to cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invitation cleanup failed.");
        }
    }
}