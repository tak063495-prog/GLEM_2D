using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public static class SliceDiscretizer
{
    public static List<SliceGeometry> DiscretizeCircle(double cx, double cz, double radius, GroundModel gm, double sliceWidthM)
    {
        if (!double.IsFinite(cx) || !double.IsFinite(cz) || !double.IsFinite(radius) || radius <= 0 ||
            !double.IsFinite(sliceWidthM) || sliceWidthM <= 0 || cz >= 0 || cz + radius > gm.TotalThicknessM)
            throw new EngineException("GLEM-2002", "Circle must define a finite, single-valued base inside the ground.");
        var thetaRange = ValidThetaRange(cz, radius);
        if (thetaRange is null)
        {
            throw new EngineException("GLEM-2002", "Fewer than 3 valid slices. Please check the slip surface shape.");
        }

        var (tMin, tMax) = thetaRange.Value;
        var arcLength = radius * (tMax - tMin);
        var n = Math.Max(1, (int)Math.Ceiling(arcLength / sliceWidthM));
        var dTheta = (tMax - tMin) / n;

        var slices = new List<SliceGeometry>();
        for (var i = 0; i < n; i++)
        {
            var t0 = tMin + i * dTheta;
            var t1 = t0 + dTheta;
            var tm = (t0 + t1) / 2.0;

            var xMid = cx + radius * Math.Sin(tm);
            var zMid = cz + radius * Math.Cos(tm);

            var hL = BaseZ(cx, cz, radius, t0);
            var hR = BaseZ(cx, cz, radius, t1);
            var heightAvg = (hL + hR) / 2.0;
            if (heightAvg <= 0.0)
            {
                continue;
            }

            var width = radius * (Math.Sin(t1) - Math.Sin(t0));
            var baseLength = 2 * radius * Math.Sin(dTheta / 2);
            var w = IntegratedWeight(gm, width, hL, hR);
            slices.Add(new SliceGeometry(i + 1, xMid, zMid, baseLength, BaseInclination(tm), w)
                { HorizontalWidthM = width });
        }

        if (slices.Count < 3)
        {
            throw new EngineException("GLEM-2002", "Fewer than 3 valid slices. Please check the slip surface shape.");
        }

        return slices;
    }

    // 非円滑動面（制御点列、M-3）の条体離散化。地表は z=0 の水平線と仮定する。
    public static List<SliceGeometry> DiscretizeFunction(FunctionSurface fs, GroundModel gm, double sliceWidthM)
    {
        if (!double.IsFinite(sliceWidthM) || sliceWidthM <= 0)
            throw new EngineException("GLEM-2002", "slice_width_m must be finite and positive.");
        var pts = fs.ControlPoints.OrderBy(p => p.X).ToList();
        if (pts.Count < 2)
        {
            throw new EngineException("GLEM-2002", "Fewer than 3 valid slices. Please check the slip surface shape.");
        }

        var slices = new List<SliceGeometry>();
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var x0 = pts[i].X;
            var z0 = pts[i].Z;
            var x1 = pts[i + 1].X;
            var z1 = pts[i + 1].Z;

            var segLen = Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            if (segLen <= 0.0)
            {
                continue;
            }

            var n = Math.Max(1, (int)Math.Ceiling(segLen / sliceWidthM));
            for (var k = 0; k < n; k++)
            {
                var t0 = k / (double)n;
                var t1 = (k + 1) / (double)n;

                var xm0 = x0 + (x1 - x0) * t0;
                var zm0 = z0 + (z1 - z0) * t0;
                var xm1 = x0 + (x1 - x0) * t1;
                var zm1 = z0 + (z1 - z0) * t1;

                var xMid = (xm0 + xm1) / 2.0;
                var zMid = (zm0 + zm1) / 2.0;
                if (zMid < 0.0 || zMid > gm.TotalThicknessM)
                {
                    continue;
                }

                // Base depth below the flat ground surface (z positive downward), same convention as DiscretizeCircle
                var hAvg = (zm0 + zm1) / 2.0;
                if (hAvg <= 0.0)
                {
                    continue;
                }

                var alpha = Math.Atan2(z1 - z0, x1 - x0);
                alpha = Math.Clamp(alpha, -Math.PI / 2 + 0.017, Math.PI / 2 - 0.017); // ±89 deg

                var width = xm1 - xm0;
                var w = IntegratedWeight(gm, width, zm0, zm1);
                slices.Add(new SliceGeometry(slices.Count + 1, xMid, zMid, segLen / n, alpha, w)
                    { HorizontalWidthM = width });
            }
        }

        if (slices.Count < 3)
        {
            throw new EngineException("GLEM-2002", "Fewer than 3 valid slices. Please check the slip surface shape.");
        }

        return slices;
    }

    public static double BaseInclination(double theta)
    {
        // z is positive downward: dz/dx=-tan(theta) on the valid upper semicircle.
        return Math.Atan2(-Math.Sin(theta), Math.Cos(theta));
    }

    private static double IntegratedWeight(GroundModel gm, double width, double z0, double z1)
    {
        var cuts = new List<double> { 0, 1 };
        var depth = 0.0;
        foreach (var layer in gm.Layers)
        {
            depth += layer.ThicknessM;
            if (z1 != z0)
            {
                var t = (depth - z0) / (z1 - z0);
                if (t > 0 && t < 1) cuts.Add(t);
            }
        }
        cuts.Sort();
        double Overburden(double z)
        {
            var sum = 0.0; var top = 0.0;
            foreach (var layer in gm.Layers)
            {
                sum += layer.GammaKnm3 * Math.Clamp(z - top, 0, layer.ThicknessM);
                top += layer.ThicknessM;
            }
            return sum;
        }
        var weight = 0.0;
        for (var i = 1; i < cuts.Count; i++)
            weight += width * (cuts[i] - cuts[i-1]) *
                (Overburden(z0 + (z1-z0)*cuts[i-1]) + Overburden(z0 + (z1-z0)*cuts[i])) / 2;
        return weight;
    }

    private static double BaseZ(double cx, double cz, double radius, double theta) => cz + radius * Math.Cos(theta);

    private static (double Min, double Max)? ValidThetaRange(double cz, double radius)
    {
        var cosLimit = -cz / radius;
        if (cosLimit < -1.0 || cosLimit > 1.0)
        {
            return null;
        }

        var bound = Math.Acos(cosLimit);
        return (-bound, bound);
    }
}
