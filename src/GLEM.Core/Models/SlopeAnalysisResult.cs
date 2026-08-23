namespace GLEM.Core.Models;

public sealed record SliceResult(
    int SliceNo,
    double X,
    double Z,
    double WKnPerM,
    double AlphaDeg,
    double UKpa,
    double NpKnPerM,
    double CTermKnPerM,
    double PhiTermKnPerM);

public sealed record SlopeAnalysisResult(
    double MinFs,
    SlopeMethod Method,
    CriticalSurface CriticalSurface,
    IReadOnlyList<SliceResult> Slices,
    bool Converged,
    int Iterations);
