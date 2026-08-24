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
        // Explicit language keeps this test independent of the host UI culture (e.g. ja-JP).
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var content = new ReportContent
        {
            Language = ReportLanguage.English,
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

        // 結果（斜面安定・沈下）— 手法名は英語の表示名（ReportText.English）で出力される
        html.Should().Contain("1.327");
        html.Should().Contain("Bishop (simplified)");
        html.Should().Contain("R = 12.4 m");
        html.Should().Contain("314.5 mm");
        html.Should().Contain("T90 = 480 days");

        // 図面（base64埋め込み）
        html.Should().Contain("data:image/png;base64,");
    }

    // P0 localization regression: explicit ReportLanguage must drive the report text, and numbers/dates
    // must stay dot-decimal / fixed-format even when CurrentCulture is a non-English comma-decimal culture.

    private static ReportContent SampleReportContent(ReportLanguage language, SlopeMethod slopeMethod = SlopeMethod.BishopSimplified) => new()
    {
        Language = language,
        AppVersion = "1.0.0",
        GeneratedAt = new DateTime(2026, 8, 22, 12, 0, 0),
        Project = new ProjectData
        {
            ProjectName = "A&B <Test>",
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
        SlopeResult = SampleSlopeResult() with { Method = slopeMethod },
        SettlementResult = SampleSettlementResult(),
        Figures = { new ReportFigure("Cross section", new byte[] { 0x89, 0x50, 0x4E, 0x47 }) }
    };

    [Fact]
    public void Report_ExplicitEnglish_UnderCommaDecimalCulture_HasEnglishTextAndInvariantNumbers()
    {
        // de-DE: non-English host culture with comma decimals — the report must not follow it.
        using var _ = new CultureScope(new CultureInfo("de-DE"));

        var html = ReportGenerator.Generate(SampleReportContent(ReportLanguage.English));

        // ドキュメント言語・メタデータ
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("<h1>GLEM Analysis Report</h1>");
        html.Should().Contain("Version: 1.0.0");
        html.Should().Contain("Generated at: 2026-08-22 12:00:00"); // fixed invariant date

        // 主要見出し（英語）
        html.Should().Contain("<h2>1. Input Summary</h2>");
        html.Should().Contain("<h2>2. Slope Stability Results</h2>");
        html.Should().Contain("<h2>3. Settlement Results</h2>");

        // 手法・排水条件・yes/no・日単位の表現（英語）
        html.Should().Contain("Bishop (simplified)");
        html.Should().Contain("Drainage: Single");
        html.Should().Contain("Converged: yes");
        html.Should().Contain("(12 iterations)");
        html.Should().Contain("T90 = 480 days");

        // 数値はドット小数のまま（de-DE のカンマ小数にならない）
        html.Should().Contain("Minimum safety factor FS = <b>1.327</b>");
        html.Should().Contain("R = 12.4 m, center (3.2, -8.7)");
        html.Should().Contain("<b>314.5 mm</b>");

        // 逆言語のコア見出しが混入しないこと
        html.Should().NotContain("入力概要");
        html.Should().NotContain("斜面安定解析結果");
        html.Should().NotContain("沈下解析結果");

        // HTML エンコードと図面（base64）は引き続き機能すること
        html.Should().Contain("A&amp;B &lt;Test&gt;");
        html.Should().NotContain("<Test>");
        html.Should().Contain("data:image/png;base64,iVBORw=="); // base64 of { 0x89, 0x50, 0x4E, 0x47 }
    }

    [Fact]
    public void Report_ExplicitJapanese_UnderJapaneseHostCulture_HasJapaneseTextAndInvariantNumbers()
    {
        using var _ = new CultureScope(new CultureInfo("ja-JP"));

        var html = ReportGenerator.Generate(SampleReportContent(ReportLanguage.Japanese));

        // ドキュメント言語・メタデータ
        html.Should().Contain("<html lang=\"ja\">");
        html.Should().Contain("<h1>GLEM 解析レポート</h1>");
        html.Should().Contain("バージョン：");
        html.Should().Contain("生成日時：");
        html.Should().Contain("2026-08-22 12:00:00"); // fixed invariant date

        // 主要見出し（日本語）
        html.Should().Contain("<h2>1. 入力概要</h2>");
        html.Should().Contain("<h2>2. 斜面安定解析結果</h2>");
        html.Should().Contain("<h2>3. 沈下解析結果</h2>");

        // 手法・排水条件・yes/no・日単位の表現（日本語）
        html.Should().Contain("ビショップ簡易法");
        html.Should().Contain("単一排水");
        html.Should().Contain("収束： はい"); // label と値の間にスペースが入る
        html.Should().Contain("（12 反復）");
        html.Should().Contain("T90 = 480 日");

        // 数値はドット小数のまま
        html.Should().Contain("最小安全率 FS = <b>1.327</b>");
        html.Should().Contain("R = 12.4 m、中心（3.2, -8.7）");
        html.Should().Contain("<b>314.5 mm</b>");

        // 逆言語のコア見出しが混入しないこと
        html.Should().NotContain("Input Summary");
        html.Should().NotContain("Slope Stability Results");
        html.Should().NotContain("Settlement Results");

        // HTML エンコードと図面（base64）は引き続き機能すること
        html.Should().Contain("A&amp;B &lt;Test&gt;");
        html.Should().NotContain("<Test>");
        html.Should().Contain("data:image/png;base64,iVBORw=="); // base64 of { 0x89, 0x50, 0x4E, 0x47 }
    }

    [Theory]
    [InlineData(ReportLanguage.English, "Caution: the generalized Janbu λc correction is a GLEM-specific approximation")]
    [InlineData(ReportLanguage.Japanese, "注意：一般化ヤンブ法の λc 補正は、公開された補正概念を参考にした GLEM 固有の近似です。")]
    public void Report_JanbuResult_AlwaysIncludesLocalizedApproximationWarning(ReportLanguage language, string expected)
    {
        var html = ReportGenerator.Generate(SampleReportContent(language, SlopeMethod.JanbuGeneralized));

        html.Should().Contain("class=\"warning\"");
        html.Should().Contain(expected);
    }

    // P0 invariant-format regression: CSV wire format must not follow a non-English comma-decimal culture.

    [Fact]
    public void T10_SlopeCsv_UnderCommaDecimalCulture_KeepsAsciiHeaderAndDotDecimals()
    {
        using var _ = new CultureScope(new CultureInfo("de-DE"));

        var path = Path.Combine(_dir, "slope-de.csv");
        CsvExporter.ExportSlope(path, SampleSlopeResult());

        var lines = File.ReadAllLines(path);

        // 安定した ASCII ヘッダ（列構成は固定）
        lines[0].Should().Be("slice_no,x_m,z_m,W_kN_per_m,alpha_deg,u_kPa,Np_kN_per_m,c_term_kN_per_m,phi_term_kN_per_m");
        lines.Should().HaveCount(3);

        // ドット小数・カンマ区切りのまま（de-DE の "96,5" にならない）
        lines[1].Should().Be("1,-5,2,96.5,12.5,3.2,88.1,12.3,40.2");
        lines[2].Should().Be("2,-2,3.5,120.75,8,5.6,110.2,15.1,52.8");

        // ラウンドトリップ: インバリアントカルチャで解析して元の値と一致すること
        var cells = lines[1].Split(',');
        double.Parse(cells[3], CultureInfo.InvariantCulture).Should().Be(96.5);
        double.Parse(cells[7], CultureInfo.InvariantCulture).Should().Be(12.3);
    }

    [Fact]
    public void T10_SettlementCsv_UnderCommaDecimalCulture_KeepsAsciiHeaderAndDotDecimals()
    {
        using var _ = new CultureScope(new CultureInfo("de-DE"));

        var path = Path.Combine(_dir, "settlement-de.csv");
        CsvExporter.ExportSettlement(path, SampleSettlementResult());

        var lines = File.ReadAllLines(path);

        lines[0].Should().Be("time_days,U_percent,settlement_mm");
        lines.Should().HaveCount(3);
        lines[1].Should().Be("1,2.5,27");
        lines[2].Should().Be("365.25,98,304");

        // ラウンドトリップ: インバリアントカルチャで解析して元の値と一致すること
        var cells = lines[2].Split(',');
        double.Parse(cells[0], CultureInfo.InvariantCulture).Should().Be(365.25);
        double.Parse(cells[1], CultureInfo.InvariantCulture).Should().Be(98.0);
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
