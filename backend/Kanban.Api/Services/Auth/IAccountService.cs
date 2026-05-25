using Kanban.Api.Models;

namespace Kanban.Api.Services.Auth;

public interface IAccountService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<ConfirmEmailResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default);

    Task<ResendConfirmationResult> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken = default);

    Task<PasswordResetRequestResult> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task<AccountDeletionEligibilityResult> CanDeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    Task<UpdateDisplayNameResult> UpdateDisplayNameAsync(Guid userId, UpdateDisplayNameRequest request, CancellationToken cancellationToken = default);

    Task UploadProfilePhotoAsync(Guid userId, IFormFile file, CancellationToken cancellationToken = default);

    Task<(Stream Content, string ContentType)?> GetProfilePhotoStreamAsync(Guid userId, CancellationToken cancellationToken = default);

    Task DeleteProfilePhotoAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ApplicationUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
