using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Business.DTOs;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IContactService
{
    Task<bool> SaveMessageAsync(ContactMessageDto dto);
    Task<IEnumerable<ContactMessage>> GetAllMessagesAsync(string? search = null, string? status = null);
    Task<ContactMessage?> GetMessageByIdAsync(int id);
    Task<ContactMessage?> GetMessageByTokenAsync(int id, string token, string? currentUserId = null);
    Task<(bool Success, string? ErrorMessage)> AddCustomerReplyAsync(int messageId, string token, string replyText, string? userId = null, string? customerName = null);
    Task MarkAsReadAsync(int id);
    Task MarkAsUnreadAsync(int id);
    Task ToggleArchiveAsync(int id);
    Task<(bool Success, string? ErrorMessage)> ReplyToMessageAsync(int id, string replyText, string adminUserName, string? baseUrl = null);
    Task<(bool Success, string? ErrorMessage)> RetryReplyAsync(int replyId, string adminUserName, string? baseUrl = null);
    Task DeleteMessageAsync(int id);
}
