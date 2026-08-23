using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public sealed record AnalysisConditions(
    double SurchargeKpa,
    double? SurchargeStartX,
    double? SurchargeEndX,
    double Kh,
    double Kv)
{
    public static AnalysisConditions FromInput(SlopeAnalysisInput input) => new(
        input.SurchargeKpa,
        input.SurchargeStartX,
        input.SurchargeEndX,
        input.Kh,
        input.Kv);

    public double SurchargeAt(double x)
    {
        if (SurchargeKpa <= 0.0)
        {
            return 0.0;
        }

        if (SurchargeStartX is double sx && SurchargeEndX is double ex)
        {
            return x >= sx && x <= ex ? SurchargeKpa : 0.0;
        }

        return SurchargeKpa;
    }
}

public sealed record SliceComputation(
    SliceGeometry Geometry,
    double UKpa,
    double NpKnPerM,
    double CTermKnPerM,
    double PhiTermKnPerM);

public sealed record SlopeCalculation(
    double Fs,
    bool Converged,
    int Iterations,
    IReadOnlyList<SliceComputation> Slices);
