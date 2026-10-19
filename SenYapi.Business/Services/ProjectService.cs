using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class ProjectService : IProjectService
{
    private readonly SenYapiDbContext _context;
    private readonly IContentVersionService _versionService;
    private readonly IFileService _fileService;

    public ProjectService(SenYapiDbContext context, IContentVersionService versionService, IFileService fileService)
    {
        _context = context;
        _versionService = versionService;
        _fileService = fileService;
    }

    public async Task<IEnumerable<Project>> GetAllProjectsAsync(bool onlyActive = false)
    {
        var query = _context.Projects
            .Include(p => p.Images)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (onlyActive)
            query = query.Where(p => p.IsActive);

        return await query.OrderBy(p => p.DisplayOrder).ThenByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetTrashProjectsAsync()
    {
        return await _context.Projects
            .Include(p => p.Images)
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ToListAsync();
    }

    public async Task<Project?> GetProjectByIdAsync(int id, bool includeDeleted = false)
    {
        var query = _context.Projects.Include(p => p.Images).AsQueryable();
        if (!includeDeleted)
            query = query.Where(p => !p.IsDeleted);

        return await query.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project?> GetProjectBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var cleanSlug = slug.Trim().ToLowerInvariant();
        return await _context.Projects
            .Include(p => p.Images)
            .Where(p => !p.IsDeleted)
            .FirstOrDefaultAsync(p => p.Slug.ToLower() == cleanSlug);
    }

    public async Task AddProjectAsync(Project project, string adminUserName)
    {
        if (string.IsNullOrWhiteSpace(project.Slug))
        {
            project.Slug = await SuggestUniqueSlugAsync(project.Title);
        }
        else
        {
            var clean = SlugHelper.GenerateSlug(project.Slug);
            if (string.IsNullOrWhiteSpace(clean))
            {
                clean = await SuggestUniqueSlugAsync(project.Title);
            }
            project.Slug = clean;
        }

        project.CreatedAt = DateTime.UtcNow;
        project.IsDeleted = false;

        await _context.Projects.AddAsync(project);
        await _context.SaveChangesAsync();

        // Save Initial Version
        await _versionService.SaveVersionAsync("Project", project.Id, project.Title, project.Description, project.CategoryName, adminUserName);
    }

    public async Task UpdateProjectAsync(Project project, string adminUserName)
    {
        var existing = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == project.Id);
        if (existing != null)
        {
            // Snapshot previous state before update
            await _versionService.SaveVersionAsync("Project", existing.Id, existing.Title, existing.Description, existing.CategoryName, adminUserName);
        }

        // If slug is null or whitespace on update, keep existing slug (NEVER regenerate automatically on title changes)
        if (string.IsNullOrWhiteSpace(project.Slug))
        {
            project.Slug = existing?.Slug ?? SlugHelper.GenerateSlug(project.Title);
        }
        else
        {
            project.Slug = SlugHelper.GenerateSlug(project.Slug);
        }

        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteProjectAsync(int id, string adminUserName)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project != null)
        {
            project.IsDeleted = true;
            project.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RestoreProjectAsync(int id, string adminUserName)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project != null)
        {
            project.IsDeleted = false;
            project.DeletedAt = null;
            // Ensure unique slug when restoring
            project.Slug = await SuggestUniqueSlugAsync(project.Slug, project.Id);
            await _context.SaveChangesAsync();
        }
    }

    public async Task PermanentDeleteProjectAsync(int id, string adminUserName)
    {
        var project = await _context.Projects.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
        if (project != null)
        {
            _fileService.DeleteFile(project.ImageUrl);
            if (!string.IsNullOrEmpty(project.PdfBrochureUrl))
                _fileService.DeleteFile(project.PdfBrochureUrl);

            foreach (var img in project.Images)
            {
                _fileService.DeleteFile(img.ImageUrl);
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddProjectImageAsync(ProjectImage image)
    {
        await _context.ProjectImages.AddAsync(image);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteProjectImageAsync(int imageId)
    {
        var img = await _context.ProjectImages.FindAsync(imageId);
        if (img != null)
        {
            _fileService.DeleteFile(img.ImageUrl);
            _context.ProjectImages.Remove(img);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> IsSlugInUseAsync(string slug, int excludeProjectId = 0)
    {
        if (string.IsNullOrWhiteSpace(slug)) return false;
        var cleanSlug = slug.Trim().ToLowerInvariant();

        // 1. Check active projects
        var inProjects = await _context.Projects.AnyAsync(p => p.Slug.ToLower() == cleanSlug && p.Id != excludeProjectId && !p.IsDeleted);
        if (inProjects) return true;

        // 2. Check active redirects to prevent hijacking old URLs of other projects
        var redirectPath = $"/projeler/{cleanSlug}";
        var inRedirects = await _context.UrlRedirects.AnyAsync(r => r.OldUrl == redirectPath && r.IsActive);
        return inRedirects;
    }

    public async Task<string> SuggestUniqueSlugAsync(string baseSlug, int excludeProjectId = 0)
    {
        var cleanBase = SlugHelper.GenerateSlug(baseSlug);
        if (string.IsNullOrWhiteSpace(cleanBase))
            cleanBase = "proje";

        string uniqueSlug = cleanBase;
        int count = 1;
        while (await IsSlugInUseAsync(uniqueSlug, excludeProjectId))
        {
            uniqueSlug = $"{cleanBase}-{count++}";
        }
        return uniqueSlug;
    }
}
