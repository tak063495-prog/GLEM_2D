using System.Windows;
using GLEM.App.ViewModels;
using Microsoft.Win32;

namespace GLEM.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel? vm = null)
    {
        _vm = vm ?? new MainViewModel();
        InitializeComponent();
        DataContext = _vm;

        // ファイルダイアログ（View 側の責務）
        _vm.OpenRequested += OpenDialog;
        _vm.SaveAsRequested += SaveAsDialog;

        // R-3.1.5: バージョン不一致ファイルの確認ダイアログ
        _vm.VersionMismatchConfirm = message => MessageBox.Show(
            message,
            "GLEM - File Version",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

        Loaded += (_, _) =>
        {
            if (_vm.HasPendingAutosave)
            {
                var answer = MessageBox.Show(
                    "An autosaved copy from a previous (abnormal) session was found.\nRestore it?",
                    "GLEM - Autosave Restore",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer == MessageBoxResult.Yes)
                {
                    _vm.RestoreFromAutosave();
                }
                else
                {
                    _vm.DiscardAutosave();
                }
            }
        };
    }

    private void OpenDialog()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "GLEM project (*.glem)|*.glem|All files (*.*)|*.*",
            Title = "Open GLEM Project"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _vm.LoadProject(dialog.FileName);
        }
    }

    private void SaveAsDialog()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "GLEM project (*.glem)|*.glem|All files (*.*)|*.*",
            Title = "Save GLEM Project As",
            FileName = string.IsNullOrEmpty(_vm.Project.ProjectName) ? "project.glem" : _vm.Project.ProjectName + ".glem"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _vm.WriteProject(dialog.FileName);
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) => _vm.OnCleanExit();

    // 起動引数（ファイル関連付け）で指定されたプロジェクトを開く
    public void LoadProjectOnStartup(string path) => _vm.LoadProject(path);

    private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show(
        "GLEM - Generalized Limit Equilibrium Method\nSlope stability analysis & settlement prediction\nVersion 1.0.0",
        "About GLEM",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
}
