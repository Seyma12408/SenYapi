using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SenYapi.Business.DTOs;
using SenYapi.DataAccess;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class ContactService : IContactService
{
    private readonly SenYapiDbContext _context;
    private readonly IEmailService _emailService;

    public ContactService(SenYapiDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<bool> SaveMessageAsync(ContactMessageDto dto)
    {
        var entity = new ContactMessage
        {
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim(),
            Subject = dto.Subject.Trim(),
            Message = dto.Message.Trim(),
            UserId = dto.UserId,
            AccessToken = Guid.NewGuid().ToString("N"),
            IsRead = false,
            IsReplied = false,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.ContactMessages.AddAsync(entity);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<IEnumerable<ContactMessage>> GetAllMessagesAsync(string? search = null, string? status = null)
    {
        IQueryable<ContactMessage> query = _context.ContactMessages
            .Include(m => m.Replies)
            .Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim().ToLower();
            query = query.Where(m =>
                m.Name.ToLower().Contains(cleanSearch) ||
                m.Email.ToLower().Contains(cleanSearch) ||
                m.Subject.ToLower().Contains(cleanSearch) ||
                m.Message.ToLower().Contains(cleanSearch));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToLowerInvariant();
            query = s switch
            {
                "unread" => query.Where(m => !m.IsRead && !m.IsArchived),
                "read" => query.Where(m => m.IsRead && !m.IsReplied && !m.IsArchived),
                "replied" => query.Where(m => m.IsReplied && !m.IsArchived),
                "archived" => query.Where(m => m.IsArchived),
                "all" => query,
                _ => query.Where(m => !m.IsArchived)
            };
        }
        else
        {
            query = query.Where(m => !m.IsArchived);
        }

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task<ContactMessage?> GetMessageByIdAsync(int id)
    {
        var message = await _context.ContactMessages
            .Include(m => m.Replies.OrderBy(r => r.RepliedAt))
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

        if (message != null && message.AdminHasUnreadReply)
        {
            message.AdminHasUnreadReply = false;
            await _context.SaveChangesAsync();
        }

        return message;
    }

    public async Task<ContactMessage?> GetMessageByTokenAsync(int id, string token, string? currentUserId = null)
    {
        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrEmpty(currentUserId))
            return null;

        var message = await _context.ContactMessages
            .Include(m => m.Replies.OrderBy(r => r.RepliedAt))
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

        if (message == null)
            return null;

        bool tokenMatches = !string.IsNullOrWhiteSpace(token) && 
            string.Equals(message.AccessToken, token.Trim(), StringComparison.OrdinalIgnoreCase);
        bool userMatches = !string.IsNullOrEmpty(currentUserId) && 
            string.Equals(message.UserId, currentUserId, StringComparison.OrdinalIgnoreCase);

        if (!tokenMatches && !userMatches)
            return null;

        return message;
    }

    public async Task<(bool Success, string? ErrorMessage)> AddCustomerReplyAsync(int messageId, string token, string replyText, string? userId = null, string? customerName = null)
    {
        var message = await GetMessageByTokenAsync(messageId, token, userId);
        if (message == null)
            return (false, "Mesaj bulunamadı veya erişim yetkiniz yok.");

        if (string.IsNullOrWhiteSpace(replyText) || replyText.Trim().Length < 3)
            return (false, "Yanıt metni en az 3 karakter olmalıdır.");

        var cleanReply = replyText.Trim();

        // Spam ve mükerrer gönderim kontrolü (son 10 saniye)
        var recentDuplicate = message.Replies.FirstOrDefault(r =>
            r.ReplyText == cleanReply &&
            r.RepliedAt > DateTime.UtcNow.AddSeconds(-10));

        if (recentDuplicate != null)
        {
            return (false, "Bu yanıt az önce gönderildi. Lütfen biraz bekleyin.");
        }

        var sender = !string.IsNullOrWhiteSpace(customerName) ? customerName.Trim() : message.Name;

        var replyRecord = new ContactMessageReply
        {
            ContactMessageId = message.Id,
            ReplyText = cleanReply,
            RepliedBy = sender,
            SenderName = sender,
            UserId = userId,
            IsStaffReply = false,
            RepliedAt = DateTime.UtcNow,
            IsSuccess = true,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.ContactMessageReplies.Add(replyRecord);

        // Müşteri yanıt verdiğinde yöneticinin mesaj listesinde "Kullanıcı Yanıtladı" rozeti aktifleşir
        message.AdminHasUnreadReply = true;
        message.UserHasUnreadReply = false;
        message.IsRead = false; // Yönetici panelinde okunmadı olarak öne çıksın

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task MarkAsReadAsync(int id)
    {
        var message = await _context.ContactMessages.FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
        if (message != null && !message.IsRead)
        {
            message.IsRead = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAsUnreadAsync(int id)
    {
        var message = await _context.ContactMessages.FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
        if (message != null)
        {
            message.IsRead = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task ToggleArchiveAsync(int id)
    {
        var message = await _context.ContactMessages.FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
        if (message != null)
        {
            message.IsArchived = !message.IsArchived;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ReplyToMessageAsync(int id, string replyText, string adminUserName, string? baseUrl = null)
    {
        var message = await _context.ContactMessages
            .Include(m => m.Replies)
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

        if (message == null)
            return (false, "Mesaj bulunamadı.");

        if (string.IsNullOrWhiteSpace(replyText))
            return (false, "Cevap metni boş olamaz.");

        var cleanReply = replyText.Trim();

        // Mükerrer / art arda tıklamayla aynı yanıtın gönderilmesini engelle (Son 10 saniye kontrolü)
        var recentDuplicate = message.Replies.FirstOrDefault(r =>
            r.ReplyText == cleanReply &&
            r.RepliedAt > DateTime.UtcNow.AddSeconds(-10));

        if (recentDuplicate != null)
        {
            return (false, "Bu yanıt az önce gönderildi. Mükerrer gönderimleri önlemek için lütfen biraz bekleyin.");
        }

        if (string.IsNullOrWhiteSpace(message.AccessToken))
        {
            message.AccessToken = Guid.NewGuid().ToString("N");
        }

        var replyUrl = !string.IsNullOrWhiteSpace(baseUrl) 
            ? $"{baseUrl.TrimEnd('/')}/iletisim/mesaj/{message.Id}?token={message.AccessToken}"
            : $"/iletisim/mesaj/{message.Id}?token={message.AccessToken}";

        var htmlBody = GenerateEmailTemplate(message.Name, message.Subject, message.CreatedAt, message.Message, cleanReply, adminUserName, replyUrl);

        // E-posta gönderimi (Alıcı adresi kayıtlı mesajdan sunucu tarafında alınır)
        var (emailSent, emailError) = await _emailService.SendEmailAsync(
            message.Email,
            $"Re: {message.Subject} - Şen Yapı",
            htmlBody);

        var replyRecord = new ContactMessageReply
        {
            ContactMessageId = message.Id,
            ReplyText = cleanReply,
            RepliedBy = adminUserName,
            SenderName = adminUserName,
            IsStaffReply = true,
            RepliedAt = DateTime.UtcNow,
            IsSuccess = emailSent,
            ErrorMessage = emailSent ? null : emailError,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.ContactMessageReplies.Add(replyRecord);

        // Mesaj yönetici tarafından incelendiği için okundu durumuna getirilir
        message.IsRead = true;
        message.AdminHasUnreadReply = false;
        message.UserHasUnreadReply = emailSent;

        // "Yanıtlandı" durumu yalnızca e-posta başarıyla gönderildiğinde oluşur
        if (emailSent)
        {
            message.IsReplied = true;
            message.RepliedAt = DateTime.UtcNow;
            message.RepliedBy = adminUserName;
            message.ReplyMessage = cleanReply;
        }

        await _context.SaveChangesAsync();

        if (!emailSent)
        {
            return (false, $"E-posta gönderilemedi: {emailError}. Yanıt sistemde başarısız olarak kaydedildi; ayarlarınızı kontrol edip mesaj detayından yeniden gönderebilirsiniz.");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> RetryReplyAsync(int replyId, string adminUserName, string? baseUrl = null)
    {
        var reply = await _context.ContactMessageReplies
            .Include(r => r.ContactMessage)
            .FirstOrDefaultAsync(r => r.Id == replyId && r.IsActive);

        if (reply == null || reply.ContactMessage == null)
            return (false, "Yanıt kaydı veya ilgili mesaj bulunamadı.");

        var message = reply.ContactMessage;

        if (string.IsNullOrWhiteSpace(message.AccessToken))
        {
            message.AccessToken = Guid.NewGuid().ToString("N");
        }

        var replyUrl = !string.IsNullOrWhiteSpace(baseUrl) 
            ? $"{baseUrl.TrimEnd('/')}/iletisim/mesaj/{message.Id}?token={message.AccessToken}"
            : $"/iletisim/mesaj/{message.Id}?token={message.AccessToken}";

        var htmlBody = GenerateEmailTemplate(message.Name, message.Subject, message.CreatedAt, message.Message, reply.ReplyText, adminUserName, replyUrl);

        var (emailSent, emailError) = await _emailService.SendEmailAsync(
            message.Email,
            $"Re: {message.Subject} - Şen Yapı",
            htmlBody);

        reply.RepliedAt = DateTime.UtcNow;
        reply.RepliedBy = adminUserName;
        reply.SenderName = adminUserName;
        reply.IsStaffReply = true;
        reply.IsSuccess = emailSent;
        reply.ErrorMessage = emailSent ? null : emailError;

        if (emailSent)
        {
            message.IsReplied = true;
            message.RepliedAt = DateTime.UtcNow;
            message.RepliedBy = adminUserName;
            message.ReplyMessage = reply.ReplyText;
            message.UserHasUnreadReply = true;
            message.AdminHasUnreadReply = false;
        }

        await _context.SaveChangesAsync();

        if (!emailSent)
        {
            return (false, $"E-posta yeniden gönderilemedi: {emailError}");
        }

        return (true, null);
    }

    public async Task DeleteMessageAsync(int id)
    {
        var message = await _context.ContactMessages.FirstOrDefaultAsync(m => m.Id == id);
        if (message != null)
        {
            _context.ContactMessages.Remove(message);
            await _context.SaveChangesAsync();
        }
    }

    private static string GenerateEmailTemplate(string customerName, string subject, DateTime createdAt, string originalMessage, string replyText, string adminName, string? replyUrl = null)
    {
        var replyButtonHtml = !string.IsNullOrEmpty(replyUrl)
            ? $@"
                <div style='text-align: center; margin: 28px 0 12px;'>
                    <a href='{replyUrl}' style='display: inline-block; background: #030f27; color: #fdbe33; font-weight: 700; padding: 13px 26px; text-decoration: none; border-radius: 6px; font-size: 14px; border: 2px solid #fdbe33;'>
                        Konuşmayı Görüntüle ve Yanıtla &rarr;
                    </a>
                </div>
                <p style='font-size: 12px; color: #64748b; text-align: center; margin: 0 0 20px;'>
                    Yukarıdaki bağlantıya tıklayarak mesaj geçmişinizi görebilir ve yöneticimize doğrudan cevap yazabilirsiniz.
                </p>"
            : string.Empty;

        return $@"
        <div style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; max-width: 650px; margin: auto; padding: 0; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
            <div style='background: #030f27; padding: 25px 20px; text-align: center;'>
                <h2 style='color: #fdbe33; margin: 0; font-size: 24px; letter-spacing: 0.5px;'>ŞEN YAPI İNŞAAT A.Ş.</h2>
                <p style='color: #cbd5e1; margin: 6px 0 0; font-size: 13px;'>Müşteri İletişim & Danışma Servisi</p>
            </div>
            <div style='padding: 30px 25px;'>
                <p style='font-size: 16px; color: #1e293b; margin-top: 0;'>Sayın <strong>{customerName}</strong>,</p>
                <p style='font-size: 14px; color: #475569; line-height: 1.6;'>
                    Web sitemiz üzerinden iletmiş olduğunuz mesajınız yetkili ekibimiz tarafından değerlendirilmiş olup yanıtımız aşağıda bilgilerinize sunulmuştur:
                </p>
                <div style='background: #fffbeb; border-left: 4px solid #fdbe33; padding: 18px; margin: 22px 0; border-radius: 0 6px 6px 0;'>
                    <p style='margin: 0; font-size: 15px; color: #0f172a; line-height: 1.6; white-space: pre-wrap;'>{replyText.Replace("\n", "<br/>")}</p>
                </div>
                {replyButtonHtml}
                <div style='margin-top: 25px; padding: 16px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; font-size: 13px; color: #64748b;'>
                    <strong style='color: #334155;'>İlettiğiniz Mesaj Bilgileri:</strong><br/>
                    <span style='display:inline-block; margin-top: 6px;'><strong>Konu:</strong> {subject}</span><br/>
                    <span><strong>Tarih:</strong> {createdAt.ToString("dd.MM.yyyy HH:mm")}</span><br/>
                    <div style='margin-top: 6px; padding-top: 6px; border-top: 1px dashed #cbd5e1; font-style: italic;'>
                        ""{originalMessage.Replace("\n", "<br/>")}""
                    </div>
                </div>
                <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 30px 0;' />
                <p style='font-size: 12px; color: #94a3b8; margin: 0; text-align: center;'>
                    Bu e-posta, Şen Yapı Müşteri Hizmetleri adına yetkili yönetici (<strong>{adminName}</strong>) tarafından gönderilmiştir.<br/>
                    Sorularınız için web sitemizi ziyaret edebilir veya yukarıdaki butonla konuşmaya devam edebilirsiniz.
                </p>
            </div>
        </div>";
    }
}
