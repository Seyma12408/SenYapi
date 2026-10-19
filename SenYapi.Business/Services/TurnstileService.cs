using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SenYapi.Business.DTOs;

namespace SenYapi.Business.Services;

public class TurnstileService : ICaptchaService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TurnstileService> _logger;

    private const string SiteVerifyEndpoint = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public TurnstileService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<TurnstileService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsEnabled => _configuration.GetValue("Turnstile:Enabled", true);

    public string GetSiteKey()
    {
        return _configuration["Turnstile:SiteKey"]?.Trim() ?? string.Empty;
    }

    public async Task<(bool Success, string? ErrorMessage)> VerifyTokenAsync(string? token, string? expectedAction = null, string? clientIp = null)
    {
        // 1. CAPTCHA devre dışı bırakılmışsa (özel durum) doğrudan geç
        if (!IsEnabled)
        {
            _logger.LogWarning("Turnstile CAPTCHA doğrulaması yapılandırma nedeniyle devre dışı.");
            return (true, null);
        }

        // 2. Secret Key kontrolü
        var secretKey = _configuration["Turnstile:SecretKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogError("Cloudflare Turnstile SecretKey yapılandırılmamış!");
            return (false, "Cloudflare Turnstile Secret Key tanımlanmamış. Lütfen appsettings.json dosyasında veya ortam değişkenlerinde 'Turnstile:SecretKey' değerini tanımlayınız.");
        }

        // 3. Token kontrolü (İstemci CAPTCHA çözmemişse veya JS kapalıysa)
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "Lütfen güvenlik doğrulamasını (CAPTCHA) tamamlayınız.");
        }

        // 4. Cloudflare Siteverify servisine istek hazırla
        var postParameters = new Dictionary<string, string>
        {
            { "secret", secretKey },
            { "response", token.Trim() }
        };

        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            postParameters["remoteip"] = clientIp.Trim();
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("TurnstileClient");
            using var requestContent = new FormUrlEncodedContent(postParameters);
            using var response = await httpClient.PostAsync(SiteVerifyEndpoint, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cloudflare Turnstile HTTP hata kodu döndürdü: {StatusCode}", response.StatusCode);
                return (false, "Güvenlik doğrulama servisi yanıt veremedi. Lütfen tekrar deneyiniz.");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var verifyResult = JsonSerializer.Deserialize<TurnstileVerifyResponse>(responseJson);

            if (verifyResult == null)
            {
                _logger.LogWarning("Cloudflare Turnstile yanıtı çözümlenemedi: {ResponseJson}", responseJson);
                return (false, "Güvenlik doğrulama yanıtı çözümlenemedi. Lütfen tekrar deneyiniz.");
            }

            if (!verifyResult.Success)
            {
                var errorCodes = verifyResult.ErrorCodes != null ? string.Join(", ", verifyResult.ErrorCodes) : "unknown";
                _logger.LogWarning("Cloudflare Turnstile doğrulaması başarısız. Hata kodları: {Codes}", errorCodes);

                if (verifyResult.ErrorCodes != null)
                {
                    if (verifyResult.ErrorCodes.Contains("timeout-or-duplicate"))
                    {
                        return (false, "Güvenlik doğrulamasının geçerlilik süresi doldu veya daha önce kullanıldı. Lütfen kutucuğu tekrar işaretleyiniz.");
                    }
                    if (verifyResult.ErrorCodes.Contains("invalid-input-response") || verifyResult.ErrorCodes.Contains("missing-input-response"))
                    {
                        return (false, "Güvenlik doğrulaması geçersiz veya eksik. Lütfen kutucuğu tekrar işaretleyiniz.");
                    }
                    if (verifyResult.ErrorCodes.Contains("invalid-input-secret") || verifyResult.ErrorCodes.Contains("missing-input-secret"))
                    {
                        return (false, "Cloudflare Turnstile Secret Key geçersiz veya hatalı.");
                    }
                    if (verifyResult.ErrorCodes.Contains("bad-request"))
                    {
                        return (false, "Güvenlik doğrulaması isteği geçersiz.");
                    }
                }

                return (false, "Güvenlik doğrulaması başarısız oldu. Lütfen tekrar deneyiniz.");
            }

            // 5. İşlem Türü (Action) Doğrulaması
            if (!string.IsNullOrWhiteSpace(expectedAction) && !string.IsNullOrWhiteSpace(verifyResult.Action))
            {
                if (!string.Equals(verifyResult.Action, expectedAction, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Turnstile action uyumsuzluğu! Beklenen: {Expected}, Gelen: {Actual}", expectedAction, verifyResult.Action);
                    return (false, "Güvenlik doğrulaması işlem türü eşleşmedi.");
                }
            }

            return (true, null);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Cloudflare Turnstile API isteği zaman aşımına uğradı.");
            return (false, "Güvenlik doğrulaması servisi zaman aşımına uğradı. Lütfen internet bağlantınızı kontrol edip tekrar deneyiniz.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Cloudflare Turnstile API isteği ağ hatasıyla karşılaştı.");
            return (false, "Güvenlik doğrulaması servisine ulaşılamadı. Lütfen internet bağlantınızı kontrol edip tekrar deneyiniz.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloudflare Turnstile doğrulaması sırasında beklenmeyen bir hata oluştu.");
            return (false, "Güvenlik doğrulaması sırasında bir hata oluştu. Lütfen tekrar deneyiniz.");
        }
    }
}
