using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kanban.Api.Models;

[JsonConverter(typeof(GoogleDriveSharePermissionJsonConverter))]
public enum GoogleDriveSharePermission
{
    Reader = 0,
    Commenter = 1,
    Writer = 2
}

public sealed class GoogleDriveSharePermissionJsonConverter : JsonConverter<GoogleDriveSharePermission>
{
    public override GoogleDriveSharePermission Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("SharePermission must be a non-empty string.");
        }

        if (!Enum.TryParse<GoogleDriveSharePermission>(value, ignoreCase: true, out var parsed))
        {
            throw new JsonException($"Invalid SharePermission '{value}'. Expected reader, commenter, or writer.");
        }

        return parsed;
    }

    public override void Write(Utf8JsonWriter writer, GoogleDriveSharePermission value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
    }
}
