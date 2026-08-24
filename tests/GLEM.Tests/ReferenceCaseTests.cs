using FluentAssertions;
using GLEM.Core.Engines;
using GLEM.Core.Models;
using Xunit;

namespace GLEM.Tests;

/// <summary>
/// P1 locked reference cases. Expected values are independently hand-calculated from the
/// equations documented in docs/METHODS.md and are intentionally not derived by the production code.
/// </summary>
public sealed class ReferenceCaseTests
{
    private static double Deg(double value) => value * Math.PI / 180.0;

    private static GroundModel HomogeneousSoil(double cohesionKpa, double frictionAngleDeg) => new()
    {
        WaterTableDepthM = 10.0,
        Layers =
        {
            new SoilLayer
            {
                Name = "Reference soil",
                ThicknessM = 10.0,
                GammaKnm3 = 18.0,
                CohesionKpa = cohesionKpa,
                FrictionAngleDeg = frictionAngleDeg
            }
        }
    };

    private static List<SliceGeometry> ThreeSliceCase() =>
    [
        new SliceGeometry(1, -3.0, 4.0, 5.0, Deg(10), 100.0),
        new SliceGeometry(2, 0.0, 5.0, 5.0, Deg(25), 150.0),
        new SliceGeometry(3, 3.0, 4.0, 5.0, Deg(15), 120.0)
    ];

    [Fact]
    public void Fellenius_ThreeSliceHandCase_RemainsAtPublishedEquationBaseline()
    {
        var result = new FelleniusEngine().Compute(
            ThreeSliceCase(),
            HomogeneousSoil(cohesionKpa: 5.0, frictionAngleDeg: 20.0),
            new AnalysisConditions(0, null, null, 0, 0));

        result.Fs.Should().BeApproximately(1.8111263573, 1e-9);
    }

    [Fact]
    public void BishopSimplified_ThreeSliceFixedPoint_RemainsAtHandIterationBaseline()
    {
        var result = new BishopSimplifiedEngine(convergenceTolerance: 1e-10, maxIterations: 500).Compute(
            ThreeSliceCase(),
            HomogeneousSoil(cohesionKpa: 5.0, frictionAngleDeg: 20.0),
            new AnalysisConditions(0, null, null, 0, 0));

        result.Converged.Should().BeTrue();
        result.Fs.Should().BeApproximately(1.8630805036, 1e-9);
    }

    [Fact]
    public void JanbuApproximation_FourSliceHandCase_RemainsAtLockedBaseline()
    {
        var slices = new List<SliceGeometry>
        {
            new(1, -3.0, 2.5, 2.0, Math.PI / 4.0, 100.0),
            new(2, -1.0, 1.8, 2.0, Math.PI / 6.0, 120.0),
            new(3, 1.0, 1.0, 2.0, Math.PI / 12.0, 140.0),
            new(4, 3.0, 0.6, 2.0, 0.0, 150.0)
        };

        var result = new JanbuGeneralizedEngine().Compute(
            slices,
            HomogeneousSoil(cohesionKpa: 10.0, frictionAngleDeg: 25.0),
            new AnalysisConditions(0, null, null, 0, 0));

        LambdaCalculator.ComputeLambdaC(slices).Should().BeApproximately(1.1759747463, 1e-9);
        result.Fs.Should().BeApproximately(1.4997582286, 1e-9);
    }
}
