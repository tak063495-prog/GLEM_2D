param([string]$Version = '', [string]$Configuration = 'Release', [string]$Runtime = 'win-x64')
Set-StrictMode -Version Latest; $ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
  [xml]$props = Get-Content (Join-Path $repoRoot 'Directory.Build.props')
  $Version = @($props.SelectNodes('//PropertyGroup/ProductVersion')) | Where-Object { $_.InnerText.Trim() } | Select-Object -First 1 -ExpandProperty InnerText
}
$Version = $Version.Trim().TrimStart('v').Split('+')[0]
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Release version '$Version' must contain exactly three numeric components (for example, 1.2.0)." }
if ($Runtime -ne 'win-x64') { throw "Unsupported release runtime '$Runtime'. Only win-x64 is supported." }
$packageName = "GLEM-$Version-$Runtime"; $releaseRoot = Join-Path $repoRoot 'artifacts\release'; $packageDirectory = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot "$packageName.zip"; $checksumPath = "$zipPath.sha256"; $sbomPath = Join-Path $releaseRoot "$packageName.cdx.json"
New-Item $releaseRoot -ItemType Directory -Force | Out-Null
foreach ($path in @($packageDirectory, $zipPath, $checksumPath, $sbomPath)) { if (Test-Path $path) { Remove-Item $path -Recurse -Force } }
dotnet publish (Join-Path $repoRoot 'src\GLEM.App\GLEM.App.csproj') -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -p:Version=$Version -o $packageDirectory
$generated = Join-Path $packageDirectory 'GLEM.App.exe'; $exe = Join-Path $packageDirectory 'GLEM.exe'
if (-not (Test-Path $generated)) { throw "Expected publish output was not found: $generated" }; Move-Item $generated $exe
Get-ChildItem $packageDirectory -Filter '*.pdb' -File | Remove-Item -Force
Copy-Item (Join-Path $repoRoot 'README.md'), (Join-Path $repoRoot 'README.ja.md'), (Join-Path $repoRoot 'LICENSE'), (Join-Path $repoRoot 'SECURITY.md') $packageDirectory -Force
$cert = $env:WINDOWS_CERTIFICATE_BASE64; $password = $env:WINDOWS_CERTIFICATE_PASSWORD
if ($cert -and $password) {
  $pfx = Join-Path ([IO.Path]::GetTempPath()) "glem-$([guid]::NewGuid()).pfx"
  try { [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($cert)); $signtool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe' | Sort-Object FullName -Descending | Select-Object -First 1; if (-not $signtool) { throw 'signtool.exe was not found.' }; & $signtool.FullName sign /fd SHA256 /f $pfx /p $password /tr http://timestamp.digicert.com /td SHA256 $exe; if ($LASTEXITCODE -ne 0) { throw "signtool failed with exit code $LASTEXITCODE." }; & $signtool.FullName verify /pa $exe; if ($LASTEXITCODE -ne 0) { throw "Signature verification failed with exit code $LASTEXITCODE." }; Write-Output 'Signed and verified GLEM.exe.' } finally { Remove-Item $pfx -Force -ErrorAction SilentlyContinue }
} else { Write-Warning 'No signing secrets configured; package is unsigned.' }
Compress-Archive $packageDirectory $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant(); "$hash  $(Split-Path $zipPath -Leaf)" | Set-Content $checksumPath -Encoding ascii
$components = @(); $assets = Join-Path $repoRoot 'src\GLEM.App\obj\project.assets.json'
if (Test-Path $assets) { $json = Get-Content $assets -Raw | ConvertFrom-Json; foreach ($library in $json.libraries.PSObject.Properties) { if ($library.Value.type -ne 'package') { continue }; $parts = $library.Name.Split('/', 2); if ($parts.Count -eq 2) { $components += [ordered]@{ type='library'; name=$parts[0]; version=$parts[1]; purl="pkg:nuget/$($parts[0])@$($parts[1])" } } } }
$components = @($components | Sort-Object { $_['purl'] })
[ordered]@{ bomFormat='CycloneDX'; specVersion='1.5'; serialNumber="urn:uuid:$([guid]::NewGuid())"; version=1; metadata=[ordered]@{ component=[ordered]@{ type='application'; name='GLEM'; version=$Version } }; components=$components } | ConvertTo-Json -Depth 10 | Set-Content $sbomPath -Encoding utf8
Write-Output "Created: $zipPath`nCreated: $checksumPath`nCreated: $sbomPath"
