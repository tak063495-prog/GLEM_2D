# Analysis Methods, Assumptions, and Limits

This document describes the equations that GLEM actually implements. It is not a design standard. A qualified geotechnical engineer must review the input model, critical surface, numerical convergence, and applicability before using a result in design or a safety decision.

Japanese: [METHODS.ja.md](METHODS.ja.md)

## Coordinate system and common terms

- `z` is depth below the ground surface and is positive downward.
- Each slice has weight `W`, base length `ΔL`, and base inclination `α`.
- Effective strength is represented by `c'` and `φ'` at the slice midpoint layer.
- A surcharge adds `q b` to a slice whose midpoint is inside the configured range.
- Pore pressure is `u = ru σ'v` when a layer has `ru`; otherwise GLEM uses the hydrostatic groundwater-table pressure. `ru` takes priority.
- Pseudo-static coefficients enter the implemented driving term as `W(sin α + kh)` and the normal-force term through `(1 + kv)`.

## Fellenius ordinary method

GLEM evaluates:

```text
FS = Σ[c' ΔL + N' tan φ'] / Σ[W* (sin α + kh)]
N' = max(0, W* (1 + kv) cos α - u ΔL)
W* = W + q b
```

The method satisfies overall moment-style equilibrium for a circular surface but neglects interslice forces. It is generally conservative for conventional circular cases, but that tendency is not guaranteed for every geometry or loading condition.

## Bishop simplified method

GLEM iterates the fixed-point equation:

```text
mα = cos α [1 + tan α tan φ' / FS]
FS = Σ{[c' b + (W*(1 + kv) - u b) tan φ'] / mα}
     / Σ[W* (sin α + kh)]
```

Fixed-point iteration starts at `FS = 1.0`. If an iterate lies outside `mα > 0`, it switches to a bracketed scalar equilibrium solve above the largest negative-angle pole. Bracket expansion and bisection share the remaining iteration budget; exhausting it returns `Converged=false`. The default FS convergence tolerance is `0.001` with at most 200 iterations. The method is intended for circular slip surfaces and neglects interslice shear forces.

## GLEM Janbu approximation

The current GLEM mode named “generalized Janbu” is an engineering approximation; it is not a full reproduction of Janbu's general procedure with an independently solved interslice-force function. For a non-circular surface it computes the Fellenius-style resistance and driving sums, then applies:

```text
di = max(0, Wi sin αi)
αbar = Σ(di αi) / Σdi
λc = min(2.0, 1 + Σ[di |αi - αbar|] / Σdi)
FS = resistance / (λc × driving)
```

For points recognized as circular, `λc = 1.0`. The angle-spread correction is deterministic and regression-tested, but it is a project-specific approximation informed by published Janbu correction concepts. It must not be treated as equivalent to a commercial implementation of the complete Janbu generalized method. GLEM displays this warning on the settings screen, result screen, and generated report.

## Settlement model

GLEM combines three one-dimensional components:

- Immediate settlement: elastic rectangular-load influence using `Es` and Poisson's ratio.
- Primary consolidation: `Cc`/`Cr`, `e0`, initial effective stress, and optional preconsolidation pressure with base-10 logarithms.
- Secondary compression: `Cs log10(t/tc)` after the estimated end of primary consolidation.

Time-dependent consolidation uses Terzaghi-style `Tv = cv t / Hdr²`; `Hdr` is the full layer thickness for single drainage and half the thickness for double drainage. The model assumes vertical one-dimensional drainage and layer properties represented at the layer midpoint.

## Applicability and known limits

- Two-dimensional, plane-strain-style analysis only; no 3-D end effects.
- Soil layers are homogeneous within each entered layer and use effective-stress Mohr–Coulomb strength.
- No unsaturated-suction model, strain softening, progressive failure, reinforcement, anchors, or probabilistic reliability analysis.
- The groundwater model is hydrostatic unless `ru` is entered; transient seepage is outside scope.
- The pseudo-static seismic coefficients are not a dynamic response analysis.
- Circular search is grid/local refinement and does not prove a global mathematical minimum.
- Settlement is one-dimensional; lateral deformation, staged construction, nonlinear modulus variation, and coupled consolidation are outside scope.
- Numerical agreement with a reference case does not validate a project-specific soil model.

## Locked reference cases

`tests/GLEM.Tests/ReferenceCaseTests.cs` locks independently calculated values:

| Method | Reference result |
|---|---:|
| Fellenius, three slices | `FS = 1.8111263573` |
| Bishop simplified, same three slices | `FS = 1.8291038996` |
| GLEM Janbu approximation, four slices | `λc = 1.1759747463`, `FS = 1.4997582286` |

