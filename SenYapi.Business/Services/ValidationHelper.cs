using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SenYapi.Business.Services;

/// <summary>
/// Proje genelinde kullanılan ortak veri doğrulama kuralları yardımcısı.
/// </summary>
public static class ValidationHelper
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] AllowedProjectCategories = { "complete", "running", "upcoming" };
    private static readonly string[] AllowedTimeSlots = { "10:00 - 12:00", "13:00 - 15:00", "15:00 - 17:00", "17:00 - 19:00" };
    private static readonly string[] AllowedUserRoles = { "SystemAdmin", "Admin", "Editor", "Member", "User" };
    private static readonly string[] AllowedInquiryStatuses = { "Beklemede", "İncelendi", "Yanıtlandı", "Arşivlendi" };
    private static readonly string[] AllowedAppointmentStatuses = { "Beklemede", "Onaylandı", "Tamamlandı", "İptal Edildi", "Ertelendi" };
    private static readonly string[] AllowedContactMethods = { "Telefon", "WhatsApp", "E-posta" };

    /// <summary>
    /// E-posta adresinin geçerli olup olmadığını doğrular.
    /// </summary>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var clean = email.Trim();
        if (clean.Length > 254)
            return false;

        return EmailRegex.IsMatch(clean);
    }

    /// <summary>
    /// Metnin yalnızca boşluklardan oluşmadığını, minimum ve maksimum uzunluk kriterlerini sağladığını denetler.
    /// </summary>
    public static bool IsCleanText(string? text, int minLength = 1, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var len = text.Trim().Length;
        return len >= minLength && len <= maxLength;
    }

    /// <summary>
    /// URL adresinin geçerli ve güvenli olduğunu denetler (javascript:, data: ve vbscript: gibi tehlikeli şemaları kesinlikle engeller).
    /// </summary>
    public static bool IsValidUrl(string? url, bool allowEmbed = false)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var clean = url.Trim();

        // XSS & Zararlı şema engeli
        if (clean.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Google Haritalar embed iframe linki kontrolü
        if (allowEmbed && (clean.Contains("<iframe") || clean.StartsWith("https://www.google.com/maps/embed") || clean.StartsWith("https://maps.google.com/")))
        {
            return true;
        }

        if (Uri.TryCreate(clean, UriKind.Absolute, out var uriResult))
        {
            return uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps;
        }

        return false;
    }

    /// <summary>
    /// Proje kategorisinin izin verilen listede olup olmadığını denetler.
    /// </summary>
    public static bool IsValidProjectCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return false;

        return AllowedProjectCategories.Contains(category.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Randevu saat aralığının geçerli olup olmadığını denetler.
    /// </summary>
    public static bool IsValidTimeSlot(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return false;

        return AllowedTimeSlots.Contains(slot.Trim());
    }

    /// <summary>
    /// Tercih edilen iletişim kanalının izin verilen listede olup olmadığını denetler.
    /// </summary>
    public static bool IsValidContactMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return false;

        return AllowedContactMethods.Contains(method.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kullanıcı rolünün sistemde tanımlı roller arasında olup olmadığını denetler.
    /// </summary>
    public static bool IsValidRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        return AllowedUserRoles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Bilgi talebi durumunun geçerli olup olmadığını denetler.
    /// </summary>
    public static bool IsValidInquiryStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return AllowedInquiryStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Randevu durumunun geçerli olup olmadığını denetler.
    /// </summary>
    public static bool IsValidAppointmentStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return AllowedAppointmentStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sayısal değerin negatif olmadığını ve belirtilen aralıkta olduğunu denetler.
    /// </summary>
    public static bool IsValidPositiveNumber(decimal? number, decimal min = 0, decimal max = 1_000_000_000)
    {
        if (!number.HasValue)
            return true;

        return number.Value >= min && number.Value <= max;
    }

    /// <summary>
    /// Randevu tarihinin geçmişte olmadığını ve en fazla 1 yıl sonraya kadar seçildiğini doğrular.
    /// </summary>
    public static bool IsValidAppointmentDate(DateTime date)
    {
        var today = DateTime.Today;
        return date.Date >= today && date.Date <= today.AddYears(1);
    }
}
