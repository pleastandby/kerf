using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Kerf.Engine;
using Kerf.IO;

namespace Kerf.UI
{
    /// <summary>
    /// Premium Adobe After Effects-inspired cinematic splash & loading screen.
    /// Displays the Kerf folded-ribbon brand mark, hardware acceleration telemetry,
    /// dynamic subsystem loading sequence, and smooth handoff to MainWindow.
    /// </summary>
    public class SplashScreenWindow : Window
    {
        private readonly string _initialLaunchFile;
        private DispatcherTimer _timer;
        private double _progress = 0.0;
        private Border _progressBarFill;
        private TextBlock _statusText;
        private TextBlock _percentText;
        private TextBlock _simdText;
        private bool _isClosing = false;
        private MainWindow _preparedMainWindow;

        public SplashScreenWindow(string launchFilePath = null)
        {
            _initialLaunchFile = launchFilePath;

            InitializeSplashWindow();
            BuildUi();
            StartLoadingSequence();
        }

        private void InitializeSplashWindow()
        {
            Title = "Kerf Studio - Starting";
            Width = 740;
            Height = 440;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = true;
            Icon = KerfLogo.GetLogoImageSource();

            // Allow clicking anywhere to skip immediately into the workspace
            MouseLeftButtonDown += (s, e) => SkipAndLaunch();
            KeyDown += (s, e) => SkipAndLaunch();
            Cursor = Cursors.AppStarting;
        }

