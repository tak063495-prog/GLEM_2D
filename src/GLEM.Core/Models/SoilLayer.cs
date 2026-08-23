namespace GLEM.Core.Models;

public sealed class SoilLayer
{
    public string Name { get; set; } = "";

    public double ThicknessM { get; set; }

    public double GammaKnm3 { get; set; }

    public double CohesionKpa { get; set; }

    public double FrictionAngleDeg { get; set; }

    public double? PermeabilityMs { get; set; }

    public double? InitialVoidRatio { get; set; }

    public double? CompressionIndexCc { get; set; }

    public double? RecompressionIndexCr { get; set; }

    public double? SecondaryCompressionIndexCs { get; set; }

    public double? PreconsolidationPressureKpa { get; set; }

    public double? ElasticModulusKpa { get; set; }

    public double? PoissonRatio { get; set; }

    public double? RuRatio { get; set; }

    public double EffectiveCr =>
        RecompressionIndexCr ?? (CompressionIndexCc is { } cc ? 0.3 * cc : 0.0);

    public double EffectiveCs => SecondaryCompressionIndexCs ?? 0.0;

    public double EffectivePoissonRatio => PoissonRatio ?? 0.3;
}
