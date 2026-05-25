using FluentValidation;

namespace Kanban.Api.Services.Columns;

public sealed class CreateColumnRequestValidator : AbstractValidator<CreateColumnRequest>
{
    public CreateColumnRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}

public sealed class UpdateColumnRequestValidator : AbstractValidator<UpdateColumnRequest>
{
    public UpdateColumnRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}

public sealed class ReorderColumnRequestValidator : AbstractValidator<ReorderColumnRequest>
{
    public ReorderColumnRequestValidator()
    {
        RuleFor(x => x)
            .Must(req => req.BeforeColumnId.HasValue || req.AfterColumnId.HasValue)
            .WithMessage("At least one anchor column is required.");

        RuleFor(x => x)
            .Must(req => !req.BeforeColumnId.HasValue || !req.AfterColumnId.HasValue || req.BeforeColumnId != req.AfterColumnId)
            .WithMessage("Before and after anchors must be different.");
    }
}
