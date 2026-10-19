using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SenYapi.Business.Services;

public interface IEmailService
{
    Task<(bool Success, string? ErrorMessage)> SendEmailAsync(string toEmail, string subject, string htmlBody);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage)> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toEmail) || !toEmail.Contains('@'))
        {
            return (false, "Geçersiz alıcı e-posta adresi.");
        }

        var smtpHost = _configuration["Smtp:Host"]?.Trim();
        var smtpPortStr = _configuration["Smtp:Port"]?.Trim();
        var smtpUser = _configuration["Smtp:Username"]?.Trim();
        var smtpPass = _configuration["Smtp:Password"]?.Trim();
        var senderEmail = _configuration["Smtp:SenderEmail"]?.Trim();
        var senderName = _configuration["Smtp:SenderName"]?.Trim() ?? "Şen Yapı İnşaat A.Ş.";
        var enableSsl = !bool.TryParse(_configuration["Smtp:EnableSsl"], out var ssl) || ssl;
        var deliveryMethod = _configuration["Smtp:DeliveryMethod"]?.Trim();

        // 1. Ayar Eksikliği Kontrolleri (Açık hata bildirim kuralı)
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            var err = "E-posta sunucu ayarları (SMTP Host) yapılandırılmamış. Lütfen appsettings.json dosyasından e-posta ayarlarınızı tanımlayın.";
            _logger.LogWarning(err);
            return (false, err);
        }

        if (smtpHost.Equals("smtp.example.com", StringComparison.OrdinalIgnoreCase))
        {
            var err = "Smtp:Host varsayılan örnek değerinde ('smtp.example.com') bırakılmış. Lütfen geçerli bir SMTP sunucu adresi girin.";
            _logger.LogWarning(err);
            return (false, err);
        }

        // 2. Test / Güvenli Doğrulama Modu (Gerçek kişilere istenmeyen e-posta göndermeden test etmek için)
        if (smtpHost.Equals("test-outbox", StringComparison.OrdinalIgnoreCase) ||
            smtpHost.Equals("outbox", StringComparison.OrdinalIgnoreCase) ||
            deliveryMethod?.Equals("SpecifiedPickupDirectory", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                var outboxDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "mail-outbox");
                if (!Directory.Exists(outboxDir))
                    Directory.CreateDirectory(outboxDir);

                var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString()[..6]}.html";
                var fullPath = Path.Combine(outboxDir, fileName);
                var logContent = $"<!-- To: {toEmail} | Subject: {subject} | Date: {DateTime.Now} -->\n{htmlBody}";
                await File.WriteAllTextAsync(fullPath, logContent);

                _logger.LogInformation("E-posta test modunda mail-outbox klasörüne başarıyla yazıldı: {FilePath}", fullPath);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test e-posta dosyası diske yazılamadı.");
                return (false, $"Test e-posta outbox dosyası oluşturulamadı: {ex.Message}");
            }
        }

        // 3. Canlı SMTP Gönderimi
        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            senderEmail = smtpUser;
        }

        if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(senderEmail))
        {
            return (false, "SMTP kullanıcı adı (Smtp:Username) veya gönderen e-posta (Smtp:SenderEmail) eksik.");
        }

        int port = int.TryParse(smtpPortStr, out var p) ? p : 587;

        try
        {
            using var client = new SmtpClient(smtpHost, port)
            {
                EnableSsl = enableSsl,
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                Timeout = 15000
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("E-posta başarıyla gönderildi. Alıcı: {ToEmail}, Konu: {Subject}", toEmail, subject);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-posta gönderilirken hata oluştu. Alıcı: {ToEmail}", toEmail);
            return (false, $"SMTP Sunucu Hatası: {ex.Message}");
        }
    }
}
