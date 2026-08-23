using FluentAssertions;
using GLEM.Core;
using GLEM.Core.Engines;
using GLEM.Core.Models;
using Xunit;

namespace GLEM.Tests;

// Reference sources for verification values (M1 DoD: 参照値の出典を明記):
// - T-06a/b/c: Terzaghi-Kasarnovskaya consolidation theory. Exact U-Tv series solution of the
//   diffusion equation (textbook standard, e.g. Das B.M., "Principles of Geotechnical Engineering");
//   the small/large Tv approximation branches are checked against that exact series and for
//   continuity at the branch boundary.
// - T-07a: one-dimensional primary consolidation settlement S = H/(1+e0) * Cc*log10((sigma0+dS)/sigma0),
//   with overconsolidated recompression branch (Cr below preconsolidation pressure).
// - T-07b/c: immediate (elastic) settlement via the Boussinesq rectangular-load influence factor,
//   closed form I = 2B*ln((sqrt(B^2+L^2)+L)/B) + 2L*ln((sqrt(B^2+L^2)+B)/L);
//   square case cross-checked against the analytical value 4B*ln(1+sqrt(2)).
// - T-08: time-to-consolidation scaling t ~ Hd^2 from Tv = cv*t/Hd^2 (double drainage halves Hd).

public sealed class SettlementTests
{
    private static GroundModel ConsolidationGround() => new()
    {
        WaterTableDepthM = 20.0,
        Layers =
        {
            new SoilLayer
            {
                Name = "Clay",
                ThicknessM = 4.0,
                GammaKnm3 = 18.0,
                PermeabilityMs = 1e-8,
                InitialVoidRatio = 1.0,
                CompressionIndexCc = 0.3
            }
        }
    };

    private static double TimeForTimeFactor(GroundModel gm, SoilLayer layer, SettlementAnalysisInput input, double targetTv)
    {
        var sigmaV0 = StressCalculator.EffectiveVerticalStressKpa(gm, gm.LayerMidDepth(layer));
        var mv = 0.3 / ((1.0 + 1.0) * sigmaV0 * GlemConstants.Ln10);
        var cv = layer.PermeabilityMs!.Value / (mv * GlemConstants.GammaWaterKnm3);
        var hdr = input.DrainageMode == Drainage.Double ? layer.ThicknessM / 2.0 : layer.ThicknessM;
        return targetTv * hdr * hdr / cv;
    }

    private static double ExactConsolidationRatio(double tv)
    {
        var sum = 0.0;
        for (var n = 0; n < 100; n++)
        {
            var m = 2 * n + 1;
            var term = (8.0 / (Math.PI * Math.PI)) / (m * m) * Math.Exp(-(m * m) * Math.PI * Math.PI * tv / 4.0);
            sum += term;
            if (term < 1e-13)
            {
                break;
            }
        }

        return 1.0 - sum;
    }

    [Fact]
    public void T06a_ConsolidationRatio_AtTv0197_IsApproximately50Percent()
    {
        var gm = ConsolidationGround();
        var input = new SettlementAnalysisInput { LoadKpa = 50.0 };
        var t = TimeForTimeFactor(gm, gm.Layers[0], input, targetTv: 0.197);

        var u = SettlementEngine.ConsolidationRatio(gm, gm.Layers[0], input, t);

        u.Should().BeInRange(0.49, 0.51);
    }

    [Fact]
    public void T06b_ConsolidationRatio_MatchesExactSeries()
    {
        var gm = ConsolidationGround();
        var input = new SettlementAnalysisInput { LoadKpa = 50.0 };

        foreach (var tv in new[] { 0.05, 0.3, 0.8 })
        {
            var t = TimeForTimeFactor(gm, gm.Layers[0], input, targetTv: tv);
            var approximated = SettlementEngine.ConsolidationRatio(gm, gm.Layers[0], input, t);

            approximated.Should().BeApproximately(ExactConsolidationRatio(tv), 0.02);
        }
    }

    [Fact]
    public void T06c_ConsolidationRatio_ContinuousAtBranchBoundary()
    {
        var gm = ConsolidationGround();
        var input = new SettlementAnalysisInput { LoadKpa = 50.0 };

        var below = SettlementEngine.ConsolidationRatio(gm, gm.Layers[0], input, TimeForTimeFactor(gm, gm.Layers[0], input, 0.199));
        var above = SettlementEngine.ConsolidationRatio(gm, gm.Layers[0], input, TimeForTimeFactor(gm, gm.Layers[0], input, 0.201));

        (above - below).Should().BeLessThan(0.005);
    }

