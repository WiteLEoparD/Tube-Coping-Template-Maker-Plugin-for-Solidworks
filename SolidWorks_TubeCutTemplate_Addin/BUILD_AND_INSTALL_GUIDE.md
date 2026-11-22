# Build, Installation & Debugging Guide

### Native SolidWorks C# Add-in for Baja SAE Tube Cut Templates

---

## 1. Prerequisites & Environment Setup

To build and run this SolidWorks Add-in, your development workstation requires:
1. **Operating System:** Windows 10 or Windows 11 (64-bit).
2. **CAD System:** SolidWorks 2022, 2023, 2024, 2025, or 2026 (Installed in default path `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS`).
3. **IDE / Compiler:**
   - **Visual Studio 2022** (Community, Professional, or Enterprise) with **.NET Desktop Development** workload installed, OR
   - **.NET SDK** (command line `dotnet build` with .NET Framework 4.8 targeting pack).
4. **Target Framework:** .NET Framework 4.8. (SolidWorks in-process add-ins run on .NET Framework 4.8).

---

## 2. SolidWorks Interop Assembly References

The project requires three official SolidWorks Primary Interop Assemblies (PIAs):
- `SolidWorks.Interop.sldworks.dll` (Core application and model object model)
- `SolidWorks.Interop.swconst.dll` (Enums and constant definitions)
- `SolidWorks.Interop.swpublished.dll` (SwAddin and COM published interfaces)

These assemblies are located in your SolidWorks installation directory:
```
C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\
```
For convenience and portability, pre-verified copies of these DLLs are placed in the solution's `lib\` directory:
```
SolidWorks_TubeCutTemplate_Addin\lib\
```

In `SolidWorks_TubeCutTemplate_Addin.csproj`, the assemblies are referenced with `<EmbedInteropTypes>False</EmbedInteropTypes>` and `<Private>True</Private>` to ensure standard runtime resolution.

---

## 3. Building the Solution

### Option A: Building via Visual Studio 2022
1. Launch Visual Studio 2022.
2. Open `SolidWorks_TubeCutTemplate_Addin.sln`.
3. In the toolbar, set the Solution Configuration to **Release** and Solution Platform to **x64**.
4. Right-click the Solution in Solution Explorer and click **Rebuild Solution**.
5. The build will produce:
   ```
   SolidWorks_TubeCutTemplate_Addin\bin\x64\Release\BajaSAE.TubeCutTemplateAddin.dll
   ```

### Option B: Building via Command Line (PowerShell / CMD)
Open PowerShell in the project directory and run:
```powershell
$env:APPDATA = "$pwd\.appdata"
$env:DOTNET_CLI_HOME = "$pwd\.dotnet_cli"
dotnet build SolidWorks_TubeCutTemplate_Addin\SolidWorks_TubeCutTemplate_Addin.csproj -c Release -p:Platform=x64
```
Output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 4. Registering the Add-in with SolidWorks

SolidWorks loads native Add-ins via COM (Component Object Model). SolidWorks reads Add-in registration from the Windows Registry:
- Current User: `HKEY_CURRENT_USER\Software\SolidWorks\Addins\{769C5C1D-E143-4D90-A951-BEB1309CD51E}`
- Local Machine: `HKEY_LOCAL_MACHINE\SOFTWARE\SolidWorks\Addins\{769C5C1D-E143-4D90-A951-BEB1309CD51E}`

