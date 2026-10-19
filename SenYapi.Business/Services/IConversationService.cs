using System.Collections.Generic;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public interface IConversationService
{
    Task<Conversation> CreateConversationAsync(string userId, string subject, string initialMessage, string senderName);
    Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId);
    Task<IEnumerable<Conversation>> GetAllConversationsAsync();
    Task<Conversation?> GetConversationByIdAsync(int id);
    Task<ConversationMessage> SendMessageAsync(int conversationId, string senderId, string senderName, bool isAdmin, string message);
    Task MarkMessagesAsReadAsync(int conversationId, bool isAdmin);
    Task CloseConversationAsync(int id);
}
