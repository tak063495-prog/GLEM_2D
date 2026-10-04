using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public sealed class BishopSimplifiedEngine : ISlopeStabilityEngine
{
    private readonly double _convergenceTolerance;
    private readonly int _maxIterations;

    public BishopSimplifiedEngine(double convergenceTolerance = 0.001, int maxIterations = 200)
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
        // These terms depend only on this call's geometry, soil, and loading, never on the iterated FS.
        // Keep them local so changes to a GroundModel between calls cannot use stale material values.
        var prepared = new PreparedSlice[slices.Count];
        var denominator = 0.0;
        var weightScale = 0.0;
        var zeroStrength = true;
        for (var i = 0; i < slices.Count; i++)
        {
            var s = slices[i];
            var layer = gm.LayerAt(s.ZMid);
            var u = StressCalculator.PoreWaterPressureKpa(gm, layer, s.ZMid);
            var wEff = FelleniusEngine.EffectiveWeight(s, conditions);
            var phiRad = layer.FrictionAngleDeg * Math.PI / 180.0;
            var cosAlpha = Math.Cos(s.AlphaRad);
            var tanPhi = Math.Tan(phiRad);
            var cTerm = layer.CohesionKpa * s.HorizontalWidthM;
            var phiRaw = (wEff * (1.0 + conditions.Kv) - u * s.HorizontalWidthM) * tanPhi;
            var np = Math.Max(0.0, wEff * (1.0 + conditions.Kv) * cosAlpha - u * s.DeltaL);
            prepared[i] = new(u, np, cTerm, phiRaw, cosAlpha, Math.Tan(s.AlphaRad), tanPhi);
            denominator += wEff * (Math.Sin(s.AlphaRad) + conditions.Kh);
            weightScale += Math.Abs(wEff);
            zeroStrength &= layer.CohesionKpa == 0 && layer.FrictionAngleDeg == 0;
        }
        var reportFs = fs;
        if (!double.IsFinite(denominator) || denominator <= 1e-12 * Math.Max(1, weightScale))
            throw new EngineException("GLEM-2003", "The driving force is zero or less. The slip surface shape is inappropriate.");

        for (iterations = 1; iterations <= _maxIterations; iterations++)
        {
            var numerator = 0.0;
            reportFs = fs; // The corrected baseline's report describes the last evaluated iterate.

            foreach (var p in prepared)
            {
                var mAlpha = p.CosAlpha * (1.0 + p.TanAlpha * p.TanPhi / fs);
                if (!double.IsFinite(mAlpha))
                {
                    throw new EngineException("GLEM-2003", "The driving force is zero or less. The slip surface shape is inappropriate.");
                }
                if (mAlpha <= 0)
                {
                    var root = BishopIterationDomain.Solve(prepared.Select(t =>
                        new BishopTerm(t.CosAlpha, t.TanAlpha * t.TanPhi, t.CTerm + t.PhiRaw)).ToArray(),
                        denominator, _convergenceTolerance, _maxIterations - iterations + 1);
                    return BuildResult(root.Fs, root.Converged, iterations - 1 + root.Iterations, root.Fs);
                }
                numerator += (p.CTerm + p.PhiRaw) / mAlpha;
            }

            if (!double.IsFinite(denominator) || denominator <= 1e-12 * Math.Max(1, weightScale))
            {
                throw new EngineException("GLEM-2003", "The driving force is zero or less. The slip surface shape is inappropriate.");
            }

            var fsNew = numerator / denominator;
            if (!double.IsFinite(fsNew) || fsNew < 0)
                throw new EngineException("GLEM-2003", "Bishop iteration produced a nonfinite or negative safety factor.");
            if (fsNew == 0)
            {
                // Pure zero resistance has the exact finite solution FS=0; do not divide by it next pass.
                if (zeroStrength)
                {
                    fs = 0; converged = true; break;
                }
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

        return BuildResult(fs, converged, Math.Min(iterations, _maxIterations), reportFs);

        SlopeCalculation BuildResult(double resultFs, bool resultConverged, int count, double evaluatedFs)
        {
            var computations = new List<SliceComputation>(slices.Count);
            for (var i = 0; i < slices.Count; i++)
            {
                var p = prepared[i];
                var mAlpha = p.CosAlpha * (1.0 + p.TanAlpha * p.TanPhi / evaluatedFs);
                computations.Add(new SliceComputation(slices[i], p.U, p.Np, p.CTerm / mAlpha, p.PhiRaw / mAlpha));
            }
            return SlopeSafety.ValidateResult(new SlopeCalculation(resultFs, resultConverged, count, computations));
        }
    }

    private readonly record struct PreparedSlice(double U, double Np, double CTerm, double PhiRaw, double CosAlpha, double TanAlpha, double TanPhi);
}
