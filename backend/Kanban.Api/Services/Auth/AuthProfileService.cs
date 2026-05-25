using Kanban.Api.Configuration.Options;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Archive;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Services.Auth;

public sealed class AuthProfileService : IAuthProfileService
{
    private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
    };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _fileStorage;
    private readonly long _maxProfilePhotoSize;

    public AuthProfileService(
        UserManager<ApplicationUser> userManager,
        IFileStorageService fileStorage,
        IOptions<ProfilePhotoOptions> options)
    {
        _userManager = userManager;
        _fileStorage = fileStorage;
        _maxProfilePhotoSize = options.Value.MaxFileSizeBytes;
    }

    public async Task UploadProfilePhotoAsync(Guid userId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            throw new BadRequestException("File is empty.");
        }

        if (file.Length > _maxProfilePhotoSize)
        {
            throw new BadRequestException($"File exceeds the {_maxProfilePhotoSize / (1024 * 1024)} MB size limit.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedPhotoExtensions.Contains(extension))
        {
            throw new BadRequestException("Only JPG, PNG, GIF, and WebP images are allowed.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        if (!string.IsNullOrWhiteSpace(user.ProfilePhotoStorageKey))
        {
            await _fileStorage.DeleteAsync(user.ProfilePhotoStorageKey, cancellationToken);
        }

        var storageKey = $"profile-photos/{userId}/{Guid.NewGuid()}{extension}";
        using var stream = file.OpenReadStream();
        await _fileStorage.UploadAsync(storageKey, stream, file.ContentType, cancellationToken);

        user.ProfilePhotoStorageKey = storageKey;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
    }

    public async Task<(Stream Content, string ContentType)?> GetProfilePhotoStreamAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        if (string.IsNullOrWhiteSpace(user.ProfilePhotoStorageKey))
        {
            return null;
        }

        var stream = await _fileStorage.DownloadAsync(user.ProfilePhotoStorageKey, cancellationToken);
        var contentType = user.ProfilePhotoStorageKey.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png"
            : user.ProfilePhotoStorageKey.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif"
            : user.ProfilePhotoStorageKey.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
            : "image/jpeg";
        return (stream, contentType);
    }

    public async Task DeleteProfilePhotoAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        if (string.IsNullOrWhiteSpace(user.ProfilePhotoStorageKey))
        {
            return;
        }

        await _fileStorage.DeleteAsync(user.ProfilePhotoStorageKey, cancellationToken);
        user.ProfilePhotoStorageKey = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        var isCurrentValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!isCurrentValid)
        {
            throw new BadRequestException("Current password is incorrect.");
        }

        var changeResult = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changeResult.Succeeded)
        {
            var errorMessage = string.Join("; ", changeResult.Errors.Select(x => x.Description));
            throw new BadRequestException(errorMessage);
        }

        return new ChangePasswordResult("Password changed successfully.");
    }

    public async Task<UpdateDisplayNameResult> UpdateDisplayNameAsync(Guid userId, UpdateDisplayNameRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        var displayName = request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 50)
        {
            throw new BadRequestException("Display name must be between 1 and 50 characters.");
        }

        var existing = await _userManager.FindByNameAsync(displayName);
        if (existing is not null && existing.Id != userId)
        {
            throw new ConflictException($"Display name '{displayName}' is already taken.", "DUPLICATE_NAME");
        }

        user.UserName = displayName;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            if (updateResult.Errors.Any(error => error.Code == "DuplicateUserName"))
            {
                throw new ConflictException($"Display name '{displayName}' is already taken.", "DUPLICATE_NAME");
            }

            var errorMessage = string.Join("; ", updateResult.Errors.Select(x => x.Description));
            throw new BadRequestException(errorMessage);
        }

        return new UpdateDisplayNameResult(displayName);
    }
}
