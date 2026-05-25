namespace Kanban.Api.Configuration.Options;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";
    public string Url { get; set; } = "http://localhost:5173";
    public string? PasswordResetUrl { get; set; }
    public string? EmailConfirmationUrl { get; set; }
    public string? InvitationAcceptUrl { get; set; }

    public string ResolvedPasswordResetUrl => (PasswordResetUrl ?? $"{Url.TrimEnd('/')}/reset-password").TrimEnd('/');
    public string ResolvedEmailConfirmationUrl => (EmailConfirmationUrl ?? $"{Url.TrimEnd('/')}/confirm-email").TrimEnd('/');
    public string ResolvedInvitationAcceptUrl => (InvitationAcceptUrl ?? $"{Url.TrimEnd('/')}/invitations/accept").TrimEnd('/');
    public string TrimmedUrl => Url.TrimEnd('/');
}