### Method 1: Using the Red Lion Racing Graphical Installer (Recommended for Team Members)
1. Double-click [`RedLionRacing_TubeTemplates_Installer.exe`](file:///c:/Users/mhasa/OneDrive/Documents/Auto%20template%20generator/SolidWorks_TubeCutTemplate_Addin/dist/RedLionRacing_TubeTemplates_Installer.exe) (located in `dist\` or inside `RedLionRacing_SolidWorks_TubeTemplates_v1.0.zip`).
2. Click **"Install / Update Add-in"**.
3. The installer automatically extracts files to `%LOCALAPPDATA%\RedLionRacing\SolidWorks_TubeTemplates\`, registers the COM server, and enables the SolidWorks startup key.
4. **No Administrator Rights Required:** Uses per-user COM registration under `HKCU\Software\Classes\CLSID`.

### Method 2: Using the One-Click Batch Script
Run `Install_OneClick.bat` from the distribution folder.

### Method 3: Manual RegAsm Registration (For System Administrators)
Run 64-bit RegAsm directly from an elevated prompt:
```cmd
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" "C:\YourPath\SolidWorks_TubeCutTemplate_Addin\bin\x64\Release\BajaSAE.TubeCutTemplateAddin.dll" /codebase
```

### Verification in SolidWorks
1. Start SolidWorks.
2. Go to **Tools > Add-ins...** (or click the gear icon dropdown > Add-ins).
3. Scroll down to the **Other Add-ins** section.
4. Verify that **"Baja SAE Tube Cut Templates"** is listed.
5. Check both checkboxes:
   - **Active Add-ins** (Loads add-in for the current session)
   - **Start Up** (Loads add-in automatically on future SolidWorks launches)
6. Open any part or assembly. Under the **Weldments** or **Assembly** CommandManager tab, the **"Tube Cut Templates"** icon will be active.

---

## 5. Unregistering / Uninstalling the Add-in

To unregister the Add-in:
1. Open an Administrator Command Prompt.
2. Run:
   ```cmd
   cd SolidWorks_TubeCutTemplate_Addin\Scripts
   UnregisterAddin.bat
   ```
This removes the COM class registration and cleans up the SolidWorks registry keys.

---

## 6. Debugging the Add-in in Visual Studio

To debug and step through code while running SolidWorks:
1. In Visual Studio, right-click `SolidWorks_TubeCutTemplate_Addin` > **Properties**.
2. Go to the **Debug** tab.
3. Select **Start external program** and browse to your SolidWorks executable:
   ```
   C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe
   ```
4. Set a breakpoint in `SwAddin.ConnectToSW()`, `TubeTemplatePMPPage.Show()`, or `SpikeFreeUnroller.UnrollTube()`.
5. Press **F5** (Start Debugging).
6. Visual Studio will launch SolidWorks and attach the debugger to `SLDWORKS.exe`.
7. In SolidWorks, open a chassis model and click **"Tube Cut Templates"**. Visual Studio will hit your breakpoint.

---

## 7. Troubleshooting FAQ

### Problem 1: "RegAsm : error RA0000 : Administrator permissions are needed"
**Solution:** Right-click Command Prompt or PowerShell and choose **Run as Administrator** before executing `RegisterAddin.bat`.

### Problem 2: "The Add-in does not appear in SolidWorks Add-ins dialog"
**Solution:**
1. Ensure you used the 64-bit `RegAsm.exe` from `Framework64` (NOT the 32-bit `Framework` folder). SolidWorks is a 64-bit application and ignores 32-bit COM registrations.
2. Check that the registry key exists under `HKEY_CURRENT_USER\Software\SolidWorks\Addins\{769C5C1D-E143-4D90-A951-BEB1309CD51E}` with DWORD `(Default) = 1`.

### Problem 3: "CommandManager button is grayed out"
**Solution:** The button automatically checks `EnableTubeTemplatePMP()`. It is active when an active Part (`.sldprt`) or Assembly (`.sldasm`) document is open in the graphics window. It is disabled when no document or a drawing is open.

### Problem 4: "SolidWorks crashes on startup during CHECKS_ENABLE_WITH_LOG"
**Solution:**
1. This crash was caused by an inverted cookie argument in `SetAddinCallbackInfo2` and missing `[ClassInterface(ClassInterfaceType.AutoDual)]`, which caused SolidWorks to attempt dispatching enable checks against a NULL callback pointer.
2. This has been resolved in the codebase. Rebuild the solution and ensure the updated `BajaSAE.TubeCutTemplateAddin.dll` in `bin\x64\Release\` is active.
