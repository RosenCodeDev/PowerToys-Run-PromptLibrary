# Build and package the PromptLibrary PowerToys Run plugin.

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
if (-not $ProjectRoot) {
    $ProjectRoot = (Get-Location).Path
}

$PluginName = "PromptLibrary"
$Version = "1.0.0"
$AssemblyName = "Community.PowerToys.Run.Plugin.PromptLibrary"
$ProjectFile = Join-Path $ProjectRoot "src\PromptLibrary.csproj"
$PluginMetadataFile = Join-Path $ProjectRoot "src\plugin.json"
$ReleaseRoot = Join-Path $ProjectRoot "release"
$BuildRoot = Join-Path $ReleaseRoot "_build"
$PackageRoot = Join-Path $ReleaseRoot "_package"
$DataSourceDir = Join-Path $ProjectRoot "release-samples"
$Platforms = @("x64", "ARM64")
$HostDlls = @(
    "PowerToys.Common.UI.dll",
    "PowerToys.ManagedCommon.dll",
    "PowerToys.Settings.UI.Lib.dll",
    "Wox.Infrastructure.dll",
    "Wox.Plugin.dll"
)

$env:PATH = "$env:LOCALAPPDATA\dotnet;$env:PATH"

function Assert-FileExists {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Required file missing: $Path"
    }
}

Write-Host "`n=== Building $PluginName $Version ===" -ForegroundColor Cyan

Write-Host "`n[1/5] Checking .NET SDK..." -ForegroundColor Yellow
$sdkVersion = & dotnet --version
if ($LASTEXITCODE -ne 0) {
    throw ".NET SDK not found. Install the .NET 10 SDK."
}
Write-Host "  .NET SDK version: $sdkVersion" -ForegroundColor Green

Write-Host "`n[2/5] Preparing release directory..." -ForegroundColor Yellow
if (Test-Path $ReleaseRoot) {
    Remove-Item $ReleaseRoot -Recurse -Force
}
New-Item $ReleaseRoot -ItemType Directory | Out-Null
New-Item $BuildRoot -ItemType Directory | Out-Null
New-Item $PackageRoot -ItemType Directory | Out-Null

Assert-FileExists (Join-Path $DataSourceDir "user.prompt.json")
Assert-FileExists (Join-Path $DataSourceDir "user.prompt.tag.json")
Assert-FileExists $PluginMetadataFile

$pluginMetadata = Get-Content $PluginMetadataFile -Raw | ConvertFrom-Json
if ($pluginMetadata.Name -ne $PluginName) {
    throw "Plugin name mismatch: build.ps1 uses '$PluginName' but plugin.json uses '$($pluginMetadata.Name)'."
}
if ($pluginMetadata.Version -ne $Version) {
    throw "Version mismatch: build.ps1 uses '$Version' but plugin.json uses '$($pluginMetadata.Version)'."
}
if ($pluginMetadata.ExecuteFileName -ne "$AssemblyName.dll") {
    throw "Assembly mismatch: build.ps1 expects '$AssemblyName.dll' but plugin.json uses '$($pluginMetadata.ExecuteFileName)'."
}

$createdZips = @()

foreach ($platform in $Platforms) {
    Write-Host "`n[3/5] Building $platform..." -ForegroundColor Yellow

    $buildOutput = Join-Path $BuildRoot $platform
    $packageDir = Join-Path $PackageRoot $platform
    $pluginDir = Join-Path $packageDir $PluginName
    $imagesOut = Join-Path $pluginDir "Images"
    $dataOut = Join-Path $pluginDir "Data"

    New-Item $buildOutput -ItemType Directory -Force | Out-Null
    New-Item $imagesOut -ItemType Directory -Force | Out-Null
    New-Item $dataOut -ItemType Directory -Force | Out-Null

    & dotnet build $ProjectFile -c Release -p:Platform=$platform -p:OutDir="$buildOutput\"
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $platform with exit code $LASTEXITCODE"
    }

    $assemblyPath = Join-Path $buildOutput "$AssemblyName.dll"
    Assert-FileExists $assemblyPath
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString(3)
    if ($assemblyVersion -ne $Version) {
        throw "Compiled DLL version mismatch: expected '$Version' but found '$assemblyVersion'."
    }

    Copy-Item $assemblyPath $pluginDir
    $depsJson = Join-Path $buildOutput "$AssemblyName.deps.json"
    if (Test-Path $depsJson) {
        Copy-Item $depsJson $pluginDir
    }

    Copy-Item (Join-Path $buildOutput "plugin.json") $pluginDir
    Copy-Item (Join-Path $buildOutput "Images\*.png") $imagesOut
    Copy-Item (Join-Path $DataSourceDir "user.prompt.json") $dataOut
    Copy-Item (Join-Path $DataSourceDir "user.prompt.tag.json") $dataOut

    $requiredFiles = @(
        "$AssemblyName.dll",
        "$AssemblyName.deps.json",
        "plugin.json",
        "Images\prompt.dark.png",
        "Images\prompt.light.png",
        "Images\prompt-minimal.dark.png",
        "Images\prompt-minimal.light.png",
        "Data\user.prompt.json",
        "Data\user.prompt.tag.json"
    )

    foreach ($file in $requiredFiles) {
        Assert-FileExists (Join-Path $pluginDir $file)
    }

    foreach ($dll in $HostDlls) {
        if (Test-Path (Join-Path $pluginDir $dll)) {
            throw "Host DLL leaked into package: $dll"
        }
    }

    $zipPath = Join-Path $ReleaseRoot "$PluginName-$Version-$platform.zip"
    Compress-Archive -Path $pluginDir -DestinationPath $zipPath -Force
    $createdZips += $zipPath

    Write-Host "  Created: $zipPath" -ForegroundColor Green
}

Write-Host "`n[4/5] Writing checksums..." -ForegroundColor Yellow
$checksumsPath = Join-Path $ReleaseRoot "checksums.txt"
$checksumLines = foreach ($zipPath in $createdZips) {
    $hash = Get-FileHash $zipPath -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $(Split-Path $zipPath -Leaf)"
}
$checksumLines | Set-Content -Path $checksumsPath -Encoding ascii

Write-Host "`n[5/5] Done." -ForegroundColor Cyan
Write-Host "Release files:" -ForegroundColor Yellow
foreach ($zipPath in $createdZips) {
    Write-Host "  $zipPath" -ForegroundColor White
}
Write-Host "  $checksumsPath" -ForegroundColor White
Write-Host "`nInstall by extracting the zip and copying $PluginName to:" -ForegroundColor Yellow
Write-Host "  $env:LOCALAPPDATA\PowerToys\RunPlugins\$PluginName" -ForegroundColor White
Write-Host "Then restart PowerToys and type '/p' in PowerToys Run." -ForegroundColor White
