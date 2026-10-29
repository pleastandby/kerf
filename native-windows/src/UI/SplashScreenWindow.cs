using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
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
            // RIGHT COLUMN: Artwork & Loading
            // ==========================================
            var rightCol = new Grid
            {
                Margin = new Thickness(26, 22, 26, 20)
            };
            rightCol.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header / Titles
            rightCol.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Image Artwork
            rightCol.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Progress & Status
            rightCol.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // 1. Header (Brand, Version, Subtitle)
            var headerStack = new StackPanel();

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            var titleKerf = new TextBlock
            {
                Text = "KERF",
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            var titleStudio = new TextBlock
            {
                Text = " STUDIO",
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 4, 0, 0)
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
                Text = "Vector Graphics & Procedural Design Suite",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 155, 185)),
                Margin = new Thickness(0, 2, 0, 12)
            };
            headerStack.Children.Add(subtitleText);

            Grid.SetRow(headerStack, 0);
            rightCol.Children.Add(headerStack);

            // 2. Artwork image (Loading screen.png)
            var imageContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 22, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 50, 72)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 0, 14),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var artworkImage = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            string[] candidatePaths = new string[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Loading screen.png"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "assets", "Loading screen.png"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "assets", "Loading screen.png"),
                "assets/Loading screen.png"
            };

            foreach (var p in candidatePaths)
            {
                if (System.IO.File.Exists(p))
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(System.IO.Path.GetFullPath(p), UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();
                        artworkImage.Source = bmp;
                        break;
                    }
                    catch { }
                }
            }

            imageContainer.Child = artworkImage;
            Grid.SetRow(imageContainer, 1);
            rightCol.Children.Add(imageContainer);

            // 3. Progress Bar & Real-Time Status Text
            var progressStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            var statusRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 6) };
            _statusText = new TextBlock
            {
                Text = "Starting Kerf Studio...",
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

            // 4. Footer
            var footerDock = new DockPanel { LastChildFill = false };
            _simdText = new TextBlock
            {
                Text = "Kerf Studio © 2026. All rights reserved.",
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 115, 145))
            };
            DockPanel.SetDock(_simdText, Dock.Left);
            footerDock.Children.Add(_simdText);

            var hintText = new TextBlock
            {
                Text = "Click anywhere to continue",
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

            // Update real-time status messages according to progressive milestone
            if (_progress < 20)
            {
                _statusText.Text = "Starting Kerf Studio...";
            }
            else if (_progress < 40)
            {
                _statusText.Text = "Loading workspace & tools...";
            }
            else if (_progress < 60)
            {
                _statusText.Text = "Preparing infinite vector canvas...";
            }
            else if (_progress < 80)
            {
                _statusText.Text = "Loading design presets & fonts...";
            }
            else if (_progress < 95)
            {
                _statusText.Text = "Finalizing studio environment...";
            }
            else
            {
                _statusText.Text = "Ready";
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
