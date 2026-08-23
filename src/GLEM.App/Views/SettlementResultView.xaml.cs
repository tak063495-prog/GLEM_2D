using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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

        CursorReadout.Text = $"t = {nearest.TimeDays:F1} d   U = {nearest.UPercent:F1} %   S = {nearest.SettlementMm:F2} mm";
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
            MessageBox.Show("No analysis result to export.", "GLEM", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Export Settlement Time Series (CSV)",
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
            MessageBox.Show("No analysis result to report.", "GLEM", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "HTML report (*.html)|*.html|All files (*.*)|*.*",
            Title = "Generate Report",
            FileName = "glem_report.html"
        };

        if (dialog.ShowDialog(System.Windows.Window.GetWindow(this)) == true)
        {
            vm.SaveReport(dialog.FileName, RenderPlotPng());
        }
    }
}
