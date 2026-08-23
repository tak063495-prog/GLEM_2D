namespace GLEM.Core.Models;

// ImmediateMm/PrimaryMm/SecondaryMm は各時刻 t における内訳（R-6.2.1 の積み上げ表示用）。
public sealed record SettlementTimePoint(
    double TimeDays,
    double UPercent,
    double SettlementMm,
    double ImmediateMm,
    double PrimaryMm,
    double SecondaryMm);

public sealed record SettlementAnalysisResult(
    double TotalMm,
    double ImmediateMm,
    double PrimaryMm,
    double SecondaryMm,
    IReadOnlyList<SettlementTimePoint> TimeSeries,
    double? T50Days,
    double? T90Days);
