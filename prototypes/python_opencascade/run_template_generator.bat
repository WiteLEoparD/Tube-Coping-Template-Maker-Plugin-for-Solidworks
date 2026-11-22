@echo off
REM =========================================================================
REM  BSAE 2027 Rev A - Tube Cut Template Generator Launcher
REM =========================================================================

set "FREECAD_PY=C:\Program Files\FreeCAD 1.1\bin\python.exe"

if not exist "%FREECAD_PY%" (
    echo [ERROR] FreeCAD Python environment not found at: "%FREECAD_PY%"
    echo Please ensure FreeCAD 1.1 is installed in C:\Program Files\FreeCAD 1.1
    pause
    exit /b 1
)

if "%~1"=="" (
    echo [INFO] No input CAD file specified. Running parametric demo...
    "%FREECAD_PY%" "%~dp0auto_template_cli.py" --demo --output-dir "%~dp0output_templates"
) else (
    echo [INFO] Processing CAD model: %1
    "%FREECAD_PY%" "%~dp0auto_template_cli.py" %*
)

echo.
echo Template generation finished. Press any key to exit.
pause
