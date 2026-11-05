using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CafConverter.Helpers;
using CafConverter.Models;

namespace CafConverter.Services;

public sealed record OutputProbe(string FormatName, TimeSpan Duration, string Codec, int SampleRate, int Channels, bool HasAudio);

public sealed class FfprobeService(LoggingService logger)
{
    public async Task<MediaFileItem> AnalyzeAsync(string ffprobePath, string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("文件不存在或已被删除。", filePath);
        var json = await RunProbeAsync(ffprobePath, filePath, cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var item = new MediaFileItem { FilePath = Path.GetFullPath(filePath), FileSize = new FileInfo(filePath).Length };
            if (root.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var durationNode))
                item.Duration = ParseDuration(durationNode);
            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    var type = GetString(stream, "codec_type");
                    if (type == "video") item.HasVideo = true;
                    if (type != "audio" || item.HasAudio) continue;
                    item.HasAudio = true;
                    item.AudioCodec = GetString(stream, "codec_name") ?? "未知";
                    _ = int.TryParse(GetString(stream, "sample_rate"), out var sampleRate);
                    item.SampleRate = sampleRate;
                    if (stream.TryGetProperty("channels", out var channels)) item.Channels = channels.GetInt32();
                    if (item.Duration == TimeSpan.Zero && stream.TryGetProperty("duration", out var streamDuration)) item.Duration = ParseDuration(streamDuration);
                }
            }
            return item;
        }
        catch (JsonException ex) { throw new InvalidDataException("FFprobe 返回了无效的媒体信息，文件可能已损坏。", ex); }
    }

    public async Task<OutputProbe> ProbeOutputAsync(string ffprobePath, string filePath, CancellationToken cancellationToken = default)
    {
        var json = await RunProbeAsync(ffprobePath, filePath, cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var formatName = root.GetProperty("format").TryGetProperty("format_name", out var fn) ? fn.GetString() ?? "" : "";
            var duration = root.GetProperty("format").TryGetProperty("duration", out var dn) ? ParseDuration(dn) : TimeSpan.Zero;
            foreach (var stream in root.GetProperty("streams").EnumerateArray())
            {
                if (GetString(stream, "codec_type") != "audio") continue;
                _ = int.TryParse(GetString(stream, "sample_rate"), out var rate);
                var channels = stream.TryGetProperty("channels", out var cn) ? cn.GetInt32() : 0;
                return new(formatName, duration, GetString(stream, "codec_name") ?? "", rate, channels, true);
            }
            return new(formatName, duration, "", 0, 0, false);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new InvalidDataException("无法解析输出文件验证信息。", ex); }
    }

    private async Task<string> RunProbeAsync(string executable, string path, CancellationToken cancellationToken)
    {
        var info = ProcessHelper.Create(executable);
        foreach (var arg in new[] { "-v", "error", "-show_entries", "format=format_name,duration:stream=codec_type,codec_name,sample_rate,channels,duration", "-of", "json", path }) info.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = info };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("FFprobe 无法启动。");
            using var registration = cancellationToken.Register(() => ProcessHelper.TryKill(process));
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0) throw new InvalidDataException(ToUserProbeError(stderr));
            return stdout;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            logger.Error("FFprobe 启动或运行失败", ex);
            throw new InvalidOperationException("无法启动 FFprobe，可能被安全软件拦截或文件不可用。", ex);
        }
    }

    private static string ToUserProbeError(string stderr)
        => stderr.Contains("Permission denied", StringComparison.OrdinalIgnoreCase) ? "无法读取文件，文件可能被占用或没有访问权限。" : "无法分析媒体文件，文件可能损坏或格式不受支持。";
    private static string? GetString(JsonElement element, string name) => element.TryGetProperty(name, out var node) ? node.GetString() : null;
    private static TimeSpan ParseDuration(JsonElement node)
    {
        var text = node.ValueKind == JsonValueKind.String ? node.GetString() : node.GetRawText();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
    }
}
