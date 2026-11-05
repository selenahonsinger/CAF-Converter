using CafConverter.Services;

namespace CafConverter.Tests;

public sealed class FileNameServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CafConverterTests", Guid.NewGuid().ToString("N"));
    public FileNameServiceTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void AutoRenamesExistingOutputs()
    {
        File.WriteAllText(Path.Combine(_directory, "alarm.caf"), "x");
        File.WriteAllText(Path.Combine(_directory, "alarm (1).caf"), "x");
        var service = new FileNameService();
        Assert.Equal(Path.Combine(_directory, "alarm (2).caf"), service.ReserveOutputPath(Path.Combine(_directory, "alarm.mp3"), _directory, false));
    }

    [Fact]
    public void PreservesChineseFileName()
    {
        var service = new FileNameService();
        Assert.Equal(Path.Combine(_directory, "通知 声音.caf"), service.ReserveOutputPath(Path.Combine(_directory, "通知 声音.mp3"), _directory, false));
    }

    [Fact]
    public void DuplicateComparisonIgnoresCase()
    {
        var upper = Path.Combine(_directory, "MEDIA.MP3");
        var lower = Path.Combine(_directory, "media.mp3");
        Assert.True(FileNameService.AreSamePath(upper, lower));
    }

    public void Dispose() { try { Directory.Delete(_directory, true); } catch { } }
}
