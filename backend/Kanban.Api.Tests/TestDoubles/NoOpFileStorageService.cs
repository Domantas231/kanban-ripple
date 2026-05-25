using Kanban.Api.Services.Archive;

namespace Kanban.Api.Tests.TestDoubles;

internal sealed class NoOpFileStorageService : IFileStorageService
{
    public Task<string> UploadAsync(string storageKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(storageKey);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<string> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"https://test.local/{storageKey}");
    }

    public Task<Stream> DownloadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream());
    }
}
