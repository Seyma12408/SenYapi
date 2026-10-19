using System;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class ContactMessageReply : BaseEntity
{
    public int ContactMessageId { get; set; }
    public ContactMessage? ContactMessage { get; set; }

    public string ReplyText { get; set; } = string.Empty;
    public string RepliedBy { get; set; } = string.Empty;
    public DateTime RepliedAt { get; set; } = DateTime.UtcNow;
    public bool IsSuccess { get; set; } = false;
    public string? ErrorMessage { get; set; }

    // Karşılıklı konuşma alanları
    public bool IsStaffReply { get; set; } = true;
    public string? SenderName { get; set; }
    public string? UserId { get; set; }
}
