using GLEM.Core;

namespace GLEM.Core.Models;

public sealed class GroundModel
{
    public List<SoilLayer> Layers { get; set; } = new();

    public double WaterTableDepthM { get; set; }

    public double TotalThicknessM => Layers.Sum(l => l.ThicknessM);

    public SoilLayer LayerAt(double z)
    {
        var top = 0.0;
        foreach (var layer in Layers)
        {
            if (z <= top + layer.ThicknessM)
            {
                return layer;
            }

            top += layer.ThicknessM;
        }

        throw new EngineException("GLEM-2001", $"The specified depth is below the bottom of the ground: z={z} m");
    }

    public double LayerTopDepth(SoilLayer layer) => Layers.TakeWhile(l => !ReferenceEquals(l, layer)).Sum(l => l.ThicknessM);

    public double LayerMidDepth(SoilLayer layer)
    {
        var top = 0.0;
        foreach (var l in Layers)
        {
            if (ReferenceEquals(l, layer))
            {
                return top + l.ThicknessM / 2.0;
            }

            top += l.ThicknessM;
        }

        throw new EngineException("GLEM-2001", "The specified layer is not included in the ground model");
    }
}
