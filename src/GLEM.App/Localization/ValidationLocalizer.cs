using GLEM.Core.Validation;

namespace GLEM.App.Localization;

/// <summary>
/// 検証結果（GLEM-1xxx）の UI 表示用ローカライズを行う静的ヘルパー。
/// Core のコード・メッセージ・シリアライズは変更せず、表示文字列のみを置き換える。
/// </summary>
public static class ValidationLocalizer
{
    /// <summary>
    /// Code と FieldName からリソースキーを解決し、ローカライズ済みメッセージを返す。
    /// 未定義のコード/フィールドは issue.Message にフォールバックする。
    /// </summary>
    public static string GetMessage(ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var key = (issue.Code, issue.FieldName) switch
        {
            ("GLEM-1001", "layers") => "ValidationIssue_GLEM1001_Layers",
            ("GLEM-1002", "thickness_m") => "ValidationIssue_GLEM1002_Thickness",
            ("GLEM-1003", "phi_deg") => "ValidationIssue_GLEM1003_FrictionAngle",
            ("GLEM-1004", "gamma_kn_m3") => "ValidationIssue_GLEM1004_UnitWeight",
            ("GLEM-1004", "c_kpa") => "ValidationIssue_GLEM1004_Cohesion",
            ("GLEM-1004", "ru_ratio") => "ValidationIssue_GLEM1004_RuRatio",
            ("GLEM-1004", "slice_width_m") => "ValidationIssue_GLEM1004_SliceWidth",
            ("GLEM-1004", "kh") => "ValidationIssue_GLEM1004_Kh",
            ("GLEM-1004", "kv") => "ValidationIssue_GLEM1004_Kv",
            ("GLEM-1004", "coarse_grid_step_m") => "ValidationIssue_GLEM1004_CoarseGrid",
            ("GLEM-1004", "local_step_m") => "ValidationIssue_GLEM1004_LocalStep",
            ("GLEM-1004", "surcharge_start_x/surcharge_end_x") => "ValidationIssue_GLEM1004_SurchargeRange",
            ("GLEM-1005", "water_table_depth_m") => "ValidationIssue_GLEM1005_WaterTable",
            ("GLEM-1006", "k_m_s/e0/cc") => "ValidationIssue_GLEM1006_SettlementProperties",
            ("GLEM-1007", "surcharge_kpa") => "ValidationIssue_GLEM1007_Surcharge",
            ("GLEM-1007", "load_kpa") => "ValidationIssue_GLEM1007_Load",
            ("GLEM-1008", "c_kpa/phi_deg") => "ValidationIssue_GLEM1008_ZeroStrength",
            _ => null
        };

        return key is null ? issue.Message : LocalizationService.GetString(key);
    }

    /// <summary>
    /// ローカライズ済みマーカー + [Code] + ローカライズ済みメッセージの形式で問題を整形する。
    /// </summary>
    public static string FormatIssue(ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var marker = LocalizationService.GetString(issue.IsWarning ? "Validation_WarningMarker" : "Validation_ErrorMarker");
        return $"{marker} [{issue.Code}] {GetMessage(issue)}";
    }

    /// <summary>
    /// 問題一覧をサマリー文字列に整形する。
    /// 問題がなければ合格メッセージ、それ以外ならエラー/警告件数と
    /// " | " で連結した各問題の詳細を含む。
    /// </summary>
    public static string FormatSummary(IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        if (issues.Count == 0)
        {
            return LocalizationService.GetString("Validation_NoIssues");
        }

        var errors = issues.Count(i => !i.IsWarning);
        var warnings = issues.Count - errors;
        var details = string.Join(" | ", issues.Select(FormatIssue));
        return LocalizationService.Format("Validation_SummaryFormat", errors, warnings, details);
    }
}
