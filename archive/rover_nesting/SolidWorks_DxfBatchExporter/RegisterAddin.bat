@echo off
setlocal
cd /d "%~dp0"

set "REGASM=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
set "DLL_PATH=%~dp0bin\x64\Release\SolidWorksDxfBatchExporter.dll"

echo ============================================================================
echo   SolidWorks 1/8" Plate DXF Exporter - COM Registration
echo ============================================================================
echo DLL: %DLL_PATH%
echo.

if not exist "%REGASM%" (
    echo [ERROR] 64-bit RegAsm.exe not found at: %REGASM%
    pause
    exit /b 1
)

if not exist "%DLL_PATH%" (
    echo [ERROR] DLL not found at: %DLL_PATH%
    pause
    exit /b 1
)

echo Registering 64-bit COM Server with RegAsm...
"%REGASM%" "%DLL_PATH%" /codebase

if exist "%~dp0register.reg" (
    echo.
    echo Importing registry entries from register.reg...
    reg import "%~dp0register.reg"
)

echo.
echo ============================================================================
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Add-in registration completed!
    echo SolidWorks will load 'Batch 1/8" Plate DXF Exporter' on startup.
) else (
    echo [NOTICE] If registration failed, please right-click RegisterAddin.bat
    echo          and select 'Run as administrator'.
)
echo ============================================================================
echo.
pause
