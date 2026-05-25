namespace Kanban.Api.Services.Archive;

public interface IFileStorageService
{
    Task<string> UploadAsync(string storageKey, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<string> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string storageKey, CancellationToken cancellationToken = default);
}