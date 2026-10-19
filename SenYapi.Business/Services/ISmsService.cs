using System.Threading.Tasks;

namespace SenYapi.Business.Services;

public interface ISmsService
{
    bool IsConfigured { get; }
    Task<(bool Success, string? ErrorMessage)> SendSmsAsync(string phoneNumber, string message);
}
