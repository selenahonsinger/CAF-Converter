using System.Globalization;
using System.Text.RegularExpressions;

namespace CafConverter.Helpers;

public static partial class TimeSpanParser
{
    [GeneratedRegex(@"^\d+(?::\d{1,2}){0,2}(?:\.\d{1,3})?$")]
    private static partial Regex ValidPattern();

    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        if (!ValidPattern().IsMatch(text)) return false;
        var parts = text.Split(':');
        if (!double.TryParse(parts[^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)) return false;
        if (seconds >= 60 && parts.Length > 1) return false;
        var minutes = 0;
        var hours = 0;
        if (parts.Length >= 2 && (!int.TryParse(parts[^2], out minutes) || minutes >= 60 && parts.Length == 3)) return false;
        if (parts.Length == 3 && !int.TryParse(parts[0], out hours)) return false;
        try { value = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds); return value >= TimeSpan.Zero; }
        catch { return false; }
    }

    public static string Format(TimeSpan value)
    {
        var totalHours = (long)value.TotalHours;
        return totalHours > 0
            ? $"{totalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds:000}"
            : $"{(long)value.TotalMinutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";
    }

    public static string ToFfmpeg(TimeSpan value) => value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
}
