using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace Kerf.Installer
{
    public class SetupWindow : Window
    {
        private ProgressBar progressBar;
        private TextBlock statusText;
        private Button btnInstall;
        private CheckBox chkDesktop;
        private CheckBox chkStartMenu;
        private CheckBox chkAssociate;
        private CheckBox chkLaunch;
        private TextBox txtInstallDir;

        private string defaultInstallDir;

        public SetupWindow()
        {
            Title = "Kerf Studio Setup — Native DirectX Vector Design";
            Width = 560;
            Height = 440;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x14));
            Foreground = Brushes.White;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Icon = Kerf.UI.KerfLogo.GetLogoImageSource();

            var chrome = new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(0),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0)
            };
            WindowChrome.SetWindowChrome(this, chrome);

            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            defaultInstallDir = IOPath.Combine(localApp, "Programs", "KerfStudio");

            BuildUI();
        }

        private void BuildUI()
        {
            var root = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16))
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); // Footer

            // 1. HEADER
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            header.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };

            var headerDock = new DockPanel();

            var logoStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };

            var logo = Kerf.UI.KerfLogo.CreateLogoElement(24, withGlow: true);
            logo.Margin = new Thickness(0, 0, 10, 0);
            logoStack.Children.Add(logo);

            var titleBlock = new TextBlock
            {
                Text = "KERF STUDIO INSTALLER — v3.0 PRO",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            logoStack.Children.Add(titleBlock);

            DockPanel.SetDock(logoStack, Dock.Left);
            headerDock.Children.Add(logoStack);

            var btnClose = new Button
            {
                Content = "✕",
                Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xC4, 0xCC)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Width = 40,
                Height = 42,
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => Close();
            btnClose.MouseEnter += (s, e) =>
            {
                btnClose.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
                btnClose.Foreground = Brushes.White;
            };
            btnClose.MouseLeave += (s, e) =>
            {
                btnClose.Background = Brushes.Transparent;
                btnClose.Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0xC4, 0xCC));
            };
            DockPanel.SetDock(btnClose, Dock.Right);
            headerDock.Children.Add(btnClose);

            header.Child = headerDock;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // 2. CONTENT
            var content = new StackPanel { Margin = new Thickness(24, 18, 24, 12) };

            var banner = new TextBlock
            {
                Text = "Install Kerf Vector Studio",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            };
            content.Children.Add(banner);

            var desc = new TextBlock
            {
                Text = "High-performance vector design suite with DirectX acceleration, Pen tool, local storage, and .kerf file association.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0xA8)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
            content.Children.Add(desc);

            // Path box
            var lblPath = new TextBlock
            {
                Text = "INSTALLATION DIRECTORY",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            content.Children.Add(lblPath);

            txtInstallDir = new TextBox
            {
                Text = defaultInstallDir,
                Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x24)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x32, 0x32, 0x3C)),
                FontSize = 11,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 16)
            };
            content.Children.Add(txtInstallDir);

            // Options
            chkDesktop = CreateOptionCheckbox("Create Desktop Shortcut (Kerf Studio)", true);
            chkStartMenu = CreateOptionCheckbox("Create Start Menu entry", true);
            chkAssociate = CreateOptionCheckbox("Register .kerf file extension (Open .kerf with Kerf Studio)", true);
            chkLaunch = CreateOptionCheckbox("Launch Kerf Studio immediately after install", true);

            content.Children.Add(chkDesktop);
            content.Children.Add(chkStartMenu);
            content.Children.Add(chkAssociate);
            content.Children.Add(chkLaunch);

            // Progress Bar & Status
            progressBar = new ProgressBar
            {
                Height = 6,
                Background = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 16, 0, 4),
                Value = 0,
                Maximum = 100
            };
            content.Children.Add(progressBar);

            statusText = new TextBlock
            {
                Text = "Ready to install.",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98))
            };
            content.Children.Add(statusText);

            Grid.SetRow(content, 1);
            grid.Children.Add(content);

            // 3. FOOTER
            var footer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 0, 24, 0)
            };

            var footerDock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

            var versionInfo = new TextBlock
            {
                Text = "Kerf v2.4 (x64) • DirectX 11",
                FontSize = 10,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(versionInfo, Dock.Left);
            footerDock.Children.Add(versionInfo);

            btnInstall = new Button
            {
                Content = "Install Kerf Studio",
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Padding = new Thickness(16, 8, 16, 8),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnInstall.Click += OnInstallClick;
            DockPanel.SetDock(btnInstall, Dock.Right);
            footerDock.Children.Add(btnInstall);

            footer.Child = footerDock;
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            root.Child = grid;
            Content = root;
        }

        private CheckBox CreateOptionCheckbox(string label, bool isChecked)
        {
            return new CheckBox
            {
                Content = label,
                IsChecked = isChecked,
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private void OnInstallClick(object sender, RoutedEventArgs e)
        {
            if (btnInstall.Content.ToString() == "Launch Kerf Studio")
            {
                string targetExe = IOPath.Combine(txtInstallDir.Text, "KerfStudio.exe");
                if (File.Exists(targetExe))
                {
                    Process.Start(targetExe);
                }
                Close();
                return;
            }

            btnInstall.IsEnabled = false;
            PerformInstallation();
        }

        private void PerformInstallation()
        {
            try
            {
                string targetDir = txtInstallDir.Text;
                UpdateProgress(15, "Creating installation directory...");

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // Locate source KerfStudio.exe
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string srcExe = IOPath.Combine(appDir, "KerfStudio.exe");
                if (!File.Exists(srcExe))
                {
                    srcExe = IOPath.Combine(appDir, "..", "KerfStudio.exe");
                }
                if (!File.Exists(srcExe))
                {
                    srcExe = IOPath.Combine(appDir, "native-windows", "..", "KerfStudio.exe");
                }

                if (!File.Exists(srcExe))
                {
                    MessageBox.Show("KerfStudio.exe could not be found to install from:\n" + srcExe, "Source Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                    btnInstall.IsEnabled = true;
                    return;
                }

                UpdateProgress(35, "Copying application binaries...");
                string destExe = IOPath.Combine(targetDir, "KerfStudio.exe");
                File.Copy(srcExe, destExe, true);

                string srcDll = IOPath.Combine(IOPath.GetDirectoryName(srcExe), "kerf_core.dll");
                if (File.Exists(srcDll))
                {
                    File.Copy(srcDll, IOPath.Combine(targetDir, "kerf_core.dll"), true);
                }

                // High DPI Aware Config
                string configContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <startup>\n    <supportedRuntime version=\"v4.0\" sku=\".NETFramework,Version=v4.0\"/>\n  </startup>\n</configuration>";
                File.WriteAllText(destExe + ".config", configContent, Encoding.UTF8);

                // Sample Welcome Project
                UpdateProgress(55, "Initializing local storage and welcome project...");
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string projectsDir = IOPath.Combine(localApp, "KerfStudio", "Projects");
                if (!Directory.Exists(projectsDir)) Directory.CreateDirectory(projectsDir);

                string welcomeFile = IOPath.Combine(projectsDir, "Welcome to Kerf Studio.kerf");
                if (!File.Exists(welcomeFile))
                {
                    string sampleKerf = "{\"Format\":\"kerf-vector-design\",\"Version\":\"2.4.0\",\"Name\":\"Welcome to Kerf Studio\",\"Elements\":[{\"Id\":\"w1\",\"Name\":\"Welcome Card\",\"Type\":\"Frame\",\"X\":120,\"Y\":80,\"Width\":400,\"Height\":240,\"CornerRadius\":16,\"Fill\":\"#18181C\",\"Stroke\":\"#2A2A34\",\"StrokeWidth\":1,\"Opacity\":1.0},{\"Id\":\"w2\",\"Name\":\"Title\",\"Type\":\"Text\",\"Text\":\"Welcome to Kerf Studio!\",\"FontSize\":24,\"X\":150,\"Y\":120,\"Width\":340,\"Height\":36,\"Fill\":\"#FFFFFF\"},{\"Id\":\"w3\",\"Name\":\"Subtitle\",\"Type\":\"Text\",\"Text\":\"GPU-accelerated vector design with local storage and .kerf extension.\",\"FontSize\":13,\"X\":150,\"Y\":170,\"Width\":340,\"Height\":48,\"Fill\":\"#9E9EA8\"},{\"Id\":\"w4\",\"Name\":\"Pen Icon\",\"Type\":\"Star\",\"X\":440,\"Y\":120,\"Width\":50,\"Height\":50,\"Fill\":\"#0D99FF\"}]}";
                    File.WriteAllText(welcomeFile, sampleKerf, Encoding.UTF8);
                }

                // Shortcuts
                if (chkDesktop.IsChecked == true)
                {
                    UpdateProgress(70, "Creating Desktop shortcut...");
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(IOPath.Combine(desktop, "Kerf Studio.lnk"), destExe, targetDir);
                }

                if (chkStartMenu.IsChecked == true)
                {
                    UpdateProgress(80, "Creating Start Menu program entry...");
                    string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                    string progDir = IOPath.Combine(startMenu, "Programs", "Kerf Studio");
                    if (!Directory.Exists(progDir)) Directory.CreateDirectory(progDir);
                    CreateShortcut(IOPath.Combine(progDir, "Kerf Studio.lnk"), destExe, targetDir);
                }

                // File Association
                if (chkAssociate.IsChecked == true)
                {
                    UpdateProgress(90, "Registering .kerf file extension...");
                    RegisterFileAssociation(destExe);
                }

                // Register Uninstaller
                RegisterUninstallEntry(targetDir, destExe);

                UpdateProgress(100, "Installation complete!");
                btnInstall.Content = "Launch Kerf Studio";
                btnInstall.IsEnabled = true;

                if (chkLaunch.IsChecked == true)
                {
                    Process.Start(destExe);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Installation Error: " + ex.Message, "Install Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                btnInstall.IsEnabled = true;
            }
        }

        private void UpdateProgress(double val, string msg)
        {
            progressBar.Value = val;
            statusText.Text = msg;
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string workingDir)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType);
                    var shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = "Kerf Studio — Native DirectX Vector Design";
                    shortcut.Save();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Shortcut error: " + ex.Message);
            }
        }

        private void RegisterFileAssociation(string exePath)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.kerf"))
                {
                    if (key != null) key.SetValue("", "Kerf.DesignFile");
                }

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Kerf.DesignFile"))
                {
                    if (key != null) key.SetValue("", "Kerf Vector Design Document");
                }

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Kerf.DesignFile\DefaultIcon"))
                {
                    if (key != null) key.SetValue("", exePath + ",0");
                }

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Kerf.DesignFile\shell\open\command"))
                {
                    if (key != null) key.SetValue("", "\"" + exePath + "\" \"%1\"");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Registry error: " + ex.Message);
            }
        }

        private void RegisterUninstallEntry(string targetDir, string exePath)
        {
            try
            {
                string uninstBat = IOPath.Combine(targetDir, "uninstall.bat");
                var sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("echo Uninstalling Kerf Studio...");
                sb.AppendLine("taskkill /f /im KerfStudio.exe >nul 2>&1");
                sb.AppendLine("reg delete \"HKCU\\Software\\Classes\\.kerf\" /f >nul 2>&1");
                sb.AppendLine("reg delete \"HKCU\\Software\\Classes\\Kerf.DesignFile\" /f >nul 2>&1");
                sb.AppendLine("reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\KerfStudio\" /f >nul 2>&1");
                string desktopLnk = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Kerf Studio.lnk");
                sb.AppendLine(string.Format("del \"{0}\" >nul 2>&1", desktopLnk));
                string startMenuLnk = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Kerf Studio");
                sb.AppendLine(string.Format("rmdir /s /q \"{0}\" >nul 2>&1", startMenuLnk));
                sb.AppendLine("echo Kerf Studio uninstalled successfully.");
                sb.AppendLine("pause");
                File.WriteAllText(uninstBat, sb.ToString(), Encoding.UTF8);

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KerfStudio"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "Kerf Studio — Native DirectX Vector Design");
                        key.SetValue("DisplayVersion", "2.4.0");
                        key.SetValue("Publisher", "Kerf Studio");
                        key.SetValue("DisplayIcon", exePath + ",0");
                        key.SetValue("InstallLocation", targetDir);
                        key.SetValue("UninstallString", "\"" + uninstBat + "\"");
                    }
                }
            }
            catch { }
        }

        [STAThread]
        public static void Main()
        {
            var app = new Application();
            var win = new SetupWindow();
            app.Run(win);
        }
    }
}
