using System.Globalization;
using System.IO;
using FluentAssertions;
using GLEM.Core.Engines;
using GLEM.Core.IO;
using GLEM.Core.Models;
using Xunit;

namespace GLEM.Tests;

public sealed class IoTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"glem-io-{Guid.NewGuid():N}");

    public IoTests() => Directory.CreateDirectory(_dir);

    private static SlopeAnalysisResult SampleSlopeResult() => new(
        MinFs: 1.327,
        Method: SlopeMethod.BishopSimplified,
        CriticalSurface: new CircleSurface(3.2, -8.7, 12.4),
        Slices: new[]
        {
            new SliceResult(1, -5.0, 2.0, 96.5, 12.5, 3.2, 88.1, 12.3, 40.2),
            new SliceResult(2, -2.0, 3.5, 120.75, 8.0, 5.6, 110.2, 15.1, 52.8)
        },
        Converged: true,
        Iterations: 12);

    private static SettlementAnalysisResult SampleSettlementResult() => new(
        TotalMm: 314.5,
        ImmediateMm: 20.0,
        PrimaryMm: 280.0,
        SecondaryMm: 14.5,
        TimeSeries: new[]
        {
            new SettlementTimePoint(1.0, 2.5, 27.0, 20.0, 7.0, 0.0),
            new SettlementTimePoint(365.25, 98.0, 304.0, 20.0, 274.0, 10.0)
        },
        T50Days: 120.0,
        T90Days: 480.0);

    [Fact]
    public void T10_SlopeCsv_MatchesSpecColumns()
    {
        var path = Path.Combine(_dir, "slope.csv");
        CsvExporter.ExportSlope(path, SampleSlopeResult());

        var lines = File.ReadAllLines(path);

        // 機能仕様書 §6.3: ヘッダ行を含む、列構成は固定
        lines[0].Should().Be("slice_no,x_m,z_m,W_kN_per_m,alpha_deg,u_kPa,Np_kN_per_m,c_term_kN_per_m,phi_term_kN_per_m");
        lines.Should().HaveCount(3);

        var cells = lines[1].Split(',');
        cells.Should().HaveCount(9);
        cells[0].Should().Be("1");
        cells[1].Should().Be("-5");
        cells[2].Should().Be("2");
        cells[3].Should().Be("96.5");
        cells[4].Should().Be("12.5");
        cells[5].Should().Be("3.2");
        cells[6].Should().Be("88.1");
        cells[7].Should().Be("12.3");
        cells[8].Should().Be("40.2");

        // UTF-8（BOMなし）で書き出されること
        var bytes = File.ReadAllBytes(path);
        (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF).Should().BeFalse();
    }

    [Fact]
    public void T10_SettlementCsv_MatchesSpecColumns()
    {
        var path = Path.Combine(_dir, "settlement.csv");
        CsvExporter.ExportSettlement(path, SampleSettlementResult());

        var lines = File.ReadAllLines(path);

        lines[0].Should().Be("time_days,U_percent,settlement_mm");
        lines.Should().HaveCount(3);
        lines[1].Split(',').Should().Equal("1", "2.5", "27");
    }

    [Fact]
    public void Report_ContainsVersionTimestampInputsResultsAndFigure()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var content = new ReportContent
        {
            AppVersion = "1.0.0",
            GeneratedAt = new DateTime(2026, 8, 22, 12, 0, 0),
            Project = new ProjectData
            {
                ProjectName = "TestProject",
                CreatedAt = new DateTime(2026, 8, 1, 9, 30, 0),
                GroundModel = new GroundModel
                {
                    WaterTableDepthM = 5.0,
                    Layers =
                    {
                        new SoilLayer
                        {
                            Name = "Sand",
                            ThicknessM = 3.0,
                            GammaKnm3 = 18.0,
                            CohesionKpa = 0.0,
                            FrictionAngleDeg = 32.0,
                            PermeabilityMs = 1e-4,
                            InitialVoidRatio = 0.75,
                            CompressionIndexCc = 0.25
                        }
                    }
                },
                SlopeAnalysis = new SlopeAnalysisInput { Method = SlopeMethod.BishopSimplified, SliceWidthM = 1.0 },
                SettlementAnalysis = new SettlementAnalysisInput { LoadKpa = 100.0 }
            },
            SlopeResult = SampleSlopeResult(),
            SettlementResult = SampleSettlementResult(),
            Figures = { new ReportFigure("Cross section", png) }
        };

        var html = ReportGenerator.Generate(content);

        // ヘッダ: バージョン情報と生成日時（§6.4）
        html.Should().Contain("Version: 1.0.0");
        html.Should().Contain("2026-08-22 12:00:00");

        // 入力概要
        html.Should().Contain("TestProject");
        html.Should().Contain("Sand");
        html.Should().Contain("Water table depth");

        // 結果（斜面安定・沈下）
        html.Should().Contain("1.327");
        html.Should().Contain("BishopSimplified");
        html.Should().Contain("R = 12.4 m");
        html.Should().Contain("314.5 mm");
        html.Should().Contain("T90 = 480 days");

        // 図面（base64埋め込み）
        html.Should().Contain("data:image/png;base64,");
    }

    [Fact]
    public void SettlementTimeSeries_BreakdownSumsToTotal()
    {
        var gm = new GroundModel
        {
            WaterTableDepthM = 20.0,
            Layers =
            {
                new SoilLayer
                {
                    Name = "Clay",
                    ThicknessM = 4.0,
                    GammaKnm3 = 18.0,
                    PermeabilityMs = 1e-8,
                    InitialVoidRatio = 1.0,
                    CompressionIndexCc = 0.3,
                    SecondaryCompressionIndexCs = 0.02,
                    ElasticModulusKpa = 5000.0
                }
            }
        };

        var input = new SettlementAnalysisInput { LoadKpa = 50.0, DurationYears = 10.0, OutputPointCount = 10 };

        var result = new SettlementEngine().Compute(gm, input);

        foreach (var p in result.TimeSeries)
        {
            (p.ImmediateMm + p.PrimaryMm + p.SecondaryMm).Should().BeApproximately(p.SettlementMm, 1e-9);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
