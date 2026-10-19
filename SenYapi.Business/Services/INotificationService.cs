using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface INotificationService
{
    Task SendNotificationAsync(string userId, string title, string message, string? linkUrl = null);
    Task<IEnumerable<InAppNotification>> GetUserNotificationsAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(int notificationId, string userId);
    Task MarkAllAsReadAsync(string userId);
}
