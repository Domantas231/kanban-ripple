namespace Kanban.Api.Services.Archive;

public sealed class NoOpFileStorageService : IFileStorageService
{
    private readonly ILogger<NoOpFileStorageService> _logger;

    public NoOpFileStorageService(ILogger<NoOpFileStorageService> logger)
    {
        _logger = logger;
    }

    public Task<string> UploadAsync(string storageKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("No-op file storage upload called for key {StorageKey}.", storageKey);
        return Task.FromResult(storageKey);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("No-op file storage delete called for key {StorageKey}.", storageKey);
        return Task.CompletedTask;
    }

    public Task<string> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("No-op file storage signed URL called for key {StorageKey}.", storageKey);
        return Task.FromResult($"https://noop-storage/{storageKey}");
    }

    public Task<Stream> DownloadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("No-op file storage download called for key {StorageKey}.", storageKey);
        return Task.FromResult<Stream>(Stream.Null);
    }
}