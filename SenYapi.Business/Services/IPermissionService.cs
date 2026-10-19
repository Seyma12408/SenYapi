using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using SenYapi.Core.Constants;

namespace SenYapi.Business.Services;

public class PermissionCategoryGroup
{
    public string CategoryName { get; set; } = string.Empty;
    public List<PermissionItem> Permissions { get; set; } = new();
}

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permission);
    Task<bool> HasPermissionByUserIdAsync(string userId, string permission);
    Task<List<string>> GetUserPermissionsAsync(string userId);
    Task<(bool Success, string Message)> UpdateUserPermissionsAsync(string targetUserId, List<string> selectedPermissions, string adminUserName);
    List<PermissionCategoryGroup> GetCategorizedPermissions();
}