        private void BuildUi()
        {
            var rootGrid = new Grid
            {
                Margin = new Thickness(16)
            };

            // Outer backdrop container with cinematic drop shadow
            var outerCard = new Border
            {
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1.2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 42, 60)),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 32,
                    ShadowDepth = 6,
                    Opacity = 0.85
                }
            };

            // Dark obsidian gradient background
            var cardBg = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            cardBg.GradientStops.Add(new GradientStop(Color.FromRgb(11, 13, 20), 0.0));
            cardBg.GradientStops.Add(new GradientStop(Color.FromRgb(16, 19, 29), 0.5));
            cardBg.GradientStops.Add(new GradientStop(Color.FromRgb(13, 15, 23), 1.0));
            outerCard.Background = cardBg;

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Left hero column
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });   // Divider
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right loading column

            // ==========================================
            // LEFT COLUMN: Kerf Folded-Ribbon Hero Logo
            // ==========================================
            var leftHeroPanel = new Grid();

            // Ambient radial bloom behind the logo
            var radialAura = new Ellipse
            {
                Width = 240,
                Height = 240,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 30)
            };
            var auraBrush = new RadialGradientBrush();
            auraBrush.GradientStops.Add(new GradientStop(Color.FromArgb(50, 56, 189, 248), 0.0));
            auraBrush.GradientStops.Add(new GradientStop(Color.FromArgb(25, 108, 84, 182), 0.5));
            auraBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0));
            radialAura.Fill = auraBrush;
            leftHeroPanel.Children.Add(radialAura);

            // Vector folded-ribbon mark
            var logoStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var logoElement = KerfLogo.CreateLogoElement(165, withGlow: true);
            logoStack.Children.Add(logoElement);

            // Tech pill badge under the logo
            var techPill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 22, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 48, 70)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var techStack = new StackPanel { Orientation = Orientation.Horizontal };
            var pulseDot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var techText = new TextBlock
            {
                Text = "DIRECTX 11 + RUST SIMD",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center
            };
            techStack.Children.Add(pulseDot);
            techStack.Children.Add(techText);
            techPill.Child = techStack;
            logoStack.Children.Add(techPill);

            leftHeroPanel.Children.Add(logoStack);
            Grid.SetColumn(leftHeroPanel, 0);
            contentGrid.Children.Add(leftHeroPanel);

            // ==========================================
            // VERTICAL DIVIDER
            // ==========================================
            var divider = new Rectangle
            {
                Width = 1,
                Margin = new Thickness(0, 24, 0, 24)
            };
            var divBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            divBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0.0));
            divBrush.GradientStops.Add(new GradientStop(Color.FromRgb(42, 50, 75), 0.5));
            divBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
            divider.Fill = divBrush;
            Grid.SetColumn(divider, 1);
            contentGrid.Children.Add(divider);

            // ==========================================
            // RIGHT COLUMN: Subsystem & After Effects Splash
            // ==========================================
            var rightCol = new Grid
            {
                Margin = new Thickness(26, 26, 26, 22)
            };
            rightCol.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header / Titles
            rightCol.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Telemetry list
            rightCol.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Progress & Status
            rightCol.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // 1. Header (Brand, Version, Subtitle)
            var headerStack = new StackPanel();

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            var titleKerf = new TextBlock
            {
                Text = "KERF",
                FontSize = 34,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            var titleStudio = new TextBlock
            {
                Text = " STUDIO",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 5, 0, 0)
            };
            var versionBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 35, 52)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            versionBadge.Child = new TextBlock
            {
                Text = "v3.0 PRO",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252))
            };

            titleRow.Children.Add(titleKerf);
            titleRow.Children.Add(titleStudio);
            titleRow.Children.Add(versionBadge);
            headerStack.Children.Add(titleRow);

            var subtitleText = new TextBlock
            {
                Text = "GPU-Accelerated Vector Graphics & Procedural Design Suite",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 155, 185)),
                Margin = new Thickness(0, 3, 0, 14)
            };
            headerStack.Children.Add(subtitleText);

            Grid.SetRow(headerStack, 0);
            rightCol.Children.Add(headerStack);

            // 2. Subsystem Telemetry List (AE-style technical initialization details)
            var telemetryBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 17, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 36, 52)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 14)
            };

            var telStack = new StackPanel();

            telStack.Children.Add(CreateTelemetryRow("Rendering Pipeline", "Direct3D 11 Retained Visual Composition"));
            telStack.Children.Add(CreateTelemetryRow("Geometry Kernel", "Native Rust kerf_core.dll (MSVC C-ABI)"));
            telStack.Children.Add(CreateTelemetryRow("Extrema Solver", "Analytical B'(t) = 0 Quadratic Roots"));
            telStack.Children.Add(CreateTelemetryRow("Coordinate Space", "Infinite Subpixel Vector Canvas"));

            telemetryBox.Child = telStack;
            Grid.SetRow(telemetryBox, 1);
            rightCol.Children.Add(telemetryBox);

            // 3. Progress Bar & Real-Time Status Text
            var progressStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            var statusRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 6) };
            _statusText = new TextBlock
            {
                Text = "Initializing Direct3D hardware context...",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(186, 200, 222))
            };
            DockPanel.SetDock(_statusText, Dock.Left);
            statusRow.Children.Add(_statusText);

            _percentText = new TextBlock
            {
                Text = "0%",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
            };
            DockPanel.SetDock(_percentText, Dock.Right);
            statusRow.Children.Add(_percentText);
            progressStack.Children.Add(statusRow);

            // Progress bar track
            var trackBorder = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 42, 60)),
                BorderThickness = new Thickness(0.8),
                ClipToBounds = true
            };

            var trackGrid = new Grid();
            _progressBarFill = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0,
                CornerRadius = new CornerRadius(3)
            };

            // Glowing rainbow/accent gradient for the progress bar
            var barGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            barGrad.GradientStops.Add(new GradientStop(Color.FromRgb(56, 189, 248), 0.0));
            barGrad.GradientStops.Add(new GradientStop(Color.FromRgb(99, 102, 241), 0.5));
            barGrad.GradientStops.Add(new GradientStop(Color.FromRgb(236, 72, 153), 1.0));
            _progressBarFill.Background = barGrad;

            trackGrid.Children.Add(_progressBarFill);
            trackBorder.Child = trackGrid;
            progressStack.Children.Add(trackBorder);

            Grid.SetRow(progressStack, 2);
            rightCol.Children.Add(progressStack);

            // 4. Footer & Copyright
            var footerDock = new DockPanel { LastChildFill = false };
            _simdText = new TextBlock
            {
                Text = string.Format("Architecture: x64 | {0}", RustEngineInterop.SimdDescription),
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 105, 135))
            };
            DockPanel.SetDock(_simdText, Dock.Left);
            footerDock.Children.Add(_simdText);

            var hintText = new TextBlock
            {
                Text = "Click anywhere to skip",
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(115, 130, 160))
            };
            DockPanel.SetDock(hintText, Dock.Right);
            footerDock.Children.Add(hintText);

            Grid.SetRow(footerDock, 3);
            rightCol.Children.Add(footerDock);

            Grid.SetColumn(rightCol, 2);
            contentGrid.Children.Add(rightCol);

            outerCard.Child = contentGrid;
            rootGrid.Children.Add(outerCard);
            Content = rootGrid;
        }

        private UIElement CreateTelemetryRow(string label, string value)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };

            var dot = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(dot, Dock.Left);
            dock.Children.Add(dot);

            var lblBlock = new TextBlock
            {
                Text = label + ": ",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 125, 155))
            };
            DockPanel.SetDock(lblBlock, Dock.Left);
            dock.Children.Add(lblBlock);

            var valBlock = new TextBlock
            {
                Text = value,
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            dock.Children.Add(valBlock);

            return dock;
        }

        private void StartLoadingSequence()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(28)
            };
            _timer.Tick += OnLoadingTick;
            _timer.Start();

            // Asynchronously prepare the MainWindow during the splash
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    _preparedMainWindow = new MainWindow(_initialLaunchFile);
                }
                catch (Exception ex)
                {
                    Program.Log("Error pre-initializing MainWindow: " + ex.Message);
                }
            }));
        }

        private void OnLoadingTick(object sender, EventArgs e)
        {
            _progress += 1.8;

            double maxTrackW = 390.0;
            double currentW = Math.Min(maxTrackW, maxTrackW * (_progress / 100.0));
            _progressBarFill.Width = currentW;

            int intPct = Math.Min(100, (int)_progress);
            _percentText.Text = string.Format("{0}%", intPct);

            // Update real-time status messages according to progressive subsystem milestone
            if (_progress < 18)
            {
                _statusText.Text = "Initializing Direct3D 11 graphics context...";
            }
            else if (_progress < 36)
            {
                _statusText.Text = "Loading native Rust graphics engine (kerf_core.dll)...";
            }
            else if (_progress < 54)
            {
                _statusText.Text = "Calibrating AVX2 + FMA analytical cubic solvers...";
            }
            else if (_progress < 72)
            {
                _statusText.Text = "Mounting Canonical Document Engine & virtual filesystem...";
            }
            else if (_progress < 90)
            {
                _statusText.Text = "Registering Vector Tools, Snap Guides & Pen Engine...";
            }
            else
            {
                _statusText.Text = "Ready. Launching Kerf Studio...";
            }

            if (_progress >= 100.0)
            {
                _timer.Stop();
                FinishAndLaunch();
            }
        }

        private void SkipAndLaunch()
        {
            if (_isClosing) return;
            if (_timer != null) _timer.Stop();
            FinishAndLaunch();
        }

        private void FinishAndLaunch()
        {
            if (_isClosing) return;
            _isClosing = true;

            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(220)
            };

            fadeOut.Completed += (s, e) =>
            {
                try
                {
                    var app = Application.Current;
                    var win = _preparedMainWindow ?? new MainWindow(_initialLaunchFile);
                    if (app != null)
                    {
                        app.MainWindow = win;
                        win.Closed += (ws, we) =>
                        {
                            Program.Log("MainWindow Closed. Calling app.Shutdown()...");
                            app.Shutdown();
                        };
                    }
                    win.Show();
                    win.Activate();
                }
                catch (Exception ex)
                {
                    Program.Log("Exception launching MainWindow after splash: " + ex.Message);
                }
                finally
                {
                    Close();
                }
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
