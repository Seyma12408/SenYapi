using System;
using System.Collections.Generic;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class ContactMessage : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public bool IsReplied { get; set; } = false;
    public string? ReplyMessage { get; set; }
    public DateTime? RepliedAt { get; set; }
    public string? RepliedBy { get; set; }
    public bool IsArchived { get; set; } = false;

    // Özel konuşma ve güvenli takip token'ı
    public string AccessToken { get; set; } = Guid.NewGuid().ToString("N");
    public string? UserId { get; set; }
    public bool UserHasUnreadReply { get; set; } = false;
    public bool AdminHasUnreadReply { get; set; } = false;

    public List<ContactMessageReply> Replies { get; set; } = new();
}
