namespace SenYapi.Business.Services;

public interface ILoginProtectionService
{
    int GetFailedAttempts(string clientKey);
    bool RequiresCaptcha(string clientKey, string clientIp);
    void RecordFailedAttempt(string clientKey, string clientIp);
    void ResetFailedAttempts(string clientKey, string clientIp);
}
