using System.Reflection;

namespace CafConverter.Services;

public sealed class ToolExtractionService(LoggingService logger)
{
    public const string ToolVersion = "ffmpeg-9.0.1-essentials";
    private const long MinimumToolSize = 1024 * 1024;

    public async Task<(string Ffmpeg, string Ffprobe)?> TryExtractAsync(CancellationToken cancellationToken = default)
    {
        var assembly = typeof(ToolExtractionService).Assembly;
        var names = assembly.GetManifestResourceNames();
        var (ffmpegResource, ffprobeResource) = FindEmbeddedResourceNames(names);
        if (ffmpegResource is null || ffprobeResource is null) return null;

        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CafConverter", "Tools", ToolVersion);
        Directory.CreateDirectory(directory);
        var ffmpeg = Path.Combine(directory, "ffmpeg.exe");
        var ffprobe = Path.Combine(directory, "ffprobe.exe");
        await ExtractIfNeededAsync(assembly, ffmpegResource, ffmpeg, cancellationToken);
        await ExtractIfNeededAsync(assembly, ffprobeResource, ffprobe, cancellationToken);
        return IsValid(ffmpeg) && IsValid(ffprobe) ? (ffmpeg, ffprobe) : null;
    }

    private async Task ExtractIfNeededAsync(Assembly assembly, string resourceName, string target, CancellationToken cancellationToken)
    {
        if (IsValid(target)) return;
        var temp = target + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using var input = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidDataException($"无法读取内嵌资源 {resourceName}");
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            if (!IsValid(temp)) throw new InvalidDataException("内嵌 FFmpeg 工具大小无效");
            File.Move(temp, target, true);
            logger.Info($"已释放内嵌工具：{Path.GetFileName(target)}");
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
    }

    public static bool IsValid(string? path)
    {
        try { return !string.IsNullOrWhiteSpace(path) && File.Exists(path) && new FileInfo(path).Length >= MinimumToolSize; }
        catch { return false; }
    }

    public static (string? Ffmpeg, string? Ffprobe) FindEmbeddedResourceNames(IEnumerable<string> names)
    {
        var all = names.ToArray();
        return (
            all.FirstOrDefault(n => n.EndsWith("Resources.Tools.win_x64.ffmpeg.exe", StringComparison.OrdinalIgnoreCase)),
            all.FirstOrDefault(n => n.EndsWith("Resources.Tools.win_x64.ffprobe.exe", StringComparison.OrdinalIgnoreCase)));
    }
}
