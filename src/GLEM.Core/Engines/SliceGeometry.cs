namespace GLEM.Core.Engines;

public sealed record SliceGeometry(
    int No,
    double XMid,
    double ZMid,
    double DeltaL,
    double AlphaRad,
    double WKnPerM);
