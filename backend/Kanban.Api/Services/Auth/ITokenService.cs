using Kanban.Api.Models;

namespace Kanban.Api.Services.Auth;

public interface ITokenService
{
    Task<AuthResult> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string? refreshToken = null, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
