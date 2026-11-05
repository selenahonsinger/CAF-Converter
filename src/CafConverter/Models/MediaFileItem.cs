namespace CafConverter.Models;

public enum ConversionStatus
{
    Waiting, Analyzing, Ready, Converting, Completed, Cancelled, Failed, NoAudio
}

public sealed class MediaFileItem
{
    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);
    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();
    public long FileSize { get; set; }
    public TimeSpan Duration { get; set; }
    public string AudioCodec { get; set; } = "—";
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public bool HasAudio { get; set; }
    public bool HasVideo { get; set; }
}
