$guid = "{A4D8B923-3E4C-4F91-8D62-7F0F3D5B91A2}"
try { Remove-Item -Path "HKCU:\Software\Classes\CLSID\$guid" -Recurse -Force -ErrorAction SilentlyContinue } catch { }
try { Remove-Item -Path "HKCU:\Software\SolidWorks\Addins\$guid" -Recurse -Force -ErrorAction SilentlyContinue } catch { }
Write-Host "[SUCCESS] Unregistered SolidWorks Add-in $guid from HKCU." -ForegroundColor Green
