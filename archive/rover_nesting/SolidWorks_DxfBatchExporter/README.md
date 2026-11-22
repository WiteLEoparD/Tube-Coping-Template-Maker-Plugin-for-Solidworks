# SolidWorks 1/8" Plate DXF Batch Exporter Add-in

A native C# SolidWorks COM Add-in (.NET Framework 4.8, x64) that adds a single button to the **Assembly CommandManager**. When clicked, it batch exports unsuppressed 1/8" nominal plate parts to 1:1 DXFs with assembly quantity tags.

---

## Features & Export Logic

1. **Assembly Verification**: Checks that an assembly document is currently open and saved to disk.
2. **Graphics Freeze**: Sets `IModelView.EnableGraphicsUpdate = false` and `DocumentVisible = false` to eliminate UI redraw latency.
3. **Quantity Manifest**: Recursively traverses all assembly levels via `GetComponents(false)`, checks suppression/envelope states, and counts unique `.sldprt` files.
4. **Silent Processing**: Loads parts silently in memory with `swOpenDocOptions_Silent`.
5. **Thickness Filter**: Calculates bounding box thickness $\min(dx, dy, dz)$ on solid bodies and strictly matches nominal 1/8" (`0.00254 m` to `0.00381 m` / `0.100"` to `0.150"`).
6. **Geometry Extraction**: Evaluates planar faces with `IFace2.GetArea()` and extracts the surface normal vector.
7. **1:1 DXF Export**: Selects the largest planar face and executes `IPartDoc.ExportToDWG2` (`swExportToDWG_ExportSelectedFacesOrLoops`).
8. **File Naming & RAM Cleanup**: Saves files as `PartName_QTY-[Count].dxf` to the assembly folder, immediately closing parts with `CloseDoc` and calling `Marshal.ReleaseComObject` on all RCWs.

---

## Directory Structure

```
SolidWorks_DxfBatchExporter/
├── bin/x64/Release/
│   └── SolidWorksDxfBatchExporter.dll   <-- Compiled 64-bit Add-in
├── lib/
│   ├── SolidWorks.Interop.sldworks.dll
│   ├── SolidWorks.Interop.swconst.dll
│   └── SolidWorks.Interop.swpublished.dll
├── RegisterAddin.bat                    <-- One-click registration
├── RegisterAddin.ps1                    <-- PowerShell registration script
├── UnregisterAddin.ps1                  <-- Unregister from HKCU
├── SolidWorksDxfBatchExporter.csproj    <-- SDK-style project file
└── SwAddin.cs                           <-- Complete Add-in implementation
```

---

## Installation & Registration

- **Install/Register**: Run `RegisterAddin.bat` or run `RegisterAddin.ps1` in PowerShell.
- **Unregister**: Run `UnregisterAddin.ps1` in PowerShell.
