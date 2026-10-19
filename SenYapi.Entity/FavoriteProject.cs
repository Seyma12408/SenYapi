using System;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class FavoriteProject : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }
}
