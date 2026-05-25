namespace Kanban.Api.Configuration.Options;

public sealed class GoogleOAuthOptions
{
    public const string SectionName = "Google";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
