using Kanban.Api.Models;

namespace Kanban.Api.Services.Invitations;

public sealed record CreateInvitationRequest(string Email, ProjectRole Role = ProjectRole.Member);

public sealed record InvitationCreatedResponse(string Message);