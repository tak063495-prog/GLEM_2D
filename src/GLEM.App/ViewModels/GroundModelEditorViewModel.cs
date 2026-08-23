using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLEM.Core.IO;
using GLEM.Core.Models;
using GLEM.Core.Validation;

namespace GLEM.App.ViewModels;

// S-2 地盤モデル入力画面（§5.1/§5.2、検証UX §5.5）
public sealed partial class GroundModelEditorViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public GroundModelEditorViewModel(MainViewModel main) => _main = main;

    public ObservableCollection<LayerRow> Layers { get; } = new();

    [ObservableProperty]
    private double waterTableDepthM = 5.0;

    [ObservableProperty]
    private LayerRow? selectedLayer;

    [ObservableProperty]
    private string validationSummary = "";

    [ObservableProperty]
    private bool hasValidationErrors;

    [ObservableProperty]
    private bool hasWaterTableError;

    public IReadOnlyList<ValidationIssue> LastIssues { get; private set; } = Array.Empty<ValidationIssue>();

    public void LoadFrom(GroundModel gm)
    {
        WaterTableDepthM = gm.WaterTableDepthM;
        Layers.Clear();
        foreach (var layer in gm.Layers)
        {
            Layers.Add(new LayerRow(layer));
        }

        Reindex();
        ValidationSummary = "";
        HasValidationErrors = false;
        HasWaterTableError = false;
    }

    public GroundModel BuildGroundModel()
    {
        var gm = new GroundModel { WaterTableDepthM = WaterTableDepthM };
        foreach (var row in Layers)
        {
            row.Commit();
            gm.Layers.Add(row.Layer);
        }

        return gm;
    }

    public void ApplyTo(ProjectData project) => project.GroundModel = BuildGroundModel();

    [RelayCommand]
    private void AddLayer()
    {
        var layer = new SoilLayer
        {
            Name = $"Layer{Layers.Count + 1}",
            ThicknessM = 2.0,
            GammaKnm3 = 18.0,
            FrictionAngleDeg = 30.0
        };

        Layers.Add(new LayerRow(layer));
        Reindex();
        _main.MarkDirty();
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void RemoveSelected()
    {
        if (SelectedLayer is not { } row)
        {
            return;
        }

        Layers.Remove(row);
        SelectedLayer = null;
        Reindex();
        _main.MarkDirty();
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void MoveDown() => Move(+1);

    private bool CanRemove() => SelectedLayer is not null;

    private void Move(int delta)
    {
        if (SelectedLayer is not { } row)
        {
            return;
        }

        var i = Layers.IndexOf(row);
        var j = i + delta;
        if (j < 0 || j >= Layers.Count)
        {
            return;
        }

        Layers.Move(i, j);
        Reindex();
        _main.MarkDirty();
    }

    [RelayCommand]
    private void RunValidation()
    {
        var gm = BuildGroundModel();
        LastIssues = GroundModelValidator.Validate(gm);

        foreach (var row in Layers)
        {
            row.ApplyErrors(LastIssues);
        }

        HasWaterTableError = LastIssues.Any(i => !i.IsWarning && i.FieldName == "water_table_depth_m");
        HasValidationErrors = LastIssues.Any(i => !i.IsWarning);

        if (LastIssues.Count == 0)
        {
            ValidationSummary = "✓ Passed: no validation issues";
        }
        else
        {
            var errors = LastIssues.Where(i => !i.IsWarning).Count();
            var warnings = LastIssues.Count - errors;
            ValidationSummary = $"✗ {errors} error(s), {warnings} warning(s): " + string.Join(" | ", LastIssues.Select(i => $"{(i.IsWarning ? "W" : "E")} [{i.Code}] {i.Message}"));
        }

        _main.UpdateValidationStatus();
    }

    private void Reindex()
    {
        for (var i = 0; i < Layers.Count; i++)
        {
            Layers[i].Index = i + 1;
        }
    }
}
