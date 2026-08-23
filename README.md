# GLEM (Generalized Limit Equilibrium Method)

[![CI](https://github.com/tak063495-prog/GLEM_2D/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/tak063495-prog/GLEM_2D/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/tak063495-prog/GLEM_2D?display_name=tag)](https://github.com/tak063495-prog/GLEM_2D/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

GLEM is a Windows desktop application for geotechnical slope stability and settlement analysis. It is built with C#/.NET 8 and WPF, and performs calculations locally without a server or external service.

日本語版: [README.ja.md](README.ja.md)

## Features

- Slope stability analysis using the Fellenius, simplified Bishop, and generalized Janbu methods
- Circular slip-surface search with configurable center, radius, and slice width
- Non-circular Janbu surfaces defined by editable control points
- Pore-water pressure from ru or a groundwater table, plus surcharge and pseudo-static seismic coefficients
- One-dimensional settlement/consolidation prediction with immediate, primary, and secondary components
- `.glem` JSON project files with version checks and autosave recovery
- CSV export and self-contained HTML reports with embedded plots
- Input validation, progress reporting, cancellation, and WPF result plots

## Requirements

- Windows 10 or 11, 64-bit
- .NET SDK 8.0.x for development
- The self-contained release package does not require a pre-installed .NET runtime

## Download and run

Download the latest `GLEM-<version>-win-x64.zip` from [Releases](https://github.com/tak063495-prog/GLEM_2D/releases), extract it, and run `GLEM.exe`.

GLEM is intended for engineering analysis and verification. Results depend on the input data and model assumptions; they must be reviewed by a qualified engineer before being used for design or safety-critical decisions.

## Build and test

```powershell
dotnet restore GLEM.sln
dotnet build GLEM.sln -c Release --no-restore
dotnet test tests/GLEM.Tests/GLEM.Tests.csproj -c Release --no-build
```

To collect coverage and run the project gate:

```powershell
dotnet test tests/GLEM.Tests/GLEM.Tests.csproj -c Release --collect:"XPlat Code Coverage"
$xml = Get-ChildItem -Recurse -Include *.cobertura.xml | Sort-Object LastWriteTime -Descending | Select-Object -First 1
powershell -NoProfile -File scripts/coverage-gate.ps1 -CoverageXmlPath $xml.FullName
```

## Create a release package locally

Run the packaging script from the repository root. The output is written to the ignored `artifacts/release/` directory.

```powershell
pwsh -NoProfile -File scripts/package-release.ps1 -Version 1.0.0
```

The same packaging step runs automatically when a tag such as `v1.0.0` is pushed. The release workflow creates a GitHub Release and uploads the zip package.

## Architecture

```text
GLEM.sln
├── src/GLEM.App/      WPF desktop application (net8.0-windows)
├── src/GLEM.Core/     UI-independent domain models and calculation engines (net8.0)
└── tests/GLEM.Tests/  xUnit tests
```

## Documentation

- [Functional specification](docs/GLEM_機能仕様書.md)
- [Basic design](docs/GLEM_基本設計書.md)
- [Detailed design](docs/GLEM_詳細設計書.md)
- [Test plan](docs/GLEM_テスト計画書.md)
- [User manual](docs/GLEM_ユーザーマニュアル.md)
- [Release notes](RELEASE-NOTES.md)
- [Performance records](docs/perf/README.md)

## Contributing and security

- See [CONTRIBUTING.md](CONTRIBUTING.md) for development and pull request guidance.
- See [SECURITY.md](SECURITY.md) for private vulnerability reporting guidance.

## License

GLEM is released under the [MIT License](LICENSE).
