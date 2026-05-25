using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kanban.Api.Exceptions;

namespace Kanban.Api.Services.Google;

public sealed class GoogleDriveApiClient : IGoogleDriveApiClient
{
    private const string DriveFilesBaseUrl = "https://www.googleapis.com/drive/v3/files";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleDriveApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GoogleFileMetadata> GetFileMetadataAsync(string accessToken, string fileId, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient("GoogleDriveApi");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{DriveFilesBaseUrl}/{Uri.EscapeDataString(fileId)}?fields=id,name,mimeType,webViewLink,iconLink,thumbnailLink,size,modifiedTime");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new BadRequestException($"Failed to get file metadata for '{fileId}': {json}");
        }

        var doc = JsonSerializer.Deserialize<JsonElement>(json);

        return new GoogleFileMetadata(
            Id: doc.GetProperty("id").GetString()!,
            Name: doc.GetProperty("name").GetString()!,
            MimeType: doc.GetProperty("mimeType").GetString()!,
            WebViewLink: doc.GetProperty("webViewLink").GetString()!,
            IconLink: doc.TryGetProperty("iconLink", out var icon) ? icon.GetString() : null,
            ThumbnailLink: doc.TryGetProperty("thumbnailLink", out var thumb) ? thumb.GetString() : null,
            Size: doc.TryGetProperty("size", out var size) && size.ValueKind != JsonValueKind.Null
                ? long.Parse(size.GetString()!)
                : null,
            ModifiedTime: doc.TryGetProperty("modifiedTime", out var mod) && mod.ValueKind != JsonValueKind.Null
                ? DateTime.Parse(mod.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
                : null
        );
    }

    public async Task<List<GoogleFilePermission>> ListPermissionsAsync(string accessToken, string fileId, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient("GoogleDriveApi");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{DriveFilesBaseUrl}/{Uri.EscapeDataString(fileId)}/permissions?fields=permissions(id,emailAddress,role,type)");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new BadRequestException($"Failed to list permissions for file '{fileId}': {json}");
        }

        var doc = JsonSerializer.Deserialize<JsonElement>(json);

        if (!doc.TryGetProperty("permissions", out var permissionsArray))
        {
            return [];
        }

        var permissions = new List<GoogleFilePermission>();
        foreach (var p in permissionsArray.EnumerateArray())
        {
            permissions.Add(new GoogleFilePermission(
                Id: p.GetProperty("id").GetString()!,
                EmailAddress: p.TryGetProperty("emailAddress", out var email) ? email.GetString() : null,
                Role: p.GetProperty("role").GetString()!,
                Type: p.GetProperty("type").GetString()!
            ));
        }

        return permissions;
    }

    public async Task<bool> AddPermissionAsync(string accessToken, string fileId, string email, string role = "reader", CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient("GoogleDriveApi");

        var body = JsonSerializer.Serialize(new
        {
            role,
            type = "user",
            emailAddress = email
        });

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{DriveFilesBaseUrl}/{Uri.EscapeDataString(fileId)}/permissions?sendNotificationEmail=false");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdatePermissionAsync(string accessToken, string fileId, string permissionId, string role, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient("GoogleDriveApi");

        var body = JsonSerializer.Serialize(new { role });

        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"{DriveFilesBaseUrl}/{Uri.EscapeDataString(fileId)}/permissions/{Uri.EscapeDataString(permissionId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeletePermissionAsync(string accessToken, string fileId, string permissionId, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient("GoogleDriveApi");

        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{DriveFilesBaseUrl}/{Uri.EscapeDataString(fileId)}/permissions/{Uri.EscapeDataString(permissionId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);

        // 404 means the permission is already gone, treat as success.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return true;
        }

        return response.IsSuccessStatusCode;
    }
}
