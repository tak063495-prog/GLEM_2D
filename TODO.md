# GLEM Roadmap and TODO

Japanese: [TODO.ja.md](TODO.ja.md)

This document is the working backlog after v1.2.0. It is a planning aid, not a promise that every item will ship. Work should normally proceed in priority order: complete the P2 quality gate before expanding engineering scope in P3, and complete the relevant architecture decisions before P4 ecosystem work.

Status legend: `[ ]` not started, `[~]` in progress, `[x]` complete.

## P2 — Usability, data safety, and sustainable maintenance

P2 improves the existing v1.2 feature set without materially widening the engineering model.

- [ ] **P2-01 — Complete project round-trip persistence.** Save and restore all slope settings, settlement settings, and non-circular control points. Add fixture-based and property-level save/reopen regression tests. Preserve v1 project compatibility through an explicit file-format migration.
- [ ] **P2-02 — Protect unsaved work.** Before New, Open, or Exit, offer Save / Discard / Cancel. A failed or cancelled save must prevent the destructive action.
- [ ] **P2-03 — Make dirty and stale-result state accurate.** Every editable property and table cell must mark the project dirty. Changing an input after analysis must invalidate or clearly mark the displayed result as stale until rerun.
- [ ] **P2-04 — Harden autosave and recovery.** Use atomic writes and per-project or per-session recovery files, handle multiple application instances, delete explicitly discarded recovery data, and test crash/restart recovery.
- [ ] **P2-05 — Add Undo and Redo.** Support `Ctrl+Z` / `Ctrl+Y` for layer edits, reordering, analysis settings, and non-circular control points with a bounded history.
- [ ] **P2-06 — Improve data-entry productivity.** Add multi-cell paste from spreadsheets, row duplication, validated bulk editing, soil/material templates, and clear unit-conversion assistance.
- [ ] **P2-07 — Improve project navigation.** Add recent files, drag-and-drop `.glem` opening, remembered folders, sample projects, and a visible project name / modified indicator in the window title.
- [ ] **P2-08 — Improve result comparison and export.** Compare named scenarios, export plots as PNG/SVG or copy them to the clipboard, generate PDF-ready output, and include an input hash and approximation warnings in reports.
- [ ] **P2-09 — Add real UI and accessibility regression.** Automate WPF workflows and test keyboard-only operation, high contrast, 200% DPI, screen-reader metadata, English/Japanese clipping, and installer file opening on supported Windows versions.
- [ ] **P2-10 — Complete release trust.** Obtain and protect a code-signing certificate, configure `WINDOWS_CERTIFICATE_BASE64` and `WINDOWS_CERTIFICATE_PASSWORD`, verify signed upgrade/repair/uninstall and `.glem` association, and evaluate package-manager publication such as WinGet.
- [ ] **P2-11 — Establish dependency maintenance.** Triage Dependabot updates in compatible groups, add a supported .NET/package matrix, define a monthly update window, and require the full regression and release-package checks before merging.
- [ ] **P2-12 — Improve diagnostics and support.** Record handled and unhandled failures without project secrets, add a user-reviewable support bundle, retain bounded logs, and provide issue templates with version, reproduction, and model-sanitization guidance.

### P2 completion gate

- No known loss of user-entered project settings during Save, Open, New, Exit, autosave, or recovery.
- Unit, integration, UI, accessibility, installer, and release-package regressions pass on supported Windows versions.
- A signed release path is exercised when a certificate is available; unsigned artifacts remain explicitly identified.
- User and contributor documentation describes the new workflows in English and Japanese.

## P3 — Engineering capability and validation depth

P3 expands the analysis model. Every method must be defined by equations, assumptions, limits, reference cases, and independent engineering review before it is presented as suitable for design work.

