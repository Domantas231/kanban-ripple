namespace Kanban.Api.Services.Google;

public interface IGoogleAuthService
{
    string BuildAuthUrl(Guid userId);
    Task ExchangeCodeAsync(string code, Guid userId, CancellationToken cancellationToken = default);
    Task<string> GetAccessTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<GoogleConnectionStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DisconnectAsync(Guid userId, CancellationToken cancellationToken = default);
    Guid? ValidateState(string state);
}
