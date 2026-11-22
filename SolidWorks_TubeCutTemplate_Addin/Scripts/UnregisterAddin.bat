@echo off
:: ============================================================================
:: Baja SAE Tube Cut Templates SolidWorks Add-in - Unregistration Script
:: ============================================================================

set "REGASM=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
set "DLL_PATH=%~dp0..\bin\x64\Release\BajaSAE.TubeCutTemplateAddin.dll"

if not exist "%DLL_PATH%" (
    set "DLL_PATH=%~dp0..\bin\x64\Debug\BajaSAE.TubeCutTemplateAddin.dll"
)

echo ============================================================================
echo  Unregistering Baja SAE Tube Cut Templates Add-in from SolidWorks...
echo ============================================================================

if exist "%DLL_PATH%" (
    "%REGASM%" /u "%DLL_PATH%"
)

:: Clean up registry keys
reg delete "HKCU\Software\SolidWorks\Addins\{769C5C1D-E143-4D90-A951-BEB1309CD51E}" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\SolidWorks\Addins\{769C5C1D-E143-4D90-A951-BEB1309CD51E}" /f >nul 2>&1

echo.
echo [SUCCESS] Add-in successfully unregistered.
pause
