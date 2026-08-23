using FluentAssertions;
using GLEM.Core.Models;
using GLEM.Core.Validation;
using Xunit;

namespace GLEM.Tests;

public sealed class ValidationTests
{
    private static GroundModel ValidGround() => new()
    {
        WaterTableDepthM = 5.0,
        Layers =
        {
            new SoilLayer
            {
                Name = "TopSoil",
                ThicknessM = 3.0,
                GammaKnm3 = 18.0,
                CohesionKpa = 0.0,
                FrictionAngleDeg = 32.0
            },
            new SoilLayer
            {
                Name = "Clay",
                ThicknessM = 7.0,
                GammaKnm3 = 16.5,
                CohesionKpa = 15.0,
                FrictionAngleDeg = 18.0
            }
        }
    };

    [Fact]
    public void T09a_NoLayers_RaisesGlem1001()
    {
        var gm = new GroundModel();

        var issues = GroundModelValidator.Validate(gm);

        issues.Should().ContainSingle(i => i.Code == "GLEM-1001");
        issues.First(i => i.Code == "GLEM-1001").Message.Should().Be("No ground layers are defined");
    }

    [Fact]
    public void T09b_NonPositiveThickness_RaisesGlem1002()
    {
        var gm = ValidGround();
        gm.Layers[0].ThicknessM = 0.0;

        var issue = GroundModelValidator.Validate(gm).First(i => i.Code == "GLEM-1002");

        issue.Message.Should().Contain("TopSoil");
    }

    [Fact]
    public void T09c_FrictionAngleOutOfRange_RaisesGlem1003()
    {
        var gm = ValidGround();
        gm.Layers[1].FrictionAngleDeg = 50.0;

        GroundModelValidator.Validate(gm).Should().Contain(i => i.Code == "GLEM-1003");
    }

    [Fact]
    public void T09d_GammaOutOfRange_RaisesGlem1004()
    {
        var gm = ValidGround();
        gm.Layers[0].GammaKnm3 = 40.0;

        GroundModelValidator.Validate(gm).Should().Contain(i => i.Code == "GLEM-1004");
    }

    [Fact]
    public void T09e_WaterTableBelowGroundBottom_RaisesGlem1005()
    {
        var gm = ValidGround();
        gm.WaterTableDepthM = 20.0;

        GroundModelValidator.Validate(gm).Should().Contain(i => i.Code == "GLEM-1005");
    }

    [Fact]
    public void T09f_SetttlementMissingProperties_RaisesGlem1006()
    {
        var gm = ValidGround();

        var issues = AnalysisInputValidator.ValidateSettlement(gm, new SettlementAnalysisInput { LoadKpa = 50.0 });

        issues.Should().Contain(i => i.Code == "GLEM-1006" && i.Message.Contains("TopSoil"));
    }

    [Fact]
    public void T09g_NegativeSurcharge_RaisesGlem1007()
    {
        var gm = ValidGround();

        AnalysisInputValidator.ValidateSlope(gm, new SlopeAnalysisInput { SurchargeKpa = -5.0 })
            .Should().Contain(i => i.Code == "GLEM-1007");

        AnalysisInputValidator.ValidateSettlement(gm, new SettlementAnalysisInput { LoadKpa = 0.0 })
            .Should().Contain(i => i.Code == "GLEM-1007");
    }

    [Fact]
    public void T09h_CohesionlessLayer_RaisesGlem1008Warning()
    {
        var gm = ValidGround();
        gm.Layers[0].CohesionKpa = 0.0;
        gm.Layers[0].FrictionAngleDeg = 0.0;

        var issue = GroundModelValidator.Validate(gm).First(i => i.Code == "GLEM-1008");

        issue.IsWarning.Should().BeTrue();
    }

    [Fact]
    public void ValidGround_ProducesNoErrors()
    {
        GroundModelValidator.Validate(ValidGround())
            .Should().NotContain(i => !i.IsWarning);
    }
}
