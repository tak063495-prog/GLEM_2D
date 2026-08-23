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
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ja\"><head><meta charset=\"utf-8\">");
        sb.Append("<title>GLEM Report - ").Append(Esc(content.Project?.ProjectName ?? "Untitled")).AppendLine("</title>");
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
        sb.Append("<h1>GLEM Analysis Report</h1>\n<p class=\"meta\">Version: ").Append(Esc(content.AppVersion));
        sb.Append(" &nbsp;|&nbsp; Generated at: ").Append(content.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss", Invariant));
        if (content.Project is { } p)
        {
            sb.Append(" &nbsp;|&nbsp; Project: ").Append(Esc(p.ProjectName));
        }

        sb.AppendLine("</p>");

        // 入力概要
        if (content.Project is { } proj)
        {
            AppendInputSummary(sb, proj);
        }

        // 斜面安定結果
        if (content.SlopeResult is { } slope)
        {
            AppendSlopeResults(sb, slope);
        }

        // 沈下解析結果
        if (content.SettlementResult is { } settlement)
        {
            AppendSettlementResults(sb, settlement);
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

    private static void AppendInputSummary(StringBuilder sb, ProjectData p)
    {
        sb.AppendLine("<h2>1. Input Summary</h2>");
        if (p.CreatedAt is { } created)
        {
            sb.Append("<p class=\"meta\">Created: ").Append(created.ToString("yyyy-MM-dd HH:mm:ss", Invariant)).AppendLine("</p>");
        }

        var gm = p.GroundModel;
        sb.Append("<p>Water table depth: <b>").Append(Fmt(gm.WaterTableDepthM)).Append(" m</b></p>\n");
        sb.AppendLine("<table><tr><th>Layer</th><th>Thickness [m]</th><th>&gamma; [kN/m&sup3;]</th><th>c&#8242; [kPa]</th><th>&phi;&#8242; [&deg;]</th><th>k [m/s]</th><th>e0</th><th>Cc</th><th>Cr</th><th>Cs</th></tr>");
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
            sb.Append("<h3>Slope stability settings</h3>\n<p>Method: <b>").Append(Esc(sa.Method.ToString()))
              .Append(" &nbsp;|&nbsp; Slice width: ").Append(Fmt(sa.SliceWidthM)).Append(" m")
              .Append(" &nbsp;|&nbsp; Surcharge q: ").Append(Fmt(sa.SurchargeKpa)).Append(" kPa");
            if (sa.SurchargeStartX is { } xs && sa.SurchargeEndX is { } xe)
            {
                sb.Append(" (x = ").Append(Fmt(xs)).Append(" to ").Append(Fmt(xe)).Append(" m)");
            }

            sb.Append(" &nbsp;|&nbsp; kh: ").Append(Fmt(sa.Kh))
              .Append(" &nbsp;|&nbsp; kv: ").Append(Fmt(sa.Kv));
            if (sa.SearchRange is { } sr)
            {
                sb.Append("<br>Search range: cx [").Append(Fmt(sr.CenterXMin)).Append(", ").Append(Fmt(sr.CenterXMax))
                  .Append("], cz [").Append(Fmt(sr.CenterZMin)).Append(", ").Append(Fmt(sr.CenterZMax))
                  .Append("], R [").Append(Fmt(sr.RadiusMin)).Append(", ").Append(Fmt(sr.RadiusMax)).Append("]");
            }

            sb.AppendLine("</p>");
        }

        if (p.SettlementAnalysis is { } st)
        {
            sb.Append("<h3>Settlement settings</h3>\n<p>Load: <b>").Append(Fmt(st.LoadKpa)).Append(" kPa")
              .Append(" &nbsp;|&nbsp; Loaded area B&#215;L: ").Append(Fmt(st.LoadedAreaB)).Append(" &#215; ").Append(Fmt(st.LoadedAreaL)).Append(" m")
              .Append(" &nbsp;|&nbsp; Drainage: ").Append(Esc(st.DrainageMode.ToString()))
              .Append(" &nbsp;|&nbsp; Duration: ").Append(Fmt(st.DurationYears)).Append(" years")
              .Append(" &nbsp;|&nbsp; Output points: ").Append(st.OutputPointCount)
              .AppendLine("</p>");
        }
    }

    private static void AppendSlopeResults(StringBuilder sb, SlopeAnalysisResult r)
    {
        sb.Append("<h2>2. Slope Stability Results</h2>\n<p>Minimum safety factor FS = <b>")
          .Append(Fmt(r.MinFs))
          .Append("</b> &nbsp;|&nbsp; Method: ").Append(Esc(r.Method.ToString()))
          .Append(" &nbsp;|&nbsp; Converged: ").Append(r.Converged ? "yes" : "no")
          .Append(" (").Append(r.Iterations).Append(" iterations)</p>\n");

        switch (r.CriticalSurface)
        {
            case CircleSurface c:
                sb.Append("<p>Critical surface (circle): R = ").Append(Fmt(c.Radius)).Append(" m, center (")
                  .Append(Fmt(c.CenterX)).Append(", ").Append(Fmt(c.CenterZ)).Append(")</p>\n");
                break;
            case FunctionSurface f:
                sb.Append("<p>Critical surface (function): ").Append(f.ControlPoints.Count).Append(" control points</p>\n");
                break;
        }

        sb.AppendLine("<table><tr><th>slice_no</th><th>x [m]</th><th>z [m]</th><th>W [kN/m]</th><th>&alpha; [&deg;]</th><th>u [kPa]</th><th>N&#8242;p [kN/m]</th><th>c term [kN/m]</th><th>&phi; term [kN/m]</th></tr>");
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

    private static void AppendSettlementResults(StringBuilder sb, SettlementAnalysisResult r)
    {
        sb.Append("<h2>3. Settlement Results</h2>\n<p>Total settlement: <b>").Append(Fmt(r.TotalMm)).Append(" mm")
          .Append("</b> &nbsp;|&nbsp; Immediate: ").Append(Fmt(r.ImmediateMm))
          .Append(" mm &nbsp;|&nbsp; Primary consolidation: ").Append(Fmt(r.PrimaryMm))
          .Append(" mm &nbsp;|&nbsp; Secondary compression: ").Append(Fmt(r.SecondaryMm)).Append(" mm</p>\n");

        sb.Append("<p>T50 = ").Append(Num(r.T50Days)).Append(" days &nbsp;|&nbsp; T90 = ").Append(Num(r.T90Days)).AppendLine(" days</p>");

        sb.AppendLine("<table><tr><th>time [day]</th><th>U [%]</th><th>S [mm]</th></tr>");
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
    public string AppVersion { get; init; } = "1.0.0";

    public DateTime GeneratedAt { get; init; } = DateTime.Now;

    public ProjectData? Project { get; init; }

    public SlopeAnalysisResult? SlopeResult { get; init; }

    public SettlementAnalysisResult? SettlementResult { get; init; }

    public List<ReportFigure> Figures { get; init; } = new();
}
