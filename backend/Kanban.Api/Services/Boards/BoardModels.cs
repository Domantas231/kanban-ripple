using System.Text.Json.Serialization;

namespace Kanban.Api.Services.Boards;

public sealed record CreateBoardRequest(string Name);

public sealed record UpdateBoardRequest(string Name, int Position);

public sealed record UpdateBoardDto(string Name, int Position);

public sealed class TrelloImportRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("lists")]
    public List<TrelloList> Lists { get; init; } = new();

    [JsonPropertyName("cards")]
    public List<TrelloCard> Cards { get; init; } = new();

    [JsonPropertyName("labels")]
    public List<TrelloLabel> Labels { get; init; } = new();

    [JsonPropertyName("checklists")]
    public List<TrelloChecklist> Checklists { get; init; } = new();
}

public sealed class TrelloChecklist
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("idCard")]
    public string IdCard { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("pos")]
    public double Pos { get; init; }

    [JsonPropertyName("checkItems")]
    public List<TrelloCheckItem> CheckItems { get; init; } = new();
}

public sealed class TrelloCheckItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    [JsonPropertyName("pos")]
    public double Pos { get; init; }
}

public sealed class TrelloList
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("closed")]
    public bool Closed { get; init; }

    [JsonPropertyName("pos")]
    public double Pos { get; init; }
}

public sealed class TrelloCard
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; init; } = string.Empty;

    [JsonPropertyName("idList")]
    public string IdList { get; init; } = string.Empty;

    [JsonPropertyName("closed")]
    public bool Closed { get; init; }

    [JsonPropertyName("pos")]
    public double Pos { get; init; }

    [JsonPropertyName("idLabels")]
    public List<string> IdLabels { get; init; } = new();

    [JsonPropertyName("due")]
    public DateTime? Due { get; init; }

    [JsonPropertyName("start")]
    public DateTime? Start { get; init; }

    [JsonPropertyName("attachments")]
    public List<TrelloAttachment> Attachments { get; init; } = new();
}

public sealed class TrelloAttachment
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    [JsonPropertyName("isUpload")]
    public bool IsUpload { get; init; }
}

public sealed class TrelloLabel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; init; }
}
