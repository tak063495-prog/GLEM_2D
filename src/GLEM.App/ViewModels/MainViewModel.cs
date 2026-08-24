using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLEM.App.Localization;
using GLEM.Core;
using GLEM.Core.IO;
using GLEM.Core.Models;

namespace GLEM.App.ViewModels;

public enum Screen
{
    GroundModel,
    SlopeSettings,
    SlopeResult,
    SettlementSettings,
    SettlementResult
}

// S-1 メイン画面（§5.1、プロジェクト操作・ナビゲーション・自動保存 R-3.1.4）
public sealed partial class MainViewModel : ObservableObject
{
    private const int AutosaveIntervalSeconds = 300; // R-3.1.4: 5分間隔

    private readonly DispatcherTimer _autosaveTimer;

    public MainViewModel()
    {
        GroundModelEditor = new GroundModelEditorViewModel(this);
        SlopeAnalysis = new SlopeAnalysisViewModel(this);
        Settlement = new SettlementViewModel(this);

        // Load the initial project state into the editors (S-2 default content)
        GroundModelEditor.LoadFrom(Project.GroundModel);
        SlopeAnalysis.LoadFrom(Project.SlopeAnalysis);
        Settlement.LoadFrom(Project.SettlementAnalysis);

        HasPendingAutosave = File.Exists(AutosavePath);

        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AutosaveIntervalSeconds) };
        _autosaveTimer.Tick += (_, _) => Autosave();
        _autosaveTimer.Start();
    }

    public ProjectData Project { get; private set; } = CreateNewProject();

    public string? CurrentFilePath { get; private set; }

    public bool IsDirty { get; private set; }

    public GroundModelEditorViewModel GroundModelEditor { get; }

    public SlopeAnalysisViewModel SlopeAnalysis { get; }

    public SettlementViewModel Settlement { get; }

    [ObservableProperty]
    private Screen activeScreen = Screen.GroundModel;

    [ObservableProperty]
    private string statusText = LocalizationService.GetString("Status_Ready");

    [ObservableProperty]
    private bool hasPendingAutosave;

    // View 側（ダイアログ表示）との連携イベント
    public Action? OpenRequested { get; set; }

    public Action? SaveAsRequested { get; set; }

    // R-3.1.5: より新しいバージョンのファイルを開く際の確認（true で読み込み続行）
    public Func<string, bool>? VersionMismatchConfirm { get; set; }

    public string AutosavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GLEM", "autosave.glem");

    public void MarkDirty() => IsDirty = true;

    public void UpdateValidationStatus()
    {
        StatusText = GroundModelEditor.HasValidationErrors
            ? LocalizationService.GetString("Validation_Failed")
            : LocalizationService.GetString("Validation_Passed");
    }

    [RelayCommand]
    private void NewProject()
    {
        Project = CreateNewProject();
        CurrentFilePath = null;
        IsDirty = false;
        GroundModelEditor.LoadFrom(Project.GroundModel);
        SlopeAnalysis.Reset();
        Settlement.Reset();
        ActiveScreen = Screen.GroundModel;
        StatusText = LocalizationService.GetString("Status_NewProject");
    }

    [RelayCommand]
    private void RequestOpen() => OpenRequested?.Invoke();

    public bool LoadProject(string path)
    {
        try
        {
            Project = GlemProjectFile.Load(path);
            CurrentFilePath = path;
            IsDirty = false;
            GroundModelEditor.LoadFrom(Project.GroundModel);
            SlopeAnalysis.LoadFrom(Project.SlopeAnalysis);
            Settlement.LoadFrom(Project.SettlementAnalysis);
            ActiveScreen = Screen.GroundModel;
            StatusText = LocalizationService.Format("Status_OpenedFormat", Path.GetFileName(path));
            return true;
        }
        catch (GlemException ex) when (ex.Code == "GLEM-3001" && VersionMismatchConfirm is { } confirm && confirm(ExceptionLocalizer.Format(ex)))
        {
            // R-3.1.5: ユーザーの確認後、新しいバージョンのファイルを読み込む
            try
            {
                Project = GlemProjectFile.Load(path, allowNewerMajor: true);
                CurrentFilePath = path;
                IsDirty = false;
                GroundModelEditor.LoadFrom(Project.GroundModel);
                SlopeAnalysis.LoadFrom(Project.SlopeAnalysis);
                Settlement.LoadFrom(Project.SettlementAnalysis);
                ActiveScreen = Screen.GroundModel;
                StatusText = LocalizationService.Format("Status_OpenedNewerFormat", Path.GetFileName(path));
                return true;
            }
            catch (GlemException ex2)
            {
                StatusText = ExceptionLocalizer.Format(ex2);
                return false;
            }
        }
        catch (GlemException ex)
        {
            StatusText = ExceptionLocalizer.Format(ex);
            return false;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (CurrentFilePath is null)
        {
            RequestSaveAs();
        }
        else
        {
            WriteProject(CurrentFilePath);
        }
    }

    [RelayCommand]
    private void RequestSaveAs() => SaveAsRequested?.Invoke();

    public bool WriteProject(string path)
    {
        try
        {
            GroundModelEditor.ApplyTo(Project);
            Project.UpdatedAt = DateTime.Now;
            GlemProjectFile.Save(path, Project);
            CurrentFilePath = path;
            IsDirty = false;
            StatusText = LocalizationService.Format("Status_SavedFormat", Path.GetFileName(path));
            return true;
        }
        catch (Exception ex)
        {
            StatusText = LocalizationService.Format("Status_SaveFailedFormat", ex.Message);
            return false;
        }
    }

    [RelayCommand]
    public void Navigate(object? screen) => ActiveScreen = ParseScreen(screen);

    public static Screen ParseScreen(object? value) =>
        value is Screen s ? s : Enum.TryParse<Screen>(value?.ToString(), out var parsed) ? parsed : Screen.GroundModel;

    public void Autosave()
    {
        if (!IsDirty)
        {
            return;
        }

        try
        {
            GroundModelEditor.ApplyTo(Project);
            Directory.CreateDirectory(Path.GetDirectoryName(AutosavePath)!);
            GlemProjectFile.Save(AutosavePath, Project);
            // The time is formatted with the current culture via the {0:HH:mm:ss} format specifier.
            StatusText = LocalizationService.Format("Status_AutosavedFormat", DateTime.Now);
        }
        catch (Exception ex)
        {
            StatusText = LocalizationService.Format("Status_AutosaveFailedFormat", ex.Message);
        }
    }

    public void RestoreFromAutosave()
    {
        if (!File.Exists(AutosavePath))
        {
            HasPendingAutosave = false;
            return;
        }

        try
        {
            Project = GlemProjectFile.Load(AutosavePath);
            CurrentFilePath = null;
            IsDirty = true;
            GroundModelEditor.LoadFrom(Project.GroundModel);
            SlopeAnalysis.LoadFrom(Project.SlopeAnalysis);
            Settlement.LoadFrom(Project.SettlementAnalysis);
            HasPendingAutosave = false;
            StatusText = LocalizationService.GetString("Status_RestoredFromAutosave");
        }
        catch (GlemException ex)
        {
            StatusText = LocalizationService.Format("Status_AutosaveRestoreFailedFormat", ex.Code, ExceptionLocalizer.GetMessage(ex));
        }
    }

    public void DiscardAutosave() => HasPendingAutosave = false;

    // 正常終了時（C-09）: 自動保存ファイルを削除して「クリーン終了」マークを残す
    public void OnCleanExit()
    {
        _autosaveTimer.Stop();
        try
        {
            if (File.Exists(AutosavePath))
            {
                File.Delete(AutosavePath);
            }
        }
        catch
        {
            // 終了時の削除失敗は次回起動の復元プロンプトに委ねる
        }
    }

    private static ProjectData CreateNewProject() => new()
    {
        FormatVersion = "1.0",
        // User-visible defaults are localized; numeric defaults and FormatVersion stay stable.
        ProjectName = LocalizationService.GetString("Default_ProjectName"),
        CreatedAt = DateTime.Now,
        GroundModel = new GroundModel
        {
            WaterTableDepthM = 5.0,
            Layers =
            {
                new SoilLayer
                {
                    Name = LocalizationService.GetString("Default_Layer_TopSoil"),
                    ThicknessM = 3.0,
                    GammaKnm3 = 18.0,
                    FrictionAngleDeg = 32.0
                },
                new SoilLayer
                {
                    Name = LocalizationService.GetString("Default_Layer_Clay"),
                    ThicknessM = 7.0,
                    GammaKnm3 = 16.5,
                    CohesionKpa = 15.0,
                    FrictionAngleDeg = 18.0
                }
            }
        }
    };
}
