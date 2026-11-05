using System.ComponentModel;
using System.Runtime.CompilerServices;
using CafConverter.Commands;
using CafConverter.Helpers;
using CafConverter.Models;

namespace CafConverter.ViewModels;

public sealed class MediaFileItemViewModel : INotifyPropertyChanged
{
    private MediaFileItem _model;
    private bool _isSelected = true;
    private ConversionStatus _status = ConversionStatus.Analyzing;
    private double _progress;
    private string? _outputPath;
    private string? _errorDetail;
    private CancellationTokenSource? _taskCancellation;

    public MediaFileItemViewModel(MediaFileItem model)
    {
        _model = model;
        CancelCommand = new RelayCommand(_ => _taskCancellation?.Cancel(), _ => Status == ConversionStatus.Converting);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public MediaFileItem Model { get => _model; set { _model = value; RaiseAll(); } }
    public string FilePath => Model.FilePath;
    public string FileName => Model.FileName;
    public string Type => Model.Extension;
    public string DurationText => Model.Duration > TimeSpan.Zero ? TimeSpanParser.Format(Model.Duration) : "—";
    public string FileSizeText => FileSizeFormatter.Format(Model.FileSize);
    public string AudioFormat => Model.HasAudio ? $"{Model.AudioCodec} / {Model.SampleRate} Hz / {Model.Channels} 声道" : "—";
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public ConversionStatus Status { get => _status; set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); CancelCommand.RaiseCanExecuteChanged(); } }
    public string StatusText => Status switch
    {
        ConversionStatus.Waiting => "等待转换", ConversionStatus.Analyzing => "正在分析", ConversionStatus.Ready => "准备完成",
        ConversionStatus.Converting => "正在转换", ConversionStatus.Completed => "已完成", ConversionStatus.Cancelled => "已取消",
        ConversionStatus.Failed => "转换失败", ConversionStatus.NoAudio => "没有音频轨道", _ => "未知"
    };
    public double Progress { get => _progress; set { _progress = Math.Clamp(value, 0, 100); OnPropertyChanged(); } }
    public string? OutputPath { get => _outputPath; set { _outputPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(OutputDisplay)); } }
    public string OutputDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(OutputPath)) return "—";
            try { return $"{OutputPath} ({FileSizeFormatter.Format(new FileInfo(OutputPath).Length)})"; }
            catch { return OutputPath; }
        }
    }
    public string? ErrorDetail { get => _errorDetail; set { _errorDetail = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorDetail);
    public RelayCommand CancelCommand { get; }

    public CancellationToken CreateLinkedToken(CancellationToken global)
    {
        _taskCancellation?.Dispose();
        _taskCancellation = CancellationTokenSource.CreateLinkedTokenSource(global);
        return _taskCancellation.Token;
    }
    public void MarkCancelled() { Status = ConversionStatus.Cancelled; ErrorDetail = null; }

    private void RaiseAll()
    {
        foreach (var name in new[] { nameof(Model), nameof(FilePath), nameof(FileName), nameof(Type), nameof(DurationText), nameof(FileSizeText), nameof(AudioFormat) }) OnPropertyChanged(name);
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
