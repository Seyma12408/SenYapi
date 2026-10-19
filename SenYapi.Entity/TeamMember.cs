using System.ComponentModel.DataAnnotations.Schema;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class TeamMember : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? TwitterUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? InstagramUrl { get; set; }

    [NotMapped]
    public string Title
    {
        get => Designation;
        set => Designation = value;
    }

    [NotMapped]
    public int DisplayOrder { get; set; } = 0;
}
