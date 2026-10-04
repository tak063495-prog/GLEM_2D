using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public sealed class SettlementEngine
{
    private const double SecondsPerDay = 86400.0;
    private const double DaysPerYear = 365.25;
    private const double MinimumEffectiveStressKpa = 1.0;

    public SettlementAnalysisResult Compute(GroundModel gm, SettlementAnalysisInput input)
    {
        GLEM.Core.Validation.AnalysisInputValidator.ThrowIfErrors(
            GLEM.Core.Validation.GroundModelValidator.Validate(gm).Concat(
                GLEM.Core.Validation.AnalysisInputValidator.ValidateSettlement(gm, input)));
        var sImm = ImmediateSettlementMm(gm, input);
        var layerPrimary = PrimaryConsolidationByLayer(gm, input);
        var sPriTotal = layerPrimary.Sum(p => p.SettlementM);

        var layerTc = gm.Layers.ToDictionary(layer => layer, layer => LayerTimeTo95Seconds(gm, layer, input));
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

            var sSec = SecondaryByLayerM(gm, tSeconds, layerTc);
            var sPriNow = sPriTotal * uWeighted;
            series.Add(new SettlementTimePoint(tDays, uWeighted * 100.0, (sImm + sPriNow + sSec) * 1000.0, sImm * 1000.0, sPriNow * 1000.0, sSec * 1000.0));
        }

        var sEnd = SecondaryByLayerM(gm, durationDays * SecondsPerDay, layerTc);
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
        if (!double.IsFinite(input.LoadedAreaB) || input.LoadedAreaB <= 0 || !double.IsFinite(input.LoadedAreaL) || input.LoadedAreaL <= 0)
            throw new EngineException("GLEM-2001", "loaded_area_b/loaded_area_l must be finite and positive.");
        var elastic = gm.Layers.Where(l => l.ThicknessM > 0).ToList();
        if (elastic.Count == 0 || elastic.All(l => l.ElasticModulusKpa is null)) return 0;
        var first = elastic[0];
        // Existing formula is a homogeneous elastic half-space solution, independent of finite thickness.
        // Heterogeneous finite-layer elasticity needs a separate method and is not summed heuristically.
        if (first.ElasticModulusKpa is not { } es || !double.IsFinite(es) || es <= 0 ||
            !double.IsFinite(first.EffectivePoissonRatio) || first.EffectivePoissonRatio <= -1 || first.EffectivePoissonRatio >= .5 ||
            elastic.Any(l => l.ElasticModulusKpa != first.ElasticModulusKpa || l.EffectivePoissonRatio != first.EffectivePoissonRatio))
            throw new EngineException("GLEM-2005", "Immediate settlement supports a homogeneous elastic half-space only (es_kpa/nu). Omit all Es values for consolidation-only analysis.");
        var influence = RectangularInfluenceFactor(input.LoadedAreaB, input.LoadedAreaL);
        var nu = first.EffectivePoissonRatio;
        return input.LoadKpa * (1.0 - nu * nu) / (Math.PI * es) * influence;
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
        if (layer.ThicknessM <= 0 || cc == 0) return 1;
        if (k == 0) return 0;

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

    private static double LayerTimeTo95Seconds(GroundModel gm, SoilLayer layer, SettlementAnalysisInput input)
    {
        if (layer.PermeabilityMs is not { } k || layer.InitialVoidRatio is not { } e0 || layer.CompressionIndexCc is not { } cc || cc == 0)
            return 1; // No primary consolidation clock: explicit one-second reference for the legacy secondary law.
        if (k == 0) return double.PositiveInfinity;
        var sigma = Math.Max(MinimumEffectiveStressKpa, StressCalculator.EffectiveVerticalStressKpa(gm, gm.LayerMidDepth(layer)));
        var mv = cc / ((1 + e0) * sigma * GlemConstants.Ln10);
        var cv = k / (mv * GlemConstants.GammaWaterKnm3);
        var hdr = input.DrainageMode == Drainage.Double ? layer.ThicknessM / 2 : layer.ThicknessM;
        // Invert this engine's large-Tv consolidation law at U=.95, independent of output duration.
        var tv95 = -4 / (Math.PI * Math.PI) * Math.Log(.05 * Math.PI * Math.PI / 8);
        return Math.Max(1, tv95 * hdr * hdr / cv);
    }

    private static double SecondaryByLayerM(GroundModel gm, double tSeconds, IReadOnlyDictionary<SoilLayer, double> clocks) =>
        gm.Layers.Sum(layer => layer.EffectiveCs > 0 && layer.InitialVoidRatio is { } e0 && tSeconds > clocks[layer]
            ? layer.ThicknessM / (1 + e0) * layer.EffectiveCs * Math.Log10(tSeconds / clocks[layer]) : 0);

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

}
