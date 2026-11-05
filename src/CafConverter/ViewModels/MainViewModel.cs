using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using CafConverter.Commands;
using CafConverter.Helpers;
using CafConverter.Models;
using CafConverter.Services;

namespace CafConverter.ViewModels;

public sealed record PresetOption(ConversionPreset Value, string Name);

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LoggingService _logger = new();
    private readonly SettingsService _settingsService = new();
    private readonly ToolExtractionService _extraction;
    private readonly FfmpegLocatorService _locator;
    private readonly FfprobeService _probe;
    private readonly FfmpegService _ffmpeg;
    private readonly FileNameService _fileNames = new();
    private readonly ConversionQueueService _queue = new();
    private readonly HashSet<string> _knownFiles = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings _appSettings = new();
    private ConversionSettings _conversion = ConversionSettings.ForPreset(ConversionPreset.Bark);
    private FfmpegTools? _tools;
    private CancellationTokenSource? _allCancellation;
    private bool _isRunning;
    private bool _isDarkTheme = true;
    private string _statusMessage = "正在初始化 FFmpeg…";
    private string _startTimeText = "00:00.000";
    private string _durationText = "00:29.000";
    private string _endTimeText = "00:29.000";
    private string? _validationMessage;
    private string _summaryText = string.Empty;
    private IReadOnlyList<MediaFileItemViewModel> _progressItems = [];

    public MainViewModel()
    {
        _extraction = new(_logger); _locator = new(_extraction, _logger); _probe = new(_logger); _ffmpeg = new(_probe, _logger);
        AddFilesCommand = new AsyncRelayCommand(async parameter => await AddFilesAsync((IEnumerable<string>)(parameter ?? Array.Empty<string>())));
        RemoveCommand = new RelayCommand(p => Remove(p as MediaFileItemViewModel), _ => !IsRunning);
        RemoveSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => !IsRunning && Files.Any(f => f.IsSelected));
        ClearCommand = new RelayCommand(_ => Clear(), _ => !IsRunning && Files.Count > 0);
        SelectAllCommand = new RelayCommand(_ => { foreach (var file in Files) file.IsSelected = true; }, _ => Files.Count > 0);
        StartCommand = new AsyncRelayCommand(_ => StartConversionAsync(), _ => !IsRunning);
        CancelAllCommand = new RelayCommand(_ => CancelAll(), _ => IsRunning);
        ToggleThemeCommand = new AsyncRelayCommand(_ => ToggleThemeAsync());
        OpenOutputCommand = new RelayCommand(_ => OpenOutputDirectory());
        OpenLogCommand = new RelayCommand(_ => OpenPath(_logger.LogDirectory));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<MediaFileItemViewModel> Files { get; } = [];
    public IReadOnlyList<PresetOption> Presets { get; } = Enum.GetValues<ConversionPreset>().Select(p => new PresetOption(p, p.ToDisplayName())).ToArray();
    public IReadOnlyList<int> SampleRates => ValidationHelper.SampleRates;
    public IReadOnlyList<int> ChannelOptions { get; } = [1, 2];
    public IReadOnlyList<int> ConcurrencyOptions { get; } = [1, 2, 3, 4];
    public AsyncRelayCommand AddFilesCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand CancelAllCommand { get; }
    public AsyncRelayCommand ToggleThemeCommand { get; }
    public RelayCommand OpenOutputCommand { get; }
    public RelayCommand OpenLogCommand { get; }

    public PresetOption SelectedPreset
    {
        get => Presets.First(p => p.Value == _conversion.Preset);
        set { if (value is null || value.Value == _conversion.Preset) return; ApplyPreset(value.Value); }
    }
    public int SampleRate { get => _conversion.SampleRate; set { _conversion.SampleRate = value; OnPropertyChanged(); ValidateInputs(); } }
    public int Channels { get => _conversion.Channels; set { _conversion.Channels = value; OnPropertyChanged(); ValidateInputs(); } }
    public bool NormalizeVolume { get => _conversion.NormalizeVolume; set { _conversion.NormalizeVolume = value; OnPropertyChanged(); } }
    public bool FadeIn { get => _conversion.FadeIn; set { _conversion.FadeIn = value; OnPropertyChanged(); } }
    public bool FadeOut { get => _conversion.FadeOut; set { _conversion.FadeOut = value; OnPropertyChanged(); } }
    public double FadeInSeconds { get => _conversion.FadeInSeconds; set { _conversion.FadeInSeconds = Math.Max(0, value); OnPropertyChanged(); ValidateInputs(); } }
    public double FadeOutSeconds { get => _conversion.FadeOutSeconds; set { _conversion.FadeOutSeconds = Math.Max(0, value); OnPropertyChanged(); ValidateInputs(); } }
    public bool KeepFullDuration { get => _conversion.KeepFullDuration; set { _conversion.KeepFullDuration = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDurationEnabled)); ValidateInputs(); } }
    public bool IsCustomPreset => _conversion.Preset == ConversionPreset.Custom;
    public bool IsTrimEnabled => _conversion.Preset != ConversionPreset.HighQuality;
    public bool IsDurationEnabled => _conversion.Preset != ConversionPreset.HighQuality && !KeepFullDuration;
    public bool ShowBarkNotice => _conversion.Preset == ConversionPreset.Bark;
    public string StartTimeText { get => _startTimeText; set { _startTimeText = value; OnPropertyChanged(); ParseTimes(); } }
    public string DurationText { get => _durationText; set { _durationText = value; OnPropertyChanged(); ParseTimes(); } }
    public string EndTimeText { get => _endTimeText; private set { _endTimeText = value; OnPropertyChanged(); } }
    public string? ValidationMessage { get => _validationMessage; private set { _validationMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasValidationMessage)); } }
    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);
    public bool OutputToSource { get => _appSettings.OutputDirectoryMode == OutputDirectoryMode.Source; set { if (value) { _appSettings.OutputDirectoryMode = OutputDirectoryMode.Source; OnPropertyChanged(); OnPropertyChanged(nameof(OutputToCustom)); } } }
    public bool OutputToCustom { get => !OutputToSource; set { if (value) { _appSettings.OutputDirectoryMode = OutputDirectoryMode.Custom; OnPropertyChanged(); OnPropertyChanged(nameof(OutputToSource)); } } }
    public string OutputDirectory { get => _appSettings.RecentOutputDirectory ?? string.Empty; set { _appSettings.RecentOutputDirectory = value; OnPropertyChanged(); } }
    public bool OverwriteExisting { get => _appSettings.OverwriteExisting; set { _appSettings.OverwriteExisting = value; OnPropertyChanged(); } }
    public int Concurrency { get => _appSettings.Concurrency; set { _appSettings.Concurrency = Math.Clamp(value, 1, 4); OnPropertyChanged(); } }
    public bool IsRunning { get => _isRunning; private set { _isRunning = value; OnPropertyChanged(); RaiseCommandStates(); } }
    public bool IsDarkTheme { get => _isDarkTheme; private set { _isDarkTheme = value; OnPropertyChanged(); OnPropertyChanged(nameof(ThemeButtonText)); } }
    public string ThemeButtonText => IsDarkTheme ? "☀ 浅色" : "☾ 深色";
    public string StatusMessage { get => _statusMessage; private set { _statusMessage = value; OnPropertyChanged(); } }
    public string SummaryText { get => _summaryText; private set { _summaryText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSummary)); } }
    public bool HasSummary => !string.IsNullOrEmpty(SummaryText);
    public double TotalProgress => _progressItems.Count == 0 ? 0 : _progressItems.Average(f => f.Progress);
    public bool HasFiles => Files.Count > 0;
    public bool IsEmpty => Files.Count == 0;
    public bool ToolsAvailable => _tools is not null;
    public string FfmpegStatus => _tools is null ? "未找到 FFmpeg / FFprobe" : $"已就绪：{Path.GetFileName(_tools.FfmpegPath)}";

    public async Task InitializeAsync()
    {
        _appSettings = await _settingsService.LoadAsync();
        _conversion = ConversionSettings.ForPreset(_appSettings.DefaultPreset);
        _conversion.NormalizeVolume = _appSettings.NormalizeVolume;
        if (_conversion.Preset == ConversionPreset.Custom) { _conversion.SampleRate = _appSettings.LastSampleRate; _conversion.Channels = _appSettings.LastChannels; }
        IsDarkTheme = !_appSettings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
        ApplyTheme(); RefreshSettingsProperties();
        _tools = await _locator.LocateAsync(_appSettings.CustomFfmpegDirectory);
        StatusMessage = _tools is null ? "未找到 FFmpeg，请点击“选择 FFmpeg”指定工具目录。" : "就绪，可以添加媒体文件。";
        OnPropertyChanged(nameof(ToolsAvailable)); OnPropertyChanged(nameof(FfmpegStatus));
    }

    public async Task<bool> SetCustomFfmpegAsync(string ffmpegPath)
    {
        var directory = Path.GetDirectoryName(ffmpegPath);
        if (directory is null || !File.Exists(Path.Combine(directory, "ffprobe.exe"))) return false;
        _tools = await _locator.LocateAsync(directory);
        if (_tools is null) return false;
        _appSettings.CustomFfmpegDirectory = directory;
        await SaveSettingsAsync();
        StatusMessage = "FFmpeg 工具已就绪。";
        OnPropertyChanged(nameof(ToolsAvailable)); OnPropertyChanged(nameof(FfmpegStatus));
        var pending = Files.Where(f => !f.Model.HasAudio && f.Status == ConversionStatus.Failed).Select(f => f.FilePath).ToArray();
        foreach (var item in Files.Where(f => pending.Contains(f.FilePath, StringComparer.OrdinalIgnoreCase)).ToArray()) Remove(item);
        if (pending.Length > 0) await AddFilesAsync(pending);
        return true;
    }

    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        var supported = new HashSet<string>([".mp3", ".mp4", ".m4a", ".wav", ".flac", ".aac", ".mov", ".mkv", ".webm", ".ogg", ".opus", ".wma", ".aiff", ".aif", ".caf"], StringComparer.OrdinalIgnoreCase);
        var additions = new List<MediaFileItemViewModel>();
        foreach (var rawPath in paths)
        {
            string path;
            try { path = Path.GetFullPath(rawPath); } catch { continue; }
            if (!supported.Contains(Path.GetExtension(path)) || !_knownFiles.Add(path)) continue;
            var placeholder = new MediaFileItem { FilePath = path, FileSize = File.Exists(path) ? new FileInfo(path).Length : 0 };
            var vm = new MediaFileItemViewModel(placeholder);
            Files.Add(vm); additions.Add(vm); _logger.Info($"导入文件：{Path.GetFileName(path)}");
        }
        NotifyCollectionChanged();
        if (_tools is null)
        {
            foreach (var item in additions) { item.Status = ConversionStatus.Failed; item.ErrorDetail = "尚未配置 FFmpeg / FFprobe。"; }
            return;
        }
        using var analysisSemaphore = new SemaphoreSlim(2);
        var analysisTasks = additions.Select(async vm =>
        {
            await analysisSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var model = await _probe.AnalyzeAsync(_tools.FfprobePath, vm.FilePath).ConfigureAwait(false);
                return new AnalysisOutcome(vm, model, null);
            }
            catch (Exception ex)
            {
                _logger.Error($"分析失败：{vm.FileName}", ex);
                return new AnalysisOutcome(vm, null, ex.Message);
            }
            finally { analysisSemaphore.Release(); }
        }).ToArray();
        var outcomes = await Task.WhenAll(analysisTasks);
        foreach (var outcome in outcomes)
        {
            if (outcome.Model is not null)
            {
                outcome.Item.Model = outcome.Model;
                if (!outcome.Model.HasAudio)
                {
                    outcome.Item.Status = ConversionStatus.NoAudio;
                    outcome.Item.ErrorDetail = "该文件不包含可转换的音频轨道";
                }
                else outcome.Item.Status = ConversionStatus.Ready;
            }
            else
            {
                outcome.Item.Status = ConversionStatus.Failed;
                outcome.Item.ErrorDetail = outcome.Error;
            }
        }
        StatusMessage = $"已导入 {additions.Count} 个新文件。";
    }

    public async Task StartConversionAsync()
    {
        if (IsRunning) return;
        var selected = Files.Where(f => f.IsSelected && f.Model.HasAudio && f.Status is not ConversionStatus.Analyzing).ToArray();
        var error = ValidateBeforeStart(selected);
        if (error is not null) { MessageBox.Show(error, "无法开始转换", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        IsRunning = true; SummaryText = string.Empty; _allCancellation = new();
        _progressItems = selected; OnPropertyChanged(nameof(TotalProgress));
        foreach (var item in selected) { item.Progress = 0; item.ErrorDetail = null; item.Status = ConversionStatus.Waiting; }
        var jobs = selected.Select<MediaFileItemViewModel, Func<CancellationToken, Task>>(item => token => ConvertOneAsync(item, token)).ToArray();
        try { await _queue.RunAsync(jobs, Concurrency, _allCancellation.Token); }
        finally
        {
            IsRunning = false; _allCancellation.Dispose(); _allCancellation = null;
            var success = selected.Count(f => f.Status == ConversionStatus.Completed);
            var failed = selected.Count(f => f.Status == ConversionStatus.Failed);
            var cancelled = selected.Count(f => f.Status == ConversionStatus.Cancelled);
            SummaryText = $"转换完成  成功：{success}  失败：{failed}  取消：{cancelled}\n输出目录：{GetSummaryDirectory(selected)}";
            StatusMessage = "转换任务已结束。";
            await SaveSettingsAsync();
        }
    }

    private async Task ConvertOneAsync(MediaFileItemViewModel item, CancellationToken globalToken)
    {
        var token = item.CreateLinkedToken(globalToken);
        string? finalPath = null; string? partialPath = null;
        try
        {
            token.ThrowIfCancellationRequested();
            item.Status = ConversionStatus.Converting;
            var outputDirectory = OutputToSource ? Path.GetDirectoryName(item.FilePath)! : OutputDirectory;
            finalPath = _fileNames.ReserveOutputPath(item.FilePath, outputDirectory, OverwriteExisting);
            partialPath = FileNameService.GetPartialPath(finalPath);
            if (File.Exists(partialPath)) File.Delete(partialPath);
            var progress = new Progress<double>(value => { item.Progress = value; OnPropertyChanged(nameof(TotalProgress)); });
            var result = await _ffmpeg.ConvertAsync(_tools!, item.Model, partialPath, _conversion.Clone(), progress, token);
            if (result.Cancelled) { item.MarkCancelled(); return; }
            if (!result.Success) { item.Status = ConversionStatus.Failed; item.ErrorDetail = result.Error; return; }
            File.Move(partialPath, finalPath, OverwriteExisting);
            item.OutputPath = finalPath; item.Progress = 100; item.Status = ConversionStatus.Completed;
        }
        catch (OperationCanceledException) { item.MarkCancelled(); }
        catch (Exception ex) { item.Status = ConversionStatus.Failed; item.ErrorDetail = ToUserFileError(ex); _logger.Error($"完成输出失败：{item.FileName}", ex); }
        finally
        {
            if (partialPath is not null) try { if (File.Exists(partialPath)) File.Delete(partialPath); } catch { }
            if (finalPath is not null) _fileNames.Release(finalPath);
            OnPropertyChanged(nameof(TotalProgress));
        }
    }

    private string? ValidateBeforeStart(MediaFileItemViewModel[] selected)
    {
        if (selected.Length == 0) return "请至少勾选一个包含音频轨道的有效文件。";
        if (_tools is null) return "找不到 FFmpeg 和 FFprobe，请先选择工具位置。";
        ParseTimes(); if (ValidationMessage is not null) return ValidationMessage;
        var settingsError = ValidationHelper.Validate(_conversion); if (settingsError is not null) return settingsError;
        foreach (var item in selected)
        {
            if (!File.Exists(item.FilePath)) return $"文件不存在或已被删除：{item.FileName}";
            if (_conversion.StartTime >= item.Model.Duration) return $"开始时间超过文件长度：{item.FileName}";
            var directory = OutputToSource ? Path.GetDirectoryName(item.FilePath)! : OutputDirectory;
            try
            {
                if (string.IsNullOrWhiteSpace(directory)) return "请选择输出目录。";
                Directory.CreateDirectory(directory);
                var probePath = Path.Combine(directory, $".cafconverter-write-{Guid.NewGuid():N}.tmp");
                using (File.Create(probePath, 1, FileOptions.DeleteOnClose)) { }
                var duration = FfmpegService.GetEffectiveDuration(item.Model.Duration, _conversion);
                var needed = (long)(duration.TotalSeconds * _conversion.SampleRate * _conversion.Channels * 2 * 1.1);
                var root = Path.GetPathRoot(Path.GetFullPath(directory));
                if (root is not null && new DriveInfo(root).AvailableFreeSpace < needed) return $"磁盘空间不足，无法转换：{item.FileName}";
            }
            catch (UnauthorizedAccessException) { return $"输出目录没有写入权限：{directory}"; }
            catch (IOException ex) { return $"无法使用输出目录：{ex.Message}"; }
        }
        return null;
    }

    public void CancelAll()
    {
        if (!IsRunning) return;
        StatusMessage = "正在取消全部任务…"; _allCancellation?.Cancel();
    }

    public async Task SaveSettingsAsync()
    {
        _appSettings.Theme = IsDarkTheme ? "Dark" : "Light";
        _appSettings.DefaultPreset = _conversion.Preset; _appSettings.NormalizeVolume = NormalizeVolume;
        _appSettings.LastSampleRate = SampleRate; _appSettings.LastChannels = Channels;
        try { await _settingsService.SaveAsync(_appSettings); } catch (Exception ex) { _logger.Error("保存设置失败", ex); }
    }

    public void SetOutputDirectory(string directory) { OutputDirectory = directory; OutputToCustom = true; }
    public void CopyError(MediaFileItemViewModel? item) { if (!string.IsNullOrWhiteSpace(item?.ErrorDetail)) Clipboard.SetText(item.ErrorDetail); }
    public void OpenItemFolder(MediaFileItemViewModel? item) { if (item?.OutputPath is not null) OpenPath(Path.GetDirectoryName(item.OutputPath)!); }

    private void ApplyPreset(ConversionPreset preset)
    {
        var normalize = _conversion.NormalizeVolume;
        _conversion = ConversionSettings.ForPreset(preset); _conversion.NormalizeVolume = normalize;
        _startTimeText = "00:00.000"; _durationText = preset == ConversionPreset.HighQuality ? "完整时长" : "00:29.000";
        ParseTimes(); RefreshSettingsProperties();
    }

    private void ParseTimes()
    {
        if (!TimeSpanParser.TryParse(_startTimeText, out var start)) { ValidationMessage = "开始时间格式无效。"; return; }
        _conversion.StartTime = start;
        if (!_conversion.KeepFullDuration)
        {
            if (!TimeSpanParser.TryParse(_durationText, out var duration) || duration <= TimeSpan.Zero) { ValidationMessage = "输出时长格式无效或不大于 0。"; return; }
            _conversion.OutputDuration = duration;
        }
        ValidationMessage = ValidationHelper.Validate(_conversion);
        EndTimeText = _conversion.KeepFullDuration ? "由源文件决定" : TimeSpanParser.Format(start + _conversion.OutputDuration!.Value);
    }

    private void ValidateInputs() => ParseTimes();
    private void Remove(MediaFileItemViewModel? item) { if (item is null) return; _knownFiles.Remove(item.FilePath); Files.Remove(item); NotifyCollectionChanged(); }
    private void RemoveSelected() { foreach (var item in Files.Where(f => f.IsSelected).ToArray()) Remove(item); }
    private void Clear() { _knownFiles.Clear(); Files.Clear(); NotifyCollectionChanged(); }
    private void NotifyCollectionChanged() { OnPropertyChanged(nameof(HasFiles)); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(TotalProgress)); RaiseCommandStates(); }
    private void RaiseCommandStates() { StartCommand.RaiseCanExecuteChanged(); CancelAllCommand.RaiseCanExecuteChanged(); RemoveCommand.RaiseCanExecuteChanged(); RemoveSelectedCommand.RaiseCanExecuteChanged(); ClearCommand.RaiseCanExecuteChanged(); SelectAllCommand.RaiseCanExecuteChanged(); }

    private async Task ToggleThemeAsync()
    {
        IsDarkTheme = !IsDarkTheme; ApplyTheme(); await SaveSettingsAsync();
    }
    private void ApplyTheme()
    {
        var resources = Application.Current.Resources;
        resources["WindowBackground"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(IsDarkTheme ? "#111318" : "#F4F6FA"));
        resources["PanelBackground"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(IsDarkTheme ? "#1A1E26" : "#FFFFFF"));
        resources["ControlBackground"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(IsDarkTheme ? "#252B36" : "#E9EDF5"));
        resources["PrimaryText"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(IsDarkTheme ? "#F4F7FB" : "#172033"));
        resources["SecondaryText"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(IsDarkTheme ? "#AAB4C5" : "#5D687B"));
        resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(IsDarkTheme ? "#343C4A" : "#D6DCE7"));
    }

    private void RefreshSettingsProperties()
    {
        foreach (var name in new[] { nameof(SelectedPreset), nameof(SampleRate), nameof(Channels), nameof(NormalizeVolume), nameof(FadeIn), nameof(FadeOut), nameof(FadeInSeconds), nameof(FadeOutSeconds), nameof(KeepFullDuration), nameof(IsCustomPreset), nameof(IsTrimEnabled), nameof(IsDurationEnabled), nameof(ShowBarkNotice), nameof(StartTimeText), nameof(DurationText), nameof(EndTimeText), nameof(OutputToSource), nameof(OutputToCustom), nameof(OutputDirectory), nameof(OverwriteExisting), nameof(Concurrency) }) OnPropertyChanged(name);
    }
    private string GetSummaryDirectory(MediaFileItemViewModel[] selected) => OutputToSource ? (selected.Length == 1 ? Path.GetDirectoryName(selected[0].FilePath)! : "各源文件目录") : OutputDirectory;
    private void OpenOutputDirectory()
    {
        var directory = OutputToSource ? Files.Select(f => Path.GetDirectoryName(f.FilePath)).FirstOrDefault(d => !string.IsNullOrEmpty(d)) : OutputDirectory;
        if (!string.IsNullOrWhiteSpace(directory)) OpenPath(directory!);
    }
    private static void OpenPath(string path)
    {
        if (!Directory.Exists(path)) return;
        var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        info.ArgumentList.Add(path); Process.Start(info);
    }
    private static string ToUserFileError(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "没有写入输出目录的权限。",
        IOException => "输出文件正被占用、路径不可用或磁盘空间不足。",
        _ => ex.Message
    };
    private sealed record AnalysisOutcome(MediaFileItemViewModel Item, MediaFileItem? Model, string? Error);
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
