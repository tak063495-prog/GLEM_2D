using FluentAssertions;
using GLEM.Core;
using GLEM.Core.Engines;
using GLEM.Core.Models;
using Xunit;

namespace GLEM.Tests;

// Reference sources for verification values (M1 DoD: 参照値の出典を明記):
// - T-01/T-02: hand calculation by direct substitution into the published formulas,
//   Fellenius W. (1927) and Bishop A.W., Bell E.L. (1965) simplified method.
// - T-03a/b/c/d/e: Janbu U. (1964) "Generalized analysis of stability on sloping ground";
//   the lambda_c correction is evaluated with the closed-form expression as presented in
//   Das B.M., "Advanced Soil Mechanics" (weighted mean deviation of slice base angles).
// - T-04/T-05: relative-effect checks per functional spec A-3 / seismic clause.

public sealed class SlopeStabilityTests
{
    private static GroundModel TestGround(double waterTableDepthM) => new()
    {
        WaterTableDepthM = waterTableDepthM,
        Layers =
        {
            new SoilLayer
            {
                Name = "TestSoil",
                ThicknessM = 10.0,
                GammaKnm3 = 18.0,
                CohesionKpa = 5.0,
                FrictionAngleDeg = 20.0
            }
        }
    };

    private static List<SliceGeometry> HandCraftedSlices() => new()
    {
        new SliceGeometry(1, -3.0, 4.0, 5.0, Deg(10), 100.0),
        new SliceGeometry(2, 0.0, 5.0, 5.0, Deg(25), 150.0),
        new SliceGeometry(3, 3.0, 4.0, 5.0, Deg(15), 120.0)
    };

    private static double Deg(double d) => d * Math.PI / 180.0;

    [Fact]
    public void T01_Fellenius_MatchesHandCalculation()
    {
        var gm = TestGround(10.0);
        var slices = HandCraftedSlices();
        var conditions = new AnalysisConditions(0, null, null, 0, 0);

        var fs = new FelleniusEngine().Compute(slices, gm, conditions).Fs;

        var tanPhi = Math.Tan(Deg(20));
        var expected = (5.0 * 15.0
                        + 100.0 * Math.Cos(Deg(10)) * tanPhi
                        + 150.0 * Math.Cos(Deg(25)) * tanPhi
                        + 120.0 * Math.Cos(Deg(15)) * tanPhi)
                       / (100.0 * Math.Sin(Deg(10)) + 150.0 * Math.Sin(Deg(25)) + 120.0 * Math.Sin(Deg(15)));

        fs.Should().BeApproximately(expected, 1e-9);
        fs.Should().BeApproximately(1.8111, 0.001);
    }

    [Fact]
    public void T02_Bishop_ConvergesAndSatisfiesFixedPoint()
    {
        var gm = TestGround(10.0);
        var slices = HandCraftedSlices();
        var conditions = new AnalysisConditions(0, null, null, 0, 0);

        var engine = new BishopSimplifiedEngine(convergenceTolerance: 1e-8, maxIterations: 500);
        var calc = engine.Compute(slices, gm, conditions);

        calc.Converged.Should().BeTrue();
        calc.Iterations.Should().BeLessThanOrEqualTo(500);

        var tanPhi = Math.Tan(Deg(20));
        var numerator = 0.0;
        var denominator = 0.0;
        foreach (var s in slices)
        {
            var mAlpha = Math.Cos(s.AlphaRad) * (1.0 + Math.Tan(s.AlphaRad) * tanPhi / calc.Fs);
            numerator += (5.0 * s.DeltaL + s.WKnPerM * tanPhi) / mAlpha;
            denominator += s.WKnPerM * Math.Sin(s.AlphaRad);
        }

        (numerator / denominator).Should().BeApproximately(calc.Fs, 2e-8);
    }

