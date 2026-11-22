using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace RedLionRacing.Installer
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0)
            {
                AttachConsole(-1); // Connect to caller console
                string cmd = args[0].ToLowerInvariant();
                string targetDir = args.Length > 1 ? args[1] : InstallerCore.GetDefaultInstallPath();

                if (cmd == "/silent" || cmd == "/install" || cmd == "-i")
                {
                    try
                    {
                        Console.WriteLine($"Installing Red Lion Racing Tube Templates to: {targetDir}");
                        InstallerCore.ExtractPayload(targetDir, msg => Console.WriteLine(msg));
                        InstallerCore.RegisterCom(targetDir, msg => Console.WriteLine(msg));
                        Console.WriteLine("Installation successful.");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Installation failed: {ex.Message}");
                        Environment.Exit(1);
                    }
                    return;
                }
                else if (cmd == "/uninstall" || cmd == "-u")
                {
                    try
                    {
                        Console.WriteLine($"Uninstalling Red Lion Racing Tube Templates from: {targetDir}");
                        InstallerCore.Unregister(targetDir, msg => Console.WriteLine(msg));
                        Console.WriteLine("Uninstalled successfully.");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Uninstall failed: {ex.Message}");
                        Environment.Exit(1);
                    }
                    return;
                }
            }

            // Normal graphical GUI mode
            Application.Run(new MainForm());
        }
    }
}
