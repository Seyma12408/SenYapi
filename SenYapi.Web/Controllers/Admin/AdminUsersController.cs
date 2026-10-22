using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Entity;
using SenYapi.Web.Models;

namespace SenYapi.Web.Controllers.Admin;

[Route("Admin")]
[Authorize(Roles = "SystemAdmin")]
public class AdminUsersController : AdminBaseController
{
    private readonly IAdminUserService _adminUserService;
    private readonly IPermissionService _permissionService;
    private readonly UserManager<AppUser> _userManager;

    public AdminUsersController(
        IAdminUserService adminUserService,
        IPermissionService permissionService,
        UserManager<AppUser> userManager)
    {
        _adminUserService = adminUserService;
        _permissionService = permissionService;
        _userManager = userManager;
    }

    [HttpGet("Users")]
    public async Task<IActionResult> Users()
    {
        var users = (await _adminUserService.GetAllUsersAsync()).ToList();
        var userRoles = await _adminUserService.GetUserRolesMapAsync(users);

        ViewBag.UserRoles = userRoles;
        return View("~/Views/Admin/Users.cshtml", users);
    }

    [HttpPost("UnlockUser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockUser(string userId)
    {
        var (success, message) = await _adminUserService.UnlockUserAsync(userId, CurrentAdminName);
        if (success)
            TempData["AdminSuccess"] = message;
        else
            TempData["AdminError"] = message;

        return RedirectToAction(nameof(Users));
    }

    [HttpPost("ToggleBlockUser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBlockUser(string userId)
    {
        var (success, message, _) = await _adminUserService.ToggleBlockUserAsync(userId, CurrentAdminName);
        if (success)
            TempData["AdminSuccess"] = message;
        else
            TempData["AdminError"] = message;

        return RedirectToAction(nameof(Users));
    }

    [HttpPost("ChangeUserRole")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeUserRole(string userId, string newRole)
    {
        if (string.IsNullOrWhiteSpace(userId) || !ValidationHelper.IsValidRole(newRole))
        {
            TempData["AdminError"] = "Geçersiz kullanıcı rolü seçimi yapıldı.";
            return RedirectToAction(nameof(Users));
        }

        var (success, message) = await _adminUserService.ChangeUserRoleAsync(userId, newRole, CurrentAdminName);
        if (success)
            TempData["AdminSuccess"] = message;
        else
            TempData["AdminError"] = message;

        return RedirectToAction(nameof(Users));
    }

    [HttpPost("DeleteUserPermanently")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserPermanently(string userId)
    {
        var (success, message) = await _adminUserService.DeleteUserPermanentlyAsync(userId, CurrentAdminName);
        if (success)
            TempData["AdminSuccess"] = message;
        else
            TempData["AdminError"] = message;

        return RedirectToAction(nameof(Users));
    }

    [HttpGet("UserPermissions")]
    public async Task<IActionResult> UserPermissions(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Users));

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["AdminError"] = "Kullanıcı bulunamadı.";
            return RedirectToAction(nameof(Users));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User";
        var isSysAdmin = role == "SystemAdmin" || user.Email == "admin@senyapi.com";

        var assigned = await _permissionService.GetUserPermissionsAsync(userId);
        var categories = _permissionService.GetCategorizedPermissions();

        var viewModel = new UserPermissionsViewModel
        {
            User = user,
            Role = role,
            IsSystemAdmin = isSysAdmin,
            Categories = categories,
            AssignedPermissions = new HashSet<string>(assigned)
        };

        return View("~/Views/Admin/UserPermissions.cshtml", viewModel);
    }

    [HttpPost("UpdateUserPermissions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserPermissions(string userId, List<string> selectedPermissions)
    {
        var (success, message) = await _permissionService.UpdateUserPermissionsAsync(userId, selectedPermissions, CurrentAdminName);
        if (success)
            TempData["AdminSuccess"] = message;
        else
            TempData["AdminError"] = message;

        return RedirectToAction(nameof(UserPermissions), new { userId });
    }
}
