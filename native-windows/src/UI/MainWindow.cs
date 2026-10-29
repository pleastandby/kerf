using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using Kerf.IO;
using Kerf.Model;

namespace Kerf.UI
{
    public class MainWindow : Window
    {
        private Grid _mainContainer;
        private HomePageView _homeView;
        private StudioView _studioView;
        private Border _titleBar;
        private TextBlock _windowTitleBlock;
        private Button _restoreBtn;

        public MainWindow(string launchFilePath = null)
        {
            InitializeWindow();
            InitializeViews();

            PreviewKeyDown += (s, e) =>
            {
                if (_studioView != null && _mainContainer != null && _mainContainer.Children.Contains(_studioView))
                {
                    _studioView.HandlePreviewKeyDown(e);
                }
            };

            if (!string.IsNullOrEmpty(launchFilePath) && File.Exists(launchFilePath))
            {
                OpenDocumentFromPath(launchFilePath);
            }
            else
            {
                // Check if an autosave recovery exists
                string recoveryPath = StorageManager.CheckForAutosaveRecovery(null);
                if (!string.IsNullOrEmpty(recoveryPath))
                {
                    var res = MessageBox.Show(
                        "An unsaved project recovery file was found. Would you like to restore it?",
                        "Kerf Studio — Crash Recovery",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (res == MessageBoxResult.Yes)
                    {
                        OpenDocumentFromPath(recoveryPath);
                        return;
                    }
                    else
                    {
                        try { File.Delete(recoveryPath); } catch { }
                    }
                }

                NavigateToHome();
            }
        }

        private Grid _rootGrid;

        private void InitializeWindow()
        {
            Title = "Kerf Studio v3.0";

            double workW = SystemParameters.WorkArea.Width;
            double workH = SystemParameters.WorkArea.Height;

            // Fit comfortably within the display work area so the title bar is never pushed off screen
            Width = Math.Min(1200, Math.Max(780, workW * 0.90));
            Height = Math.Min(680, Math.Max(480, workH * 0.86));
            MinWidth = Math.Min(700, workW * 0.70);
            MinHeight = Math.Min(420, workH * 0.60);
            MaxWidth = workW;
            MaxHeight = workH;

            // Position manually within screen work area bounds
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Left + (workW - Width) / 2);
            Top = Math.Max(SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Top + Math.Max(10, (workH - Height) / 4));

            Background = new SolidColorBrush(Color.FromRgb(30, 34, 48)); // #1E2230
            Foreground = Brushes.White;
            Icon = KerfLogo.GetLogoImageSource();

            // Frameless modern window with Aero Snap & resizing support
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;

            var chrome = new WindowChrome
            {
                CaptionHeight = 0,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(6),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);

            SourceInitialized += (s, e) =>
            {
                var work = SystemParameters.WorkArea;
                if (Top < work.Top) Top = work.Top;
                if (Left < work.Left) Left = work.Left;
                if (Top + Height > work.Bottom) Top = Math.Max(work.Top, work.Bottom - Height);
                if (Left + Width > work.Right) Left = Math.Max(work.Left, work.Right - Width);
            };

            Loaded += (s, e) =>
            {
                var work = SystemParameters.WorkArea;
                if (Top < work.Top) Top = work.Top;
                if (Left < work.Left) Left = work.Left;
                if (Top + Height > work.Bottom) Top = Math.Max(work.Top, work.Bottom - Height);
            };

            Closing += OnWindowClosing;
            StateChanged += (s, e) =>
            {
                if (_restoreBtn != null)
                {
                    _restoreBtn.Content = (WindowState == WindowState.Maximized) ? "❐" : "□";
                }
                if (_rootGrid != null)
                {
                    // Prevent Windows maximize overflow from pushing title bar off-screen
                    _rootGrid.Margin = (WindowState == WindowState.Maximized) ? new Thickness(7) : new Thickness(0);
                }
            };
        }

