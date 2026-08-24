using GLEM.Core.Models;

namespace GLEM.Core.IO;

/// <summary>
/// 解析レポート本文の表示言語。
/// </summary>
public enum ReportLanguage
{
    English,
    Japanese
}

/// <summary>
/// レポート本文に使用する多言語テキストカタログ（内部ヘルパー）。
/// 各言語ごとに1つの静的インスタンスを保持し、ラベル・書式テンプレート・列挙型の表示名を提供する。
/// 数値・日付の書式化や HTML エンコードは呼び出し側（<see cref="ReportGenerator"/>）で行うため、
/// ここではテキストのみを扱う。
/// </summary>
internal sealed class ReportText
{
    public static ReportText English { get; } = new(ja: false);

    public static ReportText Japanese { get; } = new(ja: true);

    private readonly bool _ja;

    private ReportText(bool ja)
    {
        _ja = ja;

        // ドキュメントレベル
        HtmlLang = ja ? "ja" : "en";
        TitleFormat = ja ? "GLEM レポート - {0}" : "GLEM Report - {0}";
        Untitled = ja ? "無題" : "Untitled";
        H1Title = ja ? "GLEM 解析レポート" : "GLEM Analysis Report";

        // ヘッダ（バージョン情報・生成日時）
        VersionLabel = ja ? "バージョン：" : "Version:";
        GeneratedAtLabel = ja ? "生成日時：" : "Generated at:";
        ProjectLabel = ja ? "プロジェクト：" : "Project:";

        // 入力概要
        InputSummaryHeading = ja ? "1. 入力概要" : "1. Input Summary";
        CreatedLabel = ja ? "作成日：" : "Created:";
        WaterTableDepthLabel = ja ? "地下水位深さ：" : "Water table depth:";
        ThLayer = ja ? "層" : "Layer";
        ThThickness = ja ? "厚さ [m]" : "Thickness [m]";

        // 斜面安定解析の設定
        SlopeSettingsHeading = ja ? "斜面安定解析の設定" : "Slope stability settings";
        MethodLabel = ja ? "方法：" : "Method:";
        SliceWidthLabel = ja ? "スライス幅：" : "Slice width:";
        SurchargeLabel = ja ? "載荷 q：" : "Surcharge q:";
        SurchargeRangeFormat = ja ? "(x = {0} 〜 {1} m)" : "(x = {0} to {1} m)";
        KhLabel = ja ? "kh：" : "kh:";
        KvLabel = ja ? "kv：" : "kv:";
        SearchRangeLabel = ja ? "探索範囲：" : "Search range:";

        // 沈下解析の設定
        SettlementSettingsHeading = ja ? "沈下解析の設定" : "Settlement settings";
        LoadLabel = ja ? "荷重：" : "Load:";
        LoadedAreaLabel = ja ? "載荷面積 B&#215;L：" : "Loaded area B&#215;L:";
        DrainageLabel = ja ? "排水条件：" : "Drainage:";
        DurationLabel = ja ? "期間：" : "Duration:";
        YearsUnit = ja ? "年" : "years";
        OutputPointsLabel = ja ? "出力点数：" : "Output points:";

        // 斜面安定結果
        SlopeResultsHeading = ja ? "2. 斜面安定解析結果" : "2. Slope Stability Results";
        MinFsPhrase = ja ? "最小安全率 FS = " : "Minimum safety factor FS = ";
        ConvergedLabel = ja ? "収束：" : "Converged:";
        YesWord = ja ? "はい" : "yes";
        NoWord = ja ? "いいえ" : "no";
        IterationsFormat = ja ? "（{0} 反復）" : "({0} iterations)";
        CircleSurfaceFormat = ja
            ? "臨界滑動面（円）：R = {0} m、中心（{1}, {2}）"
            : "Critical surface (circle): R = {0} m, center ({1}, {2})";
        FunctionSurfaceFormat = ja
            ? "臨界滑動面（関数）：制御点 {0} 個"
            : "Critical surface (function): {0} control points";

        ThSliceNo = ja ? "スライス番号" : "slice_no";
        ThCTerm = ja ? "c 項 [kN/m]" : "c term [kN/m]";
        ThPhiTerm = ja ? "&phi; 項 [kN/m]" : "&phi; term [kN/m]";

        // 沈下解析結果
        SettlementResultsHeading = ja ? "3. 沈下解析結果" : "3. Settlement Results";
        TotalSettlementLabel = ja ? "総沈下量：" : "Total settlement:";
        ImmediateLabel = ja ? "即時沈下：" : "Immediate:";
        PrimaryConsolidationLabel = ja ? "一次圧密：" : "Primary consolidation:";
        SecondaryCompressionLabel = ja ? "二次圧縮：" : "Secondary compression:";
        DaysUnit = ja ? "日" : "days";
        ThTime = ja ? "時間 [日]" : "time [day]";
    }

