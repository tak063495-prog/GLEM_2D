using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public sealed class JanbuGeneralizedEngine : ISlopeStabilityEngine
{
    public SlopeMethod Method => SlopeMethod.JanbuGeneralized;

    public SlopeCalculation Compute(IReadOnlyList<SliceGeometry> slices, GroundModel gm, AnalysisConditions conditions)
    {
        var lambdaC = LambdaCalculator.ComputeLambdaC(slices);
        var resisting = 0.0;
        var driving = 0.0;
        var computations = new List<SliceComputation>(slices.Count);

        foreach (var s in slices)
        {
            var layer = gm.LayerAt(s.ZMid);
            var u = StressCalculator.PoreWaterPressureKpa(gm, layer, s.ZMid);
            var wEff = FelleniusEngine.EffectiveWeight(s, conditions);
            var np = Math.Max(0.0, wEff * (1.0 + conditions.Kv) * Math.Cos(s.AlphaRad) - u * s.DeltaL);

            var cTerm = layer.CohesionKpa * s.DeltaL;
            var phiTerm = np * Math.Tan(layer.FrictionAngleDeg * Math.PI / 180.0);

            resisting += cTerm + phiTerm;
            driving += wEff * (Math.Sin(s.AlphaRad) + conditions.Kh);
            computations.Add(new SliceComputation(s, u, np, cTerm, phiTerm));
        }

        if (driving <= 0.0)
        {
            throw new EngineException("GLEM-2003", "The driving force is zero or less. The slip surface shape is inappropriate.");
        }

        return new SlopeCalculation(resisting / (lambdaC * driving), true, 1, computations);
    }
}

public static class LambdaCalculator
{
    public const double MaxLambdaC = 2.0;

    public static double ComputeLambdaC(IReadOnlyList<SliceGeometry> slices)
    {
        if (IsCircular(slices))
        {
            return 1.0;
        }

        var totalDrivingWeight = 0.0;
        foreach (var s in slices)
        {
            totalDrivingWeight += Math.Max(0.0, s.WKnPerM * Math.Sin(s.AlphaRad));
        }

        if (totalDrivingWeight <= 0.0)
        {
            return 1.0;
        }

        var alphaBar = 0.0;
        foreach (var s in slices)
        {
            alphaBar += Math.Max(0.0, s.WKnPerM * Math.Sin(s.AlphaRad)) * s.AlphaRad / totalDrivingWeight;
        }

        var deviation = 0.0;
        foreach (var s in slices)
        {
            deviation += Math.Max(0.0, s.WKnPerM * Math.Sin(s.AlphaRad)) * Math.Abs(s.AlphaRad - alphaBar) / totalDrivingWeight;
        }

        return Math.Min(MaxLambdaC, 1.0 + deviation);
    }

    public static bool IsCircular(IReadOnlyList<SliceGeometry> slices)
    {
        if (slices.Count < 3)
        {
            return false;
        }

        var p1 = slices[0];
        var p2 = slices[1];
        var p3 = slices[2];

        var d = 2.0 * (p1.XMid * (p2.ZMid - p3.ZMid) + p2.XMid * (p3.ZMid - p1.ZMid) + p3.XMid * (p1.ZMid - p2.ZMid));
        if (Math.Abs(d) < 1e-12)
        {
            return false;
        }

        var s1 = p1.XMid * p1.XMid + p1.ZMid * p1.ZMid;
        var s2 = p2.XMid * p2.XMid + p2.ZMid * p2.ZMid;
        var s3 = p3.XMid * p3.XMid + p3.ZMid * p3.ZMid;

        var cx = (s1 * (p2.ZMid - p3.ZMid) + s2 * (p3.ZMid - p1.ZMid) + s3 * (p1.ZMid - p2.ZMid)) / d;
        var cz = (s1 * (p3.XMid - p2.XMid) + s2 * (p1.XMid - p3.XMid) + s3 * (p2.XMid - p1.XMid)) / d;
        var radius = Math.Sqrt((p1.XMid - cx) * (p1.XMid - cx) + (p1.ZMid - cz) * (p1.ZMid - cz));

        var tolerance = Math.Max(1e-6, 1e-9 * radius);
        foreach (var s in slices)
        {
            var dist = Math.Sqrt((s.XMid - cx) * (s.XMid - cx) + (s.ZMid - cz) * (s.ZMid - cz));
            if (Math.Abs(dist - radius) > tolerance)
            {
                return false;
            }
        }

        return true;
    }
}