The tests retain explicit slice data and tight tolerances so an equation or sign change cannot silently alter established results.

## References

- Bishop, A. W. (1955), “The use of the Slip Circle in the Stability Analysis of Slopes,” *Géotechnique*, 5(1), 7–17. [DOI: 10.1680/geot.1955.5.1.7](https://doi.org/10.1680/geot.1955.5.1.7)
- Fellenius, W. (1936), “Calculation of the Stability of Earth Dams,” Transactions of the 2nd Congress on Large Dams, Vol. 4, 445–463.
- Janbu, N. (1973), “Slope Stability Computations,” in *Embankment-Dam Engineering: Casagrande Volume*, 47–86.
- Terzaghi, K. (1943), *Theoretical Soil Mechanics*, Wiley.

References identify the method families. The equations above, rather than a method name alone, define GLEM's implemented behavior.

## 2026-10 review: geometry, convergence, and settlement scope

The horizontal slice width is `b = ΔL cos α`; `ΔL` remains the base chord length.
Weight is the sum of layer unit weight times layer area in each vertical slice, integrated
exactly for the piecewise linear base. A circle uses a chord per slice; geometric refinement
reduces this approximation. For depth positive downward, circular `α = atan2(-(x-cx), z-cz)`.
Signed contributions must cancel for a symmetric static arc. The current ground surface is
flat (`z=0`): a symmetric static circular search has zero driving, so GLEM reports GLEM-2004,
rather than inventing a finite FS. A driving surcharge distribution or seismic coefficient
must be explicitly supplied. Circles crossing below the modeled ground or not defining a
single-valued base are invalid. Only finite, converged candidates enter the minimum.

Immediate settlement uses one **homogeneous elastic half-space** solution. Identical Es/ν
in multiple positive-thickness layers yield the same value as one layer. Heterogeneous Es/ν
or partially missing Es values produce GLEM-2005. Omit Es in all layers for consolidation-only
analysis. This is a safe applicability restriction, not a finite-layer elasticity solver:
the half-space formula has no thickness dependence, and the finite-thickness zero limit is
**NOT IMPLEMENTED** (a ground with no positive-thickness layers yields zero). Existing
`ImmediateSettlementMm`/`SecondarySettlementMm` method names return metres; result records
convert to mm. Secondary compression begins separately in each layer at its own t95,
obtained by inverting the existing large-Tv law, independent of requested output duration.
Zero permeability gives t95=infinity and zero secondary compression. For zero Cc or missing
primary-clock properties, the explicit secondary reference is one second; this corner case
requires engineering interpretation and is not a new rheological model.

The corrected three-slice Bishop reference is the positive root in [1,3] of
`Σ[(25 cos α + W tan20)/(cos α + sin α tan20/F)] - Σ(W sin α)F = 0`,
for W=(100,150,120), α=(10°,25°,15°). Independent bisection gives
`F=1.8291038995586373`, residual ≈2.84e-14 kN/m; the engine uses fixed-point iteration.
Pure cohesion independently gives `F=Σ(c ΔL)/Σ(W sin α)` at kh=kv=u=0.
For a base from (0,1) to (2,3), horizontal area=4 m²: γ=18 gives W=72 kN/m;
an upper layer 2 m thick with γ=10 and a lower layer γ=20 gives W=45 kN/m.

Reference for horizontal width/base length distinction: USACE EM 1110-2-1902,
Figures F-17/G-11, [manual](https://www.publications.usace.army.mil/Portals/76/Publications/EngineerManuals/EM_1110-2-1902.pdf).
The existing pseudo-static formula and GLEM-specific Janbu approximation are preserved;
full force-equilibrium seismic theory and sloping-ground geometry are outside this revision.

The signed three-slice dry reference has ΔL=1, W=100, α=(-60°,0°,60°),
c=100, φ=45°, kh=.1 and gives `600/(1-3/F²)+200-30F=0` on F>sqrt(3).
Independent bisection on [26,27] gives F=26.750864342576584.
For cx=0, cz=-1, r=6, γ=18 and arc subdivision width=1, the 17-slice chord geometry
gives F=33.280056023023604. Both used to be rejected at initial FS=1 despite positive roots.
The bracket equation is Σ A/[cosα (F+tanα tanφ)] - D=0; for positive dry A it is strictly
decreasing above all poles. Mixed negative resistance is not assigned an unverified root when no bracket exists.
Core engines reject nonfinite geometry/loading/material values and nonfinite FS/report fields.
Control-point surfaces require distinct finite X and finite depths in [0,H]; unsupported out-of-ground
segments are rejected rather than clipped. True base inclination replaces the old ±89° clamp.
