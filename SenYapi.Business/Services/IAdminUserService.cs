using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IAdminUserService
{
    Task<IEnumerable<AppUser>> GetAllUsersAsync();
    Task<Dictionary<string, string>> GetUserRolesMapAsync(IEnumerable<AppUser> users);
    Task<(bool Success, string Message)> UnlockUserAsync(string userId, string adminUserName);
    Task<(bool Success, string Message, bool? IsBlocked)> ToggleBlockUserAsync(string userId, string adminUserName);
    Task<(bool Success, string Message)> ChangeUserRoleAsync(string userId, string newRole, string adminUserName);
    Task<(bool Success, string Message)> DeleteUserPermanentlyAsync(string userId, string adminUserName);
}
