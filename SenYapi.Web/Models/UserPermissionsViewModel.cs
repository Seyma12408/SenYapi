using System.Collections.Generic;
using SenYapi.Business.Services;
using SenYapi.Entity;

namespace SenYapi.Web.Models;

public class UserPermissionsViewModel
{
    public AppUser User { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public bool IsSystemAdmin { get; set; }
    public List<PermissionCategoryGroup> Categories { get; set; } = new();
    public HashSet<string> AssignedPermissions { get; set; } = new();
}
