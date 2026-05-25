using FluentValidation;

namespace Kanban.Api.Services.Tags;

public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Color)
            .NotEmpty()
            .Matches("^#[0-9A-Fa-f]{6}$");
    }
}

public sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Color))
            .WithMessage("At least one of name or color must be provided.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .When(x => x.Name is not null);

        RuleFor(x => x.Color)
            .Matches("^#[0-9A-Fa-f]{6}$")
            .When(x => x.Color is not null);
    }
}
