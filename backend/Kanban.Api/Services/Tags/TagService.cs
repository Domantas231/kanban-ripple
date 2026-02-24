using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Tags;

public sealed class TagService : ITagService
{
    private readonly ApplicationDbContext _dbContext;

    public TagService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Tag> CreateAsync(Guid projectId, Guid userId, CreateTagDto data)
    {
        var normalizedName = NormalizeName(data.Name);
        var normalizedColor = NormalizeColor(data.Color);

        var projectExists = await _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(x => x.Id == projectId);

        if (!projectExists)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(projectId, userId, ProjectRole.Moderator);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var duplicateExists = await _dbContext.Tags
            .AnyAsync(x => x.ProjectId == projectId && x.Name.ToLower() == normalizedName.ToLower());

        if (duplicateExists)
        {
            throw new InvalidOperationException("Tag name must be unique within the project.");
        }

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = normalizedName,
            Color = normalizedColor,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tags.Add(tag);
        await _dbContext.SaveChangesAsync();

        return tag;
    }

    public async Task<Tag> GetByIdAsync(Guid tagId, Guid userId)
    {
        var tag = await _dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tagId);

        if (tag is null)
        {
            throw new KeyNotFoundException("Tag not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(tag.ProjectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        return tag;
    }

    public async Task<IReadOnlyList<Tag>> ListAsync(Guid projectId, Guid userId)
    {
        var projectExists = await _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(x => x.Id == projectId);

        if (!projectExists)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(projectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        return await _dbContext.Tags
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Tag> UpdateAsync(Guid tagId, Guid userId, UpdateTagDto data)
    {
        var tag = await _dbContext.Tags
            .FirstOrDefaultAsync(x => x.Id == tagId);

        if (tag is null)
        {
            throw new KeyNotFoundException("Tag not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(tag.ProjectId, userId, ProjectRole.Moderator);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        if (data.Name is null && data.Color is null)
        {
            throw new InvalidOperationException("At least one of name or color must be provided.");
        }

        if (data.Name is not null)
        {
            var normalizedName = NormalizeName(data.Name);
            var duplicateExists = await _dbContext.Tags
                .AnyAsync(x => x.ProjectId == tag.ProjectId
                    && x.Id != tag.Id
                    && x.Name.ToLower() == normalizedName.ToLower());

            if (duplicateExists)
            {
                throw new InvalidOperationException("Tag name must be unique within the project.");
            }

            tag.Name = normalizedName;
        }

        if (data.Color is not null)
        {
            tag.Color = NormalizeColor(data.Color);
        }

        await _dbContext.SaveChangesAsync();
        return tag;
    }

    public async Task DeleteAsync(Guid tagId, Guid userId)
    {
        var tag = await _dbContext.Tags
            .FirstOrDefaultAsync(x => x.Id == tagId);

        if (tag is null)
        {
            throw new KeyNotFoundException("Tag not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(tag.ProjectId, userId, ProjectRole.Moderator);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var cardTags = await _dbContext.CardTags
            .Where(x => x.TagId == tag.Id)
            .ToListAsync();

        if (cardTags.Count > 0)
        {
            _dbContext.CardTags.RemoveRange(cardTags);
        }

        _dbContext.Tags.Remove(tag);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<bool> CheckProjectAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole)
    {
        var role = await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId && x.UserId == userId)
            .Select(x => (ProjectRole?)x.Role)
            .FirstOrDefaultAsync();

        if (role is null)
        {
            return false;
        }

        return HasRequiredRole(role.Value, minimumRole);
    }

    private static bool HasRequiredRole(ProjectRole actualRole, ProjectRole minimumRole)
    {
        return actualRole <= minimumRole;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Tag name is required.");
        }

        return name.Trim();
    }

    private static string NormalizeColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            throw new InvalidOperationException("Tag color is required.");
        }

        var trimmedColor = color.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedColor, "^#[0-9A-Fa-f]{6}$"))
        {
            throw new InvalidOperationException("Tag color must be a hex string in the format #RRGGBB.");
        }

        return trimmedColor.ToUpperInvariant();
    }
}
