using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SenYapi.Business.Services;

/// <summary>
/// Türkiye telefon numarası (mobil ve sabit hat) doğrulama, normalizasyon ve biçimlendirme yardımcısı.
/// </summary>
public static class PhoneHelper
{
    private static readonly Regex IllegalCharRegex = new(@"[^\d\s\+\(\)\-\./]", RegexOptions.Compiled);
    private static readonly char[] ValidMobileSecondDigits = { '0', '1', '3', '4', '5', '6' };

    /// <summary>
    /// Ham telefon metninden yabancı ve geçersiz karakterleri kontrol ederek sadece rakamları ayıklar.
    /// Harf veya anlamsız simge içeriyorsa geçersiz sayılır (null döner).
    /// </summary>
    public static string? ExtractDigits(string? rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            return null;

        var trimmed = rawPhone.Trim();

        // Harf, özel kod veya anlamsız karakter kontrolü (örn: abc, test, #, $, %, vs.)
        if (IllegalCharRegex.IsMatch(trimmed))
            return null;

        var digits = Regex.Replace(trimmed, @"\D", "");
        if (string.IsNullOrEmpty(digits))
            return null;

        return digits;
    }

    /// <summary>
    /// Rakam dizisinden Türkiye ulusal 10 haneli numarasını ayıklar.
    /// Desteklenen girişler:
    /// - 0090XXXXXXXXXX (14 hane)
    /// - +90XXXXXXXXXX veya 90XXXXXXXXXX (12 hane)
    /// - 0XXXXXXXXXX (11 hane)
    /// - XXXXXXXXXX (10 hane)
    /// </summary>
    public static string? ExtractNationalNumber(string? rawPhone)
    {
        var digits = ExtractDigits(rawPhone);
        if (digits == null)
            return null;

        // 0090 ile başlama
        if (digits.StartsWith("0090") && digits.Length == 14)
            digits = digits.Substring(4);
        // 90 ile başlama
        else if (digits.StartsWith("90") && digits.Length == 12)
            digits = digits.Substring(2);
        // 0 ile başlama
        else if (digits.StartsWith("0") && digits.Length == 11)
            digits = digits.Substring(1);

        // Kalan ulusal numara 10 hane olmalıdır
        if (digits.Length == 10)
        {
            // Çok az farklı rakam içeren sahte numaraları ele (örn: 0000000000, 1111111111, 5555555555)
            if (digits.Distinct().Count() < 4)
                return null;

            return digits;
        }

        // 7 haneli 444'lü özel kurumsal numaralar (örn: 444 0 444, 444 0 333)
        if (digits.StartsWith("444") && digits.Length == 7 && digits.Distinct().Count() >= 2)
        {
            return digits;
        }

        return null;
    }

    /// <summary>
    /// Numaranın geçerli bir Türkiye cep telefonu (mobil hat: 5XX XXX XX XX) olup olmadığını doğrular.
    /// </summary>
    public static bool IsValidMobile(string? rawPhone)
    {
        var national = ExtractNationalNumber(rawPhone);
        if (national == null || national.Length != 10)
            return false;

        // Türkiye mobil numaraları 5 ile başlar
        if (national[0] != '5')
            return false;

        // Geçerli operatör ikinci hanesi (50X, 51X, 53X, 54X, 55X, 56X)
        if (!ValidMobileSecondDigits.Contains(national[1]))
            return false;

        return true;
    }

    /// <summary>
    /// Numaranın geçerli bir Türkiye sabit telefonu (alan kodlu: 2XX, 3XX, 4XX veya 850 / 444) olup olmadığını doğrular.
    /// </summary>
    public static bool IsValidLandline(string? rawPhone)
    {
        var national = ExtractNationalNumber(rawPhone);
        if (national == null)
            return false;

        // 444'lü kurumsal numara
        if (national.Length == 7 && national.StartsWith("444"))
            return true;

        if (national.Length == 10)
        {
            // Coğrafi alan kodları 2, 3 veya 4 ile başlar (örn: 212, 216, 312, 232...)
            if (national[0] == '2' || national[0] == '3' || national[0] == '4')
                return true;

            // 850'li kurumsal / VoIP sabit hatlar
            if (national.StartsWith("850"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Numaranın geçerli bir mobil veya (istenirse) sabit telefon olup olmadığını denetler.
    /// </summary>
    public static bool IsValid(string? rawPhone, bool allowLandline = true)
    {
        if (IsValidMobile(rawPhone))
            return true;

        if (allowLandline && IsValidLandline(rawPhone))
            return true;

        return false;
    }

    /// <summary>
    /// Numarayı standart uluslararası formata normalize eder (+905XXXXXXXXX veya sabit için +902XXXXXXXXX).
    /// Geçersiz ise null döner.
    /// </summary>
    public static string? Normalize(string? rawPhone, bool allowLandline = true)
    {
        if (IsValidMobile(rawPhone))
        {
            var national = ExtractNationalNumber(rawPhone)!;
            return "+90" + national;
        }

        if (allowLandline && IsValidLandline(rawPhone))
        {
            var national = ExtractNationalNumber(rawPhone)!;
            return "+90" + national;
        }

        return null;
    }

    /// <summary>
    /// Kullanıcı arayüzünde okunabilir ve standart Türkçe biçimde gösterir:
    /// Mobil: 0532 123 45 67 veya +90 532 123 45 67
    /// Sabit: 0212 555 01 23 veya 444 0 123
    /// </summary>
    public static string FormatDisplay(string? rawPhone, bool withCountryCode = false)
    {
        var national = ExtractNationalNumber(rawPhone);
        if (national == null)
            return rawPhone?.Trim() ?? string.Empty;

        // 444'lü numara
        if (national.Length == 7)
        {
            return $"{national.Substring(0, 3)} {national[3]} {national.Substring(4)}";
        }

        if (national.Length == 10)
        {
            var p1 = national.Substring(0, 3);
            var p2 = national.Substring(3, 3);
            var p3 = national.Substring(6, 2);
            var p4 = national.Substring(8, 2);

            if (withCountryCode)
                return $"+90 {p1} {p2} {p3} {p4}";

            return $"0{p1} {p2} {p3} {p4}";
        }

        return rawPhone?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Gizlilik amaçlı maskeleme: +90 5** *** 1234
    /// </summary>
    public static string Mask(string? rawPhone)
    {
        var normalized = Normalize(rawPhone, allowLandline: true);
        if (string.IsNullOrEmpty(normalized) || normalized.Length < 7)
            return rawPhone ?? string.Empty;

        if (normalized.StartsWith("+90") && normalized.Length >= 13)
        {
            var firstPart = normalized.Substring(0, 5); // +905X veya +902X
            var lastPart = normalized.Substring(normalized.Length - 4); // son 4 hane
            return $"{firstPart}** *** {lastPart}";
        }

        return normalized.Substring(0, 3) + "***" + normalized.Substring(normalized.Length - 2);
    }

    /// <summary>
    /// İki telefon numarasının normalizasyon altında aynı olup olmadığını karşılaştırır.
    /// </summary>
    public static bool IsMatch(string? phone1, string? phone2)
    {
        var n1 = Normalize(phone1, allowLandline: true);
        var n2 = Normalize(phone2, allowLandline: true);
        if (string.IsNullOrEmpty(n1) || string.IsNullOrEmpty(n2))
            return false;

        return string.Equals(n1, n2, StringComparison.Ordinal);
    }
}
