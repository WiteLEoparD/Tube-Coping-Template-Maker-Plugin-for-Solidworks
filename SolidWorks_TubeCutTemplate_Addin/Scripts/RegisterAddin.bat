@echo off
:: ============================================================================
:: Baja SAE Tube Cut Templates SolidWorks Add-in - Registration Script
:: Registers the 64-bit .NET COM Add-in DLL for SolidWorks 2022 - 2026
:: ============================================================================

setlocal enabledelayedexpansion

set "REGASM=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
set "DLL_PATH=%~dp0..\bin\x64\Release\BajaSAE.TubeCutTemplateAddin.dll"

if not exist "%REGASM%" (
    echo [ERROR] 64-bit RegAsm.exe not found at: %REGASM%
    pause
    exit /b 1
)

if not exist "%DLL_PATH%" (
    echo [INFO] Release DLL not found. Checking Debug build...
    set "DLL_PATH=%~dp0..\bin\x64\Debug\BajaSAE.TubeCutTemplateAddin.dll"
)

if not exist "%DLL_PATH%" (
    echo [ERROR] Add-in DLL not found. Please build the solution first in Visual Studio or via dotnet build.
    echo Searched: %DLL_PATH%
    pause
    exit /b 1
)

echo ============================================================================
echo  Registering Baja SAE Tube Cut Templates Add-in with SolidWorks...
echo  DLL: %DLL_PATH%
echo ============================================================================

"%REGASM%" "%DLL_PATH%" /codebase

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] Add-in successfully registered!
    echo SolidWorks will now load 'Baja SAE Tube Cut Templates' on startup.
    echo Look for 'Tube Cut Templates' under the Weldments and Assembly CommandManager tabs.
) else (
    echo.
    echo [WARNING] Registration returned error code %ERRORLEVEL%.
    echo Note: If writing to HKLM failed, right-click this script and 'Run as Administrator'.
)

echo.
pause
