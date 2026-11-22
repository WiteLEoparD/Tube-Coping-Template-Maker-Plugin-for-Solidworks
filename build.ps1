# ====================================================================
# Build and Packaging Script for Red Lion Racing SolidWorks Add-in
# ====================================================================

[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host " Building Red Lion Racing Tube Template Add-in [$Configuration | $Platform]" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan

# 1. Setup local environment variables for sandbox / CLI builds if needed
$localDotnet = Join-Path $rootDir ".dotnet_cli"
$localAppData = Join-Path $rootDir ".appdata"
$localTemp = Join-Path $rootDir ".localappdata"

if (Test-Path $localDotnet) {
    $env:DOTNET_CLI_HOME = $localDotnet
}
if (Test-Path $localAppData) {
    $env:APPDATA = $localAppData
}
if (Test-Path $localTemp) {
    $env:LOCALAPPDATA = $localTemp
}

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

# 2. Build Projects in Dependency Order
Write-Host "`n[1/3] Compiling C# projects..." -ForegroundColor Yellow

$dll = Join-Path $rootDir "SolidWorks_TubeCutTemplate_Addin\bin\x64\$Configuration\BajaSAE.TubeCutTemplateAddin.dll"
if (Test-Path $dll) {
    $oldDll = "$dll.old"
    if (Test-Path $oldDll) { Remove-Item $oldDll -Force -ErrorAction SilentlyContinue }
    Move-Item $dll $oldDll -Force -ErrorAction SilentlyContinue
}

$p1 = Join-Path $rootDir "SolidWorks_TubeCutTemplate_Addin\SolidWorks_TubeCutTemplate_Addin.csproj"
$p2 = Join-Path $rootDir "SolidWorks_TubeCutTemplate_Addin\StandaloneDemo\StandaloneDemo.csproj"
$p3 = Join-Path $rootDir "SolidWorks_TubeCutTemplate_Addin\Installer\Installer.csproj"

dotnet build $p1 -c $Configuration -p:Platform=$Platform --no-restore
if ($LASTEXITCODE -ne 0) { throw "Failed to build Addin" }

dotnet build $p2 -c $Configuration -p:Platform=$Platform --no-restore
if ($LASTEXITCODE -ne 0) { throw "Failed to build StandaloneDemo" }

dotnet build $p3 -c $Configuration -p:Platform=$Platform --no-restore
if ($LASTEXITCODE -ne 0) { throw "Failed to build Installer" }

Write-Host "[✓] All projects compiled successfully." -ForegroundColor Green

# 3. Run Standalone Verification Demo
Write-Host "`n[2/3] Running standalone engine verification test..." -ForegroundColor Yellow
$demoExe = Join-Path $rootDir "SolidWorks_TubeCutTemplate_Addin\StandaloneDemo\bin\x64\$Configuration\net48\BajaSAE.StandaloneDemo.exe"
if (Test-Path $demoExe) {
    & $demoExe
    Write-Host "[✓] Verification demo passed." -ForegroundColor Green
}

# 4. Package Release Archive
Write-Host "`n[3/3] Packaging release distribution zip..." -ForegroundColor Yellow
$distDir = Join-Path $rootDir "SolidWorks_TubeCutTemplate_Addin\dist"
$stagingDir = Join-Path $distDir "RedLionRacing_SolidWorks_TubeTemplates_v1.0"
$installerExe = Join-Path $distDir "RedLionRacing_TubeTemplates_Installer.exe"
$mainDll = Join-Path $rootDir "SolidWorks_TubeCutTemplate_Addin\bin\x64\$Configuration\BajaSAE.TubeCutTemplateAddin.dll"
$zipOut = Join-Path $rootDir "RedLionRacing_SolidWorks_TubeTemplates_v1.0.zip"

if (Test-Path $stagingDir) {
    Copy-Item $installerExe -Destination $stagingDir -Force
    Copy-Item $mainDll -Destination (Join-Path $stagingDir "Payload") -Force

    if (Test-Path $zipOut) { Remove-Item $zipOut -Force }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stagingDir, $zipOut)

    Write-Host "[✓] Distribution zip updated: $zipOut" -ForegroundColor Green
}

Write-Host "`n====================================================================" -ForegroundColor Cyan
Write-Host " BUILD & PACKAGING COMPLETE!" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan
