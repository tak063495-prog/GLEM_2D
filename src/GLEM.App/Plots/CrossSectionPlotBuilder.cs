using GLEM.Core.Models;
using ScottPlot;

namespace GLEM.App.Plots;

// S-4 cross-section plot spec (design doc 5.3, R-6.1.x):
// layer polygons, water table line (dashed), critical slip surface (solid bold),
// slice division lines (thin), ground surface line. z positive downward (Y inverted).
public static class CrossSectionPlotBuilder
{
    public static void Build(Plot plt, GroundModel gm, SlopeAnalysisResult result)
    {
        var sliceXs = result.Slices.Select(s => s.X).ToList();
        var xMin = (sliceXs.Count > 0 ? sliceXs.Min() : -5.0) - 2.0;
        var xMax = (sliceXs.Count > 0 ? sliceXs.Max() : 5.0) + 2.0;
        var zMax = Math.Max(gm.TotalThicknessM * 1.15, 4.0);

        // Layer polygons
        var layerColors = new[] { "#D8CBAF", "#B9A98C", "#9C8B6E", "#7F6F52", "#63553C" };
        var zTop = 0.0;
        for (var i = 0; i < gm.Layers.Count; i++)
        {
            var layer = gm.Layers[i];
            var zBottom = zTop + layer.ThicknessM;

            var poly = plt.Add.Polygon(
                new[] { xMin, xMax, xMax, xMin },
                new[] { zTop, zTop, zBottom, zBottom });
            poly.FillColor = Color.FromHex(layerColors[i % layerColors.Length]);
            poly.LineColor = Color.Gray(120);
            poly.LineWidth = 1f;
            poly.MarkerSize = 0f;

            zTop = zBottom;
        }

        // Ground surface line
        var surface = plt.Add.Line(xMin, 0.0, xMax, 0.0);
        surface.Color = Color.FromHex("#222222");
        surface.LineWidth = 2f;
        surface.MarkerSize = 0f;

        // Water table line (dashed)
        if (gm.WaterTableDepthM <= zMax)
        {
            var water = plt.Add.Line(xMin, gm.WaterTableDepthM, xMax, gm.WaterTableDepthM);
            water.Color = Color.FromHex("#1E6FD9");
            water.LineWidth = 1.5f;
            water.LinePattern = LinePattern.Dashed;
            water.MarkerSize = 0f;
        }

        // Critical slip surface (solid, bold) + slice division lines
        if (result.CriticalSurface is CircleSurface circle)
        {
            var points = new List<(double X, double Z)>();
            for (var deg = -90; deg <= 90; deg += 1)
            {
                var th = deg * Math.PI / 180.0;
                var x = circle.CenterX + circle.Radius * Math.Sin(th);
                var z = circle.CenterZ + circle.Radius * Math.Cos(th);
                if (z >= -0.25 && z <= zMax)
                {
                    points.Add((x, z));
                }
            }

            if (points.Count > 1)
            {
                var slip = plt.Add.Scatter(points.Select(p => p.X).ToArray(), points.Select(p => p.Z).ToArray());
                slip.Color = Color.FromHex("#D02020");
                slip.LineWidth = 3f;
                slip.MarkerSize = 0f;
            }

            foreach (var s in result.Slices)
            {
                var sliceLine = plt.Add.Line(s.X, 0.0, s.X, s.Z);
                sliceLine.Color = Color.Gray(140);
                sliceLine.LineWidth = 1f;
                sliceLine.MarkerSize = 0f;
            }

            // Annotations (R-6.1.2)
            plt.Add.Text($"FS_min = {result.MinFs:F3}", xMin + (xMax - xMin) * 0.02, zMax * 0.05);
            plt.Add.Text(
                $"R = {circle.Radius:F1} m   center ({circle.CenterX:F1}, {circle.CenterZ:F1})",
                xMin + (xMax - xMin) * 0.02, zMax * 0.12);
        }
        else if (result.CriticalSurface is FunctionSurface function)
        {
            // Non-circular slip surface through control points (C-04, R-6.1.3)
            var pts = function.ControlPoints.OrderBy(p => p.X).ToList();
            if (pts.Count > 1)
            {
                var slip = plt.Add.Scatter(pts.Select(p => p.X).ToArray(), pts.Select(p => p.Z).ToArray());
                slip.Color = Color.FromHex("#D02020");
                slip.LineWidth = 3f;
                slip.MarkerSize = 4f;
            }

            foreach (var s in result.Slices)
            {
                var sliceLine = plt.Add.Line(s.X, 0.0, s.X, s.Z);
                sliceLine.Color = Color.Gray(140);
                sliceLine.LineWidth = 1f;
                sliceLine.MarkerSize = 0f;
            }

            plt.Add.Text($"FS_min = {result.MinFs:F3}", xMin + (xMax - xMin) * 0.02, zMax * 0.05);
            plt.Add.Text($"{pts.Count} control points", xMin + (xMax - xMin) * 0.02, zMax * 0.12);
        }

        plt.Axes.Bottom.Label.Text = "x [m]";
        plt.Axes.Left.Label.Text = "z [m]";

        // Limits + Y inversion (z positive downward)
        plt.Axes.SetLimits(xMin, xMax, -1.0, zMax);
        plt.Axes.InvertY();
    }
}
