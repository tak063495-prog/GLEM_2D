using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using GLEM.App.ViewModels;
using GLEM.Core.Models;
using Xunit;

namespace GLEM.Tests;

// L2 integration tests against the WPF view models (test plan C-01/C-02, R-3.1.4).
public sealed class UiViewModelTests : IDisposable
{
    private static string AutosavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GLEM", "autosave.glem");

    [Fact]
    public void C01_NewProject_HasTwoLayersAndValidationPasses()
    {
        // The validation summary is localized; pin an explicit UI culture so the assertion below
        // does not depend on the host's UI culture (e.g. ja-JP).
        using var _ = new CultureScope(new CultureInfo("en-US"));

        var vm = new MainViewModel();

        vm.GroundModelEditor.Layers.Should().HaveCount(2);

        vm.GroundModelEditor.RunValidationCommand.Execute(null);

        vm.GroundModelEditor.HasValidationErrors.Should().BeFalse();
        vm.GroundModelEditor.ValidationSummary.Should().Contain("Passed");
    }

    [Fact]
    public void C02_InvalidThickness_HighlightsCellAndShowsSpecMessage()
    {
        var vm = new MainViewModel();
        vm.GroundModelEditor.Layers[0].ThicknessM = -5.0;

        vm.GroundModelEditor.RunValidationCommand.Execute(null);

        vm.GroundModelEditor.HasValidationErrors.Should().BeTrue();
        vm.GroundModelEditor.LastIssues.Should().Contain(i => i.Code == "GLEM-1002");
        vm.GroundModelEditor.Layers[0].ErrorFields.Should().Contain("thickness_m");
        vm.GroundModelEditor.ValidationSummary.Should().Contain("GLEM-1002");
    }

    [Fact]
    public void C02_WaterTableBelowGroundBottom_IsFlagged()
    {
        var vm = new MainViewModel();
        vm.GroundModelEditor.WaterTableDepthM = 99.0;

        vm.GroundModelEditor.RunValidationCommand.Execute(null);

        vm.GroundModelEditor.HasWaterTableError.Should().BeTrue();
        vm.GroundModelEditor.LastIssues.Should().Contain(i => i.Code == "GLEM-1005");
    }

    [Fact]
    public void Navigation_SwitchesActiveScreen()
    {
        var vm = new MainViewModel();

        vm.NavigateCommand.Execute("SlopeSettings");
        vm.ActiveScreen.Should().Be(Screen.SlopeSettings);

        vm.NavigateCommand.Execute("SettlementResult");
        vm.ActiveScreen.Should().Be(Screen.SettlementResult);

        vm.NavigateCommand.Execute("GroundModel");
        vm.ActiveScreen.Should().Be(Screen.GroundModel);
    }

    [Fact]
    public void R314_Autosave_RestoreRoundTrip()
    {
        if (File.Exists(AutosavePath))
        {
            File.Delete(AutosavePath);
        }

        try
        {
            var vm = new MainViewModel();
            vm.HasPendingAutosave.Should().BeFalse();

            vm.Project.ProjectName = "AutosaveRoundTrip";
            vm.MarkDirty();
            vm.Autosave();

            File.Exists(AutosavePath).Should().BeTrue();

            // Simulate restart: a new instance sees the pending autosave (C-09)
            var restarted = new MainViewModel();
            restarted.HasPendingAutosave.Should().BeTrue();

            restarted.RestoreFromAutosave();

            restarted.Project.ProjectName.Should().Be("AutosaveRoundTrip");
            restarted.HasPendingAutosave.Should().BeFalse();
            restarted.OnCleanExit();
        }
        finally
        {
            if (File.Exists(AutosavePath))
            {
                File.Delete(AutosavePath);
            }
        }
    }

    [Fact]
    public async Task C03_MethodSwitching_ProducesValidFsForAllMethods()
    {
        var vm = new MainViewModel();

        // Bounded manual search range keeps the test fast
        vm.SlopeAnalysis.AutoSearch = false;
        vm.SlopeAnalysis.CxMin = -4.0;
        vm.SlopeAnalysis.CxMax = 4.0;
        vm.SlopeAnalysis.CzMin = -6.0;
        vm.SlopeAnalysis.CzMax = -2.0;
        vm.SlopeAnalysis.RadiusMin = 5.0;
        vm.SlopeAnalysis.RadiusMax = 7.0;

        foreach (var method in new[] { SlopeMethod.Fellenius, SlopeMethod.BishopSimplified, SlopeMethod.JanbuGeneralized })
        {
            vm.SlopeAnalysis.Method = method;
            await ((IAsyncRelayCommand)vm.SlopeAnalysis.RunCommand).ExecuteAsync(null);

            vm.SlopeAnalysis.Result.Should().NotBeNull($"for {method}");
            vm.SlopeAnalysis.Result!.MinFs.Should().BeGreaterThan(0.3);
            vm.SlopeAnalysis.Result.Method.Should().Be(method);
        }
    }

