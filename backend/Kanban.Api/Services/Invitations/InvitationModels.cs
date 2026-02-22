namespace Kanban.Api.Services.Invitations;

public sealed record CreateInvitationRequest(string Email);

public sealed record InvitationCreatedResponse(string Message);