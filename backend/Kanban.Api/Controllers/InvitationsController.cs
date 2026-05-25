using Kanban.Api.Services.Invitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class InvitationsController : KanbanControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpPost("invitations/{token}/accept")]
    public async Task<IActionResult> Accept(string token)
    {
        var userId = GetUserId();
        await _invitationService.AcceptInvitationAsync(token, userId);
        return NoContent();
    }
}
