using Kanban.Api.Models;

namespace Kanban.Api.Services.Attachments;

public interface IAttachmentService
{
    Task<Attachment> AddAsync(Guid cardId, Guid userId, IFormFile file);
    Task RemoveAsync(Guid attachmentId, Guid userId);
    Task<AttachmentDownloadResult> GetDownloadUrlAsync(Guid attachmentId, Guid userId);
    Task<AttachmentStreamResult> GetDownloadStreamAsync(Guid attachmentId, Guid userId);
}
