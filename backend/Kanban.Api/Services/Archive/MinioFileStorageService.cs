using Amazon.S3;
using Amazon.S3.Model;

namespace Kanban.Api.Services.Archive;

public sealed class MinioFileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<MinioFileStorageService> _logger;

    public MinioFileStorageService(IAmazonS3 s3Client, IConfiguration configuration, ILogger<MinioFileStorageService> logger)
    {
        _s3Client = s3Client;
        _bucketName = configuration["FileStorage:BucketName"] ?? "kanban-attachments";
        _logger = logger;
    }

    public async Task<string> UploadAsync(string storageKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey,
            InputStream = content,
            ContentType = contentType,
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);

        _logger.LogInformation("Uploaded file to storage with key {StorageKey}.", storageKey);

        return storageKey;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey,
        };

        await _s3Client.DeleteObjectAsync(request, cancellationToken);

        _logger.LogInformation("Deleted file from storage with key {StorageKey}.", storageKey);
    }

    public Task<string> GenerateSignedUrlAsync(string storageKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = storageKey,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET,
        };

        var url = _s3Client.GetPreSignedURL(request);

        return Task.FromResult(url);
    }

    public async Task<Stream> DownloadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey,
        };

        var response = await _s3Client.GetObjectAsync(request, cancellationToken);
        return response.ResponseStream;
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var buckets = await _s3Client.ListBucketsAsync(cancellationToken);
            if (buckets.Buckets.All(b => b.BucketName != _bucketName))
            {
                await _s3Client.PutBucketAsync(_bucketName, cancellationToken);
                _logger.LogInformation("Created bucket {BucketName}.", _bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure bucket {BucketName} exists.", _bucketName);
        }
    }
}
