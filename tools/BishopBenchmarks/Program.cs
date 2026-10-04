using System.Diagnostics;
using System.Text.Json;
using GLEM.Core;
using GLEM.Core.Engines;
using GLEM.Core.Models;

// The reference engine is the corrected pre-performance implementation at commit 6952478.
// This harness deliberately compares every result field and failure/convergence state, without relaxed tolerances.
var gm = new GroundModel { WaterTableDepthM = 10, Layers = { new SoilLayer { ThicknessM = 10, GammaKnm3 = 18, CohesionKpa = 5, FrictionAngleDeg = 20 } } };
var conditions = new AnalysisConditions(10, null, null, .1, 0);
var evidence = new List<object>();
foreach (var n in new[] { 3, 100, 1000 })
{
    var slices = Enumerable.Range(0, n).Select(i => new SliceGeometry(i+1, i, 5, 1, .1 + .4 * i / n, 100 + i % 13)).ToList();
    var baseline = new BishopBaselineEngine(1e-10, 200);
    var current = new BishopSimplifiedEngine(1e-10, 200);
    // Force enough calls for stable, warmed .NET execution; report first calls separately.
    var coldBase = TimeOne(() => baseline.Compute(slices, gm, conditions));
    var coldCurrent = TimeOne(() => current.Compute(slices, gm, conditions));
    Compare(baseline, current, slices, gm, conditions);
    for (var i=0; i<100; i++) { baseline.Compute(slices, gm, conditions); current.Compute(slices, gm, conditions); }
    var repeat = Math.Clamp(100000 / n, 100, 5000);
    var before = new List<double>(); var after = new List<double>();
    long beforeBytes=0, afterBytes=0;
    for (var round=0; round<9; round++)
    {
        // Alternate order to reduce temperature/clock bias.
        if (round % 2 == 0) { Measure(baseline, before, ref beforeBytes); Measure(current, after, ref afterBytes); }
        else { Measure(current, after, ref afterBytes); Measure(baseline, before, ref beforeBytes); }
    }
    void Measure(ISlopeStabilityEngine engine, List<double> timings, ref long bytes)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var allocation = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        for (var i=0; i<repeat; i++) engine.Compute(slices, gm, conditions);
        watch.Stop(); bytes = (GC.GetAllocatedBytesForCurrentThread()-allocation) / repeat;
        timings.Add(watch.Elapsed.TotalMilliseconds / repeat);
    }
    before.Sort(); after.Sort();
    var result = current.Compute(slices, gm, conditions);
    evidence.Add(new { slices=n, repeat, rounds=9, initial_case_includes_jit=n==3, first_reference_ms=coldBase, first_current_ms=coldCurrent,
        reference_median_ms=before[4], current_median_ms=after[4], speedup=before[4]/after[4],
        reference_bytes=beforeBytes, current_bytes=afterBytes, fs=result.Fs, result.Converged, result.Iterations, exact_all_fields=true });
}
// Nonconverged, zero resistance, and invalid static-driving cases must preserve their state.
var one = new[] { new SliceGeometry(1, 0, 5, 1, .3, 100) };
Compare(new BishopBaselineEngine(1e-14, 1), new BishopSimplifiedEngine(1e-14, 1), one, gm, conditions);
gm.Layers[0].CohesionKpa=0; gm.Layers[0].FrictionAngleDeg=0;
Compare(new BishopBaselineEngine(), new BishopSimplifiedEngine(), one, gm, conditions);
var symmetric = new[] { one[0] with { AlphaRad=-.3 }, one[0] };
Compare(new BishopBaselineEngine(), new BishopSimplifiedEngine(), symmetric, gm, new(0,null,null,0,0));
var random = new Random(4242);
for (var sample=0; sample<150; sample++)
{
    var model = new GroundModel { WaterTableDepthM=sample % 11, Layers = {
        new SoilLayer { ThicknessM=4, GammaKnm3=18, CohesionKpa=sample%15, FrictionAngleDeg=sample%35, RuRatio=sample%3==0 ? .2 : null },
        new SoilLayer { ThicknessM=6, GammaKnm3=20, CohesionKpa=sample%20, FrictionAngleDeg=sample%40 } } };
    var geometry=Enumerable.Range(0, 3+sample%97).Select(i =>
        new SliceGeometry(i+1, i-50, .1+9.8*random.NextDouble(), .5+random.NextDouble(), -.3+.9*random.NextDouble(), 40+460*random.NextDouble())).ToList();
    var loading=new AnalysisConditions(20, -10, 10, .1, sample%2*.1);
    var limit=sample%7==0 ? 1 : 200;
    Compare(new BishopBaselineEngine(1e-10,limit), new BishopSimplifiedEngine(1e-10,limit), geometry, model, loading);
}
Console.WriteLine(JsonSerializer.Serialize(new { runtime=Environment.Version.ToString(), os=Environment.OSVersion.ToString(),
    workers=1, tolerance=1e-10, max_iterations=200, tiered_compilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
    cases=evidence, randomized_equivalence_cases=150, failure_and_nonconvergence_states_equal=true }, new JsonSerializerOptions { WriteIndented=true }));

static double TimeOne(Func<SlopeCalculation> run) { var sw=Stopwatch.StartNew(); run(); sw.Stop(); return sw.Elapsed.TotalMilliseconds; }
static void Compare(ISlopeStabilityEngine a, ISlopeStabilityEngine b, IReadOnlyList<SliceGeometry> slices, GroundModel gm, AnalysisConditions conditions)
{
    SlopeCalculation? before=null, after=null; EngineException? ea=null, eb=null;
    try { before=a.Compute(slices,gm,conditions); } catch (EngineException e) { ea=e; }
    try { after=b.Compute(slices,gm,conditions); } catch (EngineException e) { eb=e; }
    if (ea is not null || eb is not null) { if(ea?.Code != eb?.Code || ea?.Message != eb?.Message) throw new Exception("Failure state changed"); return; }
    if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after)) throw new Exception("Result/convergence/report fields changed");
}
