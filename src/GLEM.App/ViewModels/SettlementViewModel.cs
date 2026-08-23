using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLEM.Core;
using GLEM.Core.Engines;
using GLEM.Core.IO;
using GLEM.Core.Models;
using GLEM.Core.Validation;

namespace GLEM.App.ViewModels;

// S-5 沈下解析設定 / S-6 結果（§5.1）
public sealed partial class SettlementViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public SettlementViewModel(MainViewModel main) => _main = main;

    [ObservableProperty]
    private double loadKpa = 100.0;

    [ObservableProperty]
    private double loadedAreaB = 6.0;

    [ObservableProperty]
    private double loadedAreaL = 6.0;

    [ObservableProperty]
    private Drainage drainageMode = Drainage.Single;

    public Drainage[] DrainageOptions { get; } = Enum.GetValues<Drainage>();

    [ObservableProperty]
    private double durationYears = 10.0;

    [ObservableProperty]
    private int outputPointCount = 50;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private SettlementAnalysisResult? result;

    [ObservableProperty]
    private string? runError;

    public void LoadFrom(SettlementAnalysisInput? input)
    {
        if (input is null)
        {
            Reset();
            return;
        }

        LoadKpa = input.LoadKpa;
        LoadedAreaB = input.LoadedAreaB;
        LoadedAreaL = input.LoadedAreaL;
        DrainageMode = input.DrainageMode;
        DurationYears = input.DurationYears;
        OutputPointCount = input.OutputPointCount;
    }

    public void Reset()
    {
        LoadKpa = 100.0;
        LoadedAreaB = 6.0;
        LoadedAreaL = 6.0;
        DrainageMode = Drainage.Single;
        DurationYears = 10.0;
        OutputPointCount = 50;
        Result = null;
        RunError = null;
    }

    public SettlementAnalysisInput BuildInput() => new()
    {
        LoadKpa = LoadKpa,
        LoadedAreaB = LoadedAreaB,
        LoadedAreaL = LoadedAreaL,
        DrainageMode = DrainageMode,
        DurationYears = DurationYears,
        OutputPointCount = Math.Clamp(OutputPointCount, 2, 1000)
    };

    private bool IsIdle => !IsRunning;

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task RunAsync()
    {
        var gm = _main.GroundModelEditor.BuildGroundModel();
        var input = BuildInput();

        var groundIssues = GroundModelValidator.Validate(gm);
        if (groundIssues.Any(i => !i.IsWarning))
        {
            RunError = string.Join(" | ", groundIssues.Where(i => !i.IsWarning).Select(i => $"[{i.Code}] {i.Message}"));
            return;
        }

        var inputIssues = AnalysisInputValidator.ValidateSettlement(gm, input);
        if (inputIssues.Any(i => !i.IsWarning))
        {
            RunError = string.Join(" | ", inputIssues.Where(i => !i.IsWarning).Select(i => $"[{i.Code}] {i.Message}"));
            return;
        }

        IsRunning = true;
        Result = null;
        RunError = null;

        try
        {
            Result = await Task.Run(() => new SettlementEngine().Compute(gm, input));
            _main.Navigate(Screen.SettlementResult);
        }
        catch (GlemException ex)
        {
            RunError = $"[{ex.Code}] {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _main.MarkDirty();
        }
    }

    public void ExportCsv(string path)
    {
        if (Result is not { } result)
        {
            return;
        }

        CsvExporter.ExportSettlement(path, result);
    }

    public void SaveReport(string path, byte[]? stPlotPng) => ReportGenerator.Save(path, new ReportContent
    {
        Project = _main.Project,
        SettlementResult = Result,
        Figures = stPlotPng is null ? new List<ReportFigure>() : new List<ReportFigure> { new("Settlement-time curve", stPlotPng) }
    });
}
