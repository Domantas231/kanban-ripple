namespace Kanban.Api.Services.Auth;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record PasswordResetRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record ConfirmEmailRequest(string Email, string Token);

public sealed record ResendConfirmationRequest(string Email);

public sealed record RegisterResult(string Message, string Email);

public sealed record ConfirmEmailResult(string Message);

public sealed record ResendConfirmationResult(string Message);

public sealed record AuthResult(
    Guid UserId,
    string Email,
    string? UserName,
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

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ChangePasswordResult(string Message);

public sealed record UpdateDisplayNameRequest(string DisplayName);

public sealed record UpdateDisplayNameResult(string DisplayName);
