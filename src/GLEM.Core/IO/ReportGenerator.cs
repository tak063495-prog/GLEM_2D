using System.Globalization;
using System.IO;
using System.Text;
using GLEM.Core.Models;

namespace GLEM.Core.IO;

public sealed record ReportFigure(string Title, byte[]? PngBytes);

// 機能仕様書 §6.4: 入力概要・解析結果・図面を1つのレポート（HTML）として出力。
// ヘッダにバージョン情報と生成日時を含む。
public static class ReportGenerator
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Generate(ReportContent content)
    {
        var t = content.Language == ReportLanguage.Japanese ? ReportText.Japanese : ReportText.English;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.Append("<html lang=\"").Append(t.HtmlLang).AppendLine("\"><head><meta charset=\"utf-8\">");
        var projectName = content.Project?.ProjectName ?? t.Untitled;
        sb.Append("<title>").Append(string.Format(Invariant, t.TitleFormat, Esc(projectName))).AppendLine("</title>");
        sb.AppendLine("""
            <style>
            body{font-family:'Segoe UI',sans-serif;margin:24px;color:#222}
            h1{border-bottom:2px solid #345;font-size:20px}
            h2{font-size:16px;margin-top:28px;border-left:4px solid #345;padding-left:8px}
            table{border-collapse:collapse;margin:8px 0}
            th,td{border:1px solid #9ab;padding:4px 10px;text-align:right;font-size:13px}
            th{background:#eef;text-align:center}
            td:first-child,th:first-child{text-align:left}
            .meta{color:#567;font-size:12px}
            img{max-width:860px;border:1px solid #ccd;margin:8px 0}
            </style>
            """);
        sb.AppendLine("</head><body>");

        // ヘッダ（バージョン情報・生成日時）
        sb.Append("<h1>").Append(t.H1Title).AppendLine("</h1>\n<p class=\"meta\">").Append(t.VersionLabel).Append(" ").Append(Esc(content.AppVersion));
        sb.Append(" &nbsp;|&nbsp; ").Append(t.GeneratedAtLabel).Append(" ").Append(content.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss", Invariant));
        if (content.Project is { } p)
        {
            sb.Append(" &nbsp;|&nbsp; ").Append(t.ProjectLabel).Append(" ").Append(Esc(p.ProjectName));
        }

        sb.AppendLine("</p>");

        // 入力概要
        if (content.Project is { } proj)
        {
            AppendInputSummary(sb, proj, t);
        }

        // 斜面安定結果
        if (content.SlopeResult is { } slope)
        {
            AppendSlopeResults(sb, slope, t);
        }

        // 沈下解析結果
        if (content.SettlementResult is { } settlement)
        {
            AppendSettlementResults(sb, settlement, t);
        }

        // 図面
        foreach (var fig in content.Figures)
        {
            sb.Append("<h2>").Append(Esc(fig.Title)).AppendLine("</h2>");
            if (fig.PngBytes is { Length: > 0 } png)
            {
                sb.Append("<img alt=\"").Append(Esc(fig.Title)).Append("\" src=\"data:image/png;base64,")
                  .Append(Convert.ToBase64String(png))
                  .AppendLine("\">");
            }
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public static void Save(string path, ReportContent content) =>
        File.WriteAllText(path, Generate(content), new UTF8Encoding(false));

    private static void AppendInputSummary(StringBuilder sb, ProjectData p, ReportText t)
    {
        sb.Append("<h2>").Append(t.InputSummaryHeading).AppendLine("</h2>");
        if (p.CreatedAt is { } created)
        {
            sb.Append("<p class=\"meta\">").Append(t.CreatedLabel).Append(" ").Append(created.ToString("yyyy-MM-dd HH:mm:ss", Invariant)).AppendLine("</p>");
        }

        var gm = p.GroundModel;
        sb.Append("<p>").Append(t.WaterTableDepthLabel).Append(" <b>").Append(Fmt(gm.WaterTableDepthM)).Append(" m</b></p>\n");
        sb.AppendLine("<table><tr><th>" + t.ThLayer + "</th><th>" + t.ThThickness + "</th><th>&gamma; [kN/m&sup3;]</th><th>c&#8242; [kPa]</th><th>&phi;&#8242; [&deg;]</th><th>k [m/s]</th><th>e0</th><th>Cc</th><th>Cr</th><th>Cs</th></tr>");
        foreach (var l in gm.Layers)
        {
            sb.Append("<tr><td>").Append(Esc(l.Name))
              .Append("</td><td>").Append(Fmt(l.ThicknessM))
              .Append("</td><td>").Append(Fmt(l.GammaKnm3))
              .Append("</td><td>").Append(Fmt(l.CohesionKpa))
              .Append("</td><td>").Append(Fmt(l.FrictionAngleDeg))
              .Append("</td><td>").Append(Num(l.PermeabilityMs))
              .Append("</td><td>").Append(Num(l.InitialVoidRatio))
              .Append("</td><td>").Append(Num(l.CompressionIndexCc))
              .Append("</td><td>").Append(Num(l.RecompressionIndexCr))
              .Append("</td><td>").Append(Fmt(l.EffectiveCs))
              .AppendLine("</td></tr>");
        }

        sb.AppendLine("</table>");

        if (p.SlopeAnalysis is { } sa)
        {
            sb.Append("<h3>").Append(t.SlopeSettingsHeading).Append("</h3>\n<p>").Append(t.MethodLabel).Append(" <b>").Append(Esc(t.SlopeMethodDisplay(sa.Method)))
              .Append("</b> &nbsp;|&nbsp; ").Append(t.SliceWidthLabel).Append(" ").Append(Fmt(sa.SliceWidthM)).Append(" m")
              .Append(" &nbsp;|&nbsp; ").Append(t.SurchargeLabel).Append(" ").Append(Fmt(sa.SurchargeKpa)).Append(" kPa");
            if (sa.SurchargeStartX is { } xs && sa.SurchargeEndX is { } xe)
            {
                sb.Append(" ").Append(string.Format(Invariant, t.SurchargeRangeFormat, Fmt(xs), Fmt(xe)));
            }

            sb.Append(" &nbsp;|&nbsp; ").Append(t.KhLabel).Append(" ").Append(Fmt(sa.Kh))
              .Append(" &nbsp;|&nbsp; ").Append(t.KvLabel).Append(" ").Append(Fmt(sa.Kv));
            if (sa.SearchRange is { } sr)
            {
                sb.Append("<br>").Append(t.SearchRangeLabel).Append(" cx [").Append(Fmt(sr.CenterXMin)).Append(", ").Append(Fmt(sr.CenterXMax))
                  .Append("], cz [").Append(Fmt(sr.CenterZMin)).Append(", ").Append(Fmt(sr.CenterZMax))
                  .Append("], R [").Append(Fmt(sr.RadiusMin)).Append(", ").Append(Fmt(sr.RadiusMax)).Append("]");
            }

            sb.AppendLine("</p>");
        }

        if (p.SettlementAnalysis is { } st)
        {
            sb.Append("<h3>").Append(t.SettlementSettingsHeading).Append("</h3>\n<p>").Append(t.LoadLabel).Append(" <b>").Append(Fmt(st.LoadKpa)).Append(" kPa")
              .Append("</b> &nbsp;|&nbsp; ").Append(t.LoadedAreaLabel).Append(" ").Append(Fmt(st.LoadedAreaB)).Append(" &#215; ").Append(Fmt(st.LoadedAreaL)).Append(" m")
              .Append(" &nbsp;|&nbsp; ").Append(t.DrainageLabel).Append(" ").Append(Esc(t.DrainageDisplay(st.DrainageMode)))
              .Append(" &nbsp;|&nbsp; ").Append(t.DurationLabel).Append(" ").Append(Fmt(st.DurationYears)).Append(" ").Append(t.YearsUnit)
              .Append(" &nbsp;|&nbsp; ").Append(t.OutputPointsLabel).Append(" ").Append(st.OutputPointCount)
              .AppendLine("</p>");
        }
    }

    private static void AppendSlopeResults(StringBuilder sb, SlopeAnalysisResult r, ReportText t)
    {
        sb.Append("<h2>").Append(t.SlopeResultsHeading).Append("</h2>\n<p>").Append(t.MinFsPhrase).Append("<b>")
          .Append(Fmt(r.MinFs))
          .Append("</b> &nbsp;|&nbsp; ").Append(t.MethodLabel).Append(" ").Append(Esc(t.SlopeMethodDisplay(r.Method)))
          .Append(" &nbsp;|&nbsp; ").Append(t.ConvergedLabel).Append(" ").Append(r.Converged ? t.YesWord : t.NoWord)
          .Append(" ").Append(string.Format(Invariant, t.IterationsFormat, r.Iterations)).Append("</p>\n");

        switch (r.CriticalSurface)
        {
            case CircleSurface c:
                sb.Append("<p>").Append(string.Format(Invariant, t.CircleSurfaceFormat, Fmt(c.Radius), Fmt(c.CenterX), Fmt(c.CenterZ))).AppendLine("</p>");
                break;
            case FunctionSurface f:
                sb.Append("<p>").Append(string.Format(Invariant, t.FunctionSurfaceFormat, f.ControlPoints.Count)).AppendLine("</p>");
                break;
        }

        sb.AppendLine("<table><tr><th>" + t.ThSliceNo + "</th><th>x [m]</th><th>z [m]</th><th>W [kN/m]</th><th>&alpha; [&deg;]</th><th>u [kPa]</th><th>N&#8242;p [kN/m]</th><th>" + t.ThCTerm + "</th><th>" + t.ThPhiTerm + "</th></tr>");
        foreach (var s in r.Slices)
        {
            sb.Append("<tr><td>").Append(s.SliceNo)
              .Append("</td><td>").Append(Fmt(s.X))
              .Append("</td><td>").Append(Fmt(s.Z))
              .Append("</td><td>").Append(Fmt(s.WKnPerM))
              .Append("</td><td>").Append(Fmt(s.AlphaDeg))
              .Append("</td><td>").Append(Fmt(s.UKpa))
              .Append("</td><td>").Append(Fmt(s.NpKnPerM))
              .Append("</td><td>").Append(Fmt(s.CTermKnPerM))
              .Append("</td><td>").Append(Fmt(s.PhiTermKnPerM))
              .AppendLine("</td></tr>");
        }

        sb.AppendLine("</table>");
    }

    private static void AppendSettlementResults(StringBuilder sb, SettlementAnalysisResult r, ReportText t)
    {
        sb.Append("<h2>").Append(t.SettlementResultsHeading).Append("</h2>\n<p>").Append(t.TotalSettlementLabel).Append(" <b>")
          .Append(Fmt(r.TotalMm)).Append(" mm")
          .Append("</b> &nbsp;|&nbsp; ").Append(t.ImmediateLabel).Append(" ")
          .Append(Fmt(r.ImmediateMm))
          .Append(" mm &nbsp;|&nbsp; ").Append(t.PrimaryConsolidationLabel).Append(" ")
          .Append(Fmt(r.PrimaryMm))
          .Append(" mm &nbsp;|&nbsp; ").Append(t.SecondaryCompressionLabel).Append(" ")
          .Append(Fmt(r.SecondaryMm)).Append(" mm</p>\n");

        sb.Append("<p>T50 = ").Append(Num(r.T50Days)).Append(" ").Append(t.DaysUnit)
          .Append(" &nbsp;|&nbsp; T90 = ").Append(Num(r.T90Days)).Append(" ").AppendLine(t.DaysUnit + "</p>");

        sb.AppendLine("<table><tr><th>" + t.ThTime + "</th><th>U [%]</th><th>S [mm]</th></tr>");
        foreach (var p in r.TimeSeries)
        {
            sb.Append("<tr><td>").Append(Fmt(p.TimeDays))
              .Append("</td><td>").Append(Fmt(p.UPercent))
              .Append("</td><td>").Append(Fmt(p.SettlementMm))
              .AppendLine("</td></tr>");
        }

        sb.AppendLine("</table>");
    }

    private static string Fmt(double v) => v.ToString("0.######", Invariant);

    private static string Num(double? v) => v is { } x ? x.ToString("0.######", Invariant) : "-";

    private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);
}

public sealed class ReportContent
{
    public string AppVersion { get; init; } = GlemVersion.Current;

    public DateTime GeneratedAt { get; init; } = DateTime.Now;

    /// <summary>
    /// レポート本文の表示言語。明示的に指定しない場合は、生成時点の
    /// <see cref="CultureInfo.CurrentUICulture"/> から解決する（2文字言語コードが "ja" の場合のみ日本語、それ以外は英語）。
    /// </summary>
    public ReportLanguage Language { get; init; } = ResolveDefaultLanguage();

    public ProjectData? Project { get; init; }

    public SlopeAnalysisResult? SlopeResult { get; init; }

    public SettlementAnalysisResult? SettlementResult { get; init; }

    public List<ReportFigure> Figures { get; init; } = new();

    private static ReportLanguage ResolveDefaultLanguage()
    {
        var twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return string.Equals(twoLetter, "ja", StringComparison.OrdinalIgnoreCase) ? ReportLanguage.Japanese : ReportLanguage.English;
    }
}
