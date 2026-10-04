using GLEM.Core.Models;

namespace GLEM.Core.Engines;

internal static class SlopeSafety
{
    internal static void ValidateGround(GroundModel gm)
    {
        if (gm.Layers.Count == 0 || !double.IsFinite(gm.TotalThicknessM) || gm.TotalThicknessM <= 0 ||
            !double.IsFinite(gm.WaterTableDepthM) || gm.WaterTableDepthM < 0 || gm.WaterTableDepthM > gm.TotalThicknessM)
            throw new EngineException("GLEM-2001", "Ground depth and water table must be finite and inside the ground.");
        foreach (var l in gm.Layers)
            if (!double.IsFinite(l.ThicknessM) || l.ThicknessM <= 0 || !double.IsFinite(l.GammaKnm3) || l.GammaKnm3 <= 0 ||
                !double.IsFinite(l.CohesionKpa) || l.CohesionKpa < 0 || !double.IsFinite(l.FrictionAngleDeg) ||
                l.FrictionAngleDeg < 0 || l.FrictionAngleDeg >= 90 ||
                (l.RuRatio is { } ru && (!double.IsFinite(ru) || ru < 0 || ru >= 1)))
                throw new EngineException("GLEM-2001", "Slope material properties must be finite and physically admissible.");
    }
    internal static void ValidateInputs(IReadOnlyList<SliceGeometry> slices, GroundModel gm, AnalysisConditions c)
    {
        ValidateGround(gm);
        if (!double.IsFinite(c.SurchargeKpa) || c.SurchargeKpa < 0 || !double.IsFinite(c.Kh) || !double.IsFinite(c.Kv) ||
            (c.SurchargeStartX.HasValue != c.SurchargeEndX.HasValue) ||
            (c.SurchargeStartX is { } sx && c.SurchargeEndX is { } ex && (!double.IsFinite(sx) || !double.IsFinite(ex) || sx > ex)))
            throw new EngineException("GLEM-2001", "Slope loading must be finite with ordered surcharge bounds.");
        if (slices.Count == 0) throw new EngineException("GLEM-2002", "No slices are defined.");
        foreach (var s in slices)
            if (!double.IsFinite(s.XMid) || !double.IsFinite(s.ZMid) || s.ZMid < 0 || s.ZMid > gm.TotalThicknessM ||
                !double.IsFinite(s.DeltaL) || s.DeltaL <= 0 || !double.IsFinite(s.HorizontalWidthM) || s.HorizontalWidthM <= 0 ||
                !double.IsFinite(s.AlphaRad) || Math.Abs(s.AlphaRad) >= Math.PI / 2 || !double.IsFinite(s.WKnPerM) || s.WKnPerM < 0)
                throw new EngineException("GLEM-2002", "Slice coordinates, dimensions, angle and weight must be finite and inside the ground.");
    }
    internal static SlopeCalculation ValidateResult(SlopeCalculation result)
    {
        if (!double.IsFinite(result.Fs) || result.Fs < 0 || result.Slices.Any(s =>
            !double.IsFinite(s.UKpa) || !double.IsFinite(s.NpKnPerM) || !double.IsFinite(s.CTermKnPerM) || !double.IsFinite(s.PhiTermKnPerM)))
            throw new EngineException("GLEM-2003", "Slope calculation produced a nonfinite or negative safety factor or nonfinite slice report.");
        return result;
    }
}
