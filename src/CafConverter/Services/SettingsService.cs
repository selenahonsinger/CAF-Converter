using System.Text.Json;
using System.Text.Json.Serialization;
using CafConverter.Models;

namespace CafConverter.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public string SettingsDirectory { get; }
    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");
    public string CorruptBackupPath => Path.Combine(SettingsDirectory, "settings.corrupt.json");

    public SettingsService(string? settingsDirectory = null)
    {
        SettingsDirectory = settingsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CafConverter");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        try
        {
            await using var stream = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions, cancellationToken);
            if (settings is null) throw new JsonException("设置内容为空");
            settings.Concurrency = Math.Clamp(settings.Concurrency, 1, 4);
            if (!Helpers.ValidationHelper.SampleRates.Contains(settings.LastSampleRate)) settings.LastSampleRate = 44100;
            if (settings.LastChannels is not (1 or 2)) settings.LastChannels = 1;
            return settings;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.Copy(SettingsPath, CorruptBackupPath, true);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var tempPath = SettingsPath + ".tmp";
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        if (File.Exists(SettingsPath)) File.Replace(tempPath, SettingsPath, null, true);
        else File.Move(tempPath, SettingsPath);
    }
}
