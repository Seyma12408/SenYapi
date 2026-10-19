using System;
using System.Text.RegularExpressions;

namespace SenYapi.Business.Services;

public static class PhoneHelper
{
    /// <summary>
    /// Normalizes phone numbers to standard format (+905XXXXXXXXX for TR).
    /// Returns null if format is not recognized.
    /// </summary>
    public static string? Normalize(string? rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            return null;

        var digits = Regex.Replace(rawPhone, @"[^\d]", "");

        if (digits.StartsWith("0090") && digits.Length >= 14)
            digits = digits.Substring(2);

        // Turkish Mobile:
        // 905XXXXXXXXX (12 digits)
        if (digits.StartsWith("90") && digits.Length == 12 && digits[2] == '5')
            return "+" + digits;

        // 05XXXXXXXXX (11 digits)
        if (digits.StartsWith("05") && digits.Length == 11)
            return "+90" + digits.Substring(1);

        // 5XXXXXXXXX (10 digits)
        if (digits.StartsWith("5") && digits.Length == 10)
            return "+90" + digits;

        // Generic international: 10-15 digits
        if (digits.Length >= 10 && digits.Length <= 15)
            return "+" + digits;

        return null;
    }

    /// <summary>
    /// Displays masked phone number for user privacy: +90 5** *** 1234
    /// </summary>
    public static string Mask(string? rawPhone)
    {
        var normalized = Normalize(rawPhone);
        if (string.IsNullOrEmpty(normalized) || normalized.Length < 7)
            return rawPhone ?? "";

        if (normalized.StartsWith("+90") && normalized.Length == 13)
        {
            var firstPart = normalized.Substring(0, 5); // +905X
            var lastPart = normalized.Substring(normalized.Length - 4); // 1234
            return $"{firstPart}** *** {lastPart}";
        }

        return normalized.Substring(0, 3) + "***" + normalized.Substring(normalized.Length - 2);
    }

    /// <summary>
    /// Checks if two phone numbers match under normalization.
    /// </summary>
    public static bool IsMatch(string? phone1, string? phone2)
    {
        var n1 = Normalize(phone1);
        var n2 = Normalize(phone2);
        if (string.IsNullOrEmpty(n1) || string.IsNullOrEmpty(n2))
            return false;

        return string.Equals(n1, n2, StringComparison.Ordinal);
    }
}
