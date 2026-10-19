using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class NewsletterSubscriber : BaseEntity
{
    public string Email { get; set; } = string.Empty;
}
