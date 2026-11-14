using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kerf.Engine;
using IOPath = System.IO.Path;

namespace Kerf.UI
{
    /// <summary>
    /// Kerf official launch & loading modal.
    /// Perfectly matches the official brand loading mockup (rebrand/Loading screen Mockup.png):
    /// - Outer rounded card (CornerRadius 22, #17181F background, deep ambient shadow)
    /// - Inner image card bordered in glowing #46EFB1 (Electric Mint)
    /// - Background cinematic artwork (Kerf-loading-image.png)
    /// - Glowing #46EFB1 Kerf Icon mark at top right
    /// - "Kerf 3.1 Beta" with "IMAGINE / DESIGN / CREATE" subtitle at bottom left
    /// - "Loading Workspace ....." status label
    /// - Clean Electric Mint progress bar with percentage readout and copyright footer.
    /// </summary>
    public class SplashScreenWindow : Window
    {
        private readonly string _initialLaunchFile;
        private DispatcherTimer _timer;
        private double _progress = 0.0;
        private Border _trackBorder;
        private Border _progressBarFill;
        private TextBlock _statusText;
        private TextBlock _percentText;
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
            Title = ProductInfo.DisplayName;
            Width = 840;
            Height = 570;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = true;
            Icon = KerfLogo.GetLogoImageSource();

            // Allow clicking or pressing any key to skip immediately into the workspace
            MouseLeftButtonDown += (s, e) => SkipAndLaunch();
            KeyDown += (s, e) => SkipAndLaunch();
            Cursor = Cursors.AppStarting;
        }

        private ImageSource LoadBackgroundImage()
        {
            string[] candidatePaths = new string[]
            {
                IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Kerf-loading-image.png"),
                IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "assets", "Kerf-loading-image.png"),
                IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Kerf-loading-image.png"),
                IOPath.Combine(Directory.GetCurrentDirectory(), "assets", "Kerf-loading-image.png"),
                IOPath.Combine(Directory.GetCurrentDirectory(), "rebrand", "Kerf-loading-image.png"),
                IOPath.Combine(Directory.GetCurrentDirectory(), "native-windows", "assets", "Kerf-loading-image.png")
            };

            foreach (var path in candidatePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(IOPath.GetFullPath(path));
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
                catch { }
            }

            try
            {
                using (var stream = typeof(SplashScreenWindow).Assembly.GetManifestResourceStream("Kerf.LoadingImage"))
                {
                    if (stream == null) return null;
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { return null; }
        }

        private void BuildUi()
        {
            var rootGrid = new Grid
            {
                Margin = new Thickness(16)
            };

            // Outer backdrop card with smooth drop shadow
            var outerCard = new Border
            {
                CornerRadius = new CornerRadius(22),
                BorderThickness = new Thickness(1.2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 33, 44)),
                Background = new SolidColorBrush(Color.FromRgb(23, 24, 31)), // #17181F
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 36,
                    ShadowDepth = 8,
                    Opacity = 0.90
                },
                Padding = new Thickness(14)
            };

