using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using GLEM.App.Localization;
using GLEM.App.Plots;
using GLEM.App.ViewModels;
using GLEM.Core.Models;
using Microsoft.Win32;

namespace GLEM.App.Views;

public partial class SettlementResultView : UserControl
{
    private bool _logXAxis;

    public SettlementResultView()
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

    private SettlementViewModel? Vm => DataContext as SettlementViewModel;

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettlementViewModel.Result))
        {
            RebuildPlot();
        }
    }

    private void RebuildPlot()
    {
        var vm = Vm;
        if (vm?.Result is not { } result)
        {
            return;
        }

        StPlot.Plot.Clear();
        SettlementPlotBuilder.Build(StPlot.Plot, result, _logXAxis);
    }

    private void LinearAxis_Click(object sender, RoutedEventArgs e) => SetTimeScale(log: false);

    private void LogAxis_Click(object sender, RoutedEventArgs e) => SetTimeScale(log: true);

    private void SetTimeScale(bool log)
    {
        _logXAxis = log;
        RebuildPlot();
        LinearAxisButton.IsEnabled = !log;
        LogAxisButton.IsEnabled = log;
    }

    // R-6.2.3: cursor readout of t, U, S at the nearest output point
    private void StPlot_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var vm = Vm;
        if (vm?.Result is not { } result || result.TimeSeries.Count == 0)
        {
            return;
        }

        var pos = e.GetPosition(StPlot);
        var plt = StPlot.Plot;
        var coords = plt.GetCoordinates((float)pos.X, (float)pos.Y, plt.Axes.Bottom, plt.Axes.Left);

        double tData = _logXAxis ? Math.Pow(10.0, coords.X) : coords.X;
        SettlementTimePoint nearest = result.TimeSeries[0];
        foreach (var p in result.TimeSeries)
        {
            if (Math.Abs(p.TimeDays - tData) < Math.Abs(nearest.TimeDays - tData))
            {
                nearest = p;
            }
        }

        // 数値は CurrentCulture で整形される（リソース内の F 形式指定子が適用される）
        CursorReadout.Text = LocalizationService.Format("SettlementResult_CursorReadoutFormat", nearest.TimeDays, nearest.UPercent, nearest.SettlementMm);
    }

    private byte[]? RenderPlotPng()
    {
        try
        {
            var plt = StPlot.Plot;
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
            Title = LocalizationService.GetString("SettlementResult_ExportCsvDialogTitle"),
            FileName = "settlement_result.csv"
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
