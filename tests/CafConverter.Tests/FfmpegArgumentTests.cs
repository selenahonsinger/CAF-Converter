using CafConverter.Models;
using CafConverter.Services;

namespace CafConverter.Tests;

public sealed class FfmpegArgumentTests
{
    [Fact]
    public void BuildsSafeAccurateBarkArguments()
    {
        var settings = ConversionSettings.ForPreset(ConversionPreset.Bark);
        settings.StartTime = TimeSpan.FromSeconds(2.5);
        var args = FfmpegService.BuildArguments(@"C:\中文 路径\a [&].mp4", @"C:\输出\a.partial.caf", settings, TimeSpan.FromSeconds(29));
        Assert.Equal("-i", args[3]);
        Assert.Equal(@"C:\中文 路径\a [&].mp4", args[4]);
        var array = args.ToArray();
        Assert.True(Array.IndexOf(array, "-ss") > Array.IndexOf(array, "-i"));
        Assert.Contains("0:a:0", args);
        Assert.Contains("-vn", args);
        Assert.Contains("pcm_s16le", args);
        Assert.Contains("caf", args);
        Assert.Contains("pipe:1", args);
    }

    [Fact]
    public void BarkDurationNeverExceeds29Seconds()
    {
        var settings = ConversionSettings.ForPreset(ConversionPreset.Bark);
        settings.OutputDuration = TimeSpan.FromMinutes(5);
        Assert.Equal(TimeSpan.FromSeconds(29), FfmpegService.GetEffectiveDuration(TimeSpan.FromMinutes(10), settings));
    }

    [Fact]
    public void FullDurationOmitsTimeLimit()
    {
        var settings = ConversionSettings.ForPreset(ConversionPreset.HighQuality);
        var args = FfmpegService.BuildArguments("input.mp3", "output.caf", settings, TimeSpan.FromSeconds(3));
        Assert.DoesNotContain("-t", args);
        Assert.DoesNotContain("-ss", args);
    }
}
