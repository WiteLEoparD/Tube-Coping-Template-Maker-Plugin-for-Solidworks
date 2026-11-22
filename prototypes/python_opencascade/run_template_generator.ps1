# =========================================================================
#  BSAE 2027 Rev A - Tube Cut Template Generator PowerShell Launcher
# =========================================================================

param(
    [string]$InputCad = "",
    [double]$Collar = 1.5,
    [string]$OutputDir = "./output_templates",
    [switch]$Demo
)

$FreeCadPy = "C:\Program Files\FreeCAD 1.1\bin\python.exe"

if (-not (Test-Path $FreeCadPy)) {
    Write-Error "FreeCAD Python environment not found at: $FreeCadPy"
    exit 1
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$CliScript = Join-Path $ScriptDir "auto_template_cli.py"

if ($Demo -or [string]::IsNullOrWhiteSpace($InputCad)) {
    Write-Host "[INFO] Running parametric demo generator..." -ForegroundColor Cyan
    & $FreeCadPy $CliScript --demo --output-dir (Resolve-Path $OutputDir -ErrorAction SilentlyContinue)
} else {
    Write-Host "[INFO] Processing CAD file: $InputCad" -ForegroundColor Cyan
    & $FreeCadPy $CliScript $InputCad --collar $Collar --output-dir $OutputDir
}
