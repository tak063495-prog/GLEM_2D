using GLEM.Core;
using GLEM.Core.Engines;
using GLEM.Core.Models;
using GLEM.Core.Validation;
using Xunit;

namespace GLEM.Tests;

public sealed class ReviewRegressionTests
{
    private static SoilLayer Layer(double thickness = 10, double gamma = 18) => new()
    {
        ThicknessM = thickness, GammaKnm3 = gamma, CohesionKpa = 10, FrictionAngleDeg = 30,
        PermeabilityMs = 1e-8, InitialVoidRatio = 1, CompressionIndexCc = .3,
        SecondaryCompressionIndexCs = .02, ElasticModulusKpa = 20000, PoissonRatio = .3
    };
    private static GroundModel Ground(params SoilLayer[] layers) => new() { Layers = layers.ToList(), WaterTableDepthM = 0 };

    [Fact]
    public void SymmetricCircle_HasSignedTangentAndZeroStaticDriving()
    {
        var gm = Ground(Layer());
        var slices = SliceDiscretizer.DiscretizeCircle(0, -4, 6, gm, 1);
        foreach (var s in slices)
        {
            // z = cz + sqrt(r^2 - (x-cx)^2), hence dz/dx = -(x-cx)/(z-cz).
            Assert.Equal(Math.Atan2(-s.XMid, s.ZMid + 4), s.AlphaRad, 12);
        }
        Assert.InRange(Math.Abs(slices.Sum(s => s.WKnPerM * Math.Sin(s.AlphaRad))), 0, 1e-10);
        Assert.Throws<EngineException>(() => new FelleniusEngine().Compute(slices, gm, new(0, null, null, 0, 0)));
        Assert.Throws<EngineException>(() => new BishopSimplifiedEngine().Compute(slices, gm, new(0, null, null, 0, 0)));
    }

    [Fact]
    public void SlopingBaseWeight_IsHorizontalLayerAreaTimesGamma()
    {
        var gm = Ground(Layer());
        var surface = new FunctionSurface(new (double X, double Z)[] { (0, 1), (2, 3) });
        var slices = SliceDiscretizer.DiscretizeFunction(surface, gm, .5);
        // Integral from x=0 to 2 of 18*(1+x) dx = 72 kN/m.
        Assert.Equal(72, slices.Sum(s => s.WKnPerM), 10);
        var layered = Ground(Layer(2, 10), Layer(8, 20));
        var layeredSlices = SliceDiscretizer.DiscretizeFunction(surface, layered, .5);
        // Upper layer area = 3.5 m2, lower layer area = .5 m2.
        Assert.Equal(45, layeredSlices.Sum(s => s.WKnPerM), 10);
    }

    [Fact]
    public void BishopPureCohesion_HorizontalWidthGivesClosedFormFs()
    {
        var gm = Ground(Layer()); gm.Layers[0].FrictionAngleDeg = 0;
        var alpha = Math.PI / 6;
        var slices = new[] { new SliceGeometry(1, 0, 1, 2, alpha, 100) };
        var calc = new BishopSimplifiedEngine(1e-12).Compute(slices, gm, new(0, null, null, 0, 0));
        // b=l*cos(alpha); Fs=(c*b/cos(alpha))/(W*sin(alpha))=20/50=.4.
        Assert.Equal(.4, calc.Fs, 12);
    }

    [Fact]
    public void Surcharge_IsLoadTimesHorizontalWidth()
    {
        var gm = Ground(Layer()); gm.WaterTableDepthM = 10; gm.Layers[0].FrictionAngleDeg = 0;
        var alpha = Math.PI / 3;
        var calc = new FelleniusEngine().Compute(new[] { new SliceGeometry(1, 0, 1, 2, alpha, 100) }, gm, new(20, null, null, 0, 0));
        Assert.Equal(20 / (120 * Math.Sin(alpha)), calc.Fs, 12);
    }

