# Contributing to GLEM

Thank you for helping improve GLEM. Contributions are welcome for calculation engines, validation, UI, documentation, and tests.

## Development environment

- Windows 10/11 x64
- .NET SDK 8.0.x

Build and test before opening a pull request:

```powershell
dotnet restore GLEM.sln
dotnet build GLEM.sln -c Release --no-restore
dotnet test tests/GLEM.Tests/GLEM.Tests.csproj -c Release --no-build
```

Changes to `GLEM.Core` should include or update automated tests. Calculation changes should explain the engineering method, units, assumptions, and reference case used for verification.

## Pull requests

1. Create a focused branch from `main`.
2. Keep the change small and describe the motivation and scope.
3. Update the relevant specification, design document, user manual, or release notes when behavior changes.
4. Include test evidence and screenshots for meaningful UI changes.
5. Do not commit build output, `dist/`, `artifacts/`, credentials, or personal data.

The pull request template contains the project checklist, including the coverage gate and system-test items C-01 through C-10.

## Documentation language

The code and public project overview use English where practical. The detailed engineering specifications and user-facing technical documentation may remain in Japanese. Please keep terminology and units consistent with the existing documents.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