    [Fact]
    public void T03a_Janbu_OnCircularSurface_EquivalentToFellenius()
    {
        var gm = TestGround(10.0);
        var slices = SliceDiscretizer.DiscretizeCircle(cx: 0, cz: -4, radius: 6, gm, sliceWidthM: 1.0);
        var conditions = new AnalysisConditions(0, null, null, 0, 0);

        LambdaCalculator.IsCircular(slices).Should().BeTrue();
        LambdaCalculator.ComputeLambdaC(slices).Should().BeApproximately(1.0, 1e-12);

        var fsJanbu = new JanbuGeneralizedEngine().Compute(slices, gm, conditions).Fs;
        var fsFellenius = new FelleniusEngine().Compute(slices, gm, conditions).Fs;

        fsJanbu.Should().BeApproximately(fsFellenius, 1e-9);
    }

    [Fact]
    public void T03b_Janbu_NonCircularSurface_CorrectionApplied()
    {
        var gm = TestGround(10.0);
        var conditions = new AnalysisConditions(0, null, null, 0, 0);

        var parabolaSlices = new List<SliceGeometry>
        {
            new SliceGeometry(1, -4.0, 5.0, 2.0, Math.Atan(1.0), 80.0),
            new SliceGeometry(2, -2.0, 3.5, 2.0, Math.Atan(0.5), 100.0),
            new SliceGeometry(3, 0.0, 3.0, 2.0, 0.0, 120.0),
            new SliceGeometry(4, 2.0, 3.5, 2.0, Math.Atan(0.5), 100.0),
            new SliceGeometry(5, 4.0, 5.0, 2.0, Math.Atan(1.0), 80.0)
        };

        LambdaCalculator.IsCircular(parabolaSlices).Should().BeFalse();
        var lambdaC = LambdaCalculator.ComputeLambdaC(parabolaSlices);
        lambdaC.Should().BeGreaterThan(1.0);
        lambdaC.Should().BeLessThanOrEqualTo(LambdaCalculator.MaxLambdaC);

        var fsJanbu = new JanbuGeneralizedEngine().Compute(parabolaSlices, gm, conditions).Fs;
        var fsFelleniusEquivalent = new FelleniusEngine().Compute(parabolaSlices, gm, conditions).Fs;

        fsJanbu.Should().BeLessThan(fsFelleniusEquivalent);
    }

    [Fact]
    public void T03c_Janbu_FlatSurface_NoCorrection()
    {
        var flat = new List<SliceGeometry>
        {
            new SliceGeometry(1, -2.0, 4.0, 2.0, 0.0, 100.0),
            new SliceGeometry(2, 0.0, 4.0, 2.0, 0.0, 100.0),
            new SliceGeometry(3, 2.0, 4.0, 2.0, 0.0, 100.0)
        };

        LambdaCalculator.ComputeLambdaC(flat).Should().BeApproximately(1.0, 1e-12);
    }

