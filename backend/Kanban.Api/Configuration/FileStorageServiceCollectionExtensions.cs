using Amazon.S3;
using Azure.Storage.Blobs;
using Kanban.Api.Services.Archive;

namespace Kanban.Api.Configuration;

public static class FileStorageServiceCollectionExtensions
{
    private const string DefaultBucketName = "kanban-attachments";

    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var fileStorageSection = configuration.GetSection("FileStorage");
        if (!fileStorageSection.Exists())
        {
            services.AddSingleton<IFileStorageService, NoOpFileStorageService>();
            return services;
        }

        var provider = fileStorageSection["Provider"];
        var bucketName = fileStorageSection["BucketName"] ?? DefaultBucketName;

        if (string.Equals(provider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            RegisterAzureBlob(services, fileStorageSection, bucketName);
        }
        else if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase)
                 || !string.IsNullOrEmpty(fileStorageSection["ServiceUrl"]))
        {
            RegisterS3Compatible(services, fileStorageSection);
        }
        else
        {
            services.AddSingleton<IFileStorageService, NoOpFileStorageService>();
        }

        return services;
    }

    private static void RegisterAzureBlob(IServiceCollection services, IConfigurationSection section, string containerName)
    {
        var connectionString = section["ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "FileStorage:ConnectionString must be configured when FileStorage:Provider is 'AzureBlob'.");
        }

        services.AddSingleton(_ => new BlobContainerClient(connectionString, containerName));
        services.AddSingleton<IFileStorageService, AzureBlobFileStorageService>();
    }

    private static void RegisterS3Compatible(IServiceCollection services, IConfigurationSection section)
    {
        var serviceUrl = section["ServiceUrl"]
            ?? throw new InvalidOperationException(
                "FileStorage:ServiceUrl must be configured when FileStorage:Provider is 'S3'.");

        var useHttp = serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            UseHttp = useHttp,
        };
        services.AddSingleton<IAmazonS3>(new AmazonS3Client(
            section["AccessKey"] ?? "minioadmin",
            section["SecretKey"] ?? "minioadmin",
            s3Config));
        services.AddSingleton<IFileStorageService, MinioFileStorageService>();
    }
}