    [Fact]
    public void T07a_PrimaryConsolidation_MatchesHandCalculation()
    {
        var gm = new GroundModel
        {
            WaterTableDepthM = 30.0,
            Layers =
            {
                new SoilLayer
                {
                    Name = "L1",
                    ThicknessM = 4.0,
                    GammaKnm3 = 18.0,
                    InitialVoidRatio = 1.0,
                    CompressionIndexCc = 0.30
                },
                new SoilLayer
                {
                    Name = "L2",
                    ThicknessM = 6.0,
                    GammaKnm3 = 17.0,
                    InitialVoidRatio = 1.5,
                    CompressionIndexCc = 0.45,
                    RecompressionIndexCr = 0.10,
                    PreconsolidationPressureKpa = 150.0
                }
            }
        };

        var input = new SettlementAnalysisInput { LoadKpa = 50.0 };

        var sigmaI1 = 2.0 * 18.0;
        var de1 = 0.30 * Math.Log10((sigmaI1 + 50.0) / sigmaI1);
        var s1 = 4.0 / (1.0 + 1.0) * de1;

        var sigmaI2 = 4.0 * 18.0 + 3.0 * 17.0;
        var de2 = 0.10 * Math.Log10(150.0 / sigmaI2) + 0.45 * Math.Log10((sigmaI2 + 50.0) / 150.0);
        var s2 = 6.0 / (1.0 + 1.5) * de2;

        var expected = s1 + s2;

        var actual = SettlementEngine.PrimaryConsolidationTotalM(gm, input);

        actual.Should().BeApproximately(expected, 1e-9);
        actual.Should().BeApproximately(0.3145, 0.002);
    }

    [Fact]
    public void T07b_ImmediateSettlement_MatchesAnalyticalInfluenceFactor()
    {
        var gm = new GroundModel
        {
            WaterTableDepthM = 20.0,
            Layers =
            {
                new SoilLayer
                {
                    Name = "Sand",
                    ThicknessM = 5.0,
                    GammaKnm3 = 18.0,
                    ElasticModulusKpa = 20000.0,
                    PoissonRatio = 0.3
                }
            }
        };

        var input = new SettlementAnalysisInput { LoadKpa = 100.0, LoadedAreaB = 6.0, LoadedAreaL = 6.0 };

        var influence = SettlementEngine.RectangularInfluenceFactor(6.0, 6.0);
        influence.Should().BeApproximately(24.0 * Math.Log(1.0 + Math.Sqrt(2.0)), 1e-9);

        var sImm = SettlementEngine.ImmediateSettlementMm(gm, input);
        var expected = 100.0 * (1.0 - 0.3 * 0.3) / (Math.PI * 20000.0) * influence;
        sImm.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void T07c_ImmediateSettlement_IsLinearInLoad()
    {
        var gm = new GroundModel
        {
            WaterTableDepthM = 20.0,
            Layers =
            {
                new SoilLayer
                {
                    Name = "Sand",
                    ThicknessM = 5.0,
                    GammaKnm3 = 18.0,
                    ElasticModulusKpa = 20000.0
                }
            }
        };

        var sHalf = SettlementEngine.ImmediateSettlementMm(gm, new SettlementAnalysisInput { LoadKpa = 50.0 });
        var sDouble = SettlementEngine.ImmediateSettlementMm(gm, new SettlementAnalysisInput { LoadKpa = 100.0 });

        sDouble.Should().BeApproximately(2.0 * sHalf, 1e-9);
    }

    [Fact]
    public void T08_DoubleDrainage_QuartersTimeToConsolidation()
    {
        var gm = ConsolidationGround();
        var baseInput = new SettlementAnalysisInput { LoadKpa = 50.0, DurationYears = 10.0 };

        var single = new SettlementEngine().Compute(gm, baseInput with { DrainageMode = Drainage.Single });
        var dbl = new SettlementEngine().Compute(gm, baseInput with { DrainageMode = Drainage.Double });

        single.T90Days.Should().NotBeNull();
        dbl.T90Days.Should().NotBeNull();

        (dbl.T90Days!.Value / single.T90Days!.Value).Should().BeApproximately(0.25, 0.0125);
    }

    [Fact]
    public void Compute_ProducesMonotonicTimeSeries()
    {
        var gm = ConsolidationGround();
        var input = new SettlementAnalysisInput { LoadKpa = 50.0, DurationYears = 10.0, OutputPointCount = 20 };

        var result = new SettlementEngine().Compute(gm, input);

        result.TimeSeries.Should().HaveCount(20);
        for (var i = 1; i < result.TimeSeries.Count; i++)
        {
            result.TimeSeries[i].SettlementMm.Should().BeGreaterOrEqualTo(result.TimeSeries[i - 1].SettlementMm);
        }

        result.TotalMm.Should().BeGreaterThan(0.0);
    }
}
