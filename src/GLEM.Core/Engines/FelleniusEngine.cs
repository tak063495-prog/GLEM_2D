using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public sealed class FelleniusEngine : ISlopeStabilityEngine
{
    public SlopeMethod Method => SlopeMethod.Fellenius;

    public SlopeCalculation Compute(IReadOnlyList<SliceGeometry> slices, GroundModel gm, AnalysisConditions conditions)
    {
        var resisting = 0.0;
        var driving = 0.0;
        var computations = new List<SliceComputation>(slices.Count);

        foreach (var s in slices)
        {
            var layer = gm.LayerAt(s.ZMid);
            var u = StressCalculator.PoreWaterPressureKpa(gm, layer, s.ZMid);
            var wEff = EffectiveWeight(s, conditions);
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

        return new SlopeCalculation(resisting / driving, true, 1, computations);
    }

    internal static double EffectiveWeight(SliceGeometry s, AnalysisConditions conditions) =>
        s.WKnPerM + conditions.SurchargeAt(s.XMid) * s.DeltaL;
}
