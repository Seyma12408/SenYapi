using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SenYapi.Business.Services;
using SenYapi.Core.Constants;
using SenYapi.Web.Filters;

namespace SenYapi.Web.Controllers.Admin;

[Route("Admin")]
[Authorize(Roles = "SystemAdmin,Admin,Editor")]
public class AdminMessagesController : AdminBaseController
{
    private readonly IContactService _contactService;
    private readonly INewsletterService _newsletterService;
    private readonly IConversationService _conversationService;
    private readonly IAuditLogService _auditLogService;

    public AdminMessagesController(
        IContactService contactService,
        INewsletterService newsletterService,
        IConversationService conversationService,
        IAuditLogService auditLogService)
    {
        _contactService = contactService;
        _newsletterService = newsletterService;
        _conversationService = conversationService;
        _auditLogService = auditLogService;
    }

    [HttpGet("Messages")]
    [HasPermission(Permissions.MessageView)]
    public async Task<IActionResult> Messages(string? search, string? status)
    {
        var messages = await _contactService.GetAllMessagesAsync(search, status);
        var allStats = await _contactService.GetAllMessagesAsync(null, "all");

        ViewBag.TotalCount = allStats.Count(m => !m.IsArchived);
        ViewBag.UnreadCount = allStats.Count(m => !m.IsRead && !m.IsArchived);
        ViewBag.ReadCount = allStats.Count(m => m.IsRead && !m.IsReplied && !m.IsArchived);
        ViewBag.RepliedCount = allStats.Count(m => m.IsReplied && !m.IsArchived);
        ViewBag.ArchivedCount = allStats.Count(m => m.IsArchived);

        ViewBag.Search = search;
        ViewBag.Status = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();

        return View("~/Views/Admin/Messages.cshtml", messages.ToList());
    }

    [HttpGet("MessageDetail/{id}")]
    [HasPermission(Permissions.MessageView)]
    public async Task<IActionResult> MessageDetail(int id)
    {
        var message = await _contactService.GetMessageByIdAsync(id);
        if (message == null)
        {
            TempData["AdminError"] = "Mesaj bulunamadı veya silinmiş.";
            return RedirectToAction(nameof(Messages));
        }

        // Detay açıldığında okunmadıysa okundu olarak işaretle
        if (!message.IsRead)
        {
            await _contactService.MarkAsReadAsync(id);
            message.IsRead = true;
        }

        return View("~/Views/Admin/MessageDetail.cshtml", message);
    }

