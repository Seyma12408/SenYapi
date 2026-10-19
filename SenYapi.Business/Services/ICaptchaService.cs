using System.Threading.Tasks;

namespace SenYapi.Business.Services;

public interface ICaptchaService
{
    bool IsEnabled { get; }
    string GetSiteKey();
    Task<(bool Success, string? ErrorMessage)> VerifyTokenAsync(string? token, string? expectedAction = null, string? clientIp = null);
}
