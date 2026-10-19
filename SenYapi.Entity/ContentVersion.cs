using System;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class ContentVersion : BaseEntity
{
    public string EntityType { get; set; } = string.Empty; // "Project", "BlogPost"
    public int EntityId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ExtraData { get; set; }
    public string EditorUserName { get; set; } = string.Empty;
}
