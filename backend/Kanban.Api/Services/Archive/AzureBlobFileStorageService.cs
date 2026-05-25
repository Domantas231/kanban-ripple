using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Kanban.Api.Services.Archive;

public sealed class AzureBlobFileStorageService : IFileStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobFileStorageService> _logger;

    public AzureBlobFileStorageService(BlobContainerClient containerClient, ILogger<AzureBlobFileStorageService> logger)
    {
        _containerClient = containerClient;
        _logger = logger;
    }

    public async Task<string> UploadAsync(string storageKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAsync(cancellationToken);

        var blobClient = _containerClient.GetBlobClient(storageKey);
        var headers = new BlobHttpHeaders { ContentType = contentType };

        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);

        _logger.LogInformation("Uploaded blob to Azure storage with key {StorageKey}.", storageKey);

        return storageKey;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storageKey);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);

        _logger.LogInformation("Deleted blob from Azure storage with key {StorageKey}.", storageKey);
    }

    public Task<string> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storageKey);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "Azure blob client cannot generate SAS URIs. Provide a connection string or shared key credentials in FileStorage configuration.");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = storageKey,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }

    public async Task<Stream> DownloadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var blobClient = _containerClient.GetBlobClient(storageKey);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Could not ensure Azure blob container {ContainerName} exists.", _containerClient.Name);
        }
    }
}
