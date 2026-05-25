using FluentValidation;

namespace Kanban.Api.Services.Google;

public sealed class LinkGoogleDriveFilesRequestValidator : AbstractValidator<LinkGoogleDriveFilesRequest>
{
    public LinkGoogleDriveFilesRequestValidator()
    {
        RuleFor(x => x.GoogleFileIds)
            .NotEmpty()
            .Must(ids => ids.Count <= 10)
            .WithMessage("Cannot link more than 10 files at once.");

        RuleForEach(x => x.GoogleFileIds)
            .NotEmpty()
            .WithMessage("Google file ID must not be blank.");
    }
}
