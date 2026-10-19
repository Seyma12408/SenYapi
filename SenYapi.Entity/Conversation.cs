using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class Conversation : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "Acik"; // "Acik", "Cevaplandi", "Kapatildi"

    public bool UserUnread { get; set; } = false;
    public bool AdminUnread { get; set; } = true;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public List<ConversationMessage> Messages { get; set; } = new();

    [NotMapped]
    public bool IsOpen => Status != "Kapatildi";
}
