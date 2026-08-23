using CommunityToolkit.Mvvm.ComponentModel;
using GLEM.Core.Models;
using GLEM.Core.Validation;

namespace GLEM.App.ViewModels;

// S-2 ground model editor DataGrid row (SoilLayer editing wrapper, design doc 5.5 validation UX)
public sealed partial class LayerRow : ObservableObject
{
    private readonly SoilLayer _layer;

    public LayerRow(SoilLayer layer)
    {
        _layer = layer;
        Name = layer.Name;
        ThicknessM = layer.ThicknessM;
        GammaKnm3 = layer.GammaKnm3;
        CohesionKpa = layer.CohesionKpa;
        FrictionAngleDeg = layer.FrictionAngleDeg;
        PermeabilityMs = layer.PermeabilityMs;
        InitialVoidRatio = layer.InitialVoidRatio;
        CompressionIndexCc = layer.CompressionIndexCc;
        RecompressionIndexCr = layer.RecompressionIndexCr;
        PreconsolidationPressureKpa = layer.PreconsolidationPressureKpa;
        SecondaryCompressionIndexCs = layer.SecondaryCompressionIndexCs;
        ElasticModulusKpa = layer.ElasticModulusKpa;
        PoissonRatio = layer.PoissonRatio;
        RuRatio = layer.RuRatio;
    }

    public int Index { get; set; }

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private double thicknessM;

    [ObservableProperty]
    private double gammaKnm3;

    [ObservableProperty]
    private double cohesionKpa;

    [ObservableProperty]
    private double frictionAngleDeg;

    [ObservableProperty]
    private double? permeabilityMs;

    [ObservableProperty]
    private double? initialVoidRatio;

    [ObservableProperty]
    private double? compressionIndexCc;

    [ObservableProperty]
    private double? recompressionIndexCr;

    [ObservableProperty]
    private double? preconsolidationPressureKpa;

    [ObservableProperty]
    private double? secondaryCompressionIndexCs;

    [ObservableProperty]
    private double? elasticModulusKpa;

    [ObservableProperty]
    private double? poissonRatio;

    [ObservableProperty]
    private double? ruRatio;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string? errorMessage;

    public IReadOnlySet<string> ErrorFields { get; private set; } = new HashSet<string>();

    public void ApplyErrors(IReadOnlyList<ValidationIssue> issues)
    {
        var fields = new HashSet<string>();
        var messages = new List<string>();

        foreach (var issue in issues)
        {
            if (!issue.IsWarning && IsLayerField(issue.FieldName))
            {
                fields.Add(issue.FieldName);
                messages.Add($"[{issue.Code}] {issue.Message}");
            }
        }

        ErrorFields = fields;
        HasError = fields.Count > 0;
        ErrorMessage = string.Join("\n", messages);
    }

    private static bool IsLayerField(string field) =>
        field is "thickness_m" or "gamma_kn_m3" or "c_kpa" or "phi_deg" or "ru_ratio";

    public void Commit()
    {
        _layer.Name = Name;
        _layer.ThicknessM = ThicknessM;
        _layer.GammaKnm3 = GammaKnm3;
        _layer.CohesionKpa = CohesionKpa;
        _layer.FrictionAngleDeg = FrictionAngleDeg;
        _layer.PermeabilityMs = PermeabilityMs;
        _layer.InitialVoidRatio = InitialVoidRatio;
        _layer.CompressionIndexCc = CompressionIndexCc;
        _layer.RecompressionIndexCr = RecompressionIndexCr;
        _layer.PreconsolidationPressureKpa = PreconsolidationPressureKpa;
        _layer.SecondaryCompressionIndexCs = SecondaryCompressionIndexCs;
        _layer.ElasticModulusKpa = ElasticModulusKpa;
        _layer.PoissonRatio = PoissonRatio;
        _layer.RuRatio = RuRatio;
    }

    public SoilLayer Layer => _layer;
}
