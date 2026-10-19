using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class ServiceItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}
