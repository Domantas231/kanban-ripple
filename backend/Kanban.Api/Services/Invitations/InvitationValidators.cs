using FluentValidation;
using Kanban.Api.Models;

namespace Kanban.Api.Services.Invitations;

public sealed class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Role)
            .Must(role => role == ProjectRole.Manager
                || role == ProjectRole.Member
                || role == ProjectRole.Viewer)
            .WithMessage("Role must be Manager, Member, or Viewer.");
    }
}