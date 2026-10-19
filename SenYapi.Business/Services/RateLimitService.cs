using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace SenYapi.Business.Services;

public class RateLimitService : IRateLimitService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RateLimitService> _logger;
    private static readonly object _lock = new();

    public RateLimitService(IMemoryCache memoryCache, ILogger<RateLimitService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public (bool IsAllowed, string? ErrorMessage) CheckRateLimit(string actionKey, string clientIp, int maxRequests, TimeSpan window)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            clientIp = "unknown_ip";
        }

        var cacheKey = $"rl_{actionKey}_{clientIp}";
        var now = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_memoryCache.TryGetValue(cacheKey, out List<DateTime>? timestamps) || timestamps == null)
            {
                timestamps = new List<DateTime>();
            }

            var cutoff = now - window;
            timestamps.RemoveAll(t => t < cutoff);

            if (timestamps.Count >= maxRequests)
            {
                var oldest = timestamps.Min();
                var retryAfter = window - (now - oldest);
                var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));

                _logger.LogWarning("Rate limit aşıldı! Eylem: {Action}, IP: {IP}, Mevcut İstek: {Count}/{Max}, Kalan Süre: {Minutes} dk",
                    actionKey, clientIp, timestamps.Count, maxRequests, minutes);

                return (false, $"Çok fazla istek gönderildi. Lütfen {minutes} dakika sonra tekrar deneyiniz.");
            }

            timestamps.Add(now);
            _memoryCache.Set(cacheKey, timestamps, window);
            return (true, null);
        }
    }
}
