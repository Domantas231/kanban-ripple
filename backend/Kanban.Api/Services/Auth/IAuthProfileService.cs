using Microsoft.AspNetCore.Http;

namespace Kanban.Api.Services.Auth;

public interface IAuthProfileService
{
    Task UploadProfilePhotoAsync(Guid userId, IFormFile file, CancellationToken cancellationToken = default);
    Task<(Stream Content, string ContentType)?> GetProfilePhotoStreamAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteProfilePhotoAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<UpdateDisplayNameResult> UpdateDisplayNameAsync(Guid userId, UpdateDisplayNameRequest request, CancellationToken cancellationToken = default);
}
