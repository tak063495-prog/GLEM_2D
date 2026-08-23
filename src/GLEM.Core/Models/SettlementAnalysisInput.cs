namespace GLEM.Core.Models;

public sealed record SettlementAnalysisInput
{
    public double LoadKpa { get; init; }

    public double LoadedAreaB { get; init; } = 6.0;

    public double LoadedAreaL { get; init; } = 6.0;

    public Drainage DrainageMode { get; init; } = Drainage.Single;

    public double DurationYears { get; init; } = 10.0;

    public int OutputPointCount { get; init; } = 50;
}
