using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CafConverter.ViewModels;
using CafConverter.Services;
using Microsoft.Win32;

namespace CafConverter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly LoggingService _logger = new();
    private bool _forceClose;

    public MainWindow()
    {
        _logger.Info("正在创建主窗口");
        InitializeComponent();
        _logger.Info("主窗口界面已加载");
        DataContext = _viewModel;
        Loaded += async (_, _) =>
        {
            _logger.Info("开始初始化媒体工具");
            await _viewModel.InitializeAsync();
            _logger.Info($"媒体工具初始化结束：{_viewModel.FfmpegStatus}");
            if (!_viewModel.ToolsAvailable)
                MessageBox.Show("未找到 FFmpeg 和 FFprobe。请将工具放入程序 tools 目录，或点击右侧“选择 FFmpeg”指定 ffmpeg.exe；同目录必须包含 ffprobe.exe。", "需要 FFmpeg", MessageBoxButton.OK, MessageBoxImage.Information);
        };
    }

    private async void OnAddFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要转换的媒体文件", Multiselect = true,
            Filter = "支持的媒体文件|*.mp3;*.mp4;*.m4a;*.wav;*.flac;*.aac;*.mov;*.mkv;*.webm;*.ogg;*.opus;*.wma;*.aiff;*.aif;*.caf|所有文件|*.*"
        };
        if (dialog.ShowDialog(this) == true) await _viewModel.AddFilesAsync(dialog.FileNames);
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            await _viewModel.AddFilesAsync(paths.Where(File.Exists));
    }
    private void OnDragEnter(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { DropOverlay.Visibility = Visibility.Visible; e.Effects = DragDropEffects.Copy; } else e.Effects = DragDropEffects.None; e.Handled = true; }
    private void OnDragLeave(object sender, DragEventArgs e) => DropOverlay.Visibility = Visibility.Collapsed;

    private void OnChooseOutputClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 CAF 输出目录", Multiselect = false };
        if (dialog.ShowDialog(this) == true) _viewModel.SetOutputDirectory(dialog.FolderName);
    }

    private async void OnChooseFfmpegClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择 ffmpeg.exe（同目录需有 ffprobe.exe）", Filter = "ffmpeg.exe|ffmpeg.exe|可执行文件|*.exe", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        if (!await _viewModel.SetCustomFfmpegAsync(dialog.FileName))
            MessageBox.Show("所选工具不可用。请确认 ffmpeg.exe 与 ffprobe.exe 来自同一发行版并位于同一目录。", "FFmpeg 配置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnOpenItemClick(object sender, RoutedEventArgs e) => _viewModel.OpenItemFolder((sender as FrameworkElement)?.DataContext as MediaFileItemViewModel);
    private void OnCopyErrorClick(object sender, RoutedEventArgs e) => _viewModel.CopyError((sender as FrameworkElement)?.DataContext as MediaFileItemViewModel);
    private void OnSettingsClick(object sender, RoutedEventArgs e) { SettingsPanel.BringIntoView(); Keyboard.Focus(SettingsPanel); }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_forceClose) return;
        if (_viewModel.IsRunning)
        {
            if (MessageBox.Show("当前仍有文件正在转换，关闭软件将取消全部任务。确定关闭吗？", "确认关闭", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { e.Cancel = true; return; }
            e.Cancel = true; _viewModel.CancelAll();
            while (_viewModel.IsRunning) await Task.Delay(100);
            await _viewModel.SaveSettingsAsync(); _forceClose = true; Close();
            return;
        }
        e.Cancel = true;
        await _viewModel.SaveSettingsAsync();
        _forceClose = true;
        Close();
    }
}
