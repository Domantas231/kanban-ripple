using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kanban.Api.Models;

[JsonConverter(typeof(ActivityActionJsonConverter))]
public enum ActivityAction
{
    Created = 0,
    Changed = 1,
    Added = 2,
    Removed = 3,
    Moved = 4,
    Archived = 5,
    Restored = 6,
    Completed = 7,
    Uncompleted = 8
}

public sealed class ActivityActionJsonConverter : JsonConverter<ActivityAction>
{
    public override ActivityAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("Action must be a non-empty string.");
        }

        if (!Enum.TryParse<ActivityAction>(value, ignoreCase: true, out var parsed))
        {
            throw new JsonException($"Invalid Action '{value}'.");
        }

        return parsed;
    }

    public override void Write(Utf8JsonWriter writer, ActivityAction value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
    }
}
