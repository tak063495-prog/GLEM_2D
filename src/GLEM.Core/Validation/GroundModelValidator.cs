using GLEM.Core.Models;

namespace GLEM.Core.Validation;

public static class GroundModelValidator
{
    public const double GammaMin = 5.0;
    public const double GammaMax = 30.0;
    public const double CohesionMax = 500.0;
    public const double FrictionAngleMax = 45.0;

    public static IReadOnlyList<ValidationIssue> Validate(GroundModel gm)
    {
        var issues = new List<ValidationIssue>();

        if (gm.Layers.Count == 0)
        {
            issues.Add(ValidationIssue.Error("GLEM-1001", "layers", "No ground layers are defined"));
            return issues;
        }

        foreach (var layer in gm.Layers)
        {
            var name = string.IsNullOrEmpty(layer.Name) ? $"Layer{gm.Layers.IndexOf(layer) + 1}" : layer.Name;

            if (!double.IsFinite(layer.ThicknessM) || layer.ThicknessM <= 0.0)
            {
                issues.Add(ValidationIssue.Error("GLEM-1002", "thickness_m", $"The thickness of layer '{name}' must be a value greater than 0"));
            }

            if (!double.IsFinite(layer.FrictionAngleDeg) || layer.FrictionAngleDeg < 0.0 || layer.FrictionAngleDeg > FrictionAngleMax)
            {
                issues.Add(ValidationIssue.Error("GLEM-1003", "phi_deg", "Please specify the effective friction angle within the range of 0 to 45 degrees"));
            }

            if (!double.IsFinite(layer.GammaKnm3) || layer.GammaKnm3 < GammaMin || layer.GammaKnm3 > GammaMax)
            {
                issues.Add(ValidationIssue.Error("GLEM-1004", "gamma_kn_m3", $"The value for 'unit weight' is outside the allowed range ({layer.GammaKnm3})"));
            }

            if (!double.IsFinite(layer.CohesionKpa) || layer.CohesionKpa < 0.0 || layer.CohesionKpa > CohesionMax)
            {
                issues.Add(ValidationIssue.Error("GLEM-1004", "c_kpa", $"The value for 'effective cohesion' is outside the allowed range ({layer.CohesionKpa})"));
            }

            if (layer.RuRatio is { } ru && (!double.IsFinite(ru) || ru < 0 || ru >= 1))
            {
                issues.Add(ValidationIssue.Error("GLEM-1004", "ru_ratio", $"The value for 'ru ratio' is outside the allowed range ({layer.RuRatio})"));
            }

            foreach (var (field, value, strictlyPositive) in new (string, double?, bool)[]
            {
                ("k_m_s", layer.PermeabilityMs, false), ("e0", layer.InitialVoidRatio, false),
                ("cc", layer.CompressionIndexCc, false), ("cr", layer.RecompressionIndexCr, false),
                ("cs", layer.SecondaryCompressionIndexCs, false), ("sigma_pc_kpa", layer.PreconsolidationPressureKpa, true),
                ("es_kpa", layer.ElasticModulusKpa, true)
            })
                if (value is { } v && (!double.IsFinite(v) || (strictlyPositive ? v <= 0 : v < 0)))
                    issues.Add(ValidationIssue.Error("GLEM-1004", field, $"Layer '{name}': {field} must be finite and {(strictlyPositive ? "positive" : "nonnegative")}."));
            if (layer.PoissonRatio is { } nu && (!double.IsFinite(nu) || nu <= -1 || nu >= .5))
                issues.Add(ValidationIssue.Error("GLEM-1004", "nu", $"Layer '{name}': Poisson ratio must satisfy -1 < nu < 0.5."));

            if (layer.CohesionKpa == 0.0 && layer.FrictionAngleDeg == 0.0)
            {
                issues.Add(ValidationIssue.Warning("GLEM-1008", "c_kpa/phi_deg", "A layer containing cohesionless soil (c'=0, φ'=0) is included. Please check the results."));
            }
        }

        if (!double.IsFinite(gm.WaterTableDepthM) || !double.IsFinite(gm.TotalThicknessM) || gm.WaterTableDepthM < 0.0 || gm.WaterTableDepthM > gm.TotalThicknessM)
        {
            issues.Add(ValidationIssue.Error("GLEM-1005", "water_table_depth_m", "Please specify the groundwater level at a depth between the ground surface and the bottom of the ground"));
        }

        return issues;
    }
}
