using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public sealed class BishopBaselineEngine : ISlopeStabilityEngine
{
    private readonly double _convergenceTolerance;
    private readonly int _maxIterations;

    public BishopBaselineEngine(double convergenceTolerance = 0.001, int maxIterations = 200)
    {
        if (!double.IsFinite(convergenceTolerance) || convergenceTolerance <= 0 || maxIterations < 1)
            throw new EngineException("GLEM-2001", "convergence_tolerance and max_iterations must be positive and finite.");
        _convergenceTolerance = convergenceTolerance;
        _maxIterations = maxIterations;
    }

    public SlopeMethod Method => SlopeMethod.BishopSimplified;

    public SlopeCalculation Compute(IReadOnlyList<SliceGeometry> slices, GroundModel gm, AnalysisConditions conditions)
    {
        SlopeSafety.ValidateInputs(slices, gm, conditions);
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
                var wEff = EffectiveWeight(s, conditions);
                var phiRad = layer.FrictionAngleDeg * Math.PI / 180.0;

                var mAlpha = Math.Cos(s.AlphaRad) * (1.0 + Math.Tan(s.AlphaRad) * Math.Tan(phiRad) / fs);
                if (!double.IsFinite(mAlpha))
                {
                    throw new EngineException("GLEM-2003", "The driving force is zero or less. The slip surface shape is inappropriate.");
                }
                if (mAlpha <= 0)
                {
                    var driving = slices.Sum(x => EffectiveWeight(x, conditions) * (Math.Sin(x.AlphaRad) + conditions.Kh));
                    if (!double.IsFinite(driving) || driving <= 1e-12 * Math.Max(1, slices.Sum(x => Math.Abs(EffectiveWeight(x, conditions)))))
                        throw new EngineException("GLEM-2003", "The driving force is zero or less. The slip surface shape is inappropriate.");
                    var terms = slices.Select(x =>
                    {
                        var l = gm.LayerAt(x.ZMid); var tanPhi = Math.Tan(l.FrictionAngleDeg * Math.PI / 180.0);
                        return new BishopTerm(Math.Cos(x.AlphaRad), Math.Tan(x.AlphaRad) * tanPhi,
                            l.CohesionKpa * x.HorizontalWidthM + (EffectiveWeight(x, conditions) * (1 + conditions.Kv) -
                            StressCalculator.PoreWaterPressureKpa(gm, l, x.ZMid) * x.HorizontalWidthM) * tanPhi);
                    }).ToArray();
                    var root = BishopIterationDomain.Solve(terms, driving, _convergenceTolerance, _maxIterations - iterations + 1);
                    var report = slices.Select(x =>
                    {
                        var l = gm.LayerAt(x.ZMid); var u0 = StressCalculator.PoreWaterPressureKpa(gm, l, x.ZMid);
                        var w0 = EffectiveWeight(x, conditions); var tanPhi = Math.Tan(l.FrictionAngleDeg * Math.PI / 180.0);
                        var m = Math.Cos(x.AlphaRad) * (1 + Math.Tan(x.AlphaRad) * tanPhi / root.Fs);
                        return new SliceComputation(x, u0, Math.Max(0, w0 * (1 + conditions.Kv) * Math.Cos(x.AlphaRad) - u0 * x.DeltaL),
                            l.CohesionKpa * x.HorizontalWidthM / m, (w0 * (1 + conditions.Kv) - u0 * x.HorizontalWidthM) * tanPhi / m);
                    }).ToList();
                    return SlopeSafety.ValidateResult(new(root.Fs, root.Converged, iterations - 1 + root.Iterations, report));
                }

                var cTerm = layer.CohesionKpa * s.HorizontalWidthM;
                var phiRaw = (wEff * (1.0 + conditions.Kv) - u * s.HorizontalWidthM) * Math.Tan(phiRad);
                var np = Math.Max(0.0, wEff * (1.0 + conditions.Kv) * Math.Cos(s.AlphaRad) - u * s.DeltaL);

                numerator += (cTerm + phiRaw) / mAlpha;
                denominator += wEff * (Math.Sin(s.AlphaRad) + conditions.Kh);
                pass.Add(new SliceComputation(s, u, np, cTerm / mAlpha, phiRaw / mAlpha));
            }

            if (!double.IsFinite(denominator) || denominator <= 1e-12 * Math.Max(1, slices.Sum(s => Math.Abs(EffectiveWeight(s, conditions)))))
            {
                throw new EngineException("GLEM-2003", "The driving force is zero or less. The slip surface shape is inappropriate.");
            }

            var fsNew = numerator / denominator;
            computations = pass;
            if (!double.IsFinite(fsNew) || fsNew < 0)
                throw new EngineException("GLEM-2003", "Bishop iteration produced a nonfinite or negative safety factor.");
            if (fsNew == 0)
            {
                // Pure zero resistance has the exact finite solution FS=0; do not divide by it next pass.
                if (slices.All(s => gm.LayerAt(s.ZMid).CohesionKpa == 0 && gm.LayerAt(s.ZMid).FrictionAngleDeg == 0))
                    return SlopeSafety.ValidateResult(new SlopeCalculation(0, true, iterations, computations));
                throw new EngineException("GLEM-2003", "Bishop iteration has zero effective resistance.");
            }

            if (Math.Abs(fsNew - fs) < _convergenceTolerance)
            {
                fs = fsNew;
                converged = true;
                break;
            }

            fs = fsNew;
        }

        return SlopeSafety.ValidateResult(new SlopeCalculation(fs, converged, Math.Min(iterations, _maxIterations), computations));
    }
    // Inline the internal Core helper without changing its arithmetic.
    private static double EffectiveWeight(SliceGeometry s, AnalysisConditions conditions) =>
        s.WKnPerM + conditions.SurchargeAt(s.XMid) * s.HorizontalWidthM;
}

