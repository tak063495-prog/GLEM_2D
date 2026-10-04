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

        if (!double.IsFinite(input.SurchargeKpa) || input.SurchargeKpa < 0.0)
        {
            issues.Add(ValidationIssue.Error("GLEM-1007", "surcharge_kpa", "Please specify the surcharge load as a value of 0 or greater"));
        }

        if (!double.IsFinite(input.SliceWidthM) || input.SliceWidthM < SliceWidthMin || input.SliceWidthM > SliceWidthMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "slice_width_m", $"The value for 'slice width' is outside the allowed range ({input.SliceWidthM})"));
        }

        if (!double.IsFinite(input.Kh) || input.Kh < 0.0 || input.Kh > SeismicCoefficientMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "kh", $"The value for 'pseudo-static coefficient kh' is outside the allowed range ({input.Kh})"));
        }

        if (!double.IsFinite(input.Kv) || input.Kv < 0.0 || input.Kv > SeismicCoefficientMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "kv", $"The value for 'pseudo-static coefficient kv' is outside the allowed range ({input.Kv})"));
        }

        if (!double.IsFinite(input.CoarseGridStepM) || input.CoarseGridStepM < CoarseGridStepMin || input.CoarseGridStepM > CoarseGridStepMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "coarse_grid_step_m", $"The value for 'coarse grid interval' is outside the allowed range ({input.CoarseGridStepM})"));
        }

        if (!double.IsFinite(input.LocalStepM) || input.LocalStepM < LocalStepMin || input.LocalStepM > LocalStepMax)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "local_step_m", $"The value for 'local search step' is outside the allowed range ({input.LocalStepM})"));
        }

        if (input.SurchargeStartX is double sx && input.SurchargeEndX is double ex && sx >= ex)
        {
            issues.Add(ValidationIssue.Error("GLEM-1004", "surcharge_start_x/surcharge_end_x", $"The value for 'surcharge range' is outside the allowed range (start < end required)"));
        }
        if ((input.SurchargeStartX.HasValue != input.SurchargeEndX.HasValue) ||
            (input.SurchargeStartX is { } start && !double.IsFinite(start)) ||
            (input.SurchargeEndX is { } end && !double.IsFinite(end)))
            issues.Add(ValidationIssue.Error("GLEM-1004", "surcharge_start_x/surcharge_end_x", "Specify both finite surcharge endpoints, or omit both."));
        if (!double.IsFinite(input.ConvergenceTolerance) || input.ConvergenceTolerance <= 0)
            issues.Add(ValidationIssue.Error("GLEM-1004", "convergence_tolerance", "Convergence tolerance must be finite and positive."));
        if (input.MaxIterations < 1)
            issues.Add(ValidationIssue.Error("GLEM-1004", "max_iterations", "Max iterations must be positive."));
        if (!Enum.IsDefined(input.Method))
            issues.Add(ValidationIssue.Error("GLEM-1004", "method", "Unknown slope method."));
        if (input.SearchRange is { } r &&
            (new[] { r.CenterXMin, r.CenterXMax, r.CenterZMin, r.CenterZMax, r.RadiusMin, r.RadiusMax }.Any(v => !double.IsFinite(v)) ||
             r.CenterXMin > r.CenterXMax || r.CenterZMin > r.CenterZMax || r.RadiusMin > r.RadiusMax || r.RadiusMin <= 0))
            issues.Add(ValidationIssue.Error("GLEM-1004", "search_range", "Search range must be finite, ordered, with positive radii."));

        return issues;
    }

    public static IReadOnlyList<ValidationIssue> ValidateSettlement(GroundModel gm, SettlementAnalysisInput input)
    {
        var issues = new List<ValidationIssue>();

        if (!double.IsFinite(input.LoadKpa) || input.LoadKpa <= 0.0)
        {
            issues.Add(ValidationIssue.Error("GLEM-1007", "load_kpa", "Please specify the surcharge load as a value of 0 or greater"));
        }
        foreach (var (field, value) in new[] { ("loaded_area_b", input.LoadedAreaB), ("loaded_area_l", input.LoadedAreaL), ("duration_years", input.DurationYears) })
            if (!double.IsFinite(value) || value <= 0)
                issues.Add(ValidationIssue.Error("GLEM-1004", field, $"{field} must be finite and positive."));
        if (input.OutputPointCount < 1)
            issues.Add(ValidationIssue.Error("GLEM-1004", "output_point_count", "Output point count must be positive."));
        if (!Enum.IsDefined(input.DrainageMode))
            issues.Add(ValidationIssue.Error("GLEM-1004", "drainage_mode", "Unknown drainage mode."));

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

    public static void ThrowIfErrors(IEnumerable<ValidationIssue> issues)
    {
        var errors = issues.Where(x => !x.IsWarning).ToList();
        if (errors.Count > 0)
            throw new EngineException("GLEM-2001", string.Join("; ", errors.Select(x => $"{x.FieldName}: {x.Message}")));
    }
}
