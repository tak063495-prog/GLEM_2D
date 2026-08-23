param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$Version = $Version.TrimStart("v")
$packageName = "GLEM-$Version-$Runtime"
$releaseRoot = Join-Path $repoRoot "artifacts\release"
$packageDirectory = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot "$packageName.zip"
$projectPath = Join-Path $repoRoot "src\GLEM.App\GLEM.App.csproj"

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
if (Test-Path -LiteralPath $packageDirectory) {
    Remove-Item -LiteralPath $packageDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $packageDirectory

$generatedExe = Join-Path $packageDirectory "GLEM.App.exe"
$releaseExe = Join-Path $packageDirectory "GLEM.exe"
if (-not (Test-Path -LiteralPath $generatedExe)) {
    throw "Expected publish output was not found: $generatedExe"
}
Move-Item -LiteralPath $generatedExe -Destination $releaseExe
Get-ChildItem -LiteralPath $packageDirectory -Filter "*.pdb" -File | Remove-Item -Force

Compress-Archive -Path $packageDirectory -DestinationPath $zipPath -CompressionLevel Optimal
Write-Output "Created: $zipPath"
