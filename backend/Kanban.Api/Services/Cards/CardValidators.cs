using FluentValidation;

namespace Kanban.Api.Services.Cards;

public sealed class CreateCardRequestValidator : AbstractValidator<CreateCardRequest>
{
    public CreateCardRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(5000)
            .When(x => x.Description is not null);

        RuleFor(x => x.EstimatedHours)
            .GreaterThan(0)
            .LessThanOrEqualTo(10000)
            .When(x => x.EstimatedHours.HasValue);

        RuleFor(x => x)
            .Must(req => !req.StartDate.HasValue || !req.DueDate.HasValue || req.DueDate.Value >= req.StartDate.Value)
            .WithMessage("Due date cannot be before start date.");
    }
}

public sealed class UpdateCardRequestValidator : AbstractValidator<UpdateCardRequest>
{
    public UpdateCardRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(5000)
            .When(x => x.Description is not null);

        RuleFor(x => x.EstimatedHours)
            .GreaterThan(0)
            .LessThanOrEqualTo(10000)
            .When(x => x.EstimatedHours.HasValue);

        RuleFor(x => x.Version)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x)
            .Must(req => !req.StartDate.HasValue || !req.DueDate.HasValue || req.DueDate.Value >= req.StartDate.Value)
            .WithMessage("Due date cannot be before start date.");
    }
}

public sealed class ScheduleCardRequestValidator : AbstractValidator<ScheduleCardRequest>
{
    public ScheduleCardRequestValidator()
    {
        RuleFor(x => x)
            .Must(req => !req.StartDate.HasValue || !req.DueDate.HasValue || req.DueDate.Value >= req.StartDate.Value)
            .WithMessage("Due date cannot be before start date.");
    }
}

public sealed class MoveCardRequestValidator : AbstractValidator<MoveCardRequest>
{
    public MoveCardRequestValidator()
    {
        RuleFor(x => x.ColumnId)
            .NotEmpty();

        RuleFor(x => x.Position)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateSubtaskRequestValidator : AbstractValidator<CreateSubtaskRequest>
{
    public CreateSubtaskRequestValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500);
    }
}

public sealed class UpdateSubtaskRequestValidator : AbstractValidator<UpdateSubtaskRequest>
{
    public UpdateSubtaskRequestValidator()
    {
        RuleFor(x => x)
            .Must(req => req.Description is not null || req.Completed.HasValue || req.Position.HasValue)
            .WithMessage("At least one subtask field must be provided.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500)
            .When(x => x.Description is not null);

        RuleFor(x => x.Position)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Position.HasValue);
    }
}