    [Fact]
    public void ImmediateSettlement_HomogeneousSplitIsInvariant()
    {
        var input = new SettlementAnalysisInput { LoadKpa = 100 };
        var whole = SettlementEngine.ImmediateSettlementMm(Ground(Layer()), input);
        var split = SettlementEngine.ImmediateSettlementMm(Ground(Layer(5), Layer(5)), input);
        // One elastic half-space solution, 4B*ln(1+sqrt(2)), in metres despite legacy method name.
        var reference = 100 * .91 / (Math.PI * 20000) * 24 * Math.Log(1 + Math.Sqrt(2));
        Assert.Equal(reference, whole, 12);
        Assert.Equal(whole, split, 12);
        Assert.Equal(0, SettlementEngine.ImmediateSettlementMm(Ground(Layer(0)), input));
    }

    [Fact]
    public void ImmediateSettlement_HeterogeneousElasticHalfSpaceIsExplicitlyUnsupported()
    {
        var gm = Ground(Layer(5), Layer(5)); gm.Layers[1].ElasticModulusKpa = 10000;
        Assert.Throws<EngineException>(() => SettlementEngine.ImmediateSettlementMm(gm, new() { LoadKpa = 100 }));
    }

    [Fact]
    public void SecondarySettlement_PastValueIndependentOfOutputDurationAndLayerTc()
    {
        var fast = Layer(2); fast.PermeabilityMs = 1e-6;
        var slow = Layer(8); slow.PermeabilityMs = 1e-12;
        var gm = Ground(fast, slow);
        var shortRun = new SettlementEngine().Compute(gm, new() { LoadKpa = 100, DurationYears = 1, OutputPointCount = 2 });
        var longRun = new SettlementEngine().Compute(gm, new() { LoadKpa = 100, DurationYears = 100, OutputPointCount = 2 });
        Assert.Equal(shortRun.TimeSeries[0].SecondaryMm, longRun.TimeSeries[0].SecondaryMm, 10);
        Assert.True(shortRun.SecondaryMm > 0); // Fast layer already consolidates; slow layer must not delay it.
    }

    [Fact]
    public void BishopZeroResistance_IsFiniteAndConverged()
    {
        var gm = Ground(Layer()); gm.Layers[0].CohesionKpa = 0; gm.Layers[0].FrictionAngleDeg = 0;
        var calc = new BishopSimplifiedEngine().Compute(new[] { new SliceGeometry(1, 0, 1, 1, .3, 100) }, gm, new(0, null, null, 0, 0));
        Assert.Equal(0, calc.Fs); Assert.True(calc.Converged);
    }

    [Fact]
    public void SearchRejectsAllUnconvergedCandidates()
    {
        var gm = Ground(Layer());
        var error = Assert.Throws<EngineException>(() => new SlipSurfaceSearcher().Search(gm,
            new() { Method = SlopeMethod.BishopSimplified, Kh = .1, MaxIterations = 1, ConvergenceTolerance = 1e-14,
                SearchRange = new(-1, 1, -4, -4, 6, 6) }));
        Assert.Contains("converged", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationRejectsNonfiniteAndUnsafeFields()
    {
        var gm = Ground(Layer()); gm.Layers[0].PermeabilityMs = -1; gm.Layers[0].InitialVoidRatio = -1;
        gm.Layers[0].PoissonRatio = .5; gm.Layers[0].GammaKnm3 = double.NaN;
        var fields = GroundModelValidator.Validate(gm).Where(x => !x.IsWarning).Select(x => x.FieldName).ToList();
        Assert.Contains("k_m_s", fields); Assert.Contains("e0", fields); Assert.Contains("nu", fields); Assert.Contains("gamma_kn_m3", fields);
        var settlement = AnalysisInputValidator.ValidateSettlement(gm, new() { LoadKpa = 100, LoadedAreaB = 0, DurationYears = double.NaN });
        Assert.Contains(settlement, x => x.FieldName == "loaded_area_b");
        Assert.Contains(settlement, x => x.FieldName == "duration_years");
        var slope = AnalysisInputValidator.ValidateSlope(gm, new() { Kh = double.NaN, SearchRange = new(1, -1, -4, -2, 6, 5) });
        Assert.Contains(slope, x => x.FieldName == "kh"); Assert.Contains(slope, x => x.FieldName == "search_range");
        Assert.Throws<EngineException>(() => new SettlementEngine().Compute(gm, new() { LoadKpa = 100, LoadedAreaB = 0 }));
    }
}
