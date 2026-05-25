using FluentValidation;

namespace Kanban.Api.Services.Planner;

public sealed class CreatePlannedBlockRequestValidator : AbstractValidator<CreatePlannedBlockRequest>
{
    public CreatePlannedBlockRequestValidator()
    {
        RuleFor(x => x.CardId)
            .NotEmpty();

        RuleFor(x => x.Date)
            .NotEmpty();

        RuleFor(x => x.StartTime)
            .NotEmpty();

        RuleFor(x => x.EndTime)
            .NotEmpty()
            .GreaterThan(x => x.StartTime)
            .WithMessage("End time must be after start time.");
    }
}

public sealed class UpdatePlannedBlockRequestValidator : AbstractValidator<UpdatePlannedBlockRequest>
{
    public UpdatePlannedBlockRequestValidator()
    {
        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .When(x => x.StartTime.HasValue && x.EndTime.HasValue)
            .WithMessage("End time must be after start time.");
    }
}
