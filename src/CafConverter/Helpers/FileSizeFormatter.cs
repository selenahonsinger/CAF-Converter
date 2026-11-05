using System.Globalization;

namespace CafConverter.Helpers;

public static class FileSizeFormatter
{
    public static string Format(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)Math.Max(0, bytes);
        var index = 0;
        while (size >= 1024 && index < units.Length - 1) { size /= 1024; index++; }
        return string.Create(CultureInfo.CurrentCulture, $"{size:0.##} {units[index]}");
    }
}
