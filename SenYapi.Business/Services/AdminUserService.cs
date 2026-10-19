using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class AdminUserService : IAdminUserService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IBlogService _blogService;
    private readonly IFileService _fileService;
    private readonly IAuditLogService _auditLogService;

    public AdminUserService(
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        IBlogService blogService,
        IFileService fileService,
        IAuditLogService auditLogService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _blogService = blogService;
        _fileService = fileService;
        _auditLogService = auditLogService;
    }

    public async Task<IEnumerable<AppUser>> GetAllUsersAsync()
    {
        return await _userManager.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
    }

    public async Task<Dictionary<string, string>> GetUserRolesMapAsync(IEnumerable<AppUser> users)
    {
        var userRoles = new Dictionary<string, string>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            if (roles.Contains("SystemAdmin"))
                userRoles[u.Id] = "SystemAdmin";
            else if (roles.Contains("Admin"))
                userRoles[u.Id] = "Admin";
            else if (roles.Contains("Editor"))
                userRoles[u.Id] = "Editor";
            else
                userRoles[u.Id] = roles.FirstOrDefault() ?? "User";
        }
        return userRoles;
    }

    public async Task<(bool Success, string Message)> UnlockUserAsync(string userId, string adminUserName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "Kullanıcı bulunamadı.");

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);
        await _auditLogService.LogAsync(adminUserName, "Kilit Kaldırma", "AppUser", $"Kullanıcı: {user.UserName}");

        return (true, $"'{user.UserName}' kullanıcısının hesap kilidi kaldırıldı.");
    }

    public async Task<(bool Success, string Message, bool? IsBlocked)> ToggleBlockUserAsync(string userId, string adminUserName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "Kullanıcı bulunamadı.", null);

        // SystemAdmin Guard: Sistem Yöneticisi hesabı asla engellenemez
        if (await _userManager.IsInRoleAsync(user, "SystemAdmin") || user.Email == "admin@senyapi.com")
        {
            return (false, "Sistem Yöneticisi (SystemAdmin) hesabı engellenemez.", null);
        }

        // Last Admin Guard: if user is Admin and we are blocking them, ensure another active admin exists
        if (!user.IsBlocked && await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var activeAdmins = admins.Where(a => !a.IsBlocked && !a.IsDeactivated && a.Id != user.Id);
            if (!activeAdmins.Any())
            {
                return (false, "Sistemdeki son aktif Yönetici (Admin) hesabı engellenemez.", null);
            }
        }

        user.IsBlocked = !user.IsBlocked;
        await _userManager.UpdateAsync(user);

        // Invalidate session immediately on block/unblock
        await _userManager.UpdateSecurityStampAsync(user);

        await _auditLogService.LogAsync(adminUserName, "Engel Değişimi", "AppUser", $"Kullanıcı: {user.UserName}, Engelli: {user.IsBlocked}");

        var msg = user.IsBlocked
            ? $"'{user.UserName}' hesabı engellendi."
            : $"'{user.UserName}' hesabının engeli kaldırıldı.";

        return (true, msg, user.IsBlocked);
    }

    public async Task<(bool Success, string Message)> ChangeUserRoleAsync(string userId, string newRole, string adminUserName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "Kullanıcı bulunamadı.");

        if (!await _roleManager.RoleExistsAsync(newRole))
            return (false, "Geçersiz rol seçimi.");

        // SystemAdmin Guard 1: Sistem Yöneticisi hesabının yetkisi kaldırılamaz/düşürülemez
        if ((await _userManager.IsInRoleAsync(user, "SystemAdmin") || user.Email == "admin@senyapi.com") && newRole != "SystemAdmin")
        {
            return (false, "Sistem Yöneticisi (SystemAdmin) hesabının yetkisi kaldırılamaz veya rolü düşürülemez.");
        }

        // SystemAdmin Guard 2: Sistem Yöneticisi yetkisi yalnızca açıkça belirlenen sorumlu hesaba tanımlanabilir
        if (newRole == "SystemAdmin" && user.Email != "admin@senyapi.com")
        {
            return (false, "Sistem Yöneticisi (SystemAdmin) yetkisi yalnızca ana sorumlu hesaba tanımlanabilir.");
        }

        // Last Admin Guard: if demoting an admin, ensure at least one active admin remains
        if (await _userManager.IsInRoleAsync(user, "Admin") && newRole != "Admin")
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var activeAdmins = admins.Where(a => !a.IsBlocked && !a.IsDeactivated && a.Id != user.Id);
            if (!activeAdmins.Any())
            {
                return (false, "Sistemdeki son Yönetici (Admin) rolünden düşürülemez.");
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, newRole);

        // Invalidate stale authentication cookie immediately
        await _userManager.UpdateSecurityStampAsync(user);

        await _auditLogService.LogAsync(adminUserName, "Rol Güncelleme", "AppUser", $"Kullanıcı: {user.UserName}, Yeni Rol: {newRole}");

        return (true, $"'{user.UserName}' kullanıcısının rolü '{newRole}' olarak güncellendi.");
    }

    public async Task<(bool Success, string Message)> DeleteUserPermanentlyAsync(string userId, string adminUserName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (false, "Kullanıcı bulunamadı.");

        // SystemAdmin Guard: Sistem Yöneticisi hesabı silinemez
        if (await _userManager.IsInRoleAsync(user, "SystemAdmin") || user.Email == "admin@senyapi.com")
        {
            return (false, "Sistem Yöneticisi (SystemAdmin) hesabı kalıcı olarak silinemez.");
        }

        // Last Admin Guard
        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var activeAdmins = admins.Where(a => !a.IsBlocked && !a.IsDeactivated && a.Id != user.Id);
            if (!activeAdmins.Any())
            {
                return (false, "Sistemdeki son Yönetici (Admin) hesabı silinemez.");
            }
        }

        // Anonymize user comments
        var userComments = await _blogService.GetCommentsByUserIdAsync(user.Id);
        foreach (var c in userComments)
        {
            c.Name = "[Silinmiş Kullanıcı]";
            c.Email = "silindi@senyapi.local";
            c.UserId = null;
            await _blogService.UpdateCommentAsync(c);
        }

        // Delete user photo if any
        if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
        {
            _fileService.DeleteFile(user.ProfilePictureUrl);
        }

        await _userManager.DeleteAsync(user);
        await _auditLogService.LogAsync(adminUserName, "Kullanıcı Silme", "AppUser", $"Silinen Kullanıcı: {user.UserName}");

        return (true, $"'{user.UserName}' hesabı kalıcı olarak silindi ve yorumları anonimleştirildi.");
    }
}
