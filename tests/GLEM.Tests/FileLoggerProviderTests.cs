using FluentAssertions;
using GLEM.Core.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GLEM.Tests;

public sealed class FileLoggerProviderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "glem-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Log_Information_WritesLineToFile()
    {
        using var provider = new FileLoggerProvider(_dir, LogLevel.Information);
        provider.CreateLogger("test.category").LogInformation("hello world");

        var file = Path.Combine(_dir, $"glem-{DateTime.Now:yyyyMMdd}.log");
        File.Exists(file).Should().BeTrue();
        File.ReadAllText(file).Should().Contain("[Information] test.category: hello world");
    }

    [Fact]
    public void Log_BelowMinimumLevel_IsNotWritten()
    {
        using var provider = new FileLoggerProvider(_dir, LogLevel.Warning);
        provider.CreateLogger("cat").LogDebug("debug message");

        Directory.EnumerateFiles(_dir, "glem-*.log").Should().BeEmpty();
    }

    [Fact]
    public void Log_WithException_IncludesStackTrace()
    {
        using var provider = new FileLoggerProvider(_dir);

        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            provider.CreateLogger("cat").LogError(ex, "failed operation");
        }

        var file = Path.Combine(_dir, $"glem-{DateTime.Now:yyyyMMdd}.log");
        File.ReadAllText(file).Should().Contain("System.InvalidOperationException: boom");
    }

    [Fact]
    public void PruneOldLogs_KeepsOnlyLatestSeven()
    {
        Directory.CreateDirectory(_dir);
        for (var i = 1; i <= 8; i++)
        {
            File.WriteAllText(Path.Combine(_dir, $"glem-2026010{i}.log"), "old");
        }

        using var provider = new FileLoggerProvider(_dir);

        Directory.EnumerateFiles(_dir, "glem-*.log").Should().HaveCount(7);
    }

    [Fact]
    public void BeginScope_And_Dispose_DoNotThrow()
    {
        using var provider = new FileLoggerProvider(_dir);
        var logger = provider.CreateLogger("cat");

        using (logger.BeginScope(new object()))
        {
            // scope 中のログ出力も正常に動作することを確認
            logger.LogInformation("inside scope");
        }

        Action act = () => provider.Dispose();
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
