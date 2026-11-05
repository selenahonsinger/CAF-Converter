using System.Diagnostics;
using CafConverter.Helpers;

namespace CafConverter.Tests;

public sealed class FfmpegIntegrationTests
{
    [Fact]
    public async Task ConvertsGeneratedToneWhenToolsAreAvailable()
    {
        var ffmpeg = FindTool("ffmpeg.exe"); var ffprobe = FindTool("ffprobe.exe");
        if (ffmpeg is null || ffprobe is null) return;
        var directory = Path.Combine(Path.GetTempPath(), "CafConverterIntegration", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var output = Path.Combine(directory, "测试 tone.caf");
            var info = ProcessHelper.Create(ffmpeg);
            foreach (var arg in new[] { "-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "sine=frequency=1000:duration=3", "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", "-f", "caf", output }) info.ArgumentList.Add(arg);
            using var process = Process.Start(info)!; await process.WaitForExitAsync(TestContext.Current.CancellationToken); Assert.Equal(0, process.ExitCode);
            var probeInfo = ProcessHelper.Create(ffprobe);
            foreach (var arg in new[] { "-v", "error", "-select_streams", "a:0", "-show_entries", "format=format_name:stream=codec_name,sample_rate,channels", "-of", "default=nw=1", output }) probeInfo.ArgumentList.Add(arg);
            using var probe = Process.Start(probeInfo)!; var textTask = probe.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken); await probe.WaitForExitAsync(TestContext.Current.CancellationToken); var text = await textTask;
            Assert.Contains("format_name=caf", text); Assert.Contains("codec_name=pcm_s16le", text); Assert.Contains("sample_rate=44100", text); Assert.Contains("channels=1", text);
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }

    private static string? FindTool(string name)
    {
        var embedded = Path.Combine(AppContext.BaseDirectory, "tools", name); if (File.Exists(embedded)) return embedded;
        var sourceTool = Path.Combine(Directory.GetCurrentDirectory(), "src", "CafConverter", "Resources", "Tools", "win-x64", name);
        if (File.Exists(sourceTool)) return sourceTool;
        return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Select(p => Path.Combine(p.Trim('"'), name)).FirstOrDefault(File.Exists);
    }
}
