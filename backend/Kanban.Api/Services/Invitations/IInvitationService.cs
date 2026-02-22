using Kanban.Api.Models;

namespace Kanban.Api.Services.Invitations;

public interface IInvitationService
{
    Task<string> CreateInvitationAsync(Guid projectId, Guid invitedBy, string email);
    Task AcceptInvitationAsync(string token, Guid userId);
    Task<bool> IsValidTokenAsync(string token);
    Task<Invitation> GetByTokenAsync(string token);
}
