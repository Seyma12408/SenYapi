using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class NotificationService : INotificationService
{
    private readonly SenYapiDbContext _context;

    public NotificationService(SenYapiDbContext context)
    {
        _context = context;
    }

    public async Task SendNotificationAsync(string userId, string title, string message, string? linkUrl = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var notif = new InAppNotification
        {
            UserId = userId,
            Title = title.Trim(),
            Message = message.Trim(),
            LinkUrl = linkUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _context.InAppNotifications.AddAsync(notif);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<InAppNotification>> GetUserNotificationsAsync(string userId)
    {
        return await _context.InAppNotifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.InAppNotifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(int notificationId, string userId)
    {
        var notif = await _context.InAppNotifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notif != null)
        {
            notif.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var unread = await _context.InAppNotifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var item in unread)
        {
            item.IsRead = true;
        }
        await _context.SaveChangesAsync();
    }
}
