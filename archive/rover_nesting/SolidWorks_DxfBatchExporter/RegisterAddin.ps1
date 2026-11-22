[CmdletBinding()]
param (
    [string]$Configuration = "Release"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dllPath = Join-Path $scriptDir "bin\x64\$Configuration\SolidWorksDxfBatchExporter.dll"
if (-not (Test-Path $dllPath)) {
    $dllPath = Join-Path $scriptDir "bin\x64\Debug\SolidWorksDxfBatchExporter.dll"
}

if (-not (Test-Path $dllPath)) {
    Write-Error "Could not find Add-in DLL at $dllPath. Please build the project first."
    exit 1
}

$guid = "{A4D8B923-3E4C-4F91-8D62-7F0F3D5B91A2}"
$codeBase = ([System.Uri](New-Object System.IO.FileInfo($dllPath)).FullName).AbsoluteUri

Write-Host "Registering SolidWorks Addin..." -ForegroundColor Cyan
Write-Host "DLL Path: $dllPath" -ForegroundColor White

# 1. User-level COM registration (HKCU\Software\Classes\CLSID)
$clsidPath = "HKCU:\Software\Classes\CLSID\$guid"
New-Item -Path $clsidPath -Force | Out-Null
Set-ItemProperty -Path $clsidPath -Name "(Default)" -Value "SolidWorksDxfBatchExporter.SwAddin"

$inprocPath = "$clsidPath\InprocServer32"
New-Item -Path $inprocPath -Force | Out-Null
Set-ItemProperty -Path $inprocPath -Name "(Default)" -Value "mscoree.dll"
Set-ItemProperty -Path $inprocPath -Name "ThreadingModel" -Value "Both"
Set-ItemProperty -Path $inprocPath -Name "Class" -Value "SolidWorksDxfBatchExporter.SwAddin"
Set-ItemProperty -Path $inprocPath -Name "Assembly" -Value "SolidWorksDxfBatchExporter, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
Set-ItemProperty -Path $inprocPath -Name "RuntimeVersion" -Value "v4.0.30319"
Set-ItemProperty -Path $inprocPath -Name "CodeBase" -Value $codeBase

$progIdPath = "$clsidPath\ProgId"
New-Item -Path $progIdPath -Force | Out-Null
Set-ItemProperty -Path $progIdPath -Name "(Default)" -Value "SolidWorksDxfBatchExporter.SwAddin"

# 2. SolidWorks Add-in Registration (HKCU\Software\SolidWorks\Addins)
$swAddinPath = "HKCU:\Software\SolidWorks\Addins\$guid"
New-Item -Path $swAddinPath -Force | Out-Null
Set-ItemProperty -Path $swAddinPath -Name "(Default)" -Value 1 -Type DWord
Set-ItemProperty -Path $swAddinPath -Name "Title" -Value "Batch 1/8`" Plate DXF Exporter" -Type String
Set-ItemProperty -Path $swAddinPath -Name "Description" -Value "Batch exports unsuppressed 1/8`" nominal plate parts to 1:1 DXF with quantity tags." -Type String

# 3. Also invoke regasm /codebase if available
$regasm = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
if (Test-Path $regasm) {
    try {
        Start-Process -FilePath $regasm -ArgumentList "`"$dllPath`" /codebase" -Wait -NoNewWindow
    } catch { }
}

Write-Host "`n[SUCCESS] SolidWorks 1/8`" DXF Batch Exporter Add-in registered successfully!" -ForegroundColor Green
Write-Host "SolidWorks Add-in GUID: $guid" -ForegroundColor White
Write-Host "Startup Load: Enabled (1)" -ForegroundColor White
