using System;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class InAppNotification : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; } = false;
}
