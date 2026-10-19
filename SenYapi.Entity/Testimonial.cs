using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class Testimonial : BaseEntity
{
    public string ClientName { get; set; } = string.Empty;
    public string Profession { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}
