using FluentValidation;

namespace Kanban.Api.Services.Subscriptions;

public sealed class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
{
    public CreateSubscriptionRequestValidator()
    {
        RuleFor(x => x.EntityType)
            .IsInEnum();

        RuleFor(x => x.EntityId)
            .NotEmpty();
    }
}
