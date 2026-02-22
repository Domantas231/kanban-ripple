namespace Kanban.Api.Services.Auth;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record PasswordResetRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record AuthResult(
    Guid UserId,
    string Email,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

public sealed record PasswordResetRequestResult(string Message);

public sealed record PasswordResetResult(string Message);

public sealed record AccountDeletionEligibilityResult(
    bool CanDelete,
    int OwnedProjectCount,
    string? Reason);
