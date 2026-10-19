using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using SenYapi.Core.Constants;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class PermissionService : IPermissionService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuditLogService _auditLogService;

    public PermissionService(UserManager<AppUser> userManager, IAuditLogService auditLogService)
    {
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    public Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permission)
    {
        if (user == null || user.Identity?.IsAuthenticated != true)
            return Task.FromResult(false);

        // 1. Sistem Yöneticisi her yetkiye sahiptir ve hiçbir zaman kısıtlanamaz
        if (user.IsInRole("SystemAdmin") || user.FindFirst(ClaimTypes.Email)?.Value == "admin@senyapi.com")
            return Task.FromResult(true);

        // 2. Panel erişimi gerektiren bir yetki ise, kullanıcının en başta Admin.Access yetkisi olmalıdır
        if (permission != Permissions.AdminAccess && !user.HasClaim("Permission", Permissions.AdminAccess))
        {
            return Task.FromResult(false);
        }

        // 3. Normal Admin ve Editör kullanıcıları yalnızca kendilerine açıkça verilmiş claim'e sahipse işlem yapabilir
        return Task.FromResult(user.HasClaim("Permission", permission));
    }

    public async Task<bool> HasPermissionByUserIdAsync(string userId, string permission)
    {
        if (string.IsNullOrEmpty(userId))
            return false;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.IsBlocked || user.IsDeactivated)
            return false;

        if (await _userManager.IsInRoleAsync(user, "SystemAdmin") || user.Email == "admin@senyapi.com")
            return true;

        var claims = await _userManager.GetClaimsAsync(user);
        var permClaims = claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToHashSet();

        if (permission != Permissions.AdminAccess && !permClaims.Contains(Permissions.AdminAccess))
            return false;

        return permClaims.Contains(permission);
    }

    public async Task<List<string>> GetUserPermissionsAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return new List<string>();

        // Sistem yöneticisi tüm yetkilere sahiptir
        if (await _userManager.IsInRoleAsync(user, "SystemAdmin") || user.Email == "admin@senyapi.com")
        {
            return Permissions.All.Select(p => p.Key).ToList();
        }

        var claims = await _userManager.GetClaimsAsync(user);
        return claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList();
    }

    public async Task<(bool Success, string Message)> UpdateUserPermissionsAsync(string targetUserId, List<string> selectedPermissions, string adminUserName)
    {
        var targetUser = await _userManager.FindByIdAsync(targetUserId);
        if (targetUser == null)
            return (false, "Kullanıcı bulunamadı.");

        // Guard: Sistem Yöneticisinin yetkileri değiştirilemez/kapatılamaz
        if (await _userManager.IsInRoleAsync(targetUser, "SystemAdmin") || targetUser.Email == "admin@senyapi.com")
        {
            return (false, "Sistem Yöneticisi (SystemAdmin) hesabının yetkileri kısıtlanamaz veya değiştirilemez.");
        }

        selectedPermissions ??= new List<string>();

        // Yalnızca sistemde kayıtlı geçerli anahtarları filtrele
        var validKeys = Permissions.All.Select(p => p.Key).ToHashSet();
        var normalizedPermissions = selectedPermissions.Where(p => validKeys.Contains(p)).Distinct().ToList();

        // Bağımlılık Yönetimi (Dependent Permissions):
        // Alt yetki seçildiyse (örn: Blog.Edit), üst yetki (Blog.View) ve panel erişimi (Admin.Access) zorunlu olarak eklenir
        var permMap = Permissions.All.ToDictionary(p => p.Key);
        var resolved = new HashSet<string>(normalizedPermissions);

        foreach (var pKey in normalizedPermissions)
        {
            if (permMap.TryGetValue(pKey, out var item) && !string.IsNullOrEmpty(item.ParentDependency))
            {
                resolved.Add(item.ParentDependency);
                if (permMap.TryGetValue(item.ParentDependency, out var parentItem) && !string.IsNullOrEmpty(parentItem.ParentDependency))
                {
                    resolved.Add(parentItem.ParentDependency);
                }
            }
        }

        // Eğer panel yetkisi (Admin.Access) kapalıysa, diğer hiçbir modül yetkisi verilemez
        if (!resolved.Contains(Permissions.AdminAccess) && resolved.Count > 0)
        {
            resolved.Clear();
        }

        // Mevcut izin claim'lerini kaldır
        var currentClaims = await _userManager.GetClaimsAsync(targetUser);
        var existingPermClaims = currentClaims.Where(c => c.Type == "Permission").ToList();
        if (existingPermClaims.Any())
        {
            await _userManager.RemoveClaimsAsync(targetUser, existingPermClaims);
        }

        // Yeni izin claim'lerini ekle
        var newClaims = resolved.Select(p => new Claim("Permission", p)).ToList();
        if (newClaims.Any())
        {
            await _userManager.AddClaimsAsync(targetUser, newClaims);
        }

        // Açık oturumun bu değişikliği anında (bir sonraki istekte) yansıtması için SecurityStamp güncellenir
        await _userManager.UpdateSecurityStampAsync(targetUser);

        // Kimin, kimin yetkisini ne zaman değiştirdiğini denetim günlüğüne (AuditLog) kaydet
        await _auditLogService.LogAsync(
            adminUserName,
            "Kişisel Yetki Güncelleme",
            "AppUser",
            $"{targetUser.UserName} kullanıcısının yetkileri güncellendi. Aktif Yetki Sayısı: {newClaims.Count}");

        return (true, $"'{targetUser.UserName}' kullanıcısının yetkileri başarıyla güncellendi.");
    }

    public List<PermissionCategoryGroup> GetCategorizedPermissions()
    {
        return Permissions.All
            .GroupBy(p => p.Category)
            .Select(g => new PermissionCategoryGroup
            {
                CategoryName = g.Key,
                Permissions = g.ToList()
            })
            .ToList();
    }
}
