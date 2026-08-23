using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public readonly record struct SearchProgress(double FractionComplete, int CandidateIndex, int TotalCandidates);

internal sealed class BestCandidate
{
    public required double Cx { get; init; }
    public required double Cz { get; init; }
    public required double R { get; init; }
    public required SlopeCalculation Calc { get; init; }
}

public sealed class SlipSurfaceSearcher
{
    public SlopeAnalysisResult Search(
        GroundModel gm,
        SlopeAnalysisInput input,
        IProgress<SearchProgress>? progress = null,
        CancellationToken ct = default)
    {
        var engine = CreateEngine(input);
        var conditions = AnalysisConditions.FromInput(input);
        var candidates = GenerateCandidates(gm, input);

        var best = EvaluateBest(candidates, gm, input, engine, conditions, progress, ct);
        if (best is null)
        {
            throw new EngineException("GLEM-2002", "Fewer than 3 valid slices. Please check the slip surface shape.");
        }

        var refined = LocalRefine(best, gm, input, engine, conditions, ct);
        return BuildResult(refined, gm, input);
    }

    private static ISlopeStabilityEngine CreateEngine(SlopeAnalysisInput input) => input.Method switch
    {
        SlopeMethod.Fellenius => new FelleniusEngine(),
        SlopeMethod.BishopSimplified => new BishopSimplifiedEngine(input.ConvergenceTolerance, input.MaxIterations),
        SlopeMethod.JanbuGeneralized => new JanbuGeneralizedEngine(),
        _ => throw new EngineException("GLEM-2001", $"Unknown analysis method: {input.Method}")
    };

    private static List<(double Cx, double Cz, double R)> GenerateCandidates(GroundModel gm, SlopeAnalysisInput input)
    {
        var candidates = new List<(double, double, double)>();

        if (input.SearchRange is { } range)
        {
            for (var cx = range.CenterXMin; cx <= range.CenterXMax + 1e-9; cx += input.CoarseGridStepM)
            {
                for (var cz = range.CenterZMin; cz <= range.CenterZMax + 1e-9; cz += input.CoarseGridStepM)
                {
                    for (var r = range.RadiusMin; r <= range.RadiusMax + 1e-9; r += input.CoarseGridStepM)
                    {
                        candidates.Add((cx, cz, r));
                    }
                }
            }

            return candidates;
        }

        var h = Math.Max(1.0, gm.TotalThicknessM);
        for (var cx = -2.0 * h; cx <= 2.0 * h + 1e-9; cx += input.CoarseGridStepM)
        {
            for (var cz = -2.0 * h; cz <= h + 1e-9; cz += input.CoarseGridStepM)
            {
                for (var r = 0.5 * h; r <= 3.0 * h + 1e-9; r += input.CoarseGridStepM)
                {
                    candidates.Add((cx, cz, r));
                }
            }
        }

        return candidates;
    }

    private static BestCandidate? EvaluateBest(
        List<(double Cx, double Cz, double R)> candidates,
        GroundModel gm,
        SlopeAnalysisInput input,
        ISlopeStabilityEngine engine,
        AnalysisConditions conditions,
        IProgress<SearchProgress>? progress,
        CancellationToken ct)
    {
        BestCandidate? best = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var candidate = candidates[i];

            if (TryEvaluate(candidate, gm, input, engine, conditions, out var calc))
            {
                if (best is null || calc.Fs < best.Calc.Fs)
                {
                    best = new BestCandidate { Cx = candidate.Cx, Cz = candidate.Cz, R = candidate.R, Calc = calc };
                }
            }

            progress?.Report(new SearchProgress((i + 1) / (double)candidates.Count, i + 1, candidates.Count));
        }

        return best;
    }

    private static BestCandidate LocalRefine(
        BestCandidate currentBest,
        GroundModel gm,
        SlopeAnalysisInput input,
        ISlopeStabilityEngine engine,
        AnalysisConditions conditions,
        CancellationToken ct)
    {
        var best = currentBest;
        var originCx = currentBest.Cx;
        var originCz = currentBest.Cz;
        var originR = currentBest.R;
        var halfRange = input.CoarseGridStepM / 2.0;

        double ClampX(double v) => input.SearchRange is { } r ? Math.Clamp(v, r.CenterXMin, r.CenterXMax) : v;
        double ClampZ(double v) => input.SearchRange is { } r ? Math.Clamp(v, r.CenterZMin, r.CenterZMax) : v;
        double ClampRadius(double v) => input.SearchRange is { } r ? Math.Clamp(v, r.RadiusMin, r.RadiusMax) : v;

        for (var dx = -halfRange; dx <= halfRange + 1e-9; dx += input.LocalStepM)
        {
            for (var dz = -halfRange; dz <= halfRange + 1e-9; dz += input.LocalStepM)
            {
                for (var dr = -halfRange; dr <= halfRange + 1e-9; dr += input.LocalStepM)
                {
                    ct.ThrowIfCancellationRequested();

                    var cx = ClampX(originCx + dx);
                    var cz = ClampZ(originCz + dz);
                    var radius = Math.Max(0.5 * Math.Max(1.0, gm.TotalThicknessM), ClampRadius(originR + dr));

                    if (TryEvaluate((cx, cz, radius), gm, input, engine, conditions, out var calc) && calc.Fs < best.Calc.Fs)
                    {
                        best = new BestCandidate { Cx = cx, Cz = cz, R = radius, Calc = calc };
                    }
                }
            }
        }

        return best;
    }

    private static bool TryEvaluate(
        (double Cx, double Cz, double R) candidate,
        GroundModel gm,
        SlopeAnalysisInput input,
        ISlopeStabilityEngine engine,
        AnalysisConditions conditions,
        out SlopeCalculation calc)
    {
        try
        {
            var slices = SliceDiscretizer.DiscretizeCircle(candidate.Cx, candidate.Cz, candidate.R, gm, input.SliceWidthM);
            calc = engine.Compute(slices, gm, conditions);
            return true;
        }
        catch (EngineException ex) when (ex.Code is "GLEM-2001" or "GLEM-2002" or "GLEM-2003")
        {
            calc = default!;
            return false;
        }
    }

    private static SlopeAnalysisResult BuildResult(BestCandidate best, GroundModel gm, SlopeAnalysisInput input)
    {
        var slices = best.Calc.Slices.Select(c => new SliceResult(
            c.Geometry.No,
            c.Geometry.XMid,
            c.Geometry.ZMid,
            c.Geometry.WKnPerM,
            c.Geometry.AlphaRad * 180.0 / Math.PI,
            c.UKpa,
            c.NpKnPerM,
            c.CTermKnPerM,
            c.PhiTermKnPerM)).ToList();

        return new SlopeAnalysisResult(
            best.Calc.Fs,
            input.Method,
            new CircleSurface(best.Cx, best.Cz, best.R),
            slices,
            best.Calc.Converged,
            best.Calc.Iterations);
    }
}
