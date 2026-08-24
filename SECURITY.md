# Security Policy

## Supported versions

Security fixes are currently expected for the latest `1.x` release and the `main` branch.

## Release integrity

Official GitHub Releases include SHA-256 checksum files for the ZIP and installer, plus a CycloneDX SBOM. Run `scripts/verify-release.ps1` against a downloaded portable release before use. Authenticode signatures are applied when the project's protected signing secrets are available; verify the signature status in Windows file properties. An unsigned artifact is not evidence of tampering by itself, but its SHA-256 value must match the checksum published in the same GitHub Release.

## Reporting a vulnerability

Please do not disclose suspected vulnerabilities in a public issue. Use GitHub's private vulnerability reporting or contact the repository owner through the [tak063495-prog GitHub profile](https://github.com/tak063495-prog).

Include:

- the affected version or commit;
- a clear description of the issue and its impact;
- reproducible steps or a minimal proof of concept;
- any suggested mitigation.

We will acknowledge a report when possible, investigate it, and coordinate disclosure after a fix or mitigation is available. Do not include passwords, access tokens, private project files, or other sensitive information in a report.
