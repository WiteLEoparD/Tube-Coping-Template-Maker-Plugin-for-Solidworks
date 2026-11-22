@echo off
setlocal
title Push to GitHub - Tube Coping Template Plugin for SolidWorks

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Push_To_GitHub.ps1"

pause
