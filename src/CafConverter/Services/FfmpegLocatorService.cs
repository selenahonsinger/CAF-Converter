using CafConverter.Helpers;

namespace CafConverter.Services;

public sealed record FfmpegTools(string FfmpegPath, string FfprobePath, string Version);

public sealed class FfmpegLocatorService(ToolExtractionService extraction, LoggingService logger)
{
    public async Task<FfmpegTools?> LocateAsync(string? customDirectory, CancellationToken cancellationToken = default)
    {
        var embedded = await extraction.TryExtractAsync(cancellationToken);
        if (embedded is not null) return await CreateAsync(embedded.Value.Ffmpeg, embedded.Value.Ffprobe, cancellationToken);

        foreach (var directory in CandidateDirectories(customDirectory))
        {
            var ffmpeg = Path.Combine(directory, "ffmpeg.exe");
            var ffprobe = Path.Combine(directory, "ffprobe.exe");
            if (ToolExtractionService.IsValid(ffmpeg) && ToolExtractionService.IsValid(ffprobe))
                return await CreateAsync(ffmpeg, ffprobe, cancellationToken);
        }
        return null;
    }

    private static IEnumerable<string> CandidateDirectories(string? custom)
    {
        yield return Path.Combine(AppContext.BaseDirectory, "tools");
        foreach (var part in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return part.Trim('"');
        if (!string.IsNullOrWhiteSpace(custom)) yield return custom;
    }

    private async Task<FfmpegTools?> CreateAsync(string ffmpeg, string ffprobe, CancellationToken cancellationToken)
    {
        try
        {
            var info = ProcessHelper.Create(ffmpeg);
            info.ArgumentList.Add("-version");
            using var process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("进程未启动");
            var firstLineTask = process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
            await process.WaitForExitAsync(cancellationToken);
            var version = await firstLineTask ?? "未知版本";
            if (process.ExitCode != 0) return null;
            logger.Info($"FFmpeg 路径：{ffmpeg}");
            logger.Info($"FFmpeg 版本：{version}");
            return new(ffmpeg, ffprobe, version);
        }
        catch (Exception ex) { logger.Error("FFmpeg 可用性检查失败", ex); return null; }
    }
}
