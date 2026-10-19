using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SenYapi.Core.Constants;
using SenYapi.Web.Filters;

namespace SenYapi.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdmin,Admin,Editor")]
[HasPermission(Permissions.AdminAccess)]
public abstract class AdminBaseController : Controller
{
    protected string CurrentAdminName => User.Identity?.Name ?? "Yönetici";
    protected string CurrentAdminId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
