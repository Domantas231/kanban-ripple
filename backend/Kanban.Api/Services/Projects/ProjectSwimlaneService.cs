using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Projects;

public sealed class ProjectSwimlaneService : IProjectSwimlaneService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;

    public ProjectSwimlaneService(ApplicationDbContext dbContext, IProjectAccessGuard accessGuard)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
    }

    public async Task<SwimlaneView> GetSwimlaneViewAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (project is null)
        {
            throw new NotFoundException("Project not found.");
        }
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer, cancellationToken);

        var boards = await _dbContext.Boards
            .AsNoTracking()
            .Where(board => board.ProjectId == projectId)
            .OrderBy(board => board.Position)
            .ThenBy(board => board.Id)
            .Select(board => new BoardSwimlane
            {
                Board = board,
                Columns = board.Columns
                    .OrderBy(column => column.Position)
                    .ThenBy(column => column.Id)
                    .Select(column => new ColumnSwimlane
                    {
                        Column = column,
                        Cards = column.Cards
                            .OrderBy(card => card.Position)
                            .ThenBy(card => card.Id)
                            .Select(card => new Card
                            {
                                Id = card.Id,
                                ColumnId = card.ColumnId,
                                Title = card.Title,
                                Description = card.Description,
                                Position = card.Position,
                                StartDate = card.StartDate,
                                DueDate = card.DueDate,
                                EstimatedHours = card.EstimatedHours,
                                Version = card.Version,
                                CreatedAt = card.CreatedAt,
                                UpdatedAt = card.UpdatedAt,
                                DeletedAt = card.DeletedAt,
                                CreatedBy = card.CreatedBy,
                                CardTags = card.CardTags.Select(ct => new CardTag
                                {
                                    Id = ct.Id,
                                    CardId = ct.CardId,
                                    TagId = ct.TagId,
                                    CreatedAt = ct.CreatedAt,
                                    Tag = ct.Tag
                                }).ToList(),
                                Assignments = card.Assignments.Select(a => new CardAssignment
                                {
                                    Id = a.Id,
                                    CardId = a.CardId,
                                    UserId = a.UserId,
                                    AssignedAt = a.AssignedAt,
                                    AssignedBy = a.AssignedBy,
                                    User = a.User
                                }).ToList(),
                                Subtasks = card.Subtasks
                                    .OrderBy(s => s.Position)
                                    .Select(s => new Subtask
                                    {
                                        Id = s.Id,
                                        CardId = s.CardId,
                                        Description = s.Description,
                                        Completed = s.Completed,
                                        Position = s.Position,
                                        CreatedAt = s.CreatedAt,
                                        UpdatedAt = s.UpdatedAt
                                    }).ToList(),
                                Attachments = card.Attachments.Select(att => new Attachment
                                {
                                    Id = att.Id,
                                    CardId = att.CardId,
                                    Filename = att.Filename,
                                    FileSize = att.FileSize,
                                    StorageKey = att.StorageKey,
                                    MimeType = att.MimeType,
                                    UploadedBy = att.UploadedBy,
                                    UploadedAt = att.UploadedAt
                                }).ToList(),
                                Comments = card.Comments.Select(c => new Comment
                                {
                                    Id = c.Id,
                                    CardId = c.CardId,
                                    AuthorId = c.AuthorId,
                                    Content = c.Content,
                                    CreatedAt = c.CreatedAt,
                                    UpdatedAt = c.UpdatedAt,
                                    DeletedAt = c.DeletedAt
                                }).ToList()
                            })
                            .ToList(),
                        CardCount = column.Cards.Count()
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        await PopulatePlannedBlockMinutesAsync(projectId, boards, cancellationToken);

        return new SwimlaneView
        {
            ProjectId = projectId,
            Boards = boards
        };
    }

    private async Task PopulatePlannedBlockMinutesAsync(Guid projectId, IReadOnlyList<BoardSwimlane> boards, CancellationToken cancellationToken)
    {
        var plannerRows = await _dbContext.PlannedBlocks
            .AsNoTracking()
            .Where(pb => pb.ProjectId == projectId)
            .Select(pb => new { pb.CardId, pb.Date, pb.StartTime, pb.EndTime })
            .ToListAsync(cancellationToken);

        if (plannerRows.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        var aggregates = plannerRows
            .GroupBy(x => x.CardId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var scheduled = 0;
                    var spent = 0;
                    foreach (var row in g)
                    {
                        var minutes = (int)Math.Round((row.EndTime - row.StartTime).TotalMinutes);
                        if (minutes <= 0)
                        {
                            continue;
                        }

                        scheduled += minutes;

                        var blockStart = row.Date.ToDateTime(row.StartTime, DateTimeKind.Utc);
                        var blockEnd = row.Date.ToDateTime(row.EndTime, DateTimeKind.Utc);
                        if (now >= blockEnd)
                        {
                            spent += minutes;
                        }
                        else if (now > blockStart)
                        {
                            spent += (int)Math.Round((now - blockStart).TotalMinutes);
                        }
                    }
                    return (Scheduled: scheduled, Spent: spent);
                });

        foreach (var board in boards)
        {
            foreach (var column in board.Columns)
            {
                foreach (var card in column.Cards)
                {
                    if (aggregates.TryGetValue(card.Id, out var value))
                    {
                        card.ScheduledMinutes = value.Scheduled;
                        card.SpentMinutes = value.Spent;
                    }
                }
            }
        }
    }
}
