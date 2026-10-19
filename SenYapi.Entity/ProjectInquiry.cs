using System;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class ProjectInquiry : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public string? UserId { get; set; }
    public AppUser? User { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PreferredContactMethod { get; set; } = "Telefon"; // "Telefon", "E-Posta", "WhatsApp"
    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = "Yeni"; // "Yeni", "Islemde", "Tamamlandi"
    public string? AdminReply { get; set; }
    public DateTime? AdminReplyAt { get; set; }
}
