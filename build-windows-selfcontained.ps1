param(
    [ValidateSet("win-x64", "win-arm64", "win-x86")]
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = $(Join-Path $PSScriptRoot "artifacts")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$toolsRoot = Join-Path $repoRoot ".tools"
$dotnetRoot = Join-Path $toolsRoot "dotnet"
$dotnetExe = Join-Path $dotnetRoot "dotnet.exe"
$dotnetInstallScript = Join-Path $toolsRoot "dotnet-install.ps1"
$projectRoot = Join-Path $repoRoot "v2rayN"
$projectPath = Join-Path $projectRoot "v2rayN\v2rayN.csproj"

if (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot $OutputRoot
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)

$bundleSuffix = switch ($Runtime) {
    "win-x64" { "64" }
    "win-arm64" { "arm64" }
    "win-x86" { "86" }
    default { throw "Unsupported runtime: $Runtime" }
}

$bundleName = "v2rayN-windows-$bundleSuffix"
$runtimeRoot = Join-Path $OutputRoot $Runtime
$publishDir = Join-Path $runtimeRoot "publish"
$packageRoot = Join-Path $runtimeRoot "package"
$coreZipPath = Join-Path $runtimeRoot "$bundleName.zip"
$bundleDir = Join-Path $packageRoot $bundleName
$finalZipPath = Join-Path $OutputRoot "v2rayN-lite-windows-$bundleSuffix-selfcontained.zip"
$coreZipUrl = "https://raw.githubusercontent.com/2dust/v2rayN-core-bin/refs/heads/master/$bundleName.zip"

function Ensure-Directory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
    }
}

function Reset-Directory([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRepoRoot = [System.IO.Path]::GetFullPath($repoRoot)
    if (-not $fullPath.StartsWith($fullRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear path outside repo root: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $fullPath | Out-Null
}

function Test-DotnetSdkReady {
    if (-not (Test-Path -LiteralPath $dotnetExe)) {
        return $false
    }

    $sdks = & $dotnetExe --list-sdks 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $false
    }

    return [bool]($sdks | Select-String '^10\.0\.')
}

function Ensure-DotnetSdk {
    if (Test-DotnetSdkReady) {
        return
    }

    Ensure-Directory $toolsRoot
    Ensure-Directory $dotnetRoot

    Write-Host "Downloading dotnet-install.ps1 ..."
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetInstallScript

    Write-Host "Installing .NET SDK 10.0.100 into $dotnetRoot ..."
    & powershell -NoProfile -ExecutionPolicy Bypass -File $dotnetInstallScript -Version "10.0.100" -InstallDir $dotnetRoot -Architecture "x64"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-install failed with exit code $LASTEXITCODE"
    }
}

Ensure-DotnetSdk

$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"

Ensure-Directory $OutputRoot
Reset-Directory $runtimeRoot
Reset-Directory $publishDir
Reset-Directory $packageRoot

if (Test-Path -LiteralPath $finalZipPath) {
    Remove-Item -LiteralPath $finalZipPath -Force
}

Push-Location $projectRoot
try {
    Write-Host "Restoring $projectPath for $Runtime ..."
    & $dotnetExe restore $projectPath -r $Runtime
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }

    Write-Host "Publishing self-contained app ..."
    & $dotnetExe publish $projectPath -c $Configuration -r $Runtime -p:SelfContained=true -p:EnableWindowsTargeting=true -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "Downloading bundled core assets from $coreZipUrl ..."
Invoke-WebRequest -Uri $coreZipUrl -OutFile $coreZipPath

Write-Host "Validating downloaded core bundle ..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($coreZipPath)
    $zip.Dispose()
}
catch {
    throw "Downloaded core bundle is not a valid zip archive: $coreZipPath`n$($_.Exception.Message)"
}

Write-Host "Expanding core bundle ..."
Expand-Archive -LiteralPath $coreZipPath -DestinationPath $packageRoot -Force

if (-not (Test-Path -LiteralPath $bundleDir)) {
    throw "Expected bundle directory not found after extraction: $bundleDir"
}

Write-Host "Copying bundled core assets into publish directory ..."
$coreBinDir = Join-Path $bundleDir "bin"
$publishBinDir = Join-Path $publishDir "bin"
if (-not (Test-Path -LiteralPath $coreBinDir)) {
    throw "Expected core bin directory not found after extraction: $coreBinDir"
}
Ensure-Directory $publishBinDir
Get-ChildItem -LiteralPath $coreBinDir -Force | Copy-Item -Destination $publishBinDir -Recurse -Force

Write-Host "Copying published app into package ..."
Get-ChildItem -LiteralPath $publishDir -Force | Copy-Item -Destination $bundleDir -Recurse -Force

Write-Host "Compressing final package ..."
Compress-Archive -Path $bundleDir -DestinationPath $finalZipPath -Force

Write-Host "Publish directory: $publishDir"
Write-Host "Package directory: $bundleDir"
Write-Host "Final zip: $finalZipPath"