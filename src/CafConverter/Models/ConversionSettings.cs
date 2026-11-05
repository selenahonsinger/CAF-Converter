namespace CafConverter.Models;

public sealed class ConversionSettings
{
    public ConversionPreset Preset { get; set; } = ConversionPreset.Bark;
    public int SampleRate { get; set; } = 44100;
    public int Channels { get; set; } = 1;
    public string Codec { get; set; } = "pcm_s16le";
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan? OutputDuration { get; set; } = TimeSpan.FromSeconds(29);
    public bool KeepFullDuration { get; set; }
    public bool NormalizeVolume { get; set; }
    public bool FadeIn { get; set; }
    public bool FadeOut { get; set; }
    public double FadeInSeconds { get; set; } = 0.5;
    public double FadeOutSeconds { get; set; } = 0.5;

    public ConversionSettings Clone() => (ConversionSettings)MemberwiseClone();

    public static ConversionSettings ForPreset(ConversionPreset preset) => preset switch
    {
        ConversionPreset.Bark => new() { Preset = preset, SampleRate = 44100, Channels = 1, OutputDuration = TimeSpan.FromSeconds(29) },
        ConversionPreset.HighQuality => new() { Preset = preset, SampleRate = 44100, Channels = 2, KeepFullDuration = true, OutputDuration = null },
        _ => new() { Preset = preset, SampleRate = 44100, Channels = 1, OutputDuration = TimeSpan.FromSeconds(29) }
    };
}
