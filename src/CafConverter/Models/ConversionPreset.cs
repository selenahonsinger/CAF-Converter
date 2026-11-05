namespace CafConverter.Models;

public enum ConversionPreset
{
    Bark,
    HighQuality,
    Custom
}

public static class ConversionPresetNames
{
    public static string ToDisplayName(this ConversionPreset preset) => preset switch
    {
        ConversionPreset.Bark => "Bark / iPhone 通知音",
        ConversionPreset.HighQuality => "通用高质量 CAF",
        _ => "自定义"
    };
}
