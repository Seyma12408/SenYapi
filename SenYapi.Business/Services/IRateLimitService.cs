using System;

namespace SenYapi.Business.Services;

public interface IRateLimitService
{
    (bool IsAllowed, string? ErrorMessage) CheckRateLimit(string actionKey, string clientIp, int maxRequests, TimeSpan window);
}
