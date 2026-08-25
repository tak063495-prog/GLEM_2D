# GLEM (Generalized Limit Equilibrium Method)

[![CI](https://github.com/tak063495-prog/GLEM_2D/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/tak063495-prog/GLEM_2D/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/tak063495-prog/GLEM_2D?display_name=tag)](https://github.com/tak063495-prog/GLEM_2D/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

GLEM is a Windows desktop application for geotechnical slope stability and settlement analysis. It is built with C#/.NET 8 and WPF, and performs calculations locally without a server or external service.

日本語版: [README.ja.md](README.ja.md)

## Features

- Slope stability analysis using the Fellenius and simplified Bishop methods, plus GLEM's approximate Janbu-style correction for non-circular surfaces
- Circular slip-surface search with configurable center, radius, and slice width
- Non-circular Janbu surfaces defined by editable control points
- Pore-water pressure from ru or a groundwater table, plus surcharge and pseudo-static seismic coefficients
- One-dimensional settlement/consolidation prediction with immediate, primary, and secondary components
- `.glem` JSON project files with version checks and autosave recovery
- CSV export and self-contained HTML reports with embedded plots
- Input validation, progress reporting, cancellation, and WPF result plots
- English and Japanese user interfaces, validation messages, plots, and HTML reports

## Language

GLEM follows the Windows display language by default: Japanese systems use Japanese, and other systems use English. To override it, select **Language > System default / English / Japanese** and restart GLEM. The preference is stored per user in `%LOCALAPPDATA%\GLEM\settings.json`.

Project (`.glem`) and CSV data formats remain language-neutral and use stable invariant number formatting, so files can be exchanged between English and Japanese environments.

## Requirements

- Windows 10 or 11, 64-bit
- .NET SDK 8.0.x for development
- The self-contained release package does not require a pre-installed .NET runtime

## Download and run

Download either the `GLEM-<version>-win-x64-Setup.exe` installer or the portable `GLEM-<version>-win-x64.zip` from [Releases](https://github.com/tak063495-prog/GLEM_2D/releases). The installer creates Start menu entries and associates `.glem` project files; the portable package can be extracted and started with `GLEM.exe`.

Each release also provides SHA-256 checksum files for the ZIP and installer, plus a CycloneDX SBOM. Verify the portable package before use:

```powershell
$version = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.ProductVersion
$zip = Get-ChildItem artifacts/release/GLEM-$version-win-x64.zip
pwsh -NoProfile -File scripts/verify-release.ps1 -Archive $zip.FullName -Version $version
```

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
pwsh -NoProfile -File scripts/package-release.ps1
```

When `-Version` is omitted, the script reads the product version from `Directory.Build.props`. The same packaging step runs automatically when a tag such as `v1.2.0` is pushed. The release workflow verifies the archive name, version, contents, and checksum; expands it; runs `GLEM.exe --selftest`; generates an installer, SHA-256 file, and CycloneDX SBOM; and uploads all artifacts to the GitHub Release. If the repository secrets `WINDOWS_CERTIFICATE_BASE64` and `WINDOWS_CERTIFICATE_PASSWORD` are configured, both `GLEM.exe` and the installer are Authenticode-signed before publication. Builds without those secrets remain unsigned and are identified as such in the workflow log.

## Keyboard and accessibility

- `Ctrl+N`: new project
- `Ctrl+O`: open project
- `Ctrl+S`: save
- `Ctrl+Shift+S`: save as
- Keyboard tab order, access keys, and screen-reader names are provided for primary workflows.
- Plots combine labels and line patterns with color, and the UI follows Windows high-contrast system colors.

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
- [Calculation methods, assumptions, and reference cases](docs/METHODS.md) ([日本語](docs/METHODS.ja.md))
- [Release notes](RELEASE-NOTES.md)
- [Performance records](docs/perf/README.md)
- [Roadmap and TODO (P2-P4)](TODO.md) ([日本語](TODO.ja.md))

## Contributing and security

- See [CONTRIBUTING.md](CONTRIBUTING.md) for development and pull request guidance.
- See [SECURITY.md](SECURITY.md) for private vulnerability reporting guidance.

## License

GLEM is released under the [MIT License](LICENSE).
