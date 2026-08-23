namespace GLEM.Core.Models;

public sealed record SlopeAnalysisInput
{
    public SlopeMethod Method { get; init; } = SlopeMethod.BishopSimplified;

    public double SliceWidthM { get; init; } = 1.0;

    public double SurchargeKpa { get; init; }

    public double? SurchargeStartX { get; init; }

    public double? SurchargeEndX { get; init; }

    public double Kh { get; init; }

    public double Kv { get; init; }

    public SearchRange? SearchRange { get; init; }

    public double ConvergenceTolerance { get; init; } = 0.001;

    public int MaxIterations { get; init; } = 200;

    public double CoarseGridStepM { get; init; } = 2.0;

    public double LocalStepM { get; init; } = 0.5;
}
