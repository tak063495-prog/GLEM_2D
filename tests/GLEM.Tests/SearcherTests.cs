using FluentAssertions;
using GLEM.Core.Engines;
using GLEM.Core.Models;
using Xunit;

namespace GLEM.Tests;

public sealed class SearcherTests
{
    private static GroundModel TestGround() => new()
    {
        WaterTableDepthM = 10.0,
        Layers =
        {
            new SoilLayer
            {
                Name = "TestSoil",
                ThicknessM = 10.0,
                GammaKnm3 = 18.0,
                CohesionKpa = 15.0,
                FrictionAngleDeg = 30.0
            }
        }
    };

    private static SlopeAnalysisInput BoundedSearch() => new()
    {
        Method = SlopeMethod.Fellenius,
        SliceWidthM = 1.0,
        CoarseGridStepM = 2.0,
        LocalStepM = 0.5,
        SearchRange = new SearchRange(-4, 4, -6, -2, 5, 7)
    };

    [Fact]
    public void Search_FindsStableCriticalSurfaceWithinRange()
    {
        var gm = TestGround();
        var input = BoundedSearch();

        var result = new SlipSurfaceSearcher().Search(gm, input);

        result.MinFs.Should().BeGreaterThan(1.0);
        result.Method.Should().Be(SlopeMethod.Fellenius);
        result.Slices.Should().HaveCountGreaterThanOrEqualTo(3);

        if (result.CriticalSurface is CircleSurface circle)
        {
            circle.CenterX.Should().BeInRange(-4, 4);
            circle.CenterZ.Should().BeInRange(-6, -2);
            circle.Radius.Should().BeInRange(5, 7);
        }
    }

    [Fact]
    public void Search_ReportsProgressForAllCandidates()
    {
        var gm = TestGround();
        var input = BoundedSearch();
        var reports = new SyncProgress();

        new SlipSurfaceSearcher().Search(gm, input, progress: reports);

        reports.Reports.Should().NotBeEmpty();
        reports.Reports.Last().TotalCandidates.Should().BeGreaterThan(0);
        reports.Reports.Last().CandidateIndex.Should().Be(reports.Reports.Last().TotalCandidates);
    }

    private sealed class SyncProgress : IProgress<SearchProgress>
    {
        public List<SearchProgress> Reports { get; } = new();

        public void Report(SearchProgress value) => Reports.Add(value);
    }

    [Fact]
    public void Search_Cancellation_ThrowsOperationCanceled()
    {
        var gm = TestGround();
        var input = BoundedSearch();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Action act = () => new SlipSurfaceSearcher().Search(gm, input, ct: cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Search_BishopMethod_ProducesConvergedResult()
    {
        var gm = TestGround();
        var input = BoundedSearch() with { Method = SlopeMethod.BishopSimplified };

        var result = new SlipSurfaceSearcher().Search(gm, input);

        result.Converged.Should().BeTrue();
        result.MinFs.Should().BeGreaterThan(1.0);
    }
}