    [Fact]
    public async Task C04_JanbuControlPoints_ProducesNonCircularResult()
    {
        var vm = new MainViewModel();
        vm.SlopeAnalysis.Method = SlopeMethod.JanbuGeneralized;
        vm.SlopeAnalysis.ControlPoints.Add(new ControlPointRow { X = -5.0, Z = 1.0 });
        vm.SlopeAnalysis.ControlPoints.Add(new ControlPointRow { X = -2.0, Z = 3.5 });
        vm.SlopeAnalysis.ControlPoints.Add(new ControlPointRow { X = 2.0, Z = 4.5 });
        vm.SlopeAnalysis.ControlPoints.Add(new ControlPointRow { X = 6.0, Z = 1.5 });

        await ((IAsyncRelayCommand)vm.SlopeAnalysis.RunCommand).ExecuteAsync(null);

        vm.SlopeAnalysis.Result.Should().NotBeNull();
        vm.SlopeAnalysis.Result!.CriticalSurface.Should().BeOfType<FunctionSurface>();
        vm.SlopeAnalysis.Result.MinFs.Should().BeGreaterThan(0.3);
        vm.ActiveScreen.Should().Be(Screen.SlopeResult);
    }

    [Fact]
    public async Task C05_Cancel_StopsSearchAndClearsState()
    {
        // ProgressText is localized ("Starting search..." / "Cancelled"); pin an explicit UI culture
        // so the assertions below do not depend on the host's UI culture (e.g. ja-JP).
        using var _ = new CultureScope(new CultureInfo("en-US"));

        var vm = new MainViewModel();

        // Wide range → many candidates, so the search runs long enough to be cancelled mid-flight
        vm.SlopeAnalysis.AutoSearch = false;
        vm.SlopeAnalysis.CxMin = -30.0;
        vm.SlopeAnalysis.CxMax = 30.0;
        vm.SlopeAnalysis.CzMin = -20.0;
        vm.SlopeAnalysis.CzMax = -4.0;
        vm.SlopeAnalysis.RadiusMin = 5.0;
        vm.SlopeAnalysis.RadiusMax = 25.0;

        var task = ((IAsyncRelayCommand)vm.SlopeAnalysis.RunCommand).ExecuteAsync(null);

        // Wait until the search starts reporting progress (up to 3 s)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (vm.SlopeAnalysis.ProgressText == "Starting search..." && sw.ElapsedMilliseconds < 3000)
        {
            await Task.Delay(10);
        }

        vm.SlopeAnalysis.CancelCommand.Execute(null);
        await task;

        vm.SlopeAnalysis.IsRunning.Should().BeFalse();
        vm.SlopeAnalysis.Result.Should().BeNull();
        vm.SlopeAnalysis.ProgressText.Should().Be("Cancelled");
    }

    [Fact]
    public async Task C08_SettlementRun_ProducesCurveBreakdownAndT50T90()
    {
        var vm = new MainViewModel();

        // Settlement analysis requires k/e0/Cc on every layer (GLEM-1006) — set them on both layers
        var topsoil = vm.GroundModelEditor.Layers[0];
        topsoil.PermeabilityMs = 1e-7;
        topsoil.InitialVoidRatio = 0.8;
        topsoil.CompressionIndexCc = 0.2;

        var clay = vm.GroundModelEditor.Layers[1];
        clay.PermeabilityMs = 1e-8;
        clay.InitialVoidRatio = 1.5;
        clay.CompressionIndexCc = 0.3;
        clay.SecondaryCompressionIndexCs = 0.02;
        clay.ElasticModulusKpa = 20000.0;

        await ((IAsyncRelayCommand)vm.Settlement.RunCommand).ExecuteAsync(null);

        vm.Settlement.Result.Should().NotBeNull();
        var result = vm.Settlement.Result!;
        result.TimeSeries.Should().HaveCountGreaterThanOrEqualTo(2);
        result.TotalMm.Should().BeGreaterThan(0.0);
        result.ImmediateMm.Should().BeGreaterThan(0.0);
        result.PrimaryMm.Should().BeGreaterThan(0.0);
        result.SecondaryMm.Should().BeGreaterThan(0.0);
        result.T50Days.Should().NotBeNull();
        result.T90Days.Should().BeGreaterThan(result.T50Days!.Value);

        // R-6.2.1: per-time breakdown must sum to the total settlement at that time
        foreach (var p in result.TimeSeries)
        {
            var sum = p.ImmediateMm + p.PrimaryMm + p.SecondaryMm;
            sum.Should().BeApproximately(p.SettlementMm, 1e-6);
        }

        vm.ActiveScreen.Should().Be(Screen.SettlementResult);
    }

    public void Dispose()
    {
        // no shared state to clean up beyond the autosave file handled per test
    }
}
