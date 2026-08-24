using System.Windows;
using System.Windows.Controls;
using GLEM.App.Localization;
using GLEM.App.ViewModels;
using GLEM.Core;
using Microsoft.Win32;

namespace GLEM.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    // 最後に正常に保存された言語設定（保存失敗時にチェック状態を復元するために使用）
    private LanguagePreference _storedLanguage;

    public MainWindow(MainViewModel? vm = null)
    {
        _vm = vm ?? new MainViewModel();
        InitializeComponent();
        DataContext = _vm;

        // 言語メニュー：保存済みの設定に応じて該当項目のみチェックする（相互排他）
        _storedLanguage = App.LanguageStore.Load().Language;
        SetLanguageChecks(_storedLanguage);

        // ファイルダイアログ（View 側の責務）
        _vm.OpenRequested += OpenDialog;
        _vm.SaveAsRequested += SaveAsDialog;

        // R-3.1.5: バージョン不一致ファイルの確認ダイアログ
        _vm.VersionMismatchConfirm = message => MessageBox.Show(
            message,
            LocalizationService.GetString("Dialog_FileVersionTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;

        Loaded += (_, _) =>
        {
            if (_vm.HasPendingAutosave)
            {
                var answer = MessageBox.Show(
                    LocalizationService.GetString("Dialog_AutosaveRestoreMessage"),
                    LocalizationService.GetString("Dialog_AutosaveRestoreTitle"),
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
            Filter = LocalizationService.GetString("FileFilter_Glem"),
            Title = LocalizationService.GetString("Dialog_OpenProjectTitle")
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
            Filter = LocalizationService.GetString("FileFilter_Glem"),
            Title = LocalizationService.GetString("Dialog_SaveProjectAsTitle"),
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

    // 言語メニューのチェック状態を単一の設定から設定する（相互排他）
    private void SetLanguageChecks(LanguagePreference preference)
    {
        LanguageSystemMenuItem.IsChecked = preference == LanguagePreference.System;
        LanguageEnglishMenuItem.IsChecked = preference == LanguagePreference.English;
        LanguageJapaneseMenuItem.IsChecked = preference == LanguagePreference.Japanese;
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || !Enum.TryParse<LanguagePreference>(item.Tag as string, out var preference))
        {
            return;
        }

        try
        {
            App.LanguageStore.Save(new LanguageSettings(preference));
        }
        catch (Exception ex)
        {
            // WPF は Click より前に IsCheckable 項目をトグルするため、保存失敗時は最後に正常に保存された設定へ復元する
            SetLanguageChecks(_storedLanguage);
            MessageBox.Show(
                LocalizationService.Format("Dialog_LanguageSaveFailedFormat", ex.Message),
                LocalizationService.GetString("Dialog_RestartRequiredTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _storedLanguage = preference;
        SetLanguageChecks(preference);

        MessageBox.Show(
            LocalizationService.GetString("Dialog_RestartRequiredMessage"),
            LocalizationService.GetString("Dialog_RestartRequiredTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show(
        LocalizationService.Format("About_BodyFormat", GlemVersion.Current),
        LocalizationService.GetString("About_Title"),
        MessageBoxButton.OK,
        MessageBoxImage.Information);
}
