using System.Diagnostics;
using System.Globalization;
using System.Text;
using CafConverter.Helpers;
using CafConverter.Models;

namespace CafConverter.Services;

public sealed class FfmpegService(FfprobeService probe, LoggingService logger)
{
    public static IReadOnlyList<string> BuildArguments(string inputPath, string outputPath, ConversionSettings settings, TimeSpan effectiveDuration)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-i", inputPath, "-map", "0:a:0", "-vn" };
        if (settings.StartTime > TimeSpan.Zero) { args.Add("-ss"); args.Add(TimeSpanParser.ToFfmpeg(settings.StartTime)); }
        if (!settings.KeepFullDuration)
        {
            args.Add("-t"); args.Add(TimeSpanParser.ToFfmpeg(effectiveDuration));
        }
        args.AddRange(["-ac", settings.Channels.ToString(CultureInfo.InvariantCulture), "-ar", settings.SampleRate.ToString(CultureInfo.InvariantCulture), "-c:a", "pcm_s16le"]);
        var filters = BuildFilters(settings, effectiveDuration);
        if (filters.Count > 0) { args.Add("-af"); args.Add(string.Join(',', filters)); }
        args.AddRange(["-f", "caf", "-progress", "pipe:1", "-nostats", outputPath]);
        return args;
    }

    public static TimeSpan GetEffectiveDuration(TimeSpan sourceDuration, ConversionSettings settings)
    {
        var remaining = sourceDuration - settings.StartTime;
        if (remaining <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(settings), "开始时间不能超过源文件长度。");
        if (settings.KeepFullDuration) return remaining;
        var requested = settings.OutputDuration ?? remaining;
        if (settings.Preset == ConversionPreset.Bark && requested > TimeSpan.FromSeconds(29)) requested = TimeSpan.FromSeconds(29);
        return requested < remaining ? requested : remaining;
    }

    private static List<string> BuildFilters(ConversionSettings settings, TimeSpan duration)
    {
        var filters = new List<string>();
        if (settings.NormalizeVolume) filters.Add("loudnorm=I=-16:TP=-1.5:LRA=11");
        if (settings.FadeIn && settings.FadeInSeconds > 0)
            filters.Add($"afade=t=in:st=0:d={Math.Min(settings.FadeInSeconds, duration.TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture)}");
        if (settings.FadeOut && settings.FadeOutSeconds > 0)
        {
            var fadeDuration = Math.Min(settings.FadeOutSeconds, duration.TotalSeconds);
            var start = Math.Max(0, duration.TotalSeconds - fadeDuration);
            filters.Add($"afade=t=out:st={start.ToString("0.###", CultureInfo.InvariantCulture)}:d={fadeDuration.ToString("0.###", CultureInfo.InvariantCulture)}");
        }
        return filters;
    }

    public async Task<ConversionResult> ConvertAsync(FfmpegTools tools, MediaFileItem input, string partialPath, ConversionSettings settings, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var effectiveDuration = GetEffectiveDuration(input.Duration, settings);
        var arguments = BuildArguments(input.FilePath, partialPath, settings, effectiveDuration);
        var info = ProcessHelper.Create(tools.FfmpegPath);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        logger.Info($"转换开始：{input.FileName}；参数：{DescribeSafe(settings, effectiveDuration)}");
        var stderr = new StringBuilder();
        using var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        try
        {
            if (!process.Start()) return ConversionResult.Failed("FFmpeg 无法启动。");
            using var registration = cancellationToken.Register(() => ProcessHelper.TryKill(process));
            var errorTask = ReadErrorsAsync(process, stderr);
            while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
            {
                if (TryParseProgress(line, effectiveDuration, out var percent)) progress?.Report(percent);
                if (line.Equals("progress=end", StringComparison.Ordinal)) progress?.Report(100);
            }
            await process.WaitForExitAsync(cancellationToken);
            await errorTask;
            if (process.ExitCode != 0)
            {
                logger.Error($"转换失败：{input.FileName}\n{stderr}");
                return ConversionResult.Failed(ToUserConversionError(stderr.ToString(), process.ExitCode));
            }
            var validationError = await ValidateOutputAsync(tools.FfprobePath, partialPath, settings, effectiveDuration, cancellationToken);
            if (validationError is not null)
            {
                logger.Error($"输出验证失败：{input.FileName}；{validationError}\n{stderr}");
                return ConversionResult.Failed(validationError);
            }
            logger.Info($"转换及验证完成：{input.FileName}");
            return ConversionResult.Completed(partialPath);
        }
        catch (OperationCanceledException) { logger.Info($"转换已取消：{input.FileName}"); return ConversionResult.WasCancelled(); }
        catch (Exception ex)
        {
            logger.Error($"转换异常：{input.FileName}\n{stderr}", ex);
            return ConversionResult.Failed(ex is System.ComponentModel.Win32Exception ? "无法启动 FFmpeg，可能被安全软件拦截。" : ex.Message);
        }
    }

    private async Task<string?> ValidateOutputAsync(string ffprobePath, string path, ConversionSettings settings, TimeSpan expected, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length <= 0) return "输出文件不存在或为空。";
        var result = await probe.ProbeOutputAsync(ffprobePath, path, cancellationToken);
        if (!result.FormatName.Split(',').Contains("caf", StringComparer.OrdinalIgnoreCase)) return "输出容器不是 CAF。";
        if (!result.HasAudio) return "输出文件不包含音频轨道。";
        if (!result.Codec.Equals("pcm_s16le", StringComparison.OrdinalIgnoreCase)) return "输出编码不是 16 位 PCM。";
        if (result.SampleRate != settings.SampleRate) return $"输出采样率不正确（实际 {result.SampleRate} Hz）。";
        if (result.Channels != settings.Channels) return $"输出声道数不正确（实际 {result.Channels}）。";
        if (result.Duration <= TimeSpan.Zero || Math.Abs((result.Duration - expected).TotalSeconds) > 2) return "输出时长与预期不符。";
        if (settings.Preset == ConversionPreset.Bark && result.Duration > TimeSpan.FromSeconds(29.1)) return "Bark 输出超过 29 秒限制。";
        return null;
    }

    private static async Task ReadErrorsAsync(Process process, StringBuilder buffer)
    {
        while (await process.StandardError.ReadLineAsync() is { } line)
            if (buffer.Length < 1_000_000) buffer.AppendLine(line);
    }

    private static bool TryParseProgress(string line, TimeSpan duration, out double percent)
    {
        percent = 0;
        var separator = line.IndexOf('=');
        if (separator < 1 || duration <= TimeSpan.Zero) return false;
        var key = line[..separator]; var value = line[(separator + 1)..];
        double seconds;
        if ((key is "out_time_us" or "out_time_ms") && long.TryParse(value, out var microseconds)) seconds = microseconds / 1_000_000d;
        else if (key == "out_time" && TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var time)) seconds = time.TotalSeconds;
        else return false;
        percent = Math.Clamp(seconds / duration.TotalSeconds * 100, 0, 100);
        return true;
    }

    private static string DescribeSafe(ConversionSettings s, TimeSpan duration)
        => $"preset={s.Preset}; codec=pcm_s16le; rate={s.SampleRate}; channels={s.Channels}; start={TimeSpanParser.ToFfmpeg(s.StartTime)}; duration={TimeSpanParser.ToFfmpeg(duration)}; normalize={s.NormalizeVolume}; fadeIn={s.FadeIn}; fadeOut={s.FadeOut}";
    private static string ToUserConversionError(string stderr, int code)
    {
        if (stderr.Contains("No space left", StringComparison.OrdinalIgnoreCase)) return "磁盘空间不足。";
        if (stderr.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)) return "输出目录无权限，或输出文件正被其他程序占用。";
        if (stderr.Contains("Invalid data", StringComparison.OrdinalIgnoreCase)) return "输入文件损坏或格式不受支持。";
        return $"FFmpeg 转换失败（退出代码 {code}）。可查看日志了解技术详情。";
    }
}
