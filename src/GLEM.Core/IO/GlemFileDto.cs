namespace GLEM.Core.IO;

internal sealed record GlemFileDto(
    string format_version,
    string project_name,
    DateTime? created_at,
    DateTime? updated_at,
    GroundModelDto ground_model,
    SlopeAnalysisDto? slope_analysis,
    SettlementAnalysisDto? settlement_analysis);

internal sealed record GroundModelDto(
    double water_table_depth_m,
    List<LayerDto> layers);

internal sealed record LayerDto(
    string name,
    double thickness_m,
    double gamma_kn_m3,
    double c_kpa,
    double phi_deg,
    double? k_m_s,
    double? e0,
    double? cc,
    double? cr,
    double? cs,
    double? sigma_pc_kpa,
    double? es_kpa,
    double? poisson_ratio,
    double? ru_ratio);

internal sealed record SlopeAnalysisDto(
    string method,
    double slice_width_m,
    double surcharge_kpa,
    double? surcharge_start_x,
    double? surcharge_end_x,
    double kh,
    double kv,
    SearchRangeDto? search_range);

internal sealed record SearchRangeDto(
    double center_x_min,
    double center_x_max,
    double center_z_min,
    double center_z_max,
    double radius_min,
    double radius_max);

internal sealed record SettlementAnalysisDto(
    double load_kpa,
    List<double> loaded_area_m,
    string drainage,
    double duration_years,
    int output_point_count);
