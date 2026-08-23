using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public sealed class SettlementEngine
{
    private const double SecondsPerDay = 86400.0;
    private const double DaysPerYear = 365.25;
    private const double MinimumEffectiveStressKpa = 1.0;

    public SettlementAnalysisResult Compute(GroundModel gm, SettlementAnalysisInput input)
    {
        var sImm = ImmediateSettlementMm(gm, input);
        var layerPrimary = PrimaryConsolidationByLayer(gm, input);
        var sPriTotal = layerPrimary.Sum(p => p.SettlementM);

        var tCSeconds = TimeToRatioSeconds(gm, input, 0.95, layerPrimary);
        var durationDays = Math.Max(1.0, input.DurationYears * DaysPerYear);

        var series = new List<SettlementTimePoint>(input.OutputPointCount);
        for (var i = 0; i < input.OutputPointCount; i++)
        {
            var fraction = input.OutputPointCount == 1 ? 1.0 : (double)i / (input.OutputPointCount - 1);
            var tDays = Math.Pow(durationDays, fraction);
            var tSeconds = tDays * SecondsPerDay;

            var uWeighted = sPriTotal > 0.0
                ? layerPrimary.Sum(p => p.SettlementM * ConsolidationRatio(gm, p.Layer, input, tSeconds)) / sPriTotal
                : 1.0;

            var sSec = SecondarySettlementMm(gm, input, tSeconds, tCSeconds);
            var sPriNow = sPriTotal * uWeighted;
            series.Add(new SettlementTimePoint(tDays, uWeighted * 100.0, (sImm + sPriNow + sSec) * 1000.0, sImm * 1000.0, sPriNow * 1000.0, sSec * 1000.0));
        }

        var sEnd = SecondarySettlementMm(gm, input, durationDays * SecondsPerDay, tCSeconds);
        return new SettlementAnalysisResult(
            (sImm + sPriTotal + sEnd) * 1000.0,
            sImm * 1000.0,
            sPriTotal * 1000.0,
            sEnd * 1000.0,
            series,
            SolveTimeDays(gm, input, layerPrimary, 0.5, durationDays),
            SolveTimeDays(gm, input, layerPrimary, 0.9, durationDays));
    }

    public static double ImmediateSettlementMm(GroundModel gm, SettlementAnalysisInput input)
    {
        var influence = RectangularInfluenceFactor(input.LoadedAreaB, input.LoadedAreaL);
        var s = 0.0;

        foreach (var layer in gm.Layers)
        {
            if (layer.ElasticModulusKpa is not { } es || es <= 0.0)
            {
                continue;
            }

            var nu = layer.EffectivePoissonRatio;
            s += input.LoadKpa * (1.0 - nu * nu) / (Math.PI * es) * influence;
        }

        return s;
    }

    public static double RectangularInfluenceFactor(double b, double l)
    {
        var diag = Math.Sqrt(b * b + l * l);
        return 2.0 * b * Math.Log((diag + l) / b) + 2.0 * l * Math.Log((diag + b) / l);
    }

    public static double PrimaryConsolidationTotalM(GroundModel gm, SettlementAnalysisInput input) =>
        PrimaryConsolidationByLayer(gm, input).Sum(p => p.SettlementM);

    private static List<(SoilLayer Layer, double SettlementM)> PrimaryConsolidationByLayer(GroundModel gm, SettlementAnalysisInput input)
    {
        var result = new List<(SoilLayer, double)>();

        foreach (var layer in gm.Layers)
        {
            if (layer.InitialVoidRatio is not { } e0 || layer.CompressionIndexCc is null)
            {
                continue;
            }

            var sigmaI = Math.Max(MinimumEffectiveStressKpa, StressCalculator.EffectiveVerticalStressKpa(gm, gm.LayerMidDepth(layer)));
            var sigmaF = sigmaI + input.LoadKpa;
            var sigmaPc = layer.PreconsolidationPressureKpa ?? sigmaI;

            double de;
            if (sigmaF <= sigmaPc)
            {
                de = layer.EffectiveCr * Math.Log10(sigmaF / sigmaI);
            }
            else
            {
                de = layer.EffectiveCr * Math.Log10(sigmaPc / sigmaI) + layer.CompressionIndexCc.Value * Math.Log10(sigmaF / sigmaPc);
            }

            result.Add((layer, layer.ThicknessM / (1.0 + e0) * de));
        }

        return result;
    }

    public static double SecondarySettlementMm(GroundModel gm, SettlementAnalysisInput input, double tSeconds, double tCSeconds)
    {
        if (tSeconds <= tCSeconds)
        {
            return 0.0;
        }

        var s = 0.0;
        foreach (var layer in gm.Layers)
        {
            if (layer.EffectiveCs <= 0.0 || layer.InitialVoidRatio is not { } e0)
            {
                continue;
            }

            var de = layer.EffectiveCs * Math.Log10(tSeconds / tCSeconds);
            s += layer.ThicknessM / (1.0 + e0) * de;
        }

        return s;
    }

    public static double ConsolidationRatio(GroundModel gm, SoilLayer layer, SettlementAnalysisInput input, double tSeconds)
    {
        if (layer.PermeabilityMs is not { } k || layer.InitialVoidRatio is not { } e0 || layer.CompressionIndexCc is not { } cc)
        {
            return 1.0;
        }

        var sigmaV0 = Math.Max(MinimumEffectiveStressKpa, StressCalculator.EffectiveVerticalStressKpa(gm, gm.LayerMidDepth(layer)));
        var mv = cc / ((1.0 + e0) * sigmaV0 * GlemConstants.Ln10);
        var cv = k / (mv * GlemConstants.GammaWaterKnm3);

        var hdr = input.DrainageMode == Drainage.Double ? layer.ThicknessM / 2.0 : layer.ThicknessM;
        var tv = cv * tSeconds / (hdr * hdr);

        if (tv < 0.2)
        {
            return (2.0 / Math.Sqrt(Math.PI)) * Math.Sqrt(tv);
        }

        return 1.0 - (8.0 / (Math.PI * Math.PI)) * Math.Exp(-(Math.PI * Math.PI / 4.0) * tv);
    }

    private static double TimeToRatioSeconds(GroundModel gm, SettlementAnalysisInput input, double targetU, List<(SoilLayer Layer, double SettlementM)> layerPrimary)
    {
        var maxT = 0.0;
        foreach (var (layer, _) in layerPrimary)
        {
            if (!TrySolveRatio(gm, layer, input, targetU, out var t))
            {
                continue;
            }

            maxT = Math.Max(maxT, t);
        }

        return maxT > 0.0 ? maxT : input.DurationYears * DaysPerYear * SecondsPerDay;
    }

    private static double? SolveTimeDays(GroundModel gm, SettlementAnalysisInput input, List<(SoilLayer Layer, double SettlementM)> layerPrimary, double targetU, double durationDays)
    {
        var total = layerPrimary.Sum(p => p.SettlementM);
        if (total <= 0.0)
        {
            return null;
        }

        double OverallRatio(double tSeconds) =>
            layerPrimary.Sum(p => p.SettlementM * ConsolidationRatio(gm, p.Layer, input, tSeconds)) / total;

        var lo = 1.0 * SecondsPerDay;
        var hi = durationDays * SecondsPerDay;
        if (OverallRatio(hi) < targetU)
        {
            return null;
        }

        for (var i = 0; i < 200; i++)
        {
            var mid = (lo + hi) / 2.0;
            if (OverallRatio(mid) < targetU)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return (lo + hi) / 2.0 / SecondsPerDay;
    }

    private static bool TrySolveRatio(GroundModel gm, SoilLayer layer, SettlementAnalysisInput input, double targetU, out double tSeconds)
    {
        var lo = 1.0 * SecondsPerDay;
        var hi = input.DurationYears * DaysPerYear * SecondsPerDay;

        if (ConsolidationRatio(gm, layer, input, hi) < targetU)
        {
            tSeconds = 0.0;
            return false;
        }

        for (var i = 0; i < 200; i++)
        {
            var mid = (lo + hi) / 2.0;
            if (ConsolidationRatio(gm, layer, input, mid) < targetU)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        tSeconds = (lo + hi) / 2.0;
        return true;
    }
}
