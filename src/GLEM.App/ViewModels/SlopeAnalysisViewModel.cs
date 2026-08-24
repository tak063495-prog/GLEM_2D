using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLEM.App.Localization;
using GLEM.Core;
using GLEM.Core.Engines;
using GLEM.Core.IO;
using GLEM.Core.Models;
using GLEM.Core.Validation;

namespace GLEM.App.ViewModels;

// S-3 control point row for the Janbu non-circular slip surface editor (C-04)
public sealed partial class ControlPointRow : ObservableObject
{
    [ObservableProperty]
    private double x;

    [ObservableProperty]
    private double z;
}

// S-3 斜面安定解析設定 / S-4 結果（§5.1、実行シーケンス §5.4）
public sealed partial class SlopeAnalysisViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    private CancellationTokenSource? _cts;

    public SlopeAnalysisViewModel(MainViewModel main) => _main = main;

    [ObservableProperty]
    private SlopeMethod method = SlopeMethod.BishopSimplified;

    partial void OnMethodChanged(SlopeMethod value)
    {
        OnPropertyChanged(nameof(MethodIsBishop));
        OnPropertyChanged(nameof(MethodIsFellenius));
        OnPropertyChanged(nameof(MethodIsJanbu));
    }

    public bool MethodIsBishop
    {
        get => Method == SlopeMethod.BishopSimplified;
        set { if (value) Method = SlopeMethod.BishopSimplified; }
    }

    public bool MethodIsFellenius
    {
        get => Method == SlopeMethod.Fellenius;
        set { if (value) Method = SlopeMethod.Fellenius; }
    }

    public bool MethodIsJanbu
    {
        get => Method == SlopeMethod.JanbuGeneralized;
        set { if (value) Method = SlopeMethod.JanbuGeneralized; }
    }

    [ObservableProperty]
    private double sliceWidthM = 1.0;

    [ObservableProperty]
    private double surchargeKpa;

    [ObservableProperty]
    private bool hasSurchargeRange;

    [ObservableProperty]
    private double? surchargeStartX;

    [ObservableProperty]
    private double? surchargeEndX;

    [ObservableProperty]
    private double kh;

    [ObservableProperty]
    private double kv;

    [ObservableProperty]
    private bool autoSearch = true;

    [ObservableProperty]
    private double cxMin = -10.0;

    [ObservableProperty]
    private double cxMax = 10.0;

    [ObservableProperty]
    private double czMin = -8.0;

    [ObservableProperty]
    private double czMax = 2.0;

    [ObservableProperty]
    private double radiusMin = 3.0;

    [ObservableProperty]
    private double radiusMax = 15.0;

    // Janbu non-circular slip surface control points (C-04)
    public ObservableCollection<ControlPointRow> ControlPoints { get; } = new();

    [ObservableProperty]
    private ControlPointRow? selectedControlPoint;

    [RelayCommand]
    private void AddControlPoint()
    {
        var lastX = ControlPoints.Count > 0 ? ControlPoints[^1].X : -5.0;
        ControlPoints.Add(new ControlPointRow { X = lastX + 2.0, Z = 3.0 });
    }

    [RelayCommand(CanExecute = nameof(HasSelectedControlPoint))]
    private void RemoveSelectedControlPoint()
    {
        if (SelectedControlPoint is not { } row)
        {
            return;
        }

        ControlPoints.Remove(row);
        SelectedControlPoint = null;
    }

    private bool HasSelectedControlPoint() => SelectedControlPoint is not null;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private double progressFraction;

    [ObservableProperty]
    private string progressText = "";

    [ObservableProperty]
    private SlopeAnalysisResult? result;

    // 直近の解析で用いた地盤モデル（S-4 の断面図描画・レポート用）
    public GroundModel? LastGroundModel { get; private set; }

    [ObservableProperty]
    private string? runError;

    // 警告確認ダイアログ（§5.5）— View が MessageBox で実装する
    public Func<string, bool>? WarningConfirm { get; set; }

    public void LoadFrom(SlopeAnalysisInput? input)
    {
        if (input is null)
        {
            Reset();
            return;
        }

        Method = input.Method;
        SliceWidthM = input.SliceWidthM;
        SurchargeKpa = input.SurchargeKpa;
        HasSurchargeRange = input.SurchargeStartX is not null && input.SurchargeEndX is not null;
        SurchargeStartX = input.SurchargeStartX;
        SurchargeEndX = input.SurchargeEndX;
        Kh = input.Kh;
        Kv = input.Kv;

        if (input.SearchRange is { } sr)
        {
            AutoSearch = false;
            CxMin = sr.CenterXMin;
            CxMax = sr.CenterXMax;
            CzMin = sr.CenterZMin;
            CzMax = sr.CenterZMax;
            RadiusMin = sr.RadiusMin;
            RadiusMax = sr.RadiusMax;
        }
    }

    public void Reset()
    {
        Method = SlopeMethod.BishopSimplified;
        SliceWidthM = 1.0;
        SurchargeKpa = 0.0;
        HasSurchargeRange = false;
        SurchargeStartX = null;
        SurchargeEndX = null;
        Kh = 0.0;
        Kv = 0.0;
        AutoSearch = true;
        CxMin = -10.0;
        CxMax = 10.0;
        CzMin = -8.0;
        CzMax = 2.0;
        RadiusMin = 3.0;
        RadiusMax = 15.0;
        Result = null;
        RunError = null;
        ProgressText = "";
        ProgressFraction = 0.0;
        LastGroundModel = null;
        ControlPoints.Clear();
    }

    public SlopeAnalysisInput BuildInput() => new()
    {
        Method = Method,
        SliceWidthM = SliceWidthM,
        SurchargeKpa = SurchargeKpa,
        SurchargeStartX = HasSurchargeRange ? SurchargeStartX : null,
        SurchargeEndX = HasSurchargeRange ? SurchargeEndX : null,
        Kh = Kh,
        Kv = Kv,
        SearchRange = AutoSearch ? null : new SearchRange(CxMin, CxMax, CzMin, CzMax, RadiusMin, RadiusMax)
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
            RunError = string.Join(" | ", groundIssues.Where(i => !i.IsWarning).Select(ValidationLocalizer.FormatIssue));
            return;
        }

        var inputIssues = AnalysisInputValidator.ValidateSlope(gm, input);
        if (inputIssues.Any(i => !i.IsWarning))
        {
            RunError = string.Join(" | ", inputIssues.Where(i => !i.IsWarning).Select(ValidationLocalizer.FormatIssue));
            return;
        }

        var warnings = groundIssues.Concat(inputIssues).Where(i => i.IsWarning).ToList();
        if (warnings.Count > 0 && WarningConfirm is { } confirm && !confirm(string.Join("\n", warnings.Select(ValidationLocalizer.FormatIssue))))
        {
            return;
        }

        IsRunning = true;
        Result = null;
        RunError = null;
        ProgressFraction = 0.0;
        ProgressText = LocalizationService.GetString("SlopeAnalysis_ProgressStarting");
        LastGroundModel = gm;
        _cts = new CancellationTokenSource();

        var progress = new Progress<SearchProgress>(p =>
        {
            ProgressFraction = p.FractionComplete;
            ProgressText = LocalizationService.Format("SlopeAnalysis_ProgressCandidateFormat", p.CandidateIndex, p.TotalCandidates);
        });

        try
        {
            if (Method == SlopeMethod.JanbuGeneralized && ControlPoints.Count >= 3)
            {
                // Non-circular slip surface defined by control points: direct computation, no search (C-04)
                var surface = new FunctionSurface(ControlPoints.Select(p => (p.X, p.Z)).ToList());
                Result = await Task.Run(() => ComputeOnFunctionSurface(surface, gm, input), _cts.Token);
            }
            else
            {
                Result = await Task.Run(
                    () => new SlipSurfaceSearcher().Search(gm, input, progress, _cts.Token),
                    _cts.Token);
            }

            _main.Navigate(Screen.SlopeResult);
        }
        catch (OperationCanceledException)
        {
            ProgressText = LocalizationService.GetString("SlopeAnalysis_Cancelled");
        }
        catch (GlemException ex)
        {
            RunError = ExceptionLocalizer.Format(ex);
        }
        finally
        {
            IsRunning = false;
            _main.MarkDirty();
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    // C-04: direct Janbu computation on a user-defined non-circular slip surface
    private static SlopeAnalysisResult ComputeOnFunctionSurface(FunctionSurface surface, GroundModel gm, SlopeAnalysisInput input)
    {
        var slices = SliceDiscretizer.DiscretizeFunction(surface, gm, input.SliceWidthM);
        var calc = new JanbuGeneralizedEngine().Compute(slices, gm, AnalysisConditions.FromInput(input));

        var results = calc.Slices.Select(c => new SliceResult(
            c.Geometry.No,
            c.Geometry.XMid,
            c.Geometry.ZMid,
            c.Geometry.WKnPerM,
            c.Geometry.AlphaRad * 180.0 / Math.PI,
            c.UKpa,
            c.NpKnPerM,
            c.CTermKnPerM,
            c.PhiTermKnPerM)).ToList();

        return new SlopeAnalysisResult(calc.Fs, SlopeMethod.JanbuGeneralized, surface, results, calc.Converged, calc.Iterations);
    }

    public void ExportCsv(string path)
    {
        if (Result is not { } result)
        {
            return;
        }

        CsvExporter.ExportSlope(path, result);
    }

    public void SaveReport(string path, byte[]? crossSectionPng) => ReportGenerator.Save(path, new ReportContent
    {
        Project = _main.Project,
        SlopeResult = Result,
        Figures = crossSectionPng is null ? new List<ReportFigure>() : new List<ReportFigure> { new(LocalizationService.GetString("Report_FigureCrossSection"), crossSectionPng) }
    });
}
