using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace RedLionRacing.Installer
{
    public class MainForm : Form
    {
        private Panel _headerPanel;
        private Label _lblTitle;
        private Label _lblSubtitle;
        private Label _lblApp;

        private Label _lblPath;
        private TextBox _txtPath;
        private Button _btnBrowse;

        private RichTextBox _rtbLog;

        private Panel _buttonPanel;
        private Button _btnInstall;
        private Button _btnUninstall;
        private Button _btnLaunch;
        private Button _btnOpenFolder;

        private readonly Color RedLionCrimson = Color.FromArgb(184, 20, 31);
        private readonly Color DarkNavy = Color.FromArgb(20, 30, 48);

        public MainForm()
        {
            InitializeComponent();
            CheckEnvironment();
        }

        private void InitializeComponent()
        {
            this.Text = "Red Lion Racing | SolidWorks Add-in Installer";
            this.Size = new Size(580, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(248, 249, 251);

            // 1. Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = RedLionCrimson
            };

            _lblTitle = new Label
            {
                Text = "RED LION RACING",
                Font = new Font("Segoe UI", 14.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 10),
                AutoSize = true
            };

            _lblSubtitle = new Label
            {
                Text = "University of Manitoba | Formula & Baja SAE Racing",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(250, 220, 225),
                Location = new Point(22, 36),
                AutoSize = true
            };

            _lblApp = new Label
            {
                Text = "1:1 Scale Chassis Tube Cut Templates SolidWorks Add-in",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(22, 56),
                AutoSize = true
            };

            _headerPanel.Controls.Add(_lblTitle);
            _headerPanel.Controls.Add(_lblSubtitle);
            _headerPanel.Controls.Add(_lblApp);
            this.Controls.Add(_headerPanel);

            // 2. Install Path Selection
            _lblPath = new Label
            {
                Text = "Install Location:",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(20, 95),
                AutoSize = true
            };
            this.Controls.Add(_lblPath);

            _txtPath = new TextBox
            {
                Text = InstallerCore.GetDefaultInstallPath(),
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(20, 115),
                Width = 430,
                ReadOnly = false
            };
            this.Controls.Add(_txtPath);

            _btnBrowse = new Button
            {
                Text = "Browse...",
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(458, 113),
                Width = 90,
                Height = 26
            };
            _btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = _txtPath.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        _txtPath.Text = fbd.SelectedPath;
                    }
                }
            };
            this.Controls.Add(_btnBrowse);

            // 3. Log Box
            _rtbLog = new RichTextBox
            {
                Location = new Point(20, 148),
                Size = new Size(528, 260),
                Font = new Font("Consolas", 8.5f),
                BackColor = Color.White,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_rtbLog);

            // 4. Button Panel
            _buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = Color.FromArgb(240, 242, 245)
            };

            _btnInstall = new Button
            {
                Text = "Install / Update Add-in",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                BackColor = RedLionCrimson,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(20, 10),
                Size = new Size(160, 34),
                Cursor = Cursors.Hand
            };
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Click += BtnInstall_Click;
            _buttonPanel.Controls.Add(_btnInstall);

            _btnUninstall = new Button
            {
                Text = "Uninstall",
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(190, 10),
                Size = new Size(95, 34),
                Cursor = Cursors.Hand
            };
            _btnUninstall.Click += BtnUninstall_Click;
            _buttonPanel.Controls.Add(_btnUninstall);

            _btnLaunch = new Button
            {
                Text = "Launch SolidWorks",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(295, 10),
                Size = new Size(135, 34),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _btnLaunch.Click += BtnLaunch_Click;
            _buttonPanel.Controls.Add(_btnLaunch);

            _btnOpenFolder = new Button
            {
                Text = "Open Folder",
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(440, 10),
                Size = new Size(108, 34),
                Cursor = Cursors.Hand
            };
            _btnOpenFolder.Click += (s, e) =>
            {
                string p = _txtPath.Text;
                if (Directory.Exists(p))
                {
                    try { Process.Start("explorer.exe", p); } catch { }
                }
            };
            _buttonPanel.Controls.Add(_btnOpenFolder);

            this.Controls.Add(_buttonPanel);
        }

        private void Log(string message, Color? color = null)
        {
            Color c = color ?? Color.FromArgb(40, 40, 40);
            _rtbLog.SelectionStart = _rtbLog.TextLength;
            _rtbLog.SelectionLength = 0;
            _rtbLog.SelectionColor = c;
            _rtbLog.AppendText(message + Environment.NewLine);
            _rtbLog.ScrollToCaret();
            Application.DoEvents();
        }

        private void CheckEnvironment()
        {
            Log("================================================================", Color.FromArgb(120, 120, 120));
            Log(" Red Lion Racing — SolidWorks Add-in Setup (University of Manitoba)", RedLionCrimson);
            Log("================================================================", Color.FromArgb(120, 120, 120));

            string swPath = InstallerCore.FindSolidWorksPath();
            if (!string.IsNullOrEmpty(swPath))
            {
                Log($"[✓] Detected SolidWorks: {swPath}", Color.FromArgb(0, 120, 50));
                _btnLaunch.Enabled = true;
            }
            else
            {
                Log("[!] SolidWorks executable not found in default paths.", Color.FromArgb(180, 100, 0));
                Log("    (Add-in can still be registered for user account).");
            }

            if (InstallerCore.IsSolidWorksRunning())
            {
                Log("[!] Notice: SolidWorks is currently running.", Color.FromArgb(180, 100, 0));
                Log("    Please close SolidWorks before installing to allow updating files.");
            }
            else
            {
                Log("[✓] SolidWorks is closed. Ready to install.", Color.FromArgb(0, 120, 50));
            }
            Log("");
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            string targetDir = _txtPath.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show("Please choose a valid destination folder.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (InstallerCore.IsSolidWorksRunning())
            {
                var dr = MessageBox.Show(
                    "SolidWorks is currently running.\n\nPlease close SolidWorks to prevent DLL file locking during installation.\n\nContinue anyway?",
                    "SolidWorks Running",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );
                if (dr != DialogResult.Yes) return;
            }

            _btnInstall.Enabled = false;
            _btnUninstall.Enabled = false;

            try
            {
                Log("----------------------------------------------------------------", Color.FromArgb(150, 150, 150));
                Log($"[1/3] Extracting add-in assemblies to:\n      {targetDir}", Color.FromArgb(0, 70, 150));
                InstallerCore.ExtractPayload(targetDir, msg => Log(msg));

                Log("\n[2/3] Registering COM class & SolidWorks startup keys...", Color.FromArgb(0, 70, 150));
                InstallerCore.RegisterCom(targetDir, msg => Log(msg));

                Log("\n[3/3] Installation Complete!", Color.FromArgb(0, 130, 50));
                Log("================================================================", Color.FromArgb(0, 130, 50));
                Log(" SUCCESS: Add-in is now registered and active in SolidWorks!", Color.FromArgb(0, 130, 50));
                Log(" Look for 'Tube Templates' on the CommandManager ribbon", Color.FromArgb(0, 130, 50));
                Log(" or use Tools > Red Lion Racing Tube Templates.", Color.FromArgb(0, 130, 50));
                Log("================================================================", Color.FromArgb(0, 130, 50));

                _btnLaunch.Enabled = !string.IsNullOrEmpty(InstallerCore.FindSolidWorksPath());

                MessageBox.Show(
                    "Red Lion Racing Tube Cut Templates Add-in was successfully installed!\n\nLaunch SolidWorks to start generating 1:1 tube templates.",
                    "Installation Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Log($"\n[ERROR] Installation failed: {ex.Message}", Color.Red);
                MessageBox.Show($"Installation failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnInstall.Enabled = true;
                _btnUninstall.Enabled = true;
            }
        }

        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            var dr = MessageBox.Show(
                "Are you sure you want to uninstall the Red Lion Racing Tube Templates Add-in?",
                "Confirm Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (dr != DialogResult.Yes) return;

            string targetDir = _txtPath.Text.Trim();
            _btnInstall.Enabled = false;
            _btnUninstall.Enabled = false;

            try
            {
                Log("\nUninstalling Red Lion Racing Add-in...", Color.FromArgb(150, 50, 50));
                InstallerCore.Unregister(targetDir, msg => Log(msg));
                Log("Add-in successfully uninstalled.", Color.FromArgb(0, 120, 50));

                MessageBox.Show("The Add-in has been successfully uninstalled.", "Uninstalled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Uninstall failed: {ex.Message}", Color.Red);
            }
            finally
            {
                _btnInstall.Enabled = true;
                _btnUninstall.Enabled = true;
            }
        }

        private void BtnLaunch_Click(object sender, EventArgs e)
        {
            string swExe = InstallerCore.FindSolidWorksPath();
            if (!string.IsNullOrEmpty(swExe) && File.Exists(swExe))
            {
                try
                {
                    Process.Start(swExe);
                    Log($"[✓] Launched SolidWorks: {swExe}", Color.FromArgb(0, 120, 50));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not launch SolidWorks: {ex.Message}", "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
