using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class ConversationService : IConversationService
{
    private readonly SenYapiDbContext _context;
    private readonly INotificationService _notificationService;

    public ConversationService(SenYapiDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<Conversation> CreateConversationAsync(string userId, string subject, string initialMessage, string senderName)
    {
        var conv = new Conversation
        {
            UserId = userId,
            Subject = subject.Trim(),
            Status = "Acik",
            UserUnread = false,
            AdminUnread = true,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.Conversations.AddAsync(conv);
        await _context.SaveChangesAsync();

        var msg = new ConversationMessage
        {
            ConversationId = conv.Id,
            SenderId = userId,
            SenderName = senderName,
            IsAdmin = false,
            Message = initialMessage.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.ConversationMessages.AddAsync(msg);
        await _context.SaveChangesAsync();

        return conv;
    }

    public async Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId)
    {
        return await _context.Conversations
            .Include(c => c.Messages)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Conversation>> GetAllConversationsAsync()
    {
        return await _context.Conversations
            .Include(c => c.User)
            .Include(c => c.Messages)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();
    }

    public async Task<Conversation?> GetConversationByIdAsync(int id)
    {
        return await _context.Conversations
            .Include(c => c.User)
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<ConversationMessage> SendMessageAsync(int conversationId, string senderId, string senderName, bool isAdmin, string message)
    {
        var conv = await _context.Conversations.FindAsync(conversationId);
        if (conv == null) throw new InvalidOperationException("Konuşma bulunamadı.");

        var msg = new ConversationMessage
        {
            ConversationId = conversationId,
            SenderId = senderId,
            SenderName = senderName,
            IsAdmin = isAdmin,
            Message = message.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        conv.LastMessageAt = DateTime.UtcNow;
        if (isAdmin)
        {
            conv.UserUnread = true;
            conv.AdminUnread = false;
            conv.Status = "Cevaplandi";
        }
        else
        {
            conv.AdminUnread = true;
            conv.UserUnread = false;
            conv.Status = "Acik";
        }

        await _context.ConversationMessages.AddAsync(msg);
        await _context.SaveChangesAsync();

        if (isAdmin)
        {
            await _notificationService.SendNotificationAsync(
                conv.UserId,
                "Yeni Mesajınız Var",
                $"'{conv.Subject}' konulu konuşmanıza Şen Yapı yöneticisinden yanıt geldi.",
                $"/hesap/mesajlar/{conv.Id}"
            );
        }

        return msg;
    }

    public async Task MarkMessagesAsReadAsync(int conversationId, bool isAdmin)
    {
        var conv = await _context.Conversations.Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conv != null)
        {
            if (isAdmin)
            {
                conv.AdminUnread = false;
                foreach (var msg in conv.Messages.Where(m => !m.IsAdmin && !m.IsRead))
                {
                    msg.IsRead = true;
                }
            }
            else
            {
                conv.UserUnread = false;
                foreach (var msg in conv.Messages.Where(m => m.IsAdmin && !m.IsRead))
                {
                    msg.IsRead = true;
                }
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task CloseConversationAsync(int id)
    {
        var conv = await _context.Conversations.FindAsync(id);
        if (conv != null)
        {
            conv.Status = "Kapatildi";
            await _context.SaveChangesAsync();
        }
    }
}
