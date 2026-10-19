using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace SenYapi.Business.Services;

public class LoginProtectionService : ILoginProtectionService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<LoginProtectionService> _logger;

    public const int FailedAttemptThreshold = 3; // 3 art arda başarısız denemeden sonra CAPTCHA
    private const int IpAttackThreshold = 8; // IP bazlı kaba kuvvet saldırısı eşiği
    private static readonly TimeSpan SlidingWindow = TimeSpan.FromMinutes(30);

    public LoginProtectionService(IMemoryCache memoryCache, ILogger<LoginProtectionService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public int GetFailedAttempts(string clientKey)
    {
        if (string.IsNullOrWhiteSpace(clientKey)) return 0;
        return _memoryCache.TryGetValue($"login_fail_session_{clientKey}", out int count) ? count : 0;
    }

    public bool RequiresCaptcha(string clientKey, string clientIp)
    {
        var sessionFails = GetFailedAttempts(clientKey);
        if (sessionFails >= FailedAttemptThreshold)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(clientIp) &&
            _memoryCache.TryGetValue($"login_fail_ip_{clientIp}", out int ipFails) &&
            ipFails >= IpAttackThreshold)
        {
            return true;
        }

        return false;
    }

    public void RecordFailedAttempt(string clientKey, string clientIp)
    {
        if (!string.IsNullOrWhiteSpace(clientKey))
        {
            var sessionKey = $"login_fail_session_{clientKey}";
            var sessionFails = GetFailedAttempts(clientKey) + 1;
            _memoryCache.Set(sessionKey, sessionFails, SlidingWindow);
            _logger.LogInformation("Başarısız oturum giriş denemesi kaydedildi. Oturum: {ClientKey}, Başarısızlık Sayısı: {Count}", clientKey, sessionFails);
        }

        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            var ipKey = $"login_fail_ip_{clientIp}";
            var ipFails = (_memoryCache.TryGetValue(ipKey, out int currentIpFails) ? currentIpFails : 0) + 1;
            _memoryCache.Set(ipKey, ipFails, SlidingWindow);
        }
    }

    public void ResetFailedAttempts(string clientKey, string clientIp)
    {
        if (!string.IsNullOrWhiteSpace(clientKey))
        {
            _memoryCache.Remove($"login_fail_session_{clientKey}");
            _logger.LogInformation("Başarılı giriş ile oturum başarısız deneme sayacı sıfırlandı. Oturum: {ClientKey}", clientKey);
        }
    }
}
