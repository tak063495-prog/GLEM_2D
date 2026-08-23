using GLEM.Core.Models;

namespace GLEM.Core.IO;

public sealed class ProjectData
{
    public string FormatVersion { get; set; } = "1.0";

    public string ProjectName { get; set; } = "";

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public GroundModel GroundModel { get; set; } = new();

    public SlopeAnalysisInput? SlopeAnalysis { get; set; }

    public SettlementAnalysisInput? SettlementAnalysis { get; set; }
}