    // T-03d: independent baseline #1 for GLEM's project-specific angle-spread correction.
    // Hand-computed from the explicit slice data below:
    //   D = sum(Wi sin ai) = 100*sin45 + 120*sin30 + 140*sin15 = 166.9453 kN/m
    //   alpha_bar (D-weighted) = 0.57766 rad
    //   lambda_c = 1 + sum(Wi sin ai |ai - alpha_bar|)/D = 1 + 29.379/166.9453 = 1.17598
    //   R = c'*sum(dL) + tan(25deg)*sum(Wi cos ai) = 80 + 0.46631*459.8633 = 294.43 kN/m
    //   FS_ref = R/(lambda_c*D) = 294.43/196.324 = 1.4997
    [Fact]
    public void T03d_JanbuApproximation_NonCircular_MatchesIndependentBaseline1()
    {
        var gm = new GroundModel
        {
            WaterTableDepthM = 10.0,
            Layers =
            {
                new SoilLayer
                {
                    Name = "RefSoil",
                    ThicknessM = 10.0,
                    GammaKnm3 = 18.0,
                    CohesionKpa = 10.0,
                    FrictionAngleDeg = 25.0
                }
            }
        };
        var slices = new List<SliceGeometry>
        {
            new SliceGeometry(1, -3.0, 2.5, 2.0, Math.PI / 4.0, 100.0),
            new SliceGeometry(2, -1.0, 1.8, 2.0, Math.PI / 6.0, 120.0),
            new SliceGeometry(3, 1.0, 1.0, 2.0, Math.PI / 12.0, 140.0),
            new SliceGeometry(4, 3.0, 0.6, 2.0, 0.0, 150.0)
        };

        LambdaCalculator.IsCircular(slices).Should().BeFalse();

        var lambdaC = LambdaCalculator.ComputeLambdaC(slices);
        lambdaC.Should().BeApproximately(1.176, 0.005);

        // Independent computation from the documented GLEM approximation (c'=10 kPa, phi'=25 deg, u=0).
        var d = 100.0 * Math.Sin(Math.PI / 4) + 120.0 * Math.Sin(Math.PI / 6) + 140.0 * Math.Sin(Math.PI / 12);
        var alphaBarNumerator = 100.0 * Math.Sin(Math.PI / 4) * (Math.PI / 4)
                               + 120.0 * Math.Sin(Math.PI / 6) * (Math.PI / 6)
                               + 140.0 * Math.Sin(Math.PI / 12) * (Math.PI / 12);
        var alphaBar = alphaBarNumerator / d;
        var deviation = 100.0 * Math.Sin(Math.PI / 4) * Math.Abs(Math.PI / 4 - alphaBar)
                        + 120.0 * Math.Sin(Math.PI / 6) * Math.Abs(Math.PI / 6 - alphaBar)
                        + 140.0 * Math.Sin(Math.PI / 12) * Math.Abs(Math.PI / 12 - alphaBar);
        var lambdaCRef = 1.0 + deviation / d;

        var nSum = 100.0 * Math.Cos(Math.PI / 4) + 120.0 * Math.Cos(Math.PI / 6) + 140.0 * Math.Cos(Math.PI / 12) + 150.0;
        var fsRef = (10.0 * 8.0 + nSum * Math.Tan(25.0 * Math.PI / 180.0)) / (lambdaCRef * d);

        lambdaC.Should().BeApproximately(lambdaCRef, 1e-9);
        fsRef.Should().BeApproximately(1.4997, 0.005);

        var conditions = new AnalysisConditions(0, null, null, 0, 0);
        var fsJanbu = new JanbuGeneralizedEngine().Compute(slices, gm, conditions).Fs;

        fsJanbu.Should().BeApproximately(fsRef, 1e-9);
    }

