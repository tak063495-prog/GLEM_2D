using System.Text.Json;
using GLEM.Core.Models;

namespace GLEM.Core.IO;

public static class GlemProjectFile
{
    public const string CurrentFormatVersion = "1.0";
    private const int SupportedMajorVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static void Save(string path, ProjectData data)
    {
        var dto = ToDto(data);
        File.WriteAllText(path, JsonSerializer.Serialize(dto, Options));
    }

    public static ProjectData Load(string path) => Load(path, allowNewerMajor: false);

    // R-3.1.5: より新しい major バージョンのファイルは確認後に読み込み可能（allowNewerMajor=true）
    public static ProjectData Load(string path, bool allowNewerMajor)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProjectFileException("GLEM-3002", $"JSON format is invalid ({path})");
        }

        GlemFileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<GlemFileDto>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ProjectFileException("GLEM-3002", $"JSON format is invalid ({ex.Message})");
        }

        if (dto is null || dto.ground_model is null)
        {
            throw new ProjectFileException("GLEM-3002", "JSON format is invalid (missing ground model)");
        }

        CheckVersion(dto.format_version, allowNewerMajor);
        return FromDto(dto);
    }

    private static void CheckVersion(string version, bool allowNewerMajor)
    {
        var parts = version.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var major))
        {
            throw new ProjectFileException("GLEM-3002", $"JSON format is invalid (format_version: {version})");
        }

        if (major > SupportedMajorVersion && !allowNewerMajor)
        {
            throw new ProjectFileException("GLEM-3001", $"The file version ({version}) is not supported. Do you want to load it?");
        }
    }

    private static GlemFileDto ToDto(ProjectData data) => new(
        data.FormatVersion,
        data.ProjectName,
        data.CreatedAt,
        data.UpdatedAt,
        new GroundModelDto(
            data.GroundModel.WaterTableDepthM,
            data.GroundModel.Layers.Select(l => new LayerDto(
                l.Name,
                l.ThicknessM,
                l.GammaKnm3,
                l.CohesionKpa,
                l.FrictionAngleDeg,
                l.PermeabilityMs,
                l.InitialVoidRatio,
                l.CompressionIndexCc,
                l.RecompressionIndexCr,
                l.SecondaryCompressionIndexCs,
                l.PreconsolidationPressureKpa,
                l.ElasticModulusKpa,
                l.PoissonRatio,
                l.RuRatio)).ToList()),
        data.SlopeAnalysis is { } slope ? new SlopeAnalysisDto(
            MethodName(slope.Method),
            slope.SliceWidthM,
            slope.SurchargeKpa,
            slope.SurchargeStartX,
            slope.SurchargeEndX,
            slope.Kh,
            slope.Kv,
            slope.SearchRange is { } range ? new SearchRangeDto(
                range.CenterXMin, range.CenterXMax, range.CenterZMin, range.CenterZMax, range.RadiusMin, range.RadiusMax) : null) : null,
        data.SettlementAnalysis is { } settlement ? new SettlementAnalysisDto(
            settlement.LoadKpa,
            new List<double> { settlement.LoadedAreaB, settlement.LoadedAreaL },
            DrainageName(settlement.DrainageMode),
            settlement.DurationYears,
            settlement.OutputPointCount) : null);

    private static ProjectData FromDto(GlemFileDto dto) => new()
    {
        FormatVersion = dto.format_version,
        ProjectName = dto.project_name ?? "",
        CreatedAt = dto.created_at,
        UpdatedAt = dto.updated_at,
        GroundModel = new GroundModel
        {
            WaterTableDepthM = dto.ground_model.water_table_depth_m,
            Layers = dto.ground_model.layers.Select(l => new SoilLayer
            {
                Name = l.name ?? "",
                ThicknessM = l.thickness_m,
                GammaKnm3 = l.gamma_kn_m3,
                CohesionKpa = l.c_kpa,
                FrictionAngleDeg = l.phi_deg,
                PermeabilityMs = l.k_m_s,
                InitialVoidRatio = l.e0,
                CompressionIndexCc = l.cc,
                RecompressionIndexCr = l.cr,
                SecondaryCompressionIndexCs = l.cs,
                PreconsolidationPressureKpa = l.sigma_pc_kpa,
                ElasticModulusKpa = l.es_kpa,
                PoissonRatio = l.poisson_ratio,
                RuRatio = l.ru_ratio
            }).ToList()
        },
        SlopeAnalysis = dto.slope_analysis is { } s ? new SlopeAnalysisInput
        {
            Method = ParseMethod(s.method),
            SliceWidthM = s.slice_width_m,
            SurchargeKpa = s.surcharge_kpa,
            SurchargeStartX = s.surcharge_start_x,
            SurchargeEndX = s.surcharge_end_x,
            Kh = s.kh,
            Kv = s.kv,
            SearchRange = s.search_range is { } r ? new SearchRange(
                r.center_x_min, r.center_x_max, r.center_z_min, r.center_z_max, r.radius_min, r.radius_max) : null
        } : null,
        SettlementAnalysis = dto.settlement_analysis is { } st ? new SettlementAnalysisInput
        {
            LoadKpa = st.load_kpa,
            LoadedAreaB = st.loaded_area_m.Count > 0 ? st.loaded_area_m[0] : 6.0,
            LoadedAreaL = st.loaded_area_m.Count > 1 ? st.loaded_area_m[1] : 6.0,
            DrainageMode = ParseDrainage(st.drainage),
            DurationYears = st.duration_years,
            OutputPointCount = st.output_point_count
        } : null
    };

    private static string MethodName(SlopeMethod method) => method switch
    {
        SlopeMethod.Fellenius => "fellenius",
        SlopeMethod.BishopSimplified => "bishop_simplified",
        SlopeMethod.JanbuGeneralized => "janbu_generalized",
        _ => throw new ProjectFileException("GLEM-3002", $"Unknown analysis method: {method}")
    };

    private static SlopeMethod ParseMethod(string name) => name switch
    {
        "fellenius" => SlopeMethod.Fellenius,
        "bishop_simplified" => SlopeMethod.BishopSimplified,
        "janbu_generalized" => SlopeMethod.JanbuGeneralized,
        _ => throw new ProjectFileException("GLEM-3002", $"Unknown analysis method: {name}")
    };

    private static string DrainageName(Drainage drainage) => drainage == Drainage.Double ? "double" : "single";

    private static Drainage ParseDrainage(string name) => name switch
    {
        "double" => Drainage.Double,
        _ => Drainage.Single
    };
}