            var mainLayout = new Grid();
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Top Artwork Card
            mainLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(108) });                  // Bottom Loading Section

            // ==========================================
            // TOP SECTION: INNER ARTWORK CARD
            // ==========================================
            var artworkCard = new Border
            {
                CornerRadius = new CornerRadius(18),
                BorderThickness = new Thickness(1.2),
                BorderBrush = KerfBrand.MintBrush, // Glowing Electric Mint border matching mockup
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromRgb(15, 17, 24))
            };

            var artworkGrid = new Grid();

            // Load and display Kerf-loading-image.png
            var bgSource = LoadBackgroundImage();
            if (bgSource != null)
            {
                var bgImage = new Image
                {
                    Source = bgSource,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                artworkGrid.Children.Add(bgImage);
            }
            else
            {
                // Fallback rich brand gradient
                var fallbackBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                fallbackBrush.GradientStops.Add(new GradientStop(Color.FromRgb(30, 8, 46), 0.0));
                fallbackBrush.GradientStops.Add(new GradientStop(Color.FromRgb(12, 66, 71), 0.5));
                fallbackBrush.GradientStops.Add(new GradientStop(Color.FromRgb(10, 113, 123), 1.0));
                artworkGrid.Background = fallbackBrush;
            }

            // Top-Right: Official Brand Icon Mark in glowing Electric Mint
            var iconMark = KerfLogo.CreateIconElement(34, 42, fillBrush: KerfBrand.MintBrush, withGlow: true);
            iconMark.HorizontalAlignment = HorizontalAlignment.Right;
            iconMark.VerticalAlignment = VerticalAlignment.Top;
            iconMark.Margin = new Thickness(0, 24, 28, 0);
            artworkGrid.Children.Add(iconMark);

            // Bottom-Left: "Kerf 3.1 Beta" and subtitle
            var titleStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(32, 0, 0, 28)
            };

            var titleText = new TextBlock
            {
                Text = ProductInfo.DisplayName,
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 2,
                    Opacity = 0.6
                }
            };
            titleStack.Children.Add(titleText);

            var subtitleText = new TextBlock
            {
                Text = "IMAGINE   /   DESIGN   /   CREATE",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)), // #CBD5E1
                Margin = new Thickness(2, 0, 0, 0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 6,
                    ShadowDepth = 1,
                    Opacity = 0.5
                }
            };
            titleStack.Children.Add(subtitleText);

            artworkGrid.Children.Add(titleStack);
            artworkCard.Child = artworkGrid;

            Grid.SetRow(artworkCard, 0);
            mainLayout.Children.Add(artworkCard);

            // ==========================================
            // BOTTOM SECTION: LOADING WORKSPACE & PROGRESS
            // ==========================================
            var bottomPanel = new StackPanel
            {
                Margin = new Thickness(22, 14, 22, 6),
                VerticalAlignment = VerticalAlignment.Center
            };

            _statusText = new TextBlock
            {
                Text = "Loading Workspace .....",
                FontSize = 16,
                FontWeight = FontWeights.Regular,
                Foreground = new SolidColorBrush(Color.FromRgb(162, 175, 190)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            bottomPanel.Children.Add(_statusText);

            // Progress bar and percentage row
            var progressRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            _trackBorder = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(36, 40, 52)), // Dark track
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center
            };

            var trackCanvas = new Grid();
            _progressBarFill = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = KerfBrand.MintBrush // Solid glowing Electric Mint fill (#46EFB1)
            };
            trackCanvas.Children.Add(_progressBarFill);
            _trackBorder.Child = trackCanvas;

            Grid.SetColumn(_trackBorder, 0);
            progressRow.Children.Add(_trackBorder);

            _percentText = new TextBlock
            {
                Text = "0%",
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_percentText, 1);
            progressRow.Children.Add(_percentText);

            bottomPanel.Children.Add(progressRow);

            // Copyright footer
            var copyrightText = new TextBlock
            {
                Text = "Kerf © All rights reserved",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(85, 95, 115)),
                Margin = new Thickness(0, 0, 0, 0)
            };
            bottomPanel.Children.Add(copyrightText);

            Grid.SetRow(bottomPanel, 1);
            mainLayout.Children.Add(bottomPanel);

            outerCard.Child = mainLayout;
            rootGrid.Children.Add(outerCard);
            Content = rootGrid;
        }

        private void StartLoadingSequence()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(24)
            };
            _timer.Tick += OnLoadingTick;
            _timer.Start();

            // Pre-initialize MainWindow in background
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

            double trackW = _trackBorder != null && _trackBorder.ActualWidth > 0 ? _trackBorder.ActualWidth : 680.0;
            double currentW = Math.Min(trackW, trackW * (_progress / 100.0));
            _progressBarFill.Width = currentW;

            int intPct = Math.Min(100, (int)_progress);
            _percentText.Text = string.Format("{0}%", intPct);

            if (_progress < 25)
            {
                _statusText.Text = "Loading Workspace .....";
            }
            else if (_progress < 55)
            {
                _statusText.Text = "Initializing Direct3D Scene Graph .....";
            }
            else if (_progress < 85)
            {
                _statusText.Text = "Loading Vector Animation Engine .....";
            }
            else
            {
                _statusText.Text = "Launching Kerf .....";
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
