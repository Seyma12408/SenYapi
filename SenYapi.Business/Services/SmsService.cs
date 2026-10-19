using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SenYapi.Business.Services;

public class SmsService : ISmsService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public SmsService(IConfiguration configuration, ILogger<SmsService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public bool IsConfigured
    {
        get
        {
            var provider = _configuration["Sms:Provider"]?.Trim();
            if (string.IsNullOrWhiteSpace(provider))
                return false;

            if (provider.Equals("TestOutbox", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("test-outbox", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("Outbox", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var user = _configuration["Sms:Username"]?.Trim();
            var pass = _configuration["Sms:Password"]?.Trim();
            return !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SendSmsAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return (false, "Geçersiz telefon numarası.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return (false, "SMS mesaj içeriği boş olamaz.");
        }

        var provider = _configuration["Sms:Provider"]?.Trim();

        // 1. Yapılandırma Eksikliği Kontrolü
        if (!IsConfigured)
        {
            var err = "SMS servisi yapılandırılmamış. Lütfen appsettings.json dosyasından SMS sağlayıcı bilgilerinizi (Sms:Provider, Sms:Username, Sms:Password vb.) tanımlayınız.";
            _logger.LogWarning(err);
            return (false, err);
        }

        // 2. Test / Outbox Modu (Geliştirme ve test aşamasında SMS kredisi harcamadan test etmek için)
        if (provider!.Equals("TestOutbox", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("test-outbox", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("Outbox", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var outboxDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "sms-outbox");
                if (!Directory.Exists(outboxDir))
                    Directory.CreateDirectory(outboxDir);

                var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString()[..6]}.txt";
                var fullPath = Path.Combine(outboxDir, fileName);
                var logContent = $"Alıcı Telefon: {phoneNumber}\nTarih: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nMesaj:\n{message}\n";
                await File.WriteAllTextAsync(fullPath, logContent);

                _logger.LogInformation("SMS test modunda sms-outbox klasörüne başarıyla yazıldı: {FilePath}", fullPath);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test SMS dosyası diske yazılamadı.");
                return (false, $"Test SMS outbox dosyası oluşturulamadı: {ex.Message}");
            }
        }

        // 3. Gerçek SMS Sağlayıcı Entegrasyonu (Netgsm / Standart HTTP REST API)
        var username = _configuration["Sms:Username"]?.Trim();
        var password = _configuration["Sms:Password"]?.Trim();
        var header = _configuration["Sms:Header"]?.Trim() ?? "SENYAPI";
        var apiUrl = _configuration["Sms:ApiUrl"]?.Trim();

        // Telefonu temizle (Türkiye için 10 hane: 5XXXXXXXXX veya 05XXXXXXXXX)
        var digitsOnly = Regex.Replace(phoneNumber, @"[^\d]", "");
        if (digitsOnly.StartsWith("90") && digitsOnly.Length == 12)
            digitsOnly = digitsOnly.Substring(2);
        else if (digitsOnly.StartsWith("0") && digitsOnly.Length == 11)
            digitsOnly = digitsOnly.Substring(1);

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            if (provider.Equals("Netgsm", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(apiUrl))
            {
                apiUrl = string.IsNullOrWhiteSpace(apiUrl) ? "https://api.netgsm.com.tr/sms/send/get" : apiUrl;

                var builder = new UriBuilder(apiUrl);
                var query = $"usercode={Uri.EscapeDataString(username ?? "")}" +
                            $"&password={Uri.EscapeDataString(password ?? "")}" +
                            $"&gsmno={Uri.EscapeDataString(digitsOnly)}" +
                            $"&message={Uri.EscapeDataString(message)}" +
                            $"&msgheader={Uri.EscapeDataString(header)}";
                builder.Query = query;

                var response = await client.GetAsync(builder.Uri);
                var responseContent = (await response.Content.ReadAsStringAsync()).Trim();

                // Netgsm response check:
                // "00", "01", "02" means queued or submitted successfully
                if (responseContent.StartsWith("00") || responseContent.StartsWith("01") || responseContent.StartsWith("02"))
                {
                    _logger.LogInformation("SMS başarıyla iletildi. Alıcı: {Phone}, Yanıt: {Response}", phoneNumber, responseContent);
                    return (true, null);
                }

                var netgsmError = responseContent switch
                {
                    "20" => "Mesaj metni veya karakter sınır hatası.",
                    "30" => "SMS sağlayıcı kullanıcı adı veya şifre geçersiz.",
                    "40" => "Gönderici başlığı (Header) sistemde onaylı değil.",
                    "70" => "Geçersiz alıcı telefon numarası veya parametre hatası.",
                    _ => $"SMS sağlayıcı hata yanıtı verdi: Kod {responseContent}"
                };

                _logger.LogWarning("SMS gönderimi başarısız oldu. Hata: {Error}", netgsmError);
                return (false, netgsmError);
            }
            else
            {
                // Generic HTTP POST API
                var content = new FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("username", username ?? ""),
                    new System.Collections.Generic.KeyValuePair<string, string>("password", password ?? ""),
                    new System.Collections.Generic.KeyValuePair<string, string>("phone", digitsOnly),
                    new System.Collections.Generic.KeyValuePair<string, string>("message", message),
                    new System.Collections.Generic.KeyValuePair<string, string>("header", header)
                });

                var response = await client.PostAsync(apiUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SMS başarıyla gönderildi: {Phone}", phoneNumber);
                    return (true, null);
                }

                var errBody = await response.Content.ReadAsStringAsync();
                return (false, $"SMS sağlayıcı HTTP {response.StatusCode} hatası döndürdü: {errBody}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS gönderimi sırasında ağ veya sunucu hatası oluştu. Alıcı: {Phone}", phoneNumber);
            return (false, $"SMS Gönderim Hatası: {ex.Message}");
        }
    }
}
