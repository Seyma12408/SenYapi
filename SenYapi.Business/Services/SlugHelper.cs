using System.Text;
using System.Text.RegularExpressions;

namespace SenYapi.Business.Services;

public static class SlugHelper
{
    public static string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Trim();

        // Turkish character mappings
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            switch (c)
            {
                case 'ç': case 'Ç': sb.Append('c'); break;
                case 'ğ': case 'Ğ': sb.Append('g'); break;
                case 'ı': case 'I': case 'İ': sb.Append('i'); break;
                case 'ö': case 'Ö': sb.Append('o'); break;
                case 'ş': case 'Ş': sb.Append('s'); break;
                case 'ü': case 'Ü': sb.Append('u'); break;
                default: sb.Append(c); break;
            }
        }

        string result = sb.ToString().ToLowerInvariant();

        // Remove invalid chars
        result = Regex.Replace(result, @"[^a-z0-9\s-]", "");
        // Replace multiple spaces or hyphens with single hyphen
        result = Regex.Replace(result, @"[\s-]+", "-");
        // Trim hyphens from both ends
        result = result.Trim('-');

        return result;
    }
}
