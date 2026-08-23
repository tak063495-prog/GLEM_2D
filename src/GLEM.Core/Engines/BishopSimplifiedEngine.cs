using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public sealed class BishopSimplifiedEngine : ISlopeStabilityEngine
{
    private readonly double _convergenceTolerance;
    private readonly int _maxIterations;

    public BishopSimplifiedEngine(double convergenceTolerance = 0.001, int maxIterations = 200)
    {
        _convergenceTolerance = convergenceTolerance;
        _maxIterations = maxIterations;
    }

    public SlopeMethod Method => SlopeMethod.BishopSimplified;

    public SlopeCalculation Compute(IReadOnlyList<SliceGeometry> slices, GroundModel gm, AnalysisConditions conditions)
    {
        var fs = 1.0;
        var converged = false;
        var iterations = 0;
        var computations = new List<SliceComputation>(slices.Count);

        for (iterations = 1; iterations <= _maxIterations; iterations++)
        {
            var numerator = 0.0;
            var denominator = 0.0;
            var pass = new List<SliceComputation>(slices.Count);

            foreach (var s in slices)
            {
                var layer = gm.LayerAt(s.ZMid);
                var u = StressCalculator.PoreWaterPressureKpa(gm, layer, s.ZMid);
                var wEff = FelleniusEngine.EffectiveWeight(s, conditions);
                var phiRad = layer.FrictionAngleDeg * Math.PI / 180.0;

                var mAlpha = Math.Cos(s.AlphaRad) * (1.0 + Math.Tan(s.AlphaRad) * Math.Tan(phiRad) / fs);
                if (Math.Abs(mAlpha) < 1e-9)
                {
                    throw new EngineException("GLEM-2003", "The driving force is zero or less. The slip surface shape is inappropriate.");
                }

                var cTerm = layer.CohesionKpa * s.DeltaL;
                var phiRaw = (wEff * (1.0 + conditions.Kv) - u * s.DeltaL) * Math.Tan(phiRad);
                var np = Math.Max(0.0, wEff * (1.0 + conditions.Kv) * Math.Cos(s.AlphaRad) - u * s.DeltaL);

                numerator += (cTerm + phiRaw) / mAlpha;
                denominator += wEff * (Math.Sin(s.AlphaRad) + conditions.Kh);
                pass.Add(new SliceComputation(s, u, np, cTerm / mAlpha, phiRaw / mAlpha));
            }

            if (denominator <= 0.0)
            {
                throw new EngineException("GLEM-2003", "The driving force is zero or less. The slip surface shape is inappropriate.");
            }

            var fsNew = numerator / denominator;
            computations = pass;

            if (Math.Abs(fsNew - fs) < _convergenceTolerance)
            {
                fs = fsNew;
                converged = true;
                break;
            }

            fs = fsNew;
        }

        return new SlopeCalculation(fs, converged, iterations, computations);
    }
}