    [HttpPost("ReplyMessage")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.MessageReply)]
    public async Task<IActionResult> ReplyMessage(int id, string replyText, string? returnAction = null)
    {
        if (string.IsNullOrWhiteSpace(replyText))
        {
            TempData["AdminError"] = "Cevap metni boş olamaz.";
            if (returnAction == "MessageDetail")
                return RedirectToAction(nameof(MessageDetail), new { id });
            return RedirectToAction(nameof(Messages));
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var (success, errorMessage) = await _contactService.ReplyToMessageAsync(id, replyText, CurrentAdminName, baseUrl);
        if (success)
        {
            TempData["AdminSuccess"] = "Yanıt kaydedildi ve e-posta başarıyla gönderildi.";
            await _auditLogService.LogAsync(CurrentAdminName, "Mesaj Yanıtlama", "ContactMessage", $"MessageId: {id}");
        }
        else
        {
            TempData["AdminError"] = errorMessage ?? "Mesaj yanıtlanırken bir hata oluştu.";
        }

        if (returnAction == "MessageDetail")
            return RedirectToAction(nameof(MessageDetail), new { id });

        return RedirectToAction(nameof(Messages));
    }

    [HttpPost("RetryReply/{replyId}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.MessageReply)]
    public async Task<IActionResult> RetryReply(int replyId, int messageId)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var (success, errorMessage) = await _contactService.RetryReplyAsync(replyId, CurrentAdminName, baseUrl);
        if (success)
        {
            TempData["AdminSuccess"] = "Yanıt e-posta olarak başarıyla yeniden iletildi.";
            await _auditLogService.LogAsync(CurrentAdminName, "Yanıt Yeniden Gönderme", "ContactMessageReply", $"ReplyId: {replyId}, MessageId: {messageId}");
        }
        else
        {
            TempData["AdminError"] = errorMessage ?? "Yanıt yeniden gönderilirken hata oluştu.";
        }

        return RedirectToAction(nameof(MessageDetail), new { id = messageId });
    }

    [HttpPost("MarkMessageAsRead")]
    [HttpPost("MarkMessageAsRead/{id}")]
    [HttpPost("MarkMessageRead")]
    [HttpPost("MarkMessageRead/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.MessageView)]
    public async Task<IActionResult> MarkMessageAsRead(int id, string? returnAction = null)
    {
        await _contactService.MarkAsReadAsync(id);
        TempData["AdminSuccess"] = "Mesaj okundu olarak işaretlendi.";

        if (returnAction == "MessageDetail")
            return RedirectToAction(nameof(MessageDetail), new { id });

        return RedirectToAction(nameof(Messages));
    }

    [HttpPost("MarkMessageAsUnread/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.MessageView)]
    public async Task<IActionResult> MarkMessageAsUnread(int id, string? returnAction = null)
    {
        await _contactService.MarkAsUnreadAsync(id);
        TempData["AdminSuccess"] = "Mesaj okunmadı olarak işaretlendi.";

        if (returnAction == "MessageDetail")
            return RedirectToAction(nameof(MessageDetail), new { id });

        return RedirectToAction(nameof(Messages));
    }

    [HttpPost("ToggleArchiveMessage/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.MessageView)]
    public async Task<IActionResult> ToggleArchiveMessage(int id, string? returnAction = null)
    {
        await _contactService.ToggleArchiveAsync(id);
        TempData["AdminSuccess"] = "Mesajın arşiv durumu güncellendi.";

        if (returnAction == "MessageDetail")
            return RedirectToAction(nameof(MessageDetail), new { id });

        return RedirectToAction(nameof(Messages));
    }

    [HttpPost("DeleteMessage")]
    [HttpPost("DeleteMessage/{id}")]
    [ValidateAntiForgeryToken]
    [HasPermission(Permissions.MessageDelete)]
    public async Task<IActionResult> DeleteMessage(int id, string? returnAction = null)
    {
        await _contactService.DeleteMessageAsync(id);
        TempData["AdminSuccess"] = "Mesaj silindi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Mesaj Silme", "ContactMessage", $"MessageId: {id}");

        if (returnAction == "Index")
            return RedirectToAction("Index", "AdminDashboard");

        return RedirectToAction(nameof(Messages));
    }

    [HttpGet("Subscribers")]
    public async Task<IActionResult> Subscribers()
    {
        var subs = await _newsletterService.GetAllSubscribersAsync();
        return View("~/Views/Admin/Subscribers.cshtml", subs.ToList());
    }

    [HttpPost("DeleteSubscriber")]
    [HttpPost("DeleteSubscriber/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubscriber(int id)
    {
        await _newsletterService.DeleteSubscriberAsync(id);
        TempData["AdminSuccess"] = "Bülten abonesi silindi.";
        await _auditLogService.LogAsync(CurrentAdminName, "Abone Silme", "NewsletterSubscriber", $"SubscriberId: {id}");
        return RedirectToAction(nameof(Subscribers));
    }

    [HttpGet("Conversations")]
    public async Task<IActionResult> Conversations()
    {
        var conversations = await _conversationService.GetAllConversationsAsync();
        return View("~/Views/Admin/Conversations.cshtml", conversations.ToList());
    }

    [HttpGet("ConversationDetail/{id}")]
    public async Task<IActionResult> ConversationDetail(int id)
    {
        var conv = await _conversationService.GetConversationByIdAsync(id);
        if (conv == null)
        {
            TempData["AdminError"] = "Konuşma bulunamadı.";
            return RedirectToAction(nameof(Conversations));
        }

        await _conversationService.MarkMessagesAsReadAsync(id, isAdmin: true);
        return View("~/Views/Admin/ConversationDetail.cshtml", conv);
    }

    [HttpPost("ReplyConversation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyConversation(int conversationId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            TempData["AdminError"] = "Mesaj boş olamaz.";
            return RedirectToAction(nameof(ConversationDetail), new { id = conversationId });
        }

        await _conversationService.SendMessageAsync(conversationId, CurrentAdminId, CurrentAdminName, isAdmin: true, message.Trim());
        TempData["AdminSuccess"] = "Mesajınız gönderildi.";
        return RedirectToAction(nameof(ConversationDetail), new { id = conversationId });
    }

    [HttpPost("CloseConversation")]
    [HttpPost("CloseConversation/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseConversation(int id)
    {
        await _conversationService.CloseConversationAsync(id);
        TempData["AdminSuccess"] = "Konuşma kapatıldı.";
        return RedirectToAction(nameof(Conversations));
    }
}
