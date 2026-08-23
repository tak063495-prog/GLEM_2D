using System.Diagnostics;
using System.Runtime.InteropServices;
using GLEM.Core.Engines;
using GLEM.Core.Models;
using Xunit;

namespace GLEM.Tests;

// T-12 性能テスト（詳細設計書 §8.2、機能仕様書 §7）
// 標準モデル: 層数≤15・全厚50m。探索<60s / 沈下(100年・50点)<30s。
// テスト計画書 §5.3: CI では閾値の2倍超過で失敗（探索>120s、沈下>60s）。
public sealed class PerformanceTests
{
    private const double SearchBudgetSeconds = 60.0;
    private const double SettlementBudgetSeconds = 30.0;

    [Fact]
    public void StandardModel_WithinTimeBudget()
    {
        var gm = BuildStandardModel();

        // Warmup (JIT) so the measured runs are representative
        new SlipSurfaceSearcher().Search(
            gm,
            new SlopeAnalysisInput { SearchRange = new SearchRange(-5, 5, -10, -2, 4, 8), CoarseGridStepM = 2.0 });
        new SettlementEngine().Compute(gm, BuildSettlementInput());

        var searchSw = Stopwatch.StartNew();
        var slopeResult = new SlipSurfaceSearcher()
            .Search(gm, new SlopeAnalysisInput { Method = SlopeMethod.BishopSimplified });
        searchSw.Stop();

        var settlementSw = Stopwatch.StartNew();
        var settlementResult = new SettlementEngine().Compute(gm, BuildSettlementInput());
        settlementSw.Stop();

        // Hard limits: fail only beyond 2x the budget (test plan §5.3)
        Assert.True(
            searchSw.Elapsed.TotalSeconds < SearchBudgetSeconds * 2.0,
            $"Slope search took {searchSw.Elapsed.TotalSeconds:F1}s (limit {SearchBudgetSeconds * 2.0:F0}s)");
        Assert.True(
            settlementSw.Elapsed.TotalSeconds < SettlementBudgetSeconds * 2.0,
            $"Settlement analysis took {settlementSw.Elapsed.TotalSeconds:F1}s (limit {SettlementBudgetSeconds * 2.0:F0}s)");

        // Sanity: the runs actually produced results
        Assert.True(slopeResult.MinFs > 0.0);
        Assert.NotEmpty(settlementResult.TimeSeries);

        RecordMeasurement(
            searchSw.Elapsed.TotalSeconds,
            settlementSw.Elapsed.TotalSeconds,
            slopeResult.MinFs,
            settlementResult.TotalMm);
    }

    // §7 standard model: <=15 layers, slope height (total thickness) <= 50 m
    private static GroundModel BuildStandardModel()
    {
        var gm = new GroundModel { WaterTableDepthM = 5.0 };
        for (var i = 0; i < 15; i++)
        {
            var isClay = i % 2 == 1;
            gm.Layers.Add(new SoilLayer
            {
                Name = $"L{i + 1}",
                ThicknessM = 50.0 / 15.0,
                GammaKnm3 = isClay ? 17.0 : 19.0,
                CohesionKpa = isClay ? 20.0 : 0.0,
                FrictionAngleDeg = isClay ? 20.0 : 34.0,
                PermeabilityMs = isClay ? 1e-8 : 1e-6,
                InitialVoidRatio = isClay ? 1.5 : 0.7,
                CompressionIndexCc = isClay ? 0.3 : 0.2,
                SecondaryCompressionIndexCs = 0.02,
                ElasticModulusKpa = isClay ? 15000.0 : 40000.0
            });
        }

        return gm;
    }

    private static SettlementAnalysisInput BuildSettlementInput() => new()
    {
        LoadKpa = 100.0,
        LoadedAreaB = 6.0,
        LoadedAreaL = 6.0,
        DurationYears = 100.0,
        OutputPointCount = 50
    };

    // Time-series record with environment info (test plan §5.3) → docs/perf/perf-log.jsonl
    private static void RecordMeasurement(double searchSeconds, double settlementSeconds, double fs, double totalMm)
    {
        var entry = new PerfEntry(
            DateTime.Now.ToString("o"),
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            Environment.ProcessorCount,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0),
            RuntimeInformation.FrameworkDescription,
            searchSeconds,
            settlementSeconds,
            fs,
            totalMm);

        try
        {
            var dir = Path.Combine(FindSolutionRoot(), "docs", "perf");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "perf-log.jsonl"),
                System.Text.Json.JsonSerializer.Serialize(entry) + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Recording must never fail the performance test itself
        }
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GLEM.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("GLEM.sln not found above the test output directory");
    }

    private sealed record PerfEntry(
        string TimestampUtc,
        string MachineName,
        string OsDescription,
        int ProcessorCount,
        double TotalRamGb,
        string Framework,
        double SearchSeconds,
        double SettlementSeconds,
        double SlopeFs,
        double SettlementTotalMm);
}
