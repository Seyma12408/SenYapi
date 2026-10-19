using System;
using System.Threading.Tasks;
using SenYapi.Entity;

namespace SenYapi.Business.Services;

public class PhoneResetSession
{
    public string SessionToken { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string NormalizedPhone { get; set; } = string.Empty;
    public string MaskedPhone { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime ResendCooldownUntil { get; set; }
    public int FailedAttempts { get; set; } = 0;
    public bool IsVerified { get; set; } = false;
    public bool UserFound { get; set; } = false;
}

public interface IPhoneResetService
{
    Task<(bool Success, string? ErrorMessage, PhoneResetSession? Session)> RequestCodeAsync(string rawPhoneNumber);
    Task<(bool Success, string? ErrorMessage)> VerifyCodeAsync(string sessionToken, string inputCode);
    Task<(bool Success, string? ErrorMessage, AppUser? User, string? ResetToken)> CompleteResetAsync(string sessionToken, string newPassword);
    Task<(bool Success, string? ErrorMessage, int RemainingCooldownSeconds)> ResendCodeAsync(string sessionToken);
    PhoneResetSession? GetSession(string sessionToken);
}
