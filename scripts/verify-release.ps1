param([Parameter(Mandatory)][string]$Archive, [Parameter(Mandatory)][string]$Version, [string]$Checksum, [string]$Sbom)
Set-StrictMode -Version Latest; $ErrorActionPreference = 'Stop'
$expected = $Version.Trim().TrimStart('v').Split('+')[0]
if ($expected -notmatch '^\d+\.\d+\.\d+$') { throw "Release version '$expected' must contain exactly three numeric components." }
$expectedBaseName = "GLEM-$expected-win-x64"
if ((Split-Path $Archive -Leaf) -ne "$expectedBaseName.zip") { throw "Archive filename must be '$expectedBaseName.zip'." }
$hashFile = if ($Checksum) { $Checksum } else { "$Archive.sha256" }
$sbomFile = if ($Sbom) { $Sbom } else { Join-Path (Split-Path $Archive -Parent) "$expectedBaseName.cdx.json" }
if ((Split-Path $hashFile -Leaf) -ne ((Split-Path $Archive -Leaf) + '.sha256')) { throw 'Checksum filename does not match the archive filename.' }
if (-not (Test-Path $Archive) -or -not (Test-Path $hashFile) -or -not (Test-Path $sbomFile)) { throw 'Archive, checksum, or SBOM file is missing.' }
$line = (Get-Content $hashFile -Raw).Trim(); $actual = (Get-FileHash $Archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($line -notmatch "^(?<hash>[0-9a-fA-F]{64})\s+(?<name>\S+)$" -or $Matches.hash.ToLowerInvariant() -ne $actual -or $Matches.name -ne (Split-Path $Archive -Leaf)) { throw 'SHA-256 checksum or checksum filename does not match.' }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('glem-verify-' + [guid]::NewGuid().ToString('N')); New-Item $stage -ItemType Directory | Out-Null
try {
  Expand-Archive $Archive $stage -Force; $entries = @(Get-ChildItem $stage); if ($entries.Count -ne 1 -or -not $entries[0].PSIsContainer -or $entries[0].Name -ne $expectedBaseName) { throw "Archive must contain one root directory named '$expectedBaseName'." }; $root = $entries[0]; $exe = Get-Item (Join-Path $root.FullName 'GLEM.exe') -ErrorAction SilentlyContinue
  if (-not $exe) { throw 'GLEM.exe is missing from the archive.' }
  foreach ($name in @('GLEM.exe','README.md','README.ja.md','LICENSE','SECURITY.md')) { if (-not (Test-Path (Join-Path $root.FullName $name))) { throw "Expected archive content missing: $name" } }
  $proc = Start-Process $exe.FullName -ArgumentList '--selftest' -WindowStyle Hidden -Wait -PassThru; if ($proc.ExitCode -ne 0) { throw "GLEM.exe --selftest failed with exit code $($proc.ExitCode)." }
  $fileVersion = ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe.FullName).FileVersion -split '\+')[0]
  if ($fileVersion -match '^(\d+\.\d+\.\d+)(?:\.\d+)?$') { $fileVersion = $Matches[1] }
  if ($fileVersion -ne $expected) { throw "Executable version '$fileVersion' does not exactly match '$expected'." }
} finally { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
$sbomJson = Get-Content $sbomFile -Raw | ConvertFrom-Json
$components = @($sbomJson.components); $uniquePurls = @($components.purl | Sort-Object -Unique)
if ($sbomJson.bomFormat -ne 'CycloneDX' -or $sbomJson.specVersion -ne '1.5' -or $sbomJson.metadata.component.version -ne $expected -or $components.Count -eq 0 -or $components.Count -ne $uniquePurls.Count) { throw 'CycloneDX SBOM format, version, components, or PURL uniqueness validation failed.' }
Write-Output "Verified $Archive, checksum, executable version, self-test, and SBOM"
