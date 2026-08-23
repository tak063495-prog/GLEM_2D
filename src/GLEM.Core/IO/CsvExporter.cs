using System.Globalization;
using System.IO;
using System.Text;
using GLEM.Core.Models;

namespace GLEM.Core.IO;

// 機能仕様書 §6.3: UTF-8・カンマ区切り・ヘッダ行を含む。列構成は固定（T-10）。
public static class CsvExporter
{
    public const string SlopeHeader = "slice_no,x_m,z_m,W_kN_per_m,alpha_deg,u_kPa,Np_kN_per_m,c_term_kN_per_m,phi_term_kN_per_m";

    public const string SettlementHeader = "time_days,U_percent,settlement_mm";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static void ExportSlope(string path, SlopeAnalysisResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(SlopeHeader);
        foreach (var s in result.Slices)
        {
            sb.Append(s.SliceNo.ToString(Invariant)).Append(',')
              .Append(Fmt(s.X)).Append(',')
              .Append(Fmt(s.Z)).Append(',')
              .Append(Fmt(s.WKnPerM)).Append(',')
              .Append(Fmt(s.AlphaDeg)).Append(',')
              .Append(Fmt(s.UKpa)).Append(',')
              .Append(Fmt(s.NpKnPerM)).Append(',')
              .Append(Fmt(s.CTermKnPerM)).Append(',')
              .Append(Fmt(s.PhiTermKnPerM));
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static void ExportSettlement(string path, SettlementAnalysisResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(SettlementHeader);
        foreach (var p in result.TimeSeries)
        {
            sb.Append(Fmt(p.TimeDays)).Append(',')
              .Append(Fmt(p.UPercent)).Append(',')
              .Append(Fmt(p.SettlementMm));
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Fmt(double v) => v.ToString("0.######", Invariant);
}
