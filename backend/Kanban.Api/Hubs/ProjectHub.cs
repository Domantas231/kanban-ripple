using System.Security.Claims;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kanban.Api.Hubs;

[Authorize]
public class ProjectHub(IProjectService projectService) : Hub<IProjectClient>
{
    public async Task JoinProject(Guid projectId)
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new HubException("Unauthorized");
        }

        var hasAccess = await projectService.CheckAccessAsync(projectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new HubException("Access denied to project");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetProjectGroupName(projectId));
    }

    public async Task LeaveProject(Guid projectId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetProjectGroupName(projectId));
    }

    public static string GetProjectGroupName(Guid projectId) => $"project_{projectId}";
}
