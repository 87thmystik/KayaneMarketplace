using System.Text.RegularExpressions;

namespace Kayane.Helpers;

public static class SlugHelper
{
    public static string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var str = text.ToLowerInvariant().Trim();
        // Remove invalid characters
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        // Convert multiple spaces into a single space
        str = Regex.Replace(str, @"\s+", " ").Trim();
        // Replace spaces with hyphens
        str = Regex.Replace(str, @"\s", "-");

        return str;
    }
}