using System.IO;
using System.Windows;
using GLEM.Core.Logging;
using Microsoft.Extensions.Logging;

namespace GLEM.App;

public partial class App : Application
{
    public static ILoggerFactory LoggerFactory { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 詳細設計書 §7: %LOCALAPPDATA%\GLEM\logs に出力、レベルは GLEM_LOG_LEVEL 環境変数で上書き可能
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GLEM", "logs");
        var minLevel = ParseLogLevel(Environment.GetEnvironmentVariable("GLEM_LOG_LEVEL"));

        LoggerFactory = new Microsoft.Extensions.Logging.LoggerFactory(new[] { new FileLoggerProvider(logDir, minLevel) });
        LoggerFactory.CreateLogger("App").LogInformation("GLEM started");

        // M4: 配布物検証用の非対話モード（詳細設計書 §9、M4 DoD「クリーン環境で起動し C-01〜C-03 を再確認」）
        if (e.Args.Length >= 2 && e.Args[0] == "--capture")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // メッセージループ開始前に async コマンドを同期実行するため、DispatcherSynchronizationContext を外す（デッドロック回避）
            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
            var ok = ReleaseVerification.CaptureScreenshots(e.Args[1]);
            LoggerFactory.CreateLogger("App").LogInformation("Capture mode finished: {Ok}", ok);
            Shutdown(ok ? 0 : 1);
            return;
        }

        if (e.Args.Length >= 1 && e.Args[0] == "--selftest")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
            var selfTestOk = ReleaseVerification.RunSelfTest();
            LoggerFactory.CreateLogger("App").LogInformation("Self-test finished: {Ok}", selfTestOk);
            Shutdown(selfTestOk ? 0 : 1);
            return;
        }

        var main = new Views.MainWindow();

        // .glem ファイルを引数で指定した場合は起動時に開く（ファイル関連付け用）
        if (e.Args.Length >= 1 && e.Args[0].EndsWith(".glem", StringComparison.OrdinalIgnoreCase) && File.Exists(e.Args[0]))
        {
            main.LoadProjectOnStartup(e.Args[0]);
        }

        MainWindow = main;
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LoggerFactory?.Dispose();
        base.OnExit(e);
    }

    private static LogLevel ParseLogLevel(string? value) =>
        Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level) ? level : LogLevel.Information;
}
