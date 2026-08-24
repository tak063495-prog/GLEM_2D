using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GLEM.App.Localization;
using GLEM.App.Plots;
using GLEM.App.ViewModels;
using Microsoft.Win32;

namespace GLEM.App.Views;

public partial class SlopeResultView : UserControl
{
    public SlopeResultView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (Vm is { } vm)
            {
                vm.PropertyChanged += OnVmPropertyChanged;
                RebuildPlot(); // Result が View 接続前に設定済みの場合（--capture モード等）にも描画する
            }
        };
    }

    private SlopeAnalysisViewModel? Vm => DataContext as SlopeAnalysisViewModel;

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SlopeAnalysisViewModel.Result))
        {
            RebuildPlot();
        }
    }

    private void RebuildPlot()
    {
        var vm = Vm;
        if (vm?.Result is not { } result || vm.LastGroundModel is not { } gm)
        {
            return;
        }

        CrossSectionPlot.Plot.Clear();
        CrossSectionPlotBuilder.Build(CrossSectionPlot.Plot, gm, result);
    }

    private byte[]? RenderPlotPng()
    {
        try
        {
            var plt = CrossSectionPlot.Plot;
            return plt.GetImageBytes(900, 560, ScottPlot.ImageFormat.Png);
        }
        catch
        {
            return null;
        }
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm?.Result is null)
        {
            MessageBox.Show(LocalizationService.GetString("SlopeResult_ExportCsvNoResultMessage"), "GLEM", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = LocalizationService.GetString("FileFilter_Csv"),
            Title = LocalizationService.GetString("SlopeResult_ExportCsvDialogTitle"),
            FileName = "slope_result.csv"
        };

        if (dialog.ShowDialog(System.Windows.Window.GetWindow(this)) == true)
        {
            vm.ExportCsv(dialog.FileName);
        }
    }

    private void Report_Click(object sender, RoutedEventArgs e)
    {
        var vm = Vm;
        if (vm?.Result is null)
        {
            MessageBox.Show(LocalizationService.GetString("SlopeResult_ReportNoResultMessage"), "GLEM", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = LocalizationService.GetString("FileFilter_Html"),
            Title = LocalizationService.GetString("SlopeResult_GenerateReportDialogTitle"),
            FileName = "glem_report.html"
        };

        if (dialog.ShowDialog(System.Windows.Window.GetWindow(this)) == true)
        {
            vm.SaveReport(dialog.FileName, RenderPlotPng());
        }
    }
}
