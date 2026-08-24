param(
    [string]$Version = "",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

# When no explicit version is given, derive it from the single product version
# property (ProductVersion) declared in Directory.Build.props.
if ([string]::IsNullOrWhiteSpace($Version)) {
    $propsPath = Join-Path $repoRoot "Directory.Build.props"
    if (-not (Test-Path -LiteralPath $propsPath)) {
        throw "Directory.Build.props not found at: $propsPath"
    }

    [xml]$propsXml = Get-Content -LiteralPath $propsPath
    $Version = @($propsXml.SelectNodes("//PropertyGroup/ProductVersion")) |
        Where-Object { $_.InnerText.Trim() -ne "" } |
        Select-Object -First 1 -ExpandProperty InnerText
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Could not resolve a version: pass -Version explicitly or set ProductVersion in Directory.Build.props."
}

$Version = $Version.Trim().TrimStart("v")
if ($Version.Length -eq 0) {
    throw "Resolved version is empty after trimming the 'v' prefix."
}
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
    -p:Version=$Version `
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
