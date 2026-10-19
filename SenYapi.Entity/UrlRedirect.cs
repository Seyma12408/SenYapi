using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class UrlRedirect : BaseEntity
{
    public string OldUrl { get; set; } = string.Empty;
    public string NewUrl { get; set; } = string.Empty;
    public int StatusCode { get; set; } = 301;
}
