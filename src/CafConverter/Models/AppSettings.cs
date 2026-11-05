namespace CafConverter.Models;

public enum OutputDirectoryMode { Source, Custom }

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string? RecentOutputDirectory { get; set; }
    public OutputDirectoryMode OutputDirectoryMode { get; set; } = OutputDirectoryMode.Source;
    public ConversionPreset DefaultPreset { get; set; } = ConversionPreset.Bark;
    public int Concurrency { get; set; } = 2;
    public bool OverwriteExisting { get; set; }
    public string? CustomFfmpegDirectory { get; set; }
    public bool NormalizeVolume { get; set; }
    public int LastSampleRate { get; set; } = 44100;
    public int LastChannels { get; set; } = 1;
}
