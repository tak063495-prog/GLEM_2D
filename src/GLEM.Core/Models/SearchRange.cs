namespace GLEM.Core.Models;

public sealed record SearchRange(
    double CenterXMin,
    double CenterXMax,
    double CenterZMin,
    double CenterZMax,
    double RadiusMin,
    double RadiusMax);
