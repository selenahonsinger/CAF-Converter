using CafConverter.Models;

namespace CafConverter.Helpers;

public static class ValidationHelper
{
    public static readonly int[] SampleRates = [22050, 32000, 44100, 48000];

    public static string? Validate(ConversionSettings settings)
    {
        if (!SampleRates.Contains(settings.SampleRate)) return "请选择有效的采样率。";
        if (settings.Channels is not (1 or 2)) return "声道数只能是单声道或立体声。";
        if (settings.StartTime < TimeSpan.Zero) return "开始时间不能小于 0。";
        if (!settings.KeepFullDuration && (settings.OutputDuration is null || settings.OutputDuration <= TimeSpan.Zero)) return "输出时长必须大于 0。";
        if (settings.Preset == ConversionPreset.Bark && settings.OutputDuration > TimeSpan.FromSeconds(29)) return "Bark 预设输出不能超过 29 秒。";
        if (settings.FadeInSeconds < 0 || settings.FadeOutSeconds < 0) return "淡入淡出时长不能为负数。";
        return null;
    }
}
