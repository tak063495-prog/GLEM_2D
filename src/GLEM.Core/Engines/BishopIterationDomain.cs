namespace GLEM.Core.Engines;

internal readonly record struct BishopTerm(double CosAlpha, double TanAlphaTanPhi, double Resistance);

internal static class BishopIterationDomain
{
    // Positive-FS equilibrium divided by FS: sum A/[cos(alpha)*(FS+d)] = driving.
    internal static (double Fs, bool Converged, int Iterations) Solve(
        IReadOnlyList<BishopTerm> terms, double driving, double tolerance, int budget)
    {
        var pole = 0.0;
        foreach (var t in terms)
        {
            if (!double.IsFinite(t.CosAlpha) || t.CosAlpha <= 0 ||
                !double.IsFinite(t.TanAlphaTanPhi) || !double.IsFinite(t.Resistance))
                throw new EngineException("GLEM-2003", "Bishop equilibrium terms must be finite and have a positive base cosine.");
            pole = Math.Max(pole, -t.TanAlphaTanPhi);
        }
        var lower = Math.BitIncrement(pole);
        double Residual(double f)
        {
            var sum = 0.0;
            foreach (var t in terms) sum += t.Resistance / (t.CosAlpha * (f + t.TanAlphaTanPhi));
            return sum - driving;
        }
        var rLower = Residual(lower);
        if (double.IsNaN(rLower) || rLower <= 0)
            throw new EngineException("GLEM-2003", "No bracket for a positive Bishop equilibrium in the admissible domain.");
        var upper = Math.Max(1, 2 * lower);
        var iterations = 0;
        var rUpper = Residual(upper);
        while (rUpper > 0 && iterations < budget)
        {
            upper *= 2; iterations++; rUpper = Residual(upper);
        }
        if (!double.IsFinite(upper) || double.IsNaN(rUpper))
            throw new EngineException("GLEM-2003", "Bishop equilibrium bracket is nonfinite.");
        if (rUpper > 0) return (upper, false, iterations);
        while (iterations < budget)
        {
            var middle = lower + (upper - lower) / 2;
            var residual = Residual(middle); iterations++;
            if (!double.IsFinite(residual))
                throw new EngineException("GLEM-2003", "Bishop equilibrium residual is nonfinite.");
            if (residual == 0 || upper - lower <= tolerance) return (middle, true, iterations);
            if (residual > 0) lower = middle; else upper = middle;
        }
        return (lower + (upper - lower) / 2, false, iterations);
    }
}
