using FluentAssertions;
using GLEM.Core;
using GLEM.Core.IO;
using GLEM.Core.Models;
using Xunit;

namespace GLEM.Tests;

public sealed class ProjectFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"glem-test-{Guid.NewGuid():N}.glem");

    [Fact]
    public void T11_RoundTrip_PreservesAllFields()
    {
        var data = new ProjectData
        {
            FormatVersion = "1.0",
            ProjectName = "TestProject",
            CreatedAt = new DateTime(2026, 8, 22, 9, 0, 0, DateTimeKind.Local),
            UpdatedAt = null,
            GroundModel = new GroundModel
            {
                WaterTableDepthM = 5.0,
                Layers =
                {
                    new SoilLayer
                    {
                        Name = "Sand",
                        ThicknessM = 3.0,
                        GammaKnm3 = 18.0,
                        CohesionKpa = 0.0,
                        FrictionAngleDeg = 32.0,
                        PermeabilityMs = 1e-4,
                        InitialVoidRatio = 0.75,
                        CompressionIndexCc = 0.25,
                        RecompressionIndexCr = null,
                        SecondaryCompressionIndexCs = 0.02,
                        PreconsolidationPressureKpa = null,
                        ElasticModulusKpa = 15000.0,
                        PoissonRatio = null,
                        RuRatio = 0.3
                    }
                }
            },
            SlopeAnalysis = new SlopeAnalysisInput
            {
                Method = SlopeMethod.JanbuGeneralized,
                SliceWidthM = 1.5,
                SurchargeKpa = 20.0,
                SurchargeStartX = -3.0,
                SurchargeEndX = 3.0,
                Kh = 0.05,
                Kv = 0.02,
                SearchRange = new SearchRange(-10, 10, -8, 4, 3, 15)
            },
            SettlementAnalysis = new SettlementAnalysisInput
            {
                LoadKpa = 100.0,
                LoadedAreaB = 8.0,
                LoadedAreaL = 6.0,
                DrainageMode = Drainage.Double,
                DurationYears = 25.0,
                OutputPointCount = 100
            }
        };

        GlemProjectFile.Save(_path, data);
        var loaded = GlemProjectFile.Load(_path);

        loaded.ProjectName.Should().Be(data.ProjectName);
        loaded.FormatVersion.Should().Be("1.0");
        loaded.CreatedAt.Should().Be(data.CreatedAt);
        loaded.GroundModel.WaterTableDepthM.Should().Be(5.0);

        var layer = loaded.GroundModel.Layers.Single();
        layer.Name.Should().Be("Sand");
        layer.ThicknessM.Should().Be(3.0);
        layer.GammaKnm3.Should().Be(18.0);
        layer.FrictionAngleDeg.Should().Be(32.0);
        layer.PermeabilityMs.Should().Be(1e-4);
        layer.InitialVoidRatio.Should().Be(0.75);
        layer.CompressionIndexCc.Should().Be(0.25);
        layer.RecompressionIndexCr.Should().BeNull();
        layer.SecondaryCompressionIndexCs.Should().Be(0.02);
        layer.ElasticModulusKpa.Should().Be(15000.0);
        layer.RuRatio.Should().Be(0.3);

        var slope = loaded.SlopeAnalysis!;
        slope.Method.Should().Be(SlopeMethod.JanbuGeneralized);
        slope.SliceWidthM.Should().Be(1.5);
        slope.SurchargeKpa.Should().Be(20.0);
        slope.SurchargeStartX.Should().Be(-3.0);
        slope.Kh.Should().Be(0.05);
        slope.SearchRange!.RadiusMax.Should().Be(15.0);

        var settlement = loaded.SettlementAnalysis!;
        settlement.LoadKpa.Should().Be(100.0);
        settlement.LoadedAreaB.Should().Be(8.0);
        settlement.DrainageMode.Should().Be(Drainage.Double);
        settlement.DurationYears.Should().Be(25.0);
        settlement.OutputPointCount.Should().Be(100);
    }

    [Fact]
    public void Load_MajorVersionMismatch_RaisesGlem3001()
    {
        File.WriteAllText(_path, """{"format_version":"2.0","project_name":"x","ground_model":{"water_table_depth_m":0,"layers":[]}}""");

        Action act = () => GlemProjectFile.Load(_path);

        act.Should().Throw<ProjectFileException>().Which.Code.Should().Be("GLEM-3001");
    }

    [Fact]
    public void Load_MajorVersionMismatch_AllowNewerMajor_LoadsSuccessfully()
    {
        // R-3.1.5: ユーザー確認後の読み込み（allowNewerMajor=true）
        File.WriteAllText(_path, """{"format_version":"2.0","project_name":"x","ground_model":{"water_table_depth_m":0,"layers":[{"name":"L","thickness_m":2,"gamma_kn_m3":18,"c_kpa":0,"phi_deg":30}]}}""");

        var loaded = GlemProjectFile.Load(_path, allowNewerMajor: true);

        loaded.FormatVersion.Should().Be("2.0");
        loaded.GroundModel.Layers.Should().HaveCount(1);
    }

    [Fact]
    public void Load_MinorVersionDifference_LoadsSuccessfully()
    {
        File.WriteAllText(_path, """{"format_version":"1.1","project_name":"x","ground_model":{"water_table_depth_m":0,"layers":[{"name":"L","thickness_m":2,"gamma_kn_m3":18,"c_kpa":0,"phi_deg":30}]}}""");

        var loaded = GlemProjectFile.Load(_path);

        loaded.FormatVersion.Should().Be("1.1");
        loaded.GroundModel.Layers.Should().HaveCount(1);
    }

    [Fact]
    public void Load_MalformedJson_RaisesGlem3002()
    {
        File.WriteAllText(_path, "{ invalid json ");

        Action act = () => GlemProjectFile.Load(_path);

        act.Should().Throw<ProjectFileException>().Which.Code.Should().Be("GLEM-3002");
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
