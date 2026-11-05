namespace CafConverter.Models;

public sealed record ConversionResult(bool Success, bool Cancelled, string? OutputPath, string? Error)
{
    public static ConversionResult Completed(string output) => new(true, false, output, null);
    public static ConversionResult Failed(string error) => new(false, false, null, error);
    public static ConversionResult WasCancelled() => new(false, true, null, null);
}
