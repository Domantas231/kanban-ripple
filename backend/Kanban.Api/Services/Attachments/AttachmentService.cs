using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Archive;
using Kanban.Api.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Attachments;

public sealed class AttachmentService : IAttachmentService
{
    private const long MaxFileSize = 25L * 1024 * 1024;
    private const long MaxTotalSizePerCard = 100L * 1024 * 1024;
    private const int MaxAttachmentsPerCard = 20;
    private static readonly TimeSpan SignedUrlExpiry = TimeSpan.FromMinutes(5);

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".json", ".xml", ".md",
        ".zip", ".rar", ".7z", ".tar", ".gz",
    };

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".msi", ".scr", ".ps1", ".sh", ".com", ".vbs", ".js", ".wsf",
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IActivityRecorder _activityRecorder;
    private readonly IProjectBroadcaster _projectBroadcaster;

    public AttachmentService(
        ApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        IProjectAccessGuard accessGuard,
        IActivityRecorder activityRecorder,
        IProjectBroadcaster projectBroadcaster)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _accessGuard = accessGuard;
        _activityRecorder = activityRecorder;
        _projectBroadcaster = projectBroadcaster;
    }

    public async Task<Attachment> AddAsync(Guid cardId, Guid userId, IFormFile file)
    {
        if (file.Length == 0)
        {
            throw new BadRequestException("File is empty.");
        }

        if (file.Length > MaxFileSize)
        {
            throw new BadRequestException($"File size exceeds the maximum allowed size of {MaxFileSize / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(file.FileName);

        if (BlockedExtensions.Contains(extension))
        {
            throw new BadRequestException("This file type is not allowed.");
        }

        if (!AllowedExtensions.Contains(extension))
        {
            throw new BadRequestException("This file type is not allowed.");
        }

        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }

        var projectId = card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member);

        var cardAttachmentCount = await _dbContext.Attachments
            .Where(a => a.CardId == cardId)
            .CountAsync();

        if (cardAttachmentCount >= MaxAttachmentsPerCard)
        {
            throw new BadRequestException($"This card has reached the maximum of {MaxAttachmentsPerCard} attachments.");
        }

        var existingCardSize = await _dbContext.Attachments
            .Where(a => a.CardId == cardId)
            .SumAsync(a => (long?)a.FileSize) ?? 0L;

        if (existingCardSize + file.Length > MaxTotalSizePerCard)
        {
            throw new BadRequestException($"Total attachment size on this card would exceed the maximum allowed of {MaxTotalSizePerCard / (1024 * 1024)} MB.");
        }

        var storageKey = $"attachments/{projectId}/{cardId}/{Guid.NewGuid()}{extension}";

        using var stream = file.OpenReadStream();
        await _fileStorageService.UploadAsync(storageKey, stream, file.ContentType, default);

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            Filename = file.FileName,
            FileSize = file.Length,
            StorageKey = storageKey,
            MimeType = file.ContentType,
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow,
        };

        _dbContext.Attachments.Add(attachment);
        _activityRecorder.RecordCard(cardId, userId, ActivityAction.Added, "attachment", null, file.FileName);
        await _dbContext.SaveChangesAsync();

        await _projectBroadcaster.CardUpdated(projectId, card);

        return attachment;
    }

    public async Task RemoveAsync(Guid attachmentId, Guid userId)
    {
        var attachment = await _dbContext.Attachments
            .Include(a => a.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment is null)
        {
            throw new NotFoundException("Attachment not found.");
        }

        var projectId = attachment.Card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member);

        var isUploader = attachment.UploadedBy == userId;
        var isCardCreator = attachment.Card.CreatedBy == userId;
        if (!isUploader && !isCardCreator)
        {
            if (!await _accessGuard.HasAccessAsync(projectId, userId, ProjectRole.Manager))
            {
                throw new ForbiddenException("Only the uploader, the card creator, or a project manager can remove an attachment.");
            }
        }

        _activityRecorder.RecordCard(attachment.CardId, userId, ActivityAction.Removed, "attachment", attachment.Filename, null);

        await _fileStorageService.DeleteAsync(attachment.StorageKey);
        _dbContext.Attachments.Remove(attachment);
        await _dbContext.SaveChangesAsync();

        await _projectBroadcaster.CardUpdated(projectId, attachment.Card);
    }

    public async Task<AttachmentDownloadResult> GetDownloadUrlAsync(Guid attachmentId, Guid userId)
    {
        var attachment = await _dbContext.Attachments
            .Include(a => a.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment is null)
        {
            throw new NotFoundException("Attachment not found.");
        }

        var projectId = attachment.Card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

        var url = await _fileStorageService.GenerateSignedUrlAsync(attachment.StorageKey, SignedUrlExpiry);

        return new AttachmentDownloadResult(url, attachment.Filename);
    }

    public async Task<AttachmentStreamResult> GetDownloadStreamAsync(Guid attachmentId, Guid userId)
    {
        var attachment = await _dbContext.Attachments
            .Include(a => a.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment is null)
        {
            throw new NotFoundException("Attachment not found.");
        }

        var projectId = attachment.Card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

        var stream = await _fileStorageService.DownloadAsync(attachment.StorageKey);

        return new AttachmentStreamResult(stream, attachment.Filename, attachment.MimeType);
    }

}
