using FluentValidation;
using Kanban.Api.Models;

namespace Kanban.Api.Services.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Type)
            .IsInEnum()
            .When(x => x.Type.HasValue);
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Type)
            .NotNull()
            .IsInEnum();
    }
}

public sealed class UpgradeProjectTypeRequestValidator : AbstractValidator<UpgradeProjectTypeRequest>
{
    public UpgradeProjectTypeRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotNull()
            .IsInEnum();
    }
}

public sealed class UpdateMemberRoleRequestValidator : AbstractValidator<UpdateMemberRoleRequest>
{
    public UpdateMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotNull()
            .IsInEnum();

        RuleFor(x => x.Role)
            .NotEqual(ProjectRole.Owner)
            .When(x => x.Role.HasValue)
            .WithMessage("Cannot set member role to owner. Use ownership transfer.");
    }
}

public sealed class TransferOwnershipRequestValidator : AbstractValidator<TransferOwnershipRequest>
{
    public TransferOwnershipRequestValidator()
    {
        RuleFor(x => x.NewOwnerUserId)
            .NotEmpty();
    }
}
