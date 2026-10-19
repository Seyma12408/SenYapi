using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class ContentVersionService : IContentVersionService
{
    private readonly SenYapiDbContext _context;

    public ContentVersionService(SenYapiDbContext context)
    {
        _context = context;
    }

    public async Task SaveVersionAsync(string entityType, int entityId, string title, string content, string? summary, string editorUserName, string? extraData = null)
    {
        var lastVersionNumber = await _context.ContentVersions
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        var version = new ContentVersion
        {
            EntityType = entityType,
            EntityId = entityId,
            VersionNumber = lastVersionNumber + 1,
            Title = title,
            Content = content,
            Summary = summary,
            ExtraData = extraData,
            EditorUserName = editorUserName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.ContentVersions.AddAsync(version);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ContentVersion>> GetVersionsAsync(string entityType, int entityId)
    {
        return await _context.ContentVersions
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();
    }

    public async Task<ContentVersion?> GetVersionByIdAsync(int versionId)
    {
        return await _context.ContentVersions.FindAsync(versionId);
    }
}
