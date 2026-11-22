using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using BajaSAE.TubeCutTemplateAddin.UI;

namespace BajaSAE.TubeCutTemplateAddin
{
    /// <summary>
    /// Native SolidWorks COM Add-in Entry Point.
    /// Implements ISwAddin to integrate directly into SolidWorks CommandManager, menus,
    /// and PropertyManagerPage UI for Baja SAE 1:1 Tube Cut Templates.
    /// </summary>
    [Guid("769C5C1D-E143-4D90-A951-BEB1309CD51E")]
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class SwAddin : ISwAddin
    {
        public const string AddinTitle = "Red Lion Racing Tube Templates";
        public const string AddinDescription = "Auto-generates 1:1 scale printable cut templates for Red Lion Racing (University of Manitoba).";

        private ISldWorks _swApp;
        private int _addinCookie;
        private ICommandManager _cmdMgr;
        private TubeTemplatePMPPage _pmpPage;

        public const int CommandGroupId = 101;
        public const int CommandItemId = 1;

        #region ISwAddin Implementation

        public bool ConnectToSW(object ThisSW, int cookie)
        {
            try
            {
                _swApp = (ISldWorks)ThisSW;
                _addinCookie = cookie;

                // Set callback information:
                // ModuleHandle = 0, AddinCallbacks = this, Cookie = cookie
                _swApp.SetAddinCallbackInfo2(0, this, cookie);

                // Setup CommandManager Ribbon and Menus
                SetupCommandManager();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConnectToSW] Critical Error: {ex.Message}");
                return true; // Return true to prevent SolidWorks hard crash
            }
        }

        public bool DisconnectFromSW()
        {
            try
            {
                RemoveCommandManager();

                _pmpPage = null;
                _cmdMgr = null;
                _swApp = null;

                // Force garbage collection of COM references
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

        #region CommandManager & UI Setup

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
                    "Generate 1:1 fishmouth tube cut wraps",
                    -1,
                    true,
                    ref cmdErrors
                );

                if (cmdGroup == null) return;

                // Add command item:
                // ImageListIndex is -1 because no icon list is assigned (prevents NULL pointer dereference in SolidWorks)
                int cmdIndex = cmdGroup.AddCommandItem2(
                    "Tube Cut Templates",
                    -1,
                    "Generate 1:1 cut templates for selected round chassis tubes",
                    "Tube Cut Templates",
                    -1,
                    nameof(ShowTubeTemplatePMP),
                    nameof(EnableTubeTemplatePMP),
                    CommandItemId,
                    (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem)
                );

                cmdGroup.HasToolbar = true;
                cmdGroup.HasMenu = true;
                cmdGroup.Activate();

                // 1. Create dedicated "Tube Templates" CommandManager Tab for Part documents
                AddCommandToTab((int)swDocumentTypes_e.swDocPART, "Tube Templates", cmdGroup, cmdIndex);

                // 2. Create dedicated "Tube Templates" CommandManager Tab for Assembly documents
                AddCommandToTab((int)swDocumentTypes_e.swDocASSEMBLY, "Tube Templates", cmdGroup, cmdIndex);

                // 3. Also attempt adding to native Weldments and Assembly tabs if accessible
                AddCommandToTab((int)swDocumentTypes_e.swDocPART, "Weldments", cmdGroup, cmdIndex);
                AddCommandToTab((int)swDocumentTypes_e.swDocASSEMBLY, "Assembly", cmdGroup, cmdIndex);

                // Add top-level menu under Tools
                try
                {
                    _swApp.AddMenuItem5(
                        (int)swDocumentTypes_e.swDocPART | (int)swDocumentTypes_e.swDocASSEMBLY,
                        _addinCookie,
                        "Baja SAE Tube Cut Templates@Tools",
                        -1,
                        nameof(ShowTubeTemplatePMP),
                        nameof(EnableTubeTemplatePMP),
                        "Generate 1:1 cut templates for selected chassis tubes",
                        ""
                    );
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SetupCommandManager] Error: {ex.Message}");
            }
        }

        private void AddCommandToTab(int docType, string tabName, ICommandGroup cmdGroup, int cmdIndex)
        {
            try
            {
                if (_cmdMgr == null || cmdGroup == null) return;

                ICommandTab tab = _cmdMgr.GetCommandTab(docType, tabName);
                if (tab == null)
                {
                    tab = _cmdMgr.AddCommandTab(docType, tabName);
                }

                if (tab != null)
                {
                    int cmdId = cmdGroup.get_CommandID(cmdIndex);
                    if (cmdId == 0) return;

                    // Avoid duplicate tab boxes
                    object[] existingBoxes = tab.CommandTabBoxes() as object[];
                    bool alreadyExists = false;
                    if (existingBoxes != null)
                    {
                        foreach (object b in existingBoxes)
                        {
                            if (b is ICommandTabBox tb)
                            {
                                object cmdObj = null;
                                object styleObj = null;
                                tb.GetCommands(out cmdObj, out styleObj);
                                int[] cmds = cmdObj as int[];
                                if (cmds != null && Array.IndexOf(cmds, cmdId) >= 0)
                                {
                                    alreadyExists = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!alreadyExists)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddCommandToTab] Exception: {ex.Message}");
                // If tab cannot be modified, toolbar/menu items are still available
            }
        }

        private void RemoveCommandManager()
        {
            if (_cmdMgr != null)
            {
                _cmdMgr.RemoveCommandGroup2(CommandGroupId, true);
            }
        }

        #endregion

        #region Command Callbacks

        /// <summary>
        /// Command callback executed when user clicks the "Tube Cut Templates" button.
        /// </summary>
        public void ShowTubeTemplatePMP()
        {
            try
            {
                if (_pmpPage == null)
                {
                    _pmpPage = new TubeTemplatePMPPage(_swApp);
                }
                _pmpPage.Show();
            }
            catch (Exception ex)
            {
                _swApp?.SendMsgToUser2(
                    $"Error opening Tube Cut Templates UI: {ex.Message}",
                    (int)swMessageBoxIcon_e.swMbStop,
                    (int)swMessageBoxBtn_e.swMbOk
                );
            }
        }

        /// <summary>
        /// Command enable callback determining whether button is active.
        /// Enabled whenever an active Part or Assembly model is open.
        /// </summary>
        public int EnableTubeTemplatePMP()
        {
            try
            {
                if (_swApp == null) return 0;

                IModelDoc2 activeDoc = _swApp.ActiveDoc as IModelDoc2;
                if (activeDoc != null)
                {
                    int docType = activeDoc.GetType();
                    if (docType == (int)swDocumentTypes_e.swDocPART ||
                        docType == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        return 1; // Enabled
                    }
                }
                return 0; // Disabled
            }
            catch
            {
                return 0;
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

                // 1. Register under HKCU (Current User - works without administrator rights)
                RegisterInKey(Registry.CurrentUser, clsid);

                // 2. Register under HKLM (Local Machine - if administrator rights exist)
                try
                {
                    RegisterInKey(Registry.LocalMachine, clsid);
                }
                catch
                {
                    // HKLM may require UAC elevation; HKCU is sufficient for SolidWorks
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SwAddin] COM Registration Error: {ex.Message}");
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
