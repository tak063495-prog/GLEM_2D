using GLEM.Core.Models;

namespace GLEM.Core.Validation;

public static class AnalysisInputValidator
{
    public const double SliceWidthMin = 0.5;
    public const double SliceWidthMax = 2.0;
    public const double SeismicCoefficientMax = 0.3;
    public const double CoarseGridStepMin = 1.0;
    public const double CoarseGridStepMax = 5.0;
    public const double LocalStepMin = 0.2;
    public const double LocalStepMax = 1.0;

    public static IReadOnlyList<ValidationIssue> ValidateSlope(GroundModel gm, SlopeAnalysisInput input)
    {
        var issues = new List<ValidationIssue>();

        if (input.SurchargeKpa < 0.0)
        {
            issues.Add(ValidationIssue.Error("GLEM-1007", "surcharge_kpa", "Please specify the surcharge load as a value of 0 or greater"));
        }

        if (input.SliceWidthM < SliceWidthMin || input.SliceWidthM > SliceWidthMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "slice_width_m", $"The value for 'slice width' is outside the allowed range ({input.SliceWidthM})"));
        }

        if (input.Kh < 0.0 || input.Kh > SeismicCoefficientMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "kh", $"The value for 'pseudo-static coefficient kh' is outside the allowed range ({input.Kh})"));
        }

        if (input.Kv < 0.0 || input.Kv > SeismicCoefficientMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "kv", $"The value for 'pseudo-static coefficient kv' is outside the allowed range ({input.Kv})"));
        }

        if (input.CoarseGridStepM < CoarseGridStepMin || input.CoarseGridStepM > CoarseGridStepMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "coarse_grid_step_m", $"The value for 'coarse grid interval' is outside the allowed range ({input.CoarseGridStepM})"));
        }

        if (input.LocalStepM < LocalStepMin || input.LocalStepM > LocalStepMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "local_step_m", $"The value for 'local search step' is outside the allowed range ({input.LocalStepM})"));
        }

        if (input.SurchargeStartX is double sx && input.SurchargeEndX is double ex && sx >= ex)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "surcharge_start_x/surcharge_end_x", $"The value for 'surcharge range' is outside the allowed range (start < end required)"));
        }

        return issues;
    }

    public static IReadOnlyList<ValidationIssue> ValidateSettlement(GroundModel gm, SettlementAnalysisInput input)
    {
        var issues = new List<ValidationIssue>();

        if (input.LoadKpa <= 0.0)
        {
            issues.Add(ValidationIssue.Error("GLEM-1007", "load_kpa", "Please specify the surcharge load as a value of 0 or greater"));
        }

        foreach (var layer in gm.Layers)
        {
            var name = string.IsNullOrEmpty(layer.Name) ? "layer" : layer.Name;

            if (layer.PermeabilityMs is null || layer.InitialVoidRatio is null || layer.CompressionIndexCc is null)
            {
                issues.Add(ValidationIssue.Error("GLEM-1006", "k_m_s/e0/cc", $"Permeability coefficient, initial void ratio, and compression index must be entered for settlement analysis (layer '{name}')"));
            }
        }

        return issues;
    }
}
