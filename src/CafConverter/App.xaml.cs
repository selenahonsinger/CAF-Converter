using System.Windows;
using System.Windows.Threading;
using CafConverter.Services;

namespace CafConverter;

public partial class App : Application
{
    private readonly LoggingService _logger = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _logger.CleanupOldLogs();
        _logger.Info($"软件启动，版本 {typeof(App).Assembly.GetName().Version}");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => _logger.Error("未处理异常", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { _logger.Error("未观察任务异常", args.Exception); args.SetObserved(); };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.Error("界面未处理异常", e.Exception);
        MessageBox.Show($"发生了意外错误：{e.Exception.Message}\n\n详细信息已写入日志。", "音频转 CAF", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
