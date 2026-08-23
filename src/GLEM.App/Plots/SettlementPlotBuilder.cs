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

        AddCurve(plt, "Total", xs, series.Select(p => p.SettlementMm).ToArray(), "#222222", 2f);
        AddCurve(plt, "Immediate", xs, series.Select(p => p.ImmediateMm).ToArray(), "#E08A00", 1.5f);
        AddCurve(plt, "Primary consolidation", xs, series.Select(p => p.PrimaryMm).ToArray(), "#1E6FD9", 1.5f);
        AddCurve(plt, "Secondary compression", xs, series.Select(p => p.SecondaryMm).ToArray(), "#2E8B3A", 1.5f);

        // U=50% / U=90% dashed horizontal lines (fractions of total settlement)
        var u50 = plt.Add.Line(xMin, result.TotalMm * 0.5, xMax, result.TotalMm * 0.5);
        u50.Color = Color.FromHex("#D02020");
        u50.LinePattern = LinePattern.Dashed;
        u50.MarkerSize = 0f;

        var u90 = plt.Add.Line(xMin, result.TotalMm * 0.9, xMax, result.TotalMm * 0.9);
        u90.Color = Color.FromHex("#D02020");
        u90.LinePattern = LinePattern.Dashed;
        u90.MarkerSize = 0f;

        if (result.T50Days is { } t50)
        {
            plt.Add.Text($"U=50% (T50={t50:F0} d)", xMin + (xMax - xMin) * 0.4, result.TotalMm * 0.5);
        }

        if (result.T90Days is { } t90)
        {
            plt.Add.Text($"U=90% (T90={t90:F0} d)", xMin + (xMax - xMin) * 0.4, result.TotalMm * 0.9);
        }

        plt.Axes.Bottom.Label.Text = logX ? "time [day] (log scale)" : "time [day]";
        plt.Axes.Left.Label.Text = "settlement [mm]";

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
                    labels.Add(v.ToString("0.##"));
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
