using CafConverter.Models;
using CafConverter.Services;

namespace CafConverter.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CafConverterSettingsTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SavesAndLoadsSettings()
    {
        var service = new SettingsService(_directory);
        await service.SaveAsync(new AppSettings { Theme = "Light", Concurrency = 4, RecentOutputDirectory = "C:\\输出" }, TestContext.Current.CancellationToken);
        var loaded = await service.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Light", loaded.Theme); Assert.Equal(4, loaded.Concurrency); Assert.Equal("C:\\输出", loaded.RecentOutputDirectory);
    }

    [Fact]
    public async Task CorruptSettingsAreBackedUpAndReset()
    {
        Directory.CreateDirectory(_directory);
        var service = new SettingsService(_directory);
        await File.WriteAllTextAsync(service.SettingsPath, "{bad json", TestContext.Current.CancellationToken);
        var loaded = await service.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Dark", loaded.Theme); Assert.True(File.Exists(service.CorruptBackupPath));
    }

    public void Dispose() { try { Directory.Delete(_directory, true); } catch { } }
}
