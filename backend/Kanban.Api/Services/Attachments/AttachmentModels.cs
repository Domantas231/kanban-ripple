namespace Kanban.Api.Services.Attachments;

public sealed record AttachmentDownloadResult(string Url, string Filename);

public sealed record AttachmentStreamResult(Stream Content, string Filename, string MimeType);