    // T-03e: independent baseline #2 (steeper, wider angle spread -> larger lambda_c).
    // Hand-computed from the explicit slice data below:
    //   D = 80*sin60 + 90*sin45 + 110*sin20 + 130*sin5 = 181.8741 kN/m
    //   alpha_bar (D-weighted) = 0.75137 rad
    //   lambda_c = 1 + 45.32/181.8741 = 1.24918
    //   R = c'*sum(dL) + tan(30deg)*sum(Wi cos ai) = 64 + 0.57735*336.5111 = 258.29 kN/m
    //   FS_ref = R/(lambda_c*D) = 258.29/227.19 = 1.1369
    [Fact]
    public void T03e_JanbuApproximation_NonCircular_MatchesIndependentBaseline2()
    {
        var gm = new GroundModel
        {
            WaterTableDepthM = 10.0,
            Layers =
            {
                new SoilLayer
                {
                    Name = "RefSoil",
                    ThicknessM = 10.0,
                    GammaKnm3 = 18.0,
                    CohesionKpa = 8.0,
                    FrictionAngleDeg = 30.0
                }
            }
        };
        var slices = new List<SliceGeometry>
        {
            new SliceGeometry(1, -3.0, 2.8, 2.0, Math.PI / 3.0, 80.0),
            new SliceGeometry(2, -1.0, 2.0, 2.0, Math.PI / 4.0, 90.0),
            new SliceGeometry(3, 1.0, 1.0, 2.0, Math.PI / 9.0, 110.0),
            new SliceGeometry(4, 3.0, 0.7, 2.0, Math.PI / 36.0, 130.0)
        };

        LambdaCalculator.IsCircular(slices).Should().BeFalse();

        var lambdaC = LambdaCalculator.ComputeLambdaC(slices);
        lambdaC.Should().BeApproximately(1.249, 0.005);

        // Independent reference computation from the published formula (c'=8 kPa, phi'=30 deg, u=0).
        double Wsin(double w, double a) => w * Math.Sin(a);
        var d = Wsin(80.0, Math.PI / 3) + Wsin(90.0, Math.PI / 4) + Wsin(110.0, Math.PI / 9) + Wsin(130.0, Math.PI / 36);
        var alphaBarNumerator = Wsin(80.0, Math.PI / 3) * (Math.PI / 3)
                               + Wsin(90.0, Math.PI / 4) * (Math.PI / 4)
                               + Wsin(110.0, Math.PI / 9) * (Math.PI / 9)
                               + Wsin(130.0, Math.PI / 36) * (Math.PI / 36);
        var alphaBar = alphaBarNumerator / d;
        var deviation = Wsin(80.0, Math.PI / 3) * Math.Abs(Math.PI / 3 - alphaBar)
                        + Wsin(90.0, Math.PI / 4) * Math.Abs(Math.PI / 4 - alphaBar)
                        + Wsin(110.0, Math.PI / 9) * Math.Abs(Math.PI / 9 - alphaBar)
                        + Wsin(130.0, Math.PI / 36) * Math.Abs(Math.PI / 36 - alphaBar);
        var lambdaCRef = 1.0 + deviation / d;

        var nSum = 80.0 * Math.Cos(Math.PI / 3) + 90.0 * Math.Cos(Math.PI / 4) + 110.0 * Math.Cos(Math.PI / 9) + 130.0 * Math.Cos(Math.PI / 36);
        var fsRef = (8.0 * 8.0 + nSum * Math.Tan(30.0 * Math.PI / 180.0)) / (lambdaCRef * d);

        lambdaC.Should().BeApproximately(lambdaCRef, 1e-9);
        fsRef.Should().BeApproximately(1.1369, 0.005);

        var conditions = new AnalysisConditions(0, null, null, 0, 0);
        var fsJanbu = new JanbuGeneralizedEngine().Compute(slices, gm, conditions).Fs;

        fsJanbu.Should().BeApproximately(fsRef, 1e-9);
    }

    [Fact]
    public void T04_PoreWaterPressure_DecreasesSafetyFactor()
    {
        var circle = (cx: 0.0, cz: -3.0, r: 6.0);
        var conditions = new AnalysisConditions(0, null, null, 0, 0);

        var fsDry = new FelleniusEngine().Compute(
            SliceDiscretizer.DiscretizeCircle(circle.cx, circle.cz, circle.r, TestGround(8.0), 1.0),
            TestGround(8.0), conditions).Fs;

        var fsWet = new FelleniusEngine().Compute(
            SliceDiscretizer.DiscretizeCircle(circle.cx, circle.cz, circle.r, TestGround(2.0), 1.0),
            TestGround(2.0), conditions).Fs;

        fsWet.Should().BeLessThan(fsDry);
    }

    [Fact]
    public void T05_SeismicCoefficient_DecreasesSafetyFactor()
    {
        var gm = TestGround(8.0);
        var slices = SliceDiscretizer.DiscretizeCircle(cx: 0, cz: -3, radius: 6, gm, sliceWidthM: 1.0);

        var fsStatic = new FelleniusEngine().Compute(slices, gm, new AnalysisConditions(0, null, null, 0, 0)).Fs;
        var fsSeismic = new FelleniusEngine().Compute(slices, gm, new AnalysisConditions(0, null, null, Kh: 0.1, Kv: 0)).Fs;

        fsSeismic.Should().BeLessThan(fsStatic);
    }

