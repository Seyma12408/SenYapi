using System;
using Microsoft.AspNetCore.Identity;

namespace SenYapi.Entity;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public bool IsBlocked { get; set; } = false;
    public bool IsDeactivated { get; set; } = false;
    public DateTime? DeactivatedAt { get; set; }
    public bool DeletionRequested { get; set; } = false;
    public DateTime? DeletionRequestedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
