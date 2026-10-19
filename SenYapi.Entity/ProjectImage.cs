using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class ProjectImage : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; } = 0;
}
