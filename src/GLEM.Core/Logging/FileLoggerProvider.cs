using Microsoft.Extensions.Logging;

namespace GLEM.Core.Logging;

/// <summary>
/// 指定ディレクトリに glem-yyyyMMdd.log として書き出し、直近7ファイルのみ保持するファイルロガープロバイダ。
/// 詳細設計書 §7（ログ・診断設計）の実装。
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int MaxRetainedFiles = 7;

    private readonly string _directory;
    private readonly LogLevel _minimumLevel;
    private readonly object _gate = new();
    private string? _currentFile;

    public FileLoggerProvider(string directory, LogLevel minimumLevel = LogLevel.Information)
    {
        _directory = directory;
        _minimumLevel = minimumLevel;
        Directory.CreateDirectory(directory);
        PruneOldLogs();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal void Write(LogLevel level, string category, string message, Exception? exception)
    {
        if (level < _minimumLevel)
        {
            return;
        }

        var file = Path.Combine(_directory, $"glem-{DateTime.Now:yyyyMMdd}.log");
        lock (_gate)
        {
            if (!string.Equals(file, _currentFile, StringComparison.Ordinal))
            {
                PruneOldLogs();
                _currentFile = file;
            }

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {category}: {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception.ToString();
            }

            File.AppendAllText(file, line + Environment.NewLine);
        }
    }

    private void PruneOldLogs()
    {
        // ファイル名が日付のため、辞書順の降順 == 新しさの降順
        var obsolete = Directory.EnumerateFiles(_directory, "glem-*.log")
            .OrderByDescending(p => p)
            .Skip(MaxRetainedFiles)
            .ToList();

        foreach (var old in obsolete)
        {
            File.Delete(old);
        }
    }

    public void Dispose()
    {
        // 書き込みごとにファイルハンドルを開閉するため、解放するリソースはない
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string category, FileLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _provider._minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(logLevel, _category, formatter(state, exception), exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