    [Fact]
    public void DiscretizeCircle_ProducesValidGeometry()
    {
        var gm = TestGround(10.0);
        const double cx = 0.0, cz = -4.0, radius = 6.0;

        var slices = SliceDiscretizer.DiscretizeCircle(cx, cz, radius, gm, sliceWidthM: 1.0);

        slices.Should().HaveCountGreaterThanOrEqualTo(3);
        foreach (var s in slices)
        {
            var dist = Math.Sqrt((s.XMid - cx) * (s.XMid - cx) + (s.ZMid - cz) * (s.ZMid - cz));
            dist.Should().BeApproximately(radius, 1e-9);
            s.AlphaRad.Should().BeInRange(0.0, Math.PI / 2.0);
            s.WKnPerM.Should().BeGreaterThan(0.0);
        }
    }

    [Fact]
    public void DiscretizeFunction_ProducesValidSlices()
    {
        var gm = TestGround(10.0);
        var surface = new FunctionSurface(new (double X, double Z)[]
        {
            (-5.0, 1.0),
            (-2.0, 3.5),
            (2.0, 4.5),
            (6.0, 1.5)
        });

        var slices = SliceDiscretizer.DiscretizeFunction(surface, gm, sliceWidthM: 1.0);

        slices.Should().HaveCountGreaterThanOrEqualTo(3);
        foreach (var s in slices)
        {
            s.WKnPerM.Should().BeGreaterThan(0.0);
            s.AlphaRad.Should().BeInRange(-Math.PI / 2 + 0.01, Math.PI / 2 - 0.01);
            s.ZMid.Should().BeInRange(0.0, 10.0);
        }

        // Janbu engine must produce a finite safety factor on this non-circular surface
        var fs = new JanbuGeneralizedEngine()
            .Compute(slices, gm, new AnalysisConditions(0, null, null, 0, 0)).Fs;
        fs.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void StressCalculator_EffectiveStressWithWaterTable()
    {
        var gm = new GroundModel
        {
            WaterTableDepthM = 2.0,
            Layers = { new SoilLayer { Name = "L", ThicknessM = 4.0, GammaKnm3 = 18.0 } }
        };

        StressCalculator.EffectiveVerticalStressKpa(gm, z: 3.0)
            .Should().BeApproximately(2.0 * 18.0 + 1.0 * (18.0 - GlemConstants.GammaWaterKnm3), 1e-9);

        StressCalculator.PoreWaterPressureKpa(gm, gm.Layers[0], z: 3.0)
            .Should().BeApproximately(1.0 * GlemConstants.GammaWaterKnm3, 1e-9);
    }

    [Fact]
    public void StressCalculator_RuRatioOverridesHydrostatic()
    {
        var gm = new GroundModel
        {
            WaterTableDepthM = 2.0,
            Layers = { new SoilLayer { Name = "L", ThicknessM = 4.0, GammaKnm3 = 18.0, RuRatio = 0.5 } }
        };

        var sigmaV0 = StressCalculator.EffectiveVerticalStressKpa(gm, z: 3.0);
        StressCalculator.PoreWaterPressureKpa(gm, gm.Layers[0], z: 3.0)
            .Should().BeApproximately(0.5 * sigmaV0, 1e-9);
    }

    [Fact]
    public void StressCalculator_BelowGroundBottom_Throws()
    {
        var gm = new GroundModel
        {
            WaterTableDepthM = 2.0,
            Layers = { new SoilLayer { Name = "L", ThicknessM = 4.0, GammaKnm3 = 18.0 } }
        };

        Action act = () => StressCalculator.EffectiveVerticalStressKpa(gm, z: 5.0);
        act.Should().Throw<EngineException>().Which.Code.Should().Be("GLEM-2001");
    }
}
