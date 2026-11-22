using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Win32;
using System.Collections.Generic;

namespace RedLionRacing.Installer
{
    public static class InstallerCore
    {
        public const string AddinGuid = "{769C5C1D-E143-4D90-A951-BEB1309CD51E}";
        public const string AddinTitle = "Red Lion Racing Tube Templates";
        public const string AddinDescription = "Auto-generates 1:1 scale printable cut templates for Red Lion Racing (University of Manitoba).";
        public const string AddinDllName = "BajaSAE.TubeCutTemplateAddin.dll";

        public static string GetDefaultInstallPath()
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localApp, "RedLionRacing", "SolidWorks_TubeTemplates");
        }

        public static bool IsSolidWorksRunning()
        {
            try
            {
                var procs = Process.GetProcessesByName("SLDWORKS");
                return procs.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static string FindSolidWorksPath()
        {
            string defaultPath = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe";
            if (File.Exists(defaultPath)) return defaultPath;

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks\General"))
                {
                    if (key != null)
                    {
                        string installDir = key.GetValue("Install Path") as string;
                        if (!string.IsNullOrEmpty(installDir))
                        {
                            string exe = Path.Combine(installDir, "SLDWORKS.exe");
                            if (File.Exists(exe)) return exe;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        public static void ExtractPayload(string targetDirectory, Action<string> log)
        {
            Directory.CreateDirectory(targetDirectory);
            var asm = Assembly.GetExecutingAssembly();

            string[] resources = new string[]
            {
                "BajaSAE.TubeCutTemplateAddin.dll",
                "SolidWorks.Interop.sldworks.dll",
                "SolidWorks.Interop.swconst.dll",
                "SolidWorks.Interop.swpublished.dll",
                "red_lion_logo.png"
            };

            foreach (string resName in resources)
            {
                string targetPath = Path.Combine(targetDirectory, resName);
                if (resName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    string resDir = Path.Combine(targetDirectory, "Resources");
                    Directory.CreateDirectory(resDir);
                }

                string fullRes = null;
                foreach (string name in asm.GetManifestResourceNames())
                {
                    if (name.EndsWith(resName, StringComparison.OrdinalIgnoreCase))
                    {
                        fullRes = name;
                        break;
                    }
                }

                if (fullRes != null)
                {
                    using (Stream src = asm.GetManifestResourceStream(fullRes))
                    using (FileStream dst = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        src.CopyTo(dst);
                    }
                    if (resName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        string subCopy = Path.Combine(targetDirectory, "Resources", resName);
                        File.Copy(targetPath, subCopy, true);
                    }
                    log?.Invoke($"[✓] Extracted: {resName}");
                }
                else
                {
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Payload", resName);
                    if (!File.Exists(localPath)) localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, resName);

                    if (File.Exists(localPath))
                    {
                        File.Copy(localPath, targetPath, true);
                        log?.Invoke($"[✓] Copied: {resName}");
                    }
                    else
                    {
                        log?.Invoke($"[!] Warning: Resource not found: {resName}");
                    }
                }
            }
        }

        public static void RegisterCom(string installDir, Action<string> log)
        {
            string dllPath = Path.Combine(installDir, AddinDllName);
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"Add-in assembly not found: {dllPath}");
            }

            // 1. Per-User COM Registration (HKCU - No Administrator privileges needed!)
            string clsidKeyPath = $@"Software\Classes\CLSID\{AddinGuid}";
            using (var clsidKey = Registry.CurrentUser.CreateSubKey(clsidKeyPath))
            {
                clsidKey.SetValue(null, "BajaSAE.TubeCutTemplateAddin.SwAddin");

                using (var inprocKey = clsidKey.CreateSubKey("InprocServer32"))
                {
                    inprocKey.SetValue(null, "mscoree.dll");
                    inprocKey.SetValue("ThreadingModel", "Both");
                    inprocKey.SetValue("Class", "BajaSAE.TubeCutTemplateAddin.SwAddin");
                    inprocKey.SetValue("Assembly", "BajaSAE.TubeCutTemplateAddin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
                    inprocKey.SetValue("RuntimeVersion", "v4.0.30319");
                    inprocKey.SetValue("CodeBase", new Uri(dllPath).AbsoluteUri);
                }

                using (var progIdKey = clsidKey.CreateSubKey("ProgId"))
                {
                    progIdKey.SetValue(null, "BajaSAE.TubeCutTemplateAddin.SwAddin");
                }
            }
            log?.Invoke($"[✓] Registered COM CLSID in HKCU: {AddinGuid}");

            // 2. SolidWorks Add-in Registration (HKCU)
            string swAddinKeyPath = $@"Software\SolidWorks\Addins\{AddinGuid}";
            using (var swKey = Registry.CurrentUser.CreateSubKey(swAddinKeyPath))
            {
                swKey.SetValue(null, 1, RegistryValueKind.DWord); // 1 = Load on startup
                swKey.SetValue("Title", AddinTitle, RegistryValueKind.String);
                swKey.SetValue("Description", AddinDescription, RegistryValueKind.String);
            }
            log?.Invoke($"[✓] Enabled SolidWorks Startup Add-in in HKCU");

            // 3. Machine-Wide Registration via RegAsm if user has elevation
            try
            {
                string regasm = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe";
                if (File.Exists(regasm))
                {
                    var psi = new ProcessStartInfo(regasm, $"\"{dllPath}\" /codebase")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        p.WaitForExit(5000);
                        if (p.ExitCode == 0)
                        {
                            log?.Invoke("[✓] Registered machine-wide via 64-bit RegAsm");
                        }
                    }
                }
            }
            catch { }
        }

        public static void Unregister(string installDir, Action<string> log)
        {
            // 1. Remove HKCU SolidWorks Addin key
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\SolidWorks\Addins\{AddinGuid}", false);
                log?.Invoke("[✓] Removed SolidWorks Add-in registration from HKCU");
            }
            catch { }

            // 2. Remove HKCU COM registration
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\CLSID\{AddinGuid}", false);
                log?.Invoke("[✓] Removed COM class registration from HKCU");
            }
            catch { }

            // 3. Remove HKLM keys if running elevated
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree($@"SOFTWARE\SolidWorks\Addins\{AddinGuid}", false);
                Registry.LocalMachine.DeleteSubKeyTree($@"SOFTWARE\Classes\CLSID\{AddinGuid}", false);
                log?.Invoke("[✓] Removed HKLM registration keys (if elevated)");
            }
            catch { }

            // 4. Clean up files
            try
            {
                if (Directory.Exists(installDir))
                {
                    Directory.Delete(installDir, true);
                    log?.Invoke($"[✓] Cleaned up install directory: {installDir}");
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"[!] Note: Some files could not be deleted: {ex.Message}");
            }
        }
    }
}
