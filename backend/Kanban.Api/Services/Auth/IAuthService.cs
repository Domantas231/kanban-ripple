namespace Kanban.Api.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? refreshToken = null, CancellationToken cancellationToken = default);
    Task<PasswordResetRequestResult> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken = default);
    Task<PasswordResetResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<AccountDeletionEligibilityResult> CanDeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}
