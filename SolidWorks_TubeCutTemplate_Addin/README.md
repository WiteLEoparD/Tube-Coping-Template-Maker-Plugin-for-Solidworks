# Red Lion Racing — SolidWorks Add-in Implementation

This directory contains the primary C# .NET solution for the **Red Lion Racing Tube Cut Template Add-in**.

## Projects

1. **`SolidWorks_TubeCutTemplate_Addin.csproj`**
   - The core COM Add-in library targeting .NET Framework 4.8 (`x64`).
   - Implements `ISwAddin` and registers CommandManager buttons under Weldments and Assemblies.
   - Houses the native PropertyManagerPage (PMP), geometric analyzer, unrolling engine, and zero-dependency vector PDF, DXF, and SVG exporters.

2. **`Installer/Installer.csproj`**
   - Standalone Windows Forms single-file executable (`RedLionRacing_TubeTemplates_Installer.exe`).
   - Embeds payload assemblies and performs per-user COM registration in `HKCU\Software\Classes\CLSID\{769C5C1D-E143-4D90-A951-BEB1309CD51E}`.
   - Requires zero administrator privileges.

3. **`StandaloneDemo/StandaloneDemo.csproj`**
   - Standalone test runner that verifies geometric unrolling, centerline clocking, and template generation without launching SolidWorks.

## Building

```powershell
# From the repository root:
.\build.ps1 -Configuration Release -Platform x64
```