        private void InitializeViews()
        {
            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) }); // Custom Title Bar
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // View Container

            // 1. Custom Title Bar
            _titleBar = CreateTitleBar();
            Grid.SetRow(_titleBar, 0);
            _rootGrid.Children.Add(_titleBar);

            // 2. Main Content Container
            _mainContainer = new Grid();
            Grid.SetRow(_mainContainer, 1);
            _rootGrid.Children.Add(_mainContainer);

            // Instantiate views
            _homeView = new HomePageView();
            _homeView.NewDocumentRequested += OnNewDocumentRequested;
            _homeView.DocumentOpened += OnDocumentOpened;

            _studioView = new StudioView();
            _studioView.RequestHomeNavigation += OnRequestHomeNavigation;

            Content = _rootGrid;
        }

        private Border CreateTitleBar()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 39, 56)), // #232738
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 63, 87)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 0, 0, 0)
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
                }
                else if (e.LeftButton == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            };

            var dock = new DockPanel { LastChildFill = false };

            // Left: App name
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logo = KerfLogo.CreateLogoElement(18, withGlow: true);
            logo.Margin = new Thickness(0, 0, 8, 0);
            leftStack.Children.Add(logo);

            _windowTitleBlock = new TextBlock
            {
                Text = "Kerf Studio",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 200)),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(_windowTitleBlock);
            DockPanel.SetDock(leftStack, Dock.Left);
            dock.Children.Add(leftStack);

            // Right: Minimize, Maximize, Close buttons
            var winBtnStack = new StackPanel { Orientation = Orientation.Horizontal };
            WindowChrome.SetIsHitTestVisibleInChrome(winBtnStack, true);

            var minBtn = CreateWinButton("—", () => WindowState = WindowState.Minimized);
            _restoreBtn = CreateWinButton("□", () => WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized);
            var closeBtn = CreateWinButton("✕", () => Close(), true);

            winBtnStack.Children.Add(minBtn);
            winBtnStack.Children.Add(_restoreBtn);
            winBtnStack.Children.Add(closeBtn);

            DockPanel.SetDock(winBtnStack, Dock.Right);
            dock.Children.Add(winBtnStack);

            border.Child = dock;
            return border;
        }

        private Button CreateWinButton(string text, Action onClick, bool isClose = false)
        {
            var btn = new Button
            {
                Width = 46,
                Height = 36,
                Content = text,
                FontSize = 12,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 210)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            btn.MouseEnter += (s, e) =>
            {
                btn.Background = isClose ? new SolidColorBrush(Color.FromRgb(220, 38, 38)) : new SolidColorBrush(Color.FromRgb(35, 42, 60));
                btn.Foreground = Brushes.White;
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 210));
            };
            btn.Click += (s, e) => onClick();

            return btn;
        }

        public void NavigateToHome()
        {
            _mainContainer.Children.Clear();
            _mainContainer.Children.Add(_homeView);
            _homeView.RefreshProjects();
            _windowTitleBlock.Text = "Kerf Studio — Home";
        }

        public void NavigateToStudio(KerfDocument doc, string filePath)
        {
            _mainContainer.Children.Clear();
            _studioView.LoadDocument(doc, filePath);
            _mainContainer.Children.Add(_studioView);
            _windowTitleBlock.Text = string.Format("Kerf Studio — {0}", doc.Name);
            _studioView.Focus();
        }

        private void OnNewDocumentRequested()
        {
            var doc = new KerfDocument { Name = "Untitled Project" };
            NavigateToStudio(doc, null);
        }

        private void OnDocumentOpened(KerfDocument doc, string filePath)
        {
            NavigateToStudio(doc, filePath);
        }

        public void OpenDocumentFromPath(string path)
        {
            try
            {
                var doc = StorageManager.LoadDocument(path);
                NavigateToStudio(doc, path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open file: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                NavigateToHome();
            }
        }

        private void OnRequestHomeNavigation()
        {
            if (_studioView != null && _studioView.History.IsDirty)
            {
                var res = MessageBox.Show(
                    string.Format("Do you want to save changes to \"{0}\" before returning to Home?", _studioView.Document.Name),
                    "Kerf Studio",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    _studioView.SaveCurrentDocument();
                    NavigateToHome();
                }
                else if (res == MessageBoxResult.No)
                {
                    StorageManager.ClearAutosave(_studioView.CurrentFilePath);
                    NavigateToHome();
                }
                // Cancel: do nothing, stay in Studio
            }
            else
            {
                NavigateToHome();
            }
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if (_mainContainer.Children.Contains(_studioView) && _studioView.History.IsDirty)
            {
                var res = MessageBox.Show(
                    string.Format("Do you want to save changes to \"{0}\" before closing?", _studioView.Document.Name),
                    "Kerf Studio",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    _studioView.SaveCurrentDocument();
                }
                else if (res == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
                else if (res == MessageBoxResult.No)
                {
                    StorageManager.ClearAutosave(_studioView.CurrentFilePath);
                }
            }
        }
    }
}