    public string HtmlLang { get; }

    public string TitleFormat { get; }

    public string Untitled { get; }

    public string H1Title { get; }

    public string VersionLabel { get; }

    public string GeneratedAtLabel { get; }

    public string ProjectLabel { get; }

    public string InputSummaryHeading { get; }

    public string CreatedLabel { get; }

    public string WaterTableDepthLabel { get; }

    public string ThLayer { get; }

    public string ThThickness { get; }

    public string SlopeSettingsHeading { get; }

    public string MethodLabel { get; }

    public string SliceWidthLabel { get; }

    public string SurchargeLabel { get; }

    public string SurchargeRangeFormat { get; }

    public string KhLabel { get; }

    public string KvLabel { get; }

    public string SearchRangeLabel { get; }

    public string SettlementSettingsHeading { get; }

    public string LoadLabel { get; }

    public string LoadedAreaLabel { get; }

    public string DrainageLabel { get; }

    public string DurationLabel { get; }

    public string YearsUnit { get; }

    public string OutputPointsLabel { get; }

    public string SlopeResultsHeading { get; }

    public string MinFsPhrase { get; }

    public string ConvergedLabel { get; }

    public string YesWord { get; }

    public string NoWord { get; }

    public string IterationsFormat { get; }

    public string CircleSurfaceFormat { get; }

    public string FunctionSurfaceFormat { get; }

    public string ThSliceNo { get; }

    public string ThCTerm { get; }

    public string ThPhiTerm { get; }

    public string SettlementResultsHeading { get; }

    public string TotalSettlementLabel { get; }

    public string ImmediateLabel { get; }

    public string PrimaryConsolidationLabel { get; }

    public string SecondaryCompressionLabel { get; }

    public string DaysUnit { get; }

    public string ThTime { get; }

    /// <summary>
    /// <see cref="SlopeMethod"/> を自然な表示名に変換する。未知の値は <c>ToString()</c> にフォールバックする。
    /// </summary>
    public string SlopeMethodDisplay(SlopeMethod method) => _ja ? JapaneseSlopeMethod(method) : EnglishSlopeMethod(method);

    /// <summary>
    /// <see cref="Drainage"/> を自然な表示名に変換する。未知の値は <c>ToString()</c> にフォールバックする。
    /// </summary>
    public string DrainageDisplay(Drainage drainage) => _ja ? JapaneseDrainage(drainage) : EnglishDrainage(drainage);

    private static string EnglishSlopeMethod(SlopeMethod m) => m switch
    {
        SlopeMethod.Fellenius => "Fellenius",
        SlopeMethod.BishopSimplified => "Bishop (simplified)",
        SlopeMethod.JanbuGeneralized => "Janbu (generalized)",
        _ => m.ToString()
    };

    private static string JapaneseSlopeMethod(SlopeMethod m) => m switch
    {
        SlopeMethod.Fellenius => "フェレンイウス法",
        SlopeMethod.BishopSimplified => "ビショップ簡易法",
        SlopeMethod.JanbuGeneralized => "ヤンブ一般化法",
        _ => m.ToString()
    };

    private static string EnglishDrainage(Drainage d) => d switch
    {
        Drainage.Single => "Single",
        Drainage.Double => "Double",
        _ => d.ToString()
    };

    private static string JapaneseDrainage(Drainage d) => d switch
    {
        Drainage.Single => "単一排水",
        Drainage.Double => "二重排水",
        _ => d.ToString()
    };
}
