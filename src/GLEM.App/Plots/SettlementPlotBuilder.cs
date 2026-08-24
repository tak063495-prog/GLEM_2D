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
        var highContrast = System.Windows.SystemParameters.HighContrast;
        var highContrastBackground = WpfColor(System.Windows.SystemColors.WindowColor);
        var highContrastForeground = WpfColor(System.Windows.SystemColors.WindowTextColor);
        if (highContrast)
        {
            plt.FigureBackground.Color = highContrastBackground;
            plt.DataBackground.Color = highContrastBackground;
            plt.Axes.Color(highContrastForeground);
        }

        // Legend labels are localized prose (U/S/t symbols and units stay unchanged)
        // Curves use distinct line patterns as well as colors so they remain distinguishable
        // in grayscale and Windows high-contrast environments.
        AddCurve(plt, LocalizationService.GetString("SettlementPlot_LegendTotal"), xs, series.Select(p => p.SettlementMm).ToArray(), highContrast ? highContrastForeground : Color.FromHex("#222222"), 2.5f, LinePattern.Solid);
        AddCurve(plt, LocalizationService.GetString("SettlementPlot_LegendImmediate"), xs, series.Select(p => p.ImmediateMm).ToArray(), highContrast ? highContrastForeground : Color.FromHex("#A34F00"), 2f, LinePattern.Dotted);
        AddCurve(plt, LocalizationService.GetString("SettlementPlot_LegendPrimaryConsolidation"), xs, series.Select(p => p.PrimaryMm).ToArray(), highContrast ? highContrastForeground : Color.FromHex("#005BBB"), 2f, LinePattern.Dashed);
        AddCurve(plt, LocalizationService.GetString("SettlementPlot_LegendSecondaryCompression"), xs, series.Select(p => p.SecondaryMm).ToArray(), highContrast ? highContrastForeground : Color.FromHex("#176B2C"), 2f, LinePattern.DenselyDashed);

        // U=50% / U=90% dashed horizontal lines (fractions of total settlement)
        var u50 = plt.Add.Line(xMin, result.TotalMm * 0.5, xMax, result.TotalMm * 0.5);
        u50.Color = highContrast ? highContrastForeground : Color.FromHex("#D02020");
        u50.LinePattern = LinePattern.Dashed;
        u50.MarkerSize = 0f;

        var u90 = plt.Add.Line(xMin, result.TotalMm * 0.9, xMax, result.TotalMm * 0.9);
        u90.Color = highContrast ? highContrastForeground : Color.FromHex("#D02020");
        u90.LinePattern = LinePattern.Dotted;
        u90.MarkerSize = 0f;

        // U/T50/T90 are engineering symbols; the day unit and surrounding prose are localized, numbers use CurrentCulture
        if (result.T50Days is { } t50)
        {
            var label = plt.Add.Text(LocalizationService.Format("SettlementPlot_T50AnnotationFormat", t50), xMin + (xMax - xMin) * 0.4, result.TotalMm * 0.5);
            if (highContrast) label.LabelFontColor = highContrastForeground;
        }

        if (result.T90Days is { } t90)
        {
            var label = plt.Add.Text(LocalizationService.Format("SettlementPlot_T90AnnotationFormat", t90), xMin + (xMax - xMin) * 0.4, result.TotalMm * 0.9);
            if (highContrast) label.LabelFontColor = highContrastForeground;
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
        if (highContrast)
        {
            plt.Legend.FontColor = highContrastForeground;
            plt.Legend.BackgroundColor = highContrastBackground;
            plt.Legend.OutlineColor = highContrastForeground;
        }
    }

    private static void AddCurve(Plot plt, string legendText, double[] xs, double[] ys, Color color, float width, LinePattern pattern)
    {
        var scatter = plt.Add.Scatter(xs, ys);
        scatter.LegendText = legendText;
        scatter.Color = color;
        scatter.LineWidth = width;
        scatter.LinePattern = pattern;
        scatter.MarkerSize = 0f;
    }

    private static Color WpfColor(System.Windows.Media.Color color) =>
        Color.FromARGB((uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B));
}