- [ ] **P3-01 — Implement a complete generalized limit-equilibrium method.** Add a rigorously specified Janbu generalized, Spencer, or Morgenstern–Price solver with an explicit interslice-force function, convergence diagnostics, and published benchmark cases. Keep the existing GLEM approximation separately named for compatibility.
- [ ] **P3-02 — Support realistic ground geometry and material zoning.** Replace the flat-ground assumption with editable surface profiles, stratigraphic boundaries, lenses, and spatial material regions. Define geometry validation and migration for the next `.glem` format.
- [ ] **P3-03 — Expand groundwater and pore-pressure modelling.** Support multiple phreatic lines, spatial `ru` zones, artesian pressure, externally imported pore-pressure fields, and clearly separated steady/transient assumptions.
- [ ] **P3-04 — Add non-circular critical-surface search.** Search and optimize admissible non-circular surfaces with deterministic seeds, geometry constraints, cancellation, reproducibility, and proof that the returned candidate set was adequately explored.
- [ ] **P3-05 — Expand loads and stabilization elements.** Add line/point/distributed loads, staged seismic cases, anchors, geosynthetics, berms, and other reinforcement only with documented force conventions and verification cases.
- [ ] **P3-06 — Extend settlement and consolidation analysis.** Add staged loading, layer-specific drainage boundaries, time-varying loads, stress-dependent parameters, and settlement-by-layer output while retaining the current one-dimensional limitations where applicable.
- [ ] **P3-07 — Add sensitivity and reliability tools.** Provide parameter sweeps, scenario envelopes, tornado plots, and optional probabilistic analysis with explicit distributions, sampling seeds, and confidence reporting.
- [ ] **P3-08 — Build an authoritative verification corpus.** Curate literature, hand-calculation, and independently reproduced cases for every solver; version expected values and publish the evidence and tolerances in both languages.

### P3 completion gate

- Each new solver has independent reference values, convergence/failure tests, and documented applicability limits.
- File-format changes include forward/backward compatibility tests and a migration guide.
- Engineering documentation and UI names do not claim equivalence to a published method unless the implemented equations and validation support that claim.
- Performance baselines cover both normal and upper-bound supported models.

## P4 — Ecosystem, interoperability, and long-term scale

P4 turns GLEM from a single desktop application into an extensible engineering platform. These items require separate architecture, security, and maintenance decisions.

- [ ] **P4-01 — Provide a stable headless interface.** Add a documented CLI and/or local API for validation, analysis, report generation, deterministic exit codes, and machine-readable results without starting WPF.
- [ ] **P4-02 — Define an extension and plugin SDK.** Introduce versioned contracts for importers, exporters, solvers, and report sections with isolation, trust, compatibility, and signing rules.
- [ ] **P4-03 — Add engineering data interoperability.** Support selected formats such as DXF/LandXML geometry and structured CSV/JSON exchange after publishing coordinate, unit, precision, and provenance rules.
- [ ] **P4-04 — Decide the cross-platform UI strategy.** Evaluate continued Windows/WPF specialization versus an Avalonia or web-based client. Prototype before committing and retain a common tested core and headless interface.
- [ ] **P4-05 — Add optional collaboration and revision history.** Design local-first project history, comparisons, comments, and controlled sharing with conflict resolution and no mandatory cloud dependency.
- [ ] **P4-06 — Generalize localization and units.** Add a pluggable resource workflow, pseudo-localization, additional languages, metric/imperial display profiles, and invariant stored units with round-trip tests.
- [ ] **P4-07 — Scale large studies.** Introduce safe parallel search and batch execution, resumable jobs, resource limits, benchmark datasets, and profiling-driven optimization before considering GPU acceleration.
- [ ] **P4-08 — Establish long-term product governance.** Define LTS/support windows, release cadence, deprecation policy, security response targets, reproducible builds and provenance, archival strategy, and engineering review ownership.

### P4 completion gate

- Public API, plugin, and file-format contracts are versioned and have compatibility suites.
- A threat model covers plugins, imported files, automation interfaces, collaboration, and update delivery.
- Cross-platform or service components do not weaken local/offline operation, deterministic analysis, or user control of project data.
- Support, deprecation, and governance commitments are documented and realistically staffed.

## Recommended execution order

1. P2-01 through P2-04: eliminate project-state and recovery risks.
2. P2-09 through P2-12: create the regression and maintenance foundation.
3. P2-05 through P2-08: deliver workflow improvements on that foundation.
4. Select P3 items through an engineering requirements review; start with the verification corpus and file-format design needed by the chosen capability.
5. Start P4 only after the headless interface and extension/security architecture have approved prototypes.
