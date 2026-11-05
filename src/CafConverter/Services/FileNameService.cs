namespace CafConverter.Services;

public sealed class FileNameService
{
    private readonly object _gate = new();
    private readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase);

    public string ReserveOutputPath(string inputPath, string outputDirectory, bool overwrite)
    {
        Directory.CreateDirectory(outputDirectory);
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        lock (_gate)
        {
            var candidate = Path.Combine(outputDirectory, baseName + ".caf");
            if (overwrite && !_reserved.Contains(candidate)) { _reserved.Add(candidate); return candidate; }
            var index = 0;
            while (File.Exists(candidate) || _reserved.Contains(candidate))
                candidate = Path.Combine(outputDirectory, $"{baseName} ({++index}).caf");
            _reserved.Add(candidate);
            return candidate;
        }
    }

    public void Release(string path) { lock (_gate) _reserved.Remove(path); }
    public static string GetPartialPath(string finalPath) => Path.Combine(Path.GetDirectoryName(finalPath)!, Path.GetFileNameWithoutExtension(finalPath) + ".partial.caf");
    public static bool AreSamePath(string left, string right)
        => string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
