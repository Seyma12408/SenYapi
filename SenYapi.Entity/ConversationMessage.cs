using System;
using System.ComponentModel.DataAnnotations.Schema;
using SenYapi.Core.Entities;

namespace SenYapi.Entity;

public class ConversationMessage : BaseEntity
{
    public int ConversationId { get; set; }
    public Conversation? Conversation { get; set; }

    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; } = false;

    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;

    [NotMapped]
    public bool IsFromAdmin => IsAdmin;

    [NotMapped]
    public DateTime SentAt => CreatedAt;
}
