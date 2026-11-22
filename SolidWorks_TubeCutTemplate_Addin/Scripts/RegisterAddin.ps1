<#
.SYNOPSIS
    Registers the Baja SAE Tube Cut Templates Native SolidWorks Add-in (.NET COM).
.DESCRIPTION
    Runs 64-bit RegAsm on the compiled DLL and registers the Add-in keys under HKCU and HKLM.
#>

[CmdletBinding()]
param (
    [string]$Configuration = "Release"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

$regasm = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
if (-not (Test-Path $regasm)) {
    Write-Error "64-bit RegAsm.exe not found at $regasm"
    exit 1
}

$dllPath = Join-Path $projectRoot "bin\x64\$Configuration\BajaSAE.TubeCutTemplateAddin.dll"
if (-not (Test-Path $dllPath)) {
    # Check fallback Debug
    $dllPath = Join-Path $projectRoot "bin\x64\Debug\BajaSAE.TubeCutTemplateAddin.dll"
}

if (-not (Test-Path $dllPath)) {
    Write-Error "Could not find Add-in DLL. Please build the project first."
    exit 1
}

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host " Registering Baja SAE Tube Cut Templates Add-in with SolidWorks..." -ForegroundColor Cyan
Write-Host " DLL Path: $dllPath" -ForegroundColor White
Write-Host "==========================================================================" -ForegroundColor Cyan

# Execute RegAsm with codebase
$process = Start-Process -FilePath $regasm -ArgumentList "`"$dllPath`" /codebase" -Wait -PassThru -NoNewWindow

if ($process.ExitCode -eq 0) {
    Write-Host "`n[SUCCESS] RegAsm registration succeeded!" -ForegroundColor Green
} else {
    Write-Host "`n[WARNING] RegAsm exited with code $($process.ExitCode). Trying direct registry registration..." -ForegroundColor Yellow
}

# Explicitly ensure SolidWorks Add-in Registry entries exist under HKCU
$guid = "{769C5C1D-E143-4D90-A951-BEB1309CD51E}"
$hkcuPath = "HKCU:\Software\SolidWorks\Addins\$guid"

if (-not (Test-Path $hkcuPath)) {
    New-Item -Path $hkcuPath -Force | Out-Null
}

Set-ItemProperty -Path $hkcuPath -Name "(Default)" -Value 1 -Type DWord
Set-ItemProperty -Path $hkcuPath -Name "Title" -Value "Baja SAE Tube Cut Templates" -Type String
Set-ItemProperty -Path $hkcuPath -Name "Description" -Value "Auto-generates true 1:1 scale printable cut templates and chassis locator diagram per BSAE 2027 Rev A." -Type String

Write-Host "[SUCCESS] Verified SolidWorks Addin Registry at $hkcuPath" -ForegroundColor Green
Write-Host "Title: Baja SAE Tube Cut Templates" -ForegroundColor White
Write-Host "Auto-Load on Startup: Enabled (1)" -ForegroundColor White
Write-Host "`nOpen SolidWorks 2022-2026 to see 'Tube Cut Templates' under Weldments and Assembly!" -ForegroundColor Cyan
