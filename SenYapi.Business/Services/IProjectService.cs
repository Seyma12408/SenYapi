using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IProjectService
{
    Task<IEnumerable<Project>> GetAllProjectsAsync(bool onlyActive = false);
    Task<IEnumerable<Project>> GetTrashProjectsAsync();
    Task<Project?> GetProjectByIdAsync(int id, bool includeDeleted = false);
    Task<Project?> GetProjectBySlugAsync(string slug);
    Task<bool> IsSlugInUseAsync(string slug, int excludeProjectId = 0);
    Task<string> SuggestUniqueSlugAsync(string baseSlug, int excludeProjectId = 0);
    Task AddProjectAsync(Project project, string adminUserName);
    Task UpdateProjectAsync(Project project, string adminUserName);
    Task SoftDeleteProjectAsync(int id, string adminUserName);
    Task RestoreProjectAsync(int id, string adminUserName);
    Task PermanentDeleteProjectAsync(int id, string adminUserName);
    Task AddProjectImageAsync(ProjectImage image);
    Task DeleteProjectImageAsync(int imageId);
}
