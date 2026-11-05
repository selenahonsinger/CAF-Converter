using System.Text;

namespace CafConverter.Services;

public sealed class LoggingService
{
    private readonly object _gate = new();
    public string LogDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CafConverter", "Logs");
    public string CurrentLogPath => Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");

    public LoggingService() => Directory.CreateDirectory(LogDirectory);
    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? exception = null) => Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_gate)
                File.AppendAllText(CurrentLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {Sanitize(message)}{Environment.NewLine}", new UTF8Encoding(false));
        }
        catch { }
    }

    private static string Sanitize(string text) => text.Replace("\0", string.Empty);

    public void CleanupOldLogs()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(LogDirectory, "*.log"))
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-14)) File.Delete(file);
        }
        catch (Exception ex) { Error("清理旧日志失败", ex); }
    }
}
