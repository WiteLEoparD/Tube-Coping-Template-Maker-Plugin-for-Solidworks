using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

[assembly: ComVisible(true)]

namespace SolidWorksDxfBatchExporter
{
    /// <summary>
    /// Barebones SolidWorks COM Add-in:
    /// Traverses the active assembly, identifies unsuppressed 1/8" nominal plate parts,
    /// extracts their largest planar faces, and batch-exports them to 1:1 DXFs with quantity tags.
    /// </summary>
    [Guid("A4D8B923-3E4C-4F91-8D62-7F0F3D5B91A2")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class SwAddin : ISwAddin
    {
        public const string AddinTitle = "Batch 1/8\" Plate DXF Exporter";
        public const string AddinDescription = "Batch exports unsuppressed 1/8\" nominal plate parts to 1:1 DXF with quantity tags.";

        private const int CommandGroupId = 2001;
        private const int CommandItemId = 1;

        // Metric window for nominal 1/8" (0.125" = 0.003175 m)
        private const double MinNominalThicknessMeters = 0.00254; // 0.100" (2.54 mm)
        private const double MaxNominalThicknessMeters = 0.00381; // 0.150" (3.81 mm)

        private ISldWorks _swApp;
        private int _addinCookie;
        private ICommandManager _cmdMgr;

        #region ISwAddin Implementation

        public bool ConnectToSW(object ThisSW, int cookie)
        {
            try
            {
                _swApp = (ISldWorks)ThisSW;
                _addinCookie = cookie;

                // Register callback sink with SolidWorks
                _swApp.SetAddinCallbackInfo2(0, this, cookie);

                // Construct CommandManager & Tools menu UI
                SetupCommandManager();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConnectToSW] Error: {ex.Message}");
                return true;
            }
        }

        public bool DisconnectFromSW()
        {
            try
            {
                TeardownCommandManager();

                _cmdMgr = null;
                _swApp = null;

                // Clean up any remaining Runtime Callable Wrappers (RCWs)
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                return true;
            }
            catch
            {
                return true;
            }
        }

        #endregion

        #region CommandManager UI Setup

        private void SetupCommandManager()
        {
            try
            {
                _cmdMgr = _swApp.GetCommandManager(_addinCookie);
                if (_cmdMgr == null) return;

                int cmdErrors = 0;
                ICommandGroup cmdGroup = _cmdMgr.CreateCommandGroup2(
                    CommandGroupId,
                    AddinTitle,
                    AddinDescription,
                    "Batch export 1/8\" plates to DXF",
                    -1,
                    true,
                    ref cmdErrors
                );

                if (cmdGroup == null) return;

                // Add button item (-1 image index renders a standard text button)
                int cmdIndex = cmdGroup.AddCommandItem2(
                    "Export 1/8\" DXFs",
                    -1,
                    "Batch export unsuppressed 1/8\" nominal plates to 1:1 DXF files",
                    "Export 1/8\" DXFs",
                    -1,
                    nameof(ExecuteBatchDxfExport),
                    nameof(EnableBatchDxfExport),
                    CommandItemId,
                    (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem)
                );

                cmdGroup.HasToolbar = true;
                cmdGroup.HasMenu = true;
                cmdGroup.Activate();

                int docTypeAssembly = (int)swDocumentTypes_e.swDocASSEMBLY;

                // 1. Add directly to the Tools menu for Assembly documents (guaranteed access)
                try
                {
                    _swApp.AddMenuItem5(
                        docTypeAssembly,
                        _addinCookie,
                        "Batch Export 1/8\" Plate DXFs@Tools",
                        -1,
                        nameof(ExecuteBatchDxfExport),
                        nameof(EnableBatchDxfExport),
                        "Batch export unsuppressed 1/8\" plate parts to 1:1 DXFs",
                        ""
                    );
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddMenuItem5] Error: {ex.Message}");
                }

                // 2. Add dedicated "Batch DXF" CommandManager Tab (avoids altering locked built-in tabs)
                ICommandTab tab = _cmdMgr.GetCommandTab(docTypeAssembly, "Batch DXF");
                if (tab == null)
                {
                    tab = _cmdMgr.AddCommandTab(docTypeAssembly, "Batch DXF");
                }

                if (tab != null)
                {
                    int cmdId = cmdGroup.get_CommandID(cmdIndex);
                    if (cmdId != 0)
                    {
                        // Prevent duplicate buttons when reloaded
                        bool exists = false;
                        object[] existingBoxes = tab.CommandTabBoxes() as object[];
                        if (existingBoxes != null)
                        {
                            foreach (object boxObj in existingBoxes)
                            {
                                if (boxObj is ICommandTabBox tb)
                                {
                                    object cmdObj = null;
                                    object styleObj = null;
                                    tb.GetCommands(out cmdObj, out styleObj);
                                    int[] cmds = cmdObj as int[];
                                    if (cmds != null && Array.IndexOf(cmds, cmdId) >= 0)
                                    {
                                        exists = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (!exists)
                        {
                            ICommandTabBox tabBox = tab.AddCommandTabBox();
                            if (tabBox != null)
                            {
                                int[] cmdIDs = new int[] { cmdId };
                                int[] textTypes = new int[] { (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow };
                                tabBox.AddCommands(cmdIDs, textTypes);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SetupCommandManager] Error: {ex.Message}");
            }
        }

        private void TeardownCommandManager()
        {
            if (_cmdMgr != null)
            {
                try
                {
                    _cmdMgr.RemoveCommandGroup2(CommandGroupId, true);
                }
                catch { }
            }
        }

        #endregion

        #region Command Callbacks

        /// <summary>
        /// Enable callback: Activates button only when an Assembly document is open.
        /// </summary>
        public int EnableBatchDxfExport()
        {
            if (_swApp == null) return 0;

            IModelDoc2 activeDoc = _swApp.ActiveDoc as IModelDoc2;
            if (activeDoc != null && activeDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                return 1; // Enabled
            }
            return 0; // Disabled
        }

        /// <summary>
        /// Main execution workflow triggered by clicking the CommandManager button.
        /// </summary>
        public void ExecuteBatchDxfExport()
        {
            if (_swApp == null) return;

            // 1. Prerequisites: Verify active assembly and saved status
            IModelDoc2 activeDoc = _swApp.ActiveDoc as IModelDoc2;
            if (activeDoc == null || activeDoc.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                _swApp.SendMsgToUser2(
                    "Please open and activate an Assembly document first.",
                    (int)swMessageBoxIcon_e.swMbWarning,
                    (int)swMessageBoxBtn_e.swMbOk
                );
                return;
            }

            string asmPath = activeDoc.GetPathName();
            if (string.IsNullOrEmpty(asmPath))
            {
                _swApp.SendMsgToUser2(
                    "The active assembly must be saved to disk before running the DXF export.",
                    (int)swMessageBoxIcon_e.swMbWarning,
                    (int)swMessageBoxBtn_e.swMbOk
                );
                return;
            }

            string asmDir = Path.GetDirectoryName(asmPath);
            string asmTitle = activeDoc.GetTitle();

            // Freeze screen updates to maximize performance and prevent redraw flickering
            IModelView activeView = activeDoc.ActiveView as IModelView;
            bool prevGraphicsUpdate = true;
            if (activeView != null)
            {
                prevGraphicsUpdate = activeView.EnableGraphicsUpdate;
                activeView.EnableGraphicsUpdate = false;
            }

            // Suppress background UI window creation for opened parts
            _swApp.DocumentVisible(false, (int)swDocumentTypes_e.swDocPART);

            try
            {
                // 2. Quantity Manifest: Traverse unsuppressed components across all levels
                AssemblyDoc asmDoc = (AssemblyDoc)activeDoc;
                object[] components = asmDoc.GetComponents(false) as object[];
                Dictionary<string, int> manifest = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                if (components != null)
                {
                    foreach (object compObj in components)
                    {
                        if (compObj is IComponent2 comp)
                        {
                            try
                            {
                                if (!comp.IsSuppressed() && !comp.IsEnvelope())
                                {
                                    string compPath = comp.GetPathName();
                                    if (!string.IsNullOrEmpty(compPath) &&
                                        Path.GetExtension(compPath).Equals(".sldprt", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (manifest.ContainsKey(compPath))
                                        {
                                            manifest[compPath]++;
                                        }
                                        else
                                        {
                                            manifest[compPath] = 1;
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(comp);
                            }
                        }
                        else if (compObj != null)
                        {
                            Marshal.ReleaseComObject(compObj);
                        }
                    }
                }

                if (manifest.Count == 0)
                {
                    _swApp.SendMsgToUser2(
                        "No unsuppressed .sldprt components found in the assembly.",
                        (int)swMessageBoxIcon_e.swMbInformation,
                        (int)swMessageBoxBtn_e.swMbOk
                    );
                    return;
                }

                int exportedCount = 0;
                int skippedCount = 0;

                // 3. Silent Processing: Loop through unique parts
                foreach (KeyValuePair<string, int> entry in manifest)
                {
                    string partPath = entry.Key;
                    int qty = entry.Value;
                    string partName = Path.GetFileNameWithoutExtension(partPath);

                    int openErrors = 0;
                    int openWarnings = 0;

                    // Open part silently in the background
                    IModelDoc2 partModel = _swApp.OpenDoc6(
                        partPath,
                        (int)swDocumentTypes_e.swDocPART,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                        "",
                        ref openErrors,
                        ref openWarnings
                    ) as IModelDoc2;

                    if (partModel == null) continue;

                    string partTitle = partModel.GetTitle();
                    PartDoc partDoc = partModel as PartDoc;

                    try
                    {
                        // 4. Thickness Filter: Get solid body bounding box & filter for strictly 1/8" nominal
                        object[] bodies = partDoc.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                        if (bodies == null || bodies.Length == 0)
                        {
                            skippedCount++;
                            continue;
                        }

                        IBody2 targetBody = null;
                        foreach (object bObj in bodies)
                        {
                            if (bObj is IBody2 body)
                            {
                                if (targetBody == null)
                                {
                                    double[] box = body.GetBodyBox() as double[];
                                    if (box != null && box.Length >= 6)
                                    {
                                        double dx = Math.Abs(box[3] - box[0]);
                                        double dy = Math.Abs(box[4] - box[1]);
                                        double dz = Math.Abs(box[5] - box[2]);

                                        double thickness = Math.Min(dx, Math.Min(dy, dz));
                                        if (thickness >= MinNominalThicknessMeters && thickness <= MaxNominalThicknessMeters)
                                        {
                                            targetBody = body;
                                            continue; // Retain targetBody COM reference
                                        }
                                    }
                                }
                                Marshal.ReleaseComObject(body);
                            }
                            else if (bObj != null)
                            {
                                Marshal.ReleaseComObject(bObj);
                            }
                        }

                        if (targetBody == null)
                        {
                            skippedCount++;
                            continue;
                        }

                        IFace2 largestPlanarFace = null;
                        double maxArea = -1.0;
                        double[] faceNormal = null;

                        try
                        {
                            // 5. Geometry Extraction: Find largest planar face on body
                            object[] faces = targetBody.GetFaces() as object[];
                            if (faces != null)
                            {
                                foreach (object fObj in faces)
                                {
                                    if (fObj is IFace2 face)
                                    {
                                        Surface surf = face.GetSurface() as Surface;
                                        bool isPlane = false;
                                        double[] pParams = null;

                                        if (surf != null)
                                        {
                                            isPlane = surf.IsPlane();
                                            if (isPlane)
                                            {
                                                pParams = surf.PlaneParams as double[];
                                            }
                                            Marshal.ReleaseComObject(surf);
                                        }

                                        if (isPlane)
                                        {
                                            double area = face.GetArea();
                                            if (area > maxArea)
                                            {
                                                maxArea = area;
                                                if (largestPlanarFace != null)
                                                {
                                                    Marshal.ReleaseComObject(largestPlanarFace);
                                                }
                                                largestPlanarFace = face;

                                                if (pParams != null && pParams.Length >= 6)
                                                {
                                                    faceNormal = new double[] { pParams[3], pParams[4], pParams[5] };
                                                }
                                                continue; // Retain largest planar face COM reference
                                            }
                                        }
                                        Marshal.ReleaseComObject(face);
                                    }
                                    else if (fObj != null)
                                    {
                                        Marshal.ReleaseComObject(fObj);
                                    }
                                }
                            }

                            if (largestPlanarFace == null)
                            {
                                continue;
                            }

                            // 6. DXF Export: Temporarily activate part and select largest planar face
                            int actErrors = 0;
                            IModelDoc2 activePartDoc = _swApp.ActivateDoc3(
                                partTitle,
                                true,
                                (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref actErrors
                            ) as IModelDoc2;

                            if (activePartDoc == null)
                            {
                                activePartDoc = partModel;
                            }

                            activePartDoc.ClearSelection2(true);

                            IEntity faceEntity = largestPlanarFace as IEntity;
                            bool isSelected = false;
                            if (faceEntity != null)
                            {
                                isSelected = faceEntity.Select4(false, null);
                            }

                            if (isSelected)
                            {
                                // 12-double alignment array:
                                // [0-2]: Origin, [3-5]: X-axis vector, [6-8]: Y-axis vector, [9-11]: Normal vector
                                double[] alignment = new double[12];
                                if (faceNormal != null && faceNormal.Length == 3)
                                {
                                    alignment[9] = faceNormal[0];
                                    alignment[10] = faceNormal[1];
                                    alignment[11] = faceNormal[2];
                                }

                                // 7. Naming & Cleanup: PartName_QTY-[Count].dxf in Assembly Directory
                                string dxfFileName = $"{partName}_QTY-[{qty}].dxf";
                                string dxfFilePath = Path.Combine(asmDir, dxfFileName);

                                bool success = ((PartDoc)activePartDoc).ExportToDWG2(
                                    dxfFilePath,
                                    partPath,
                                    (int)swExportToDWG_e.swExportToDWG_ExportSelectedFacesOrLoops,
                                    true,       // ExportToSingleFile
                                    alignment,  // 12-element coordinate alignment
                                    false,      // IsXDirFlipped
                                    false,      // IsYDirFlipped
                                    0,          // SheetMetalOptions (no kerf offset)
                                    null        // Views
                                );

                                if (success)
                                {
                                    exportedCount++;
                                }
                            }
                        }
                        finally
                        {
                            if (largestPlanarFace != null)
                            {
                                Marshal.ReleaseComObject(largestPlanarFace);
                                largestPlanarFace = null;
                            }

                            if (targetBody != null)
                            {
                                Marshal.ReleaseComObject(targetBody);
                                targetBody = null;
                            }
                        }
                    }
                    finally
                    {
                        if (partModel != null)
                        {
                            Marshal.ReleaseComObject(partModel);
                            partModel = null;
                        }

                        // Close part document immediately after export to prevent RAM overflow
                        _swApp.CloseDoc(partTitle);
                    }
                }

                // Reactivate root assembly document
                int asmActErr = 0;
                _swApp.ActivateDoc3(
                    asmTitle,
                    true,
                    (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                    ref asmActErr
                );

                _swApp.SendMsgToUser2(
                    $"Batch DXF Export Completed!\n\n" +
                    $"• Exported 1/8\" Plate Parts: {exportedCount}\n" +
                    $"• Skipped Non-1/8\" Parts: {skippedCount}\n" +
                    $"• Destination: {asmDir}",
                    (int)swMessageBoxIcon_e.swMbInformation,
                    (int)swMessageBoxBtn_e.swMbOk
                );
            }
            catch (Exception ex)
            {
                _swApp.SendMsgToUser2(
                    $"Error during batch DXF export: {ex.Message}",
                    (int)swMessageBoxIcon_e.swMbStop,
                    (int)swMessageBoxBtn_e.swMbOk
                );
            }
            finally
            {
                // Restore part document visibility
                _swApp.DocumentVisible(true, (int)swDocumentTypes_e.swDocPART);

                // Restore active view screen updates
                if (activeView != null)
                {
                    activeView.EnableGraphicsUpdate = prevGraphicsUpdate;
                    Marshal.ReleaseComObject(activeView);
                    activeView = null;
                }
            }
        }

        #endregion

        #region COM Registration Functions

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                string clsid = t.GUID.ToString("B");

                // HKCU registration allows installation without elevated admin rights
                RegisterInKey(Registry.CurrentUser, clsid);

                // HKLM registration (if running in elevated context)
                try
                {
                    RegisterInKey(Registry.LocalMachine, clsid);
                }
                catch
                {
                    // Fall back to HKCU if HKLM access is restricted
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ComRegisterFunction] Error: {ex.Message}");
            }
        }

        private static void RegisterInKey(RegistryKey rootKey, string clsid)
        {
            string keyPath = $@"Software\SolidWorks\Addins\{clsid}";
            using (RegistryKey addinKey = rootKey.CreateSubKey(keyPath))
            {
                if (addinKey != null)
                {
                    addinKey.SetValue(null, 1, RegistryValueKind.DWord); // 1 = Load on SolidWorks startup
                    addinKey.SetValue("Title", AddinTitle, RegistryValueKind.String);
                    addinKey.SetValue("Description", AddinDescription, RegistryValueKind.String);
                }
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                string clsid = t.GUID.ToString("B");
                string keyPath = $@"Software\SolidWorks\Addins\{clsid}";

                try { Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); } catch { }
                try { Registry.LocalMachine.DeleteSubKeyTree(keyPath, false); } catch { }
            }
            catch { }
        }

        #endregion
    }
}
