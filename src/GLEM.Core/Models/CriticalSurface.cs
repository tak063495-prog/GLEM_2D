namespace GLEM.Core.Models;

public abstract record CriticalSurface;

public sealed record CircleSurface(double CenterX, double CenterZ, double Radius) : CriticalSurface;

public sealed record FunctionSurface(IReadOnlyList<(double X, double Z)> ControlPoints) : CriticalSurface;
