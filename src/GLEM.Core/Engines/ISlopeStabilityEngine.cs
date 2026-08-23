using GLEM.Core.Models;

namespace GLEM.Core.Engines;

public interface ISlopeStabilityEngine
{
    SlopeMethod Method { get; }

    SlopeCalculation Compute(IReadOnlyList<SliceGeometry> slices, GroundModel gm, AnalysisConditions conditions);
}
