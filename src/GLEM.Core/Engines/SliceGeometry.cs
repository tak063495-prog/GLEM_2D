namespace GLEM.Core.Engines;

public sealed record SliceGeometry(
    int No,
    double XMid,
    double ZMid,
    double DeltaL,
    double AlphaRad,
    double WKnPerM)
{
    // DeltaL is base length; vertical slices use its horizontal projection for area/load/Bishop.
    public double HorizontalWidthM { get; init; } = DeltaL * Math.Cos(AlphaRad);
}
