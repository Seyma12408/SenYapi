using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IContentVersionService
{
    Task SaveVersionAsync(string entityType, int entityId, string title, string content, string? summary, string editorUserName, string? extraData = null);
    Task<IEnumerable<ContentVersion>> GetVersionsAsync(string entityType, int entityId);
    Task<ContentVersion?> GetVersionByIdAsync(int versionId);
}
