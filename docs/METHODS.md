# Analysis Methods, Assumptions, and Limits

This document describes the equations that GLEM actually implements. It is not a design standard. A qualified geotechnical engineer must review the input model, critical surface, numerical convergence, and applicability before using a result in design or a safety decision.

Japanese: [METHODS.ja.md](METHODS.ja.md)

## Coordinate system and common terms

- `z` is depth below the ground surface and is positive downward.
- Each slice has weight `W`, base length `ΔL`, and base inclination `α`.
- Effective strength is represented by `c'` and `φ'` at the slice midpoint layer.
- A surcharge adds `q ΔL` to a slice whose midpoint is inside the configured range.
- Pore pressure is `u = ru σ'v` when a layer has `ru`; otherwise GLEM uses the hydrostatic groundwater-table pressure. `ru` takes priority.
- Pseudo-static coefficients enter the implemented driving term as `W(sin α + kh)` and the normal-force term through `(1 + kv)`.

## Fellenius ordinary method

GLEM evaluates:

```text
FS = Σ[c' ΔL + N' tan φ'] / Σ[W* (sin α + kh)]
N' = max(0, W* (1 + kv) cos α - u ΔL)
W* = W + q ΔL
```

The method satisfies overall moment-style equilibrium for a circular surface but neglects interslice forces. It is generally conservative for conventional circular cases, but that tendency is not guaranteed for every geometry or loading condition.

## Bishop simplified method

GLEM iterates the fixed-point equation:

```text
mα = cos α [1 + tan α tan φ' / FS]
FS = Σ{[c' ΔL + (W*(1 + kv) - u ΔL) tan φ'] / mα}
     / Σ[W* (sin α + kh)]
```

The initial value is `FS = 1.0`; the default convergence tolerance is `0.001` with at most 200 iterations. The result reports whether convergence occurred. The method is intended for circular slip surfaces and neglects interslice shear forces.

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
| Bishop simplified, same three slices | `FS = 1.8630805036` |
| GLEM Janbu approximation, four slices | `λc = 1.1759747463`, `FS = 1.4997582286` |

The tests retain explicit slice data and tight tolerances so an equation or sign change cannot silently alter established results.

## References

- Bishop, A. W. (1955), “The use of the Slip Circle in the Stability Analysis of Slopes,” *Géotechnique*, 5(1), 7–17. [DOI: 10.1680/geot.1955.5.1.7](https://doi.org/10.1680/geot.1955.5.1.7)
- Fellenius, W. (1936), “Calculation of the Stability of Earth Dams,” Transactions of the 2nd Congress on Large Dams, Vol. 4, 445–463.
- Janbu, N. (1973), “Slope Stability Computations,” in *Embankment-Dam Engineering: Casagrande Volume*, 47–86.
- Terzaghi, K. (1943), *Theoretical Soil Mechanics*, Wiley.

References identify the method families. The equations above, rather than a method name alone, define GLEM's implemented behavior.
