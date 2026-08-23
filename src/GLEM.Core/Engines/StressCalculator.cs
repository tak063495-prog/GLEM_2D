using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public static class StressCalculator
{
    public static double EffectiveVerticalStressKpa(GroundModel gm, double z)
    {
        var sigma = 0.0;
        var top = 0.0;

        foreach (var layer in gm.Layers)
        {
            var bottom = top + layer.ThicknessM;
            if (z <= bottom)
            {
                return sigma + LayerEffectiveStress(layer, top, z, gm.WaterTableDepthM);
            }

            sigma += LayerEffectiveStress(layer, top, bottom, gm.WaterTableDepthM);
            top = bottom;
        }

        throw new EngineException("GLEM-2001", $"The specified depth is below the bottom of the ground: z={z} m");
    }

    public static double PoreWaterPressureKpa(GroundModel gm, SoilLayer layer, double z)
    {
        if (layer.RuRatio is { } ru)
        {
            return ru * EffectiveVerticalStressKpa(gm, z);
        }

        return Math.Max(0.0, z - gm.WaterTableDepthM) * GlemConstants.GammaWaterKnm3;
    }

    private static double LayerEffectiveStress(SoilLayer layer, double zTop, double zBottom, double waterTable)
    {
        var sigma = 0.0;

        foreach (var (a, b) in SplitAt(zTop, zBottom, waterTable))
        {
            var gammaEff = a >= waterTable ? layer.GammaKnm3 - GlemConstants.GammaWaterKnm3 : layer.GammaKnm3;
            sigma += gammaEff * (b - a);
        }

        return sigma;
    }

    private static IEnumerable<(double A, double B)> SplitAt(double zTop, double zBottom, double waterTable)
    {
        if (waterTable > zTop && waterTable < zBottom)
        {
            yield return (zTop, waterTable);
            yield return (waterTable, zBottom);
        }
        else
        {
            yield return (zTop, zBottom);
        }
    }
}
