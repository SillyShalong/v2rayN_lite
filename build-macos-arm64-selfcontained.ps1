param([string]$OutputRoot = (Join-Path $PSScriptRoot "artifacts"))
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dotnet = Join-Path $root ".tools\dotnet\dotnet.exe"
$work = Join-Path $OutputRoot "osx-arm64"
$publish = Join-Path $work "publish"
$app = Join-Path $work "package\v2rayN.app"
$mac = Join-Path $app "Contents\MacOS"
$resources = Join-Path $app "Contents\Resources"
$final = Join-Path $OutputRoot "v2rayN-lite-macos-arm64-selfcontained.zip"

if (-not (Test-Path $dotnet)) { throw "Missing local .NET 10 SDK" }
$resolved = [IO.Path]::GetFullPath($work)
if (-not $resolved.StartsWith([IO.Path]::GetFullPath($root), [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe output path" }
if (Test-Path $resolved) { Remove-Item $resolved -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publish,$mac,$resources | Out-Null
$env:DOTNET_ROOT = Split-Path $dotnet

Push-Location (Join-Path $root "v2rayN")
try {
  & $dotnet publish .\v2rayN.Desktop\v2rayN.Desktop.csproj -c Release -r osx-arm64 -p:SelfContained=true -o $publish
  if ($LASTEXITCODE) { throw "App publish failed" }
  & $dotnet publish .\AmazTool\AmazTool.csproj -c Release -r osx-arm64 -p:SelfContained=true -p:PublishTrimmed=true -o $publish
  if ($LASTEXITCODE) { throw "AmazTool publish failed" }
} finally { Pop-Location }

$coreZip = Join-Path $work "core.zip"
$coreDir = Join-Path $work "core"
Invoke-WebRequest "https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/v2rayN-macos-arm64.zip" -OutFile $coreZip
Expand-Archive $coreZip $coreDir
$xray = Get-ChildItem $coreDir -Recurse -File -Filter xray | Where-Object FullName -Match '[\\/]bin[\\/]xray[\\/]xray$' | Select-Object -First 1
if (-not $xray) { throw "Xray not found in downloaded bundle" }
Get-ChildItem $publish -Force | Copy-Item -Destination $mac -Recurse -Force
$xrayDir = Join-Path $mac "bin\xray"
New-Item -ItemType Directory -Force $xrayDir | Out-Null
Copy-Item $xray.FullName (Join-Path $xrayDir "xray")
Get-ChildItem $xray.DirectoryName -File | Where-Object Name -In geoip.dat,geosite.dat | Copy-Item -Destination $xrayDir
Move-Item (Join-Path $mac "v2rayN.icns") (Join-Path $resources "AppIcon.icns")

$version = (Select-Xml -Path (Join-Path $root "v2rayN\Directory.Build.props") -XPath '/Project/PropertyGroup/Version').Node.InnerText
$plist = @" 
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>v2rayN</string><key>CFBundleIconFile</key><string>AppIcon</string>
<key>CFBundleIdentifier</key><string>2dust.v2rayN.lite</string><key>CFBundleName</key><string>v2rayN</string>
<key>CFBundlePackageType</key><string>APPL</string><key>CFBundleShortVersionString</key><string>$version</string>
<key>LSMinimumSystemVersion</key><string>12.7</string><key>NSHighResolutionCapable</key><true/>
</dict></plist>
"@
[IO.File]::WriteAllText((Join-Path $app "Contents\Info.plist"),$plist,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $mac "NotStoreConfigHere.txt"),"Use the user profile for configuration.`n",[Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.IO.Compression
$stream = [IO.File]::Open($final,[IO.FileMode]::Create)
try {
  $zip = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
  Get-ChildItem $app -Recurse -File | ForEach-Object {
    $baseUri = [Uri]::new(($app.TrimEnd('\') + '\')); $name = "v2rayN.app/" + [Uri]::UnescapeDataString($baseUri.MakeRelativeUri([Uri]::new($_.FullName)).ToString())
    $entry = $zip.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
    $entry.ExternalAttributes = $(if ($_.Name -in 'v2rayN','AmazTool','xray') { 0x81ED } else { 0x81A4 }) -shl 16
    $input=$_.OpenRead(); $output=$entry.Open(); try {$input.CopyTo($output)} finally {$output.Dispose();$input.Dispose()}
  }
  $zip.Dispose()
} finally {$stream.Dispose()}
Write-Host "Final zip: $final"
