using System.Globalization;
using GLEM.App.Localization;
using GLEM.Core.Models;
using ScottPlot;
using ScottPlot.TickGenerators;

namespace GLEM.App.Plots;

// S-6 settlement-time plot spec (design doc 5.3, R-6.2.x):
// total S-t curve, breakdown lines (immediate / primary / secondary),
// U=50% & U=90% dashed horizontal lines. X axis linear or logarithmic (R-6.2.2).
public static class SettlementPlotBuilder
{
    public static void Build(Plot plt, SettlementAnalysisResult result, bool logX = false)
    {
        var series = result.TimeSeries;
        if (series.Count == 0)
        {
            return;
        }

        double X(double tDays) => logX ? Math.Log10(Math.Max(tDays, 1e-6)) : tDays;

        var xs = series.Select(p => X(p.TimeDays)).ToArray();
        var xMin = xs.Min();
        var xMax = xs.Max();

        // Legend labels are localized prose (U/S/t symbols and units stay unchanged)
        AddCurve(plt, LocalizationService.GetString("SettlementPlot_LegendTotal"), xs, series.Select(p => p.SettlementMm).ToArray(), "#222222", 2f);
        AddCurve(plt, LocalizationService.GetString("SettlementPlot_LegendImmediate"), xs, series.Select(p => p.ImmediateMm).ToArray(), "#E08A00", 1.5f);
        AddCurve(plt, LocalizationService.GetString("SettlementPlot_LegendPrimaryConsolidation"), xs, series.Select(p => p.PrimaryMm).ToArray(), "#1E6FD9", 1.5f);
        AddCurve(plt, LocalizationService.GetString("SettlementPlot_LegendSecondaryCompression"), xs, series.Select(p => p.SecondaryMm).ToArray(), "#2E8B3A", 1.5f);

        // U=50% / U=90% dashed horizontal lines (fractions of total settlement)
        var u50 = plt.Add.Line(xMin, result.TotalMm * 0.5, xMax, result.TotalMm * 0.5);
        u50.Color = Color.FromHex("#D02020");
        u50.LinePattern = LinePattern.Dashed;
        u50.MarkerSize = 0f;

        var u90 = plt.Add.Line(xMin, result.TotalMm * 0.9, xMax, result.TotalMm * 0.9);
        u90.Color = Color.FromHex("#D02020");
        u90.LinePattern = LinePattern.Dashed;
        u90.MarkerSize = 0f;

        // U/T50/T90 are engineering symbols; the day unit and surrounding prose are localized, numbers use CurrentCulture
        if (result.T50Days is { } t50)
        {
            plt.Add.Text(LocalizationService.Format("SettlementPlot_T50AnnotationFormat", t50), xMin + (xMax - xMin) * 0.4, result.TotalMm * 0.5);
        }

        if (result.T90Days is { } t90)
        {
            plt.Add.Text(LocalizationService.Format("SettlementPlot_T90AnnotationFormat", t90), xMin + (xMax - xMin) * 0.4, result.TotalMm * 0.9);
        }

        plt.Axes.Bottom.Label.Text = logX ? LocalizationService.GetString("SettlementPlot_TimeAxisLabelLog") : LocalizationService.GetString("Unit_TimeDay");
        plt.Axes.Left.Label.Text = LocalizationService.GetString("SettlementPlot_SettlementAxisLabel");

        if (logX)
        {
            // Logarithmic ticks at powers of 10 within the plotted range
            var positions = new List<double>();
            var labels = new List<string>();
            for (var e = -2; e <= 8; e++)
            {
                var v = Math.Pow(10, e);
                if (v >= xMin * 0.99 && v <= xMax * 1.01)
                {
                    positions.Add(e);
                    // Manual tick labels are display-only strings; keep them culture-safe via CurrentCulture
                    labels.Add(v.ToString("0.##", CultureInfo.CurrentCulture));
                }
            }

            if (positions.Count > 0)
            {
                plt.Axes.Bottom.TickGenerator = new NumericManual(positions.ToArray(), labels.ToArray());
            }
        }

        plt.ShowLegend();
    }

    private static void AddCurve(Plot plt, string legendText, double[] xs, double[] ys, string hexColor, float width)
    {
        var scatter = plt.Add.Scatter(xs, ys);
        scatter.LegendText = legendText;
        scatter.Color = Color.FromHex(hexColor);
        scatter.LineWidth = width;
        scatter.MarkerSize = 0f;
    }
}
