using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Kerf.UI
{
    /// <summary>
    /// Interactive Figma-grade Color Picker control and popover.
    /// Features 2D Saturation/Value area, Hue rainbow slider, Opacity slider,
    /// Hex/RGB text entry, and curated designer color swatches.
    /// </summary>
    public class ColorPickerControl : UserControl
    {
        private double _hue = 0.0;        // 0 to 360
        private double _saturation = 1.0; // 0 to 1
        private double _value = 1.0;      // 0 to 1
        private double _alpha = 1.0;      // 0 to 1

        private Canvas _svCanvas;
        private Border _svArea;
        private Ellipse _svThumb;
        private Canvas _hueCanvas;
        private Border _hueThumb;
        private Canvas _alphaCanvas;
        private Border _alphaThumb;
        private LinearGradientBrush _alphaBrush;
        private Border _previewBox;
        private TextBox _hexInput;
        private bool _isUpdatingUi = false;

        public event Action<string, Color> ColorChanged;
        public event Action RequestClose;

        public Color SelectedColor
        {
            get { return HsvToRgb(_hue, _saturation, _value, _alpha); }
            set { SetColor(value); }
        }

        public string SelectedHex
        {
            get
            {
                var c = SelectedColor;
                if (c.A < 255)
                    return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.R, c.G, c.B, c.A);
                return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
            }
        }

        public ColorPickerControl()
        {
            BuildUi();
            SetColor(Color.FromRgb(56, 189, 248)); // Default Sky Blue
        }

        private void BuildUi()
        {
            Width = 240;
            Background = new SolidColorBrush(Color.FromRgb(20, 23, 34));

            var mainStack = new StackPanel { Margin = new Thickness(12) };

            // 0. Header with title and close button
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var title = new TextBlock
            {
                Text = "COLOR PICKER",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 180)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(title, Dock.Left);
            header.Children.Add(title);

            var closeBtn = new Button
            {
                Content = "✕",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Width = 20,
                Height = 20,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 205)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                ToolTip = "Close (Esc)",
                Template = StudioView.CreateRoundedButtonTemplate(10)
            };
            closeBtn.Click += (s, e) =>
            {
                if (RequestClose != null) RequestClose();
            };
            DockPanel.SetDock(closeBtn, Dock.Right);
            header.Children.Add(closeBtn);

            mainStack.Children.Add(header);

            // 1. 2D Saturation / Value Area
            _svArea = new Border
            {
                Width = 216,
                Height = 140,
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Cursor = Cursors.Cross
            };

            _svCanvas = new Canvas { Width = 216, Height = 140 };

            _svThumb = new Ellipse
            {
                Width = 14,
                Height = 14,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Opacity = 0.6 }
            };
            Canvas.SetLeft(_svThumb, 216 - 7);
            Canvas.SetTop(_svThumb, -7);
            _svCanvas.Children.Add(_svThumb);
            _svArea.Child = _svCanvas;

            _svArea.MouseLeftButtonDown += OnSvMouseDown;
            _svArea.MouseMove += OnSvMouseMove;
            _svArea.MouseLeftButtonUp += (s, e) =>
            {
                if (_svArea.IsMouseCaptured) _svArea.ReleaseMouseCapture();
            };

            mainStack.Children.Add(_svArea);

            // 2. Hue Rainbow Slider
            var hueTrack = new Border
            {
                Width = 216,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Margin = new Thickness(0, 10, 0, 0),
                ClipToBounds = true,
                Cursor = Cursors.Hand
            };

            var hueBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 0.0));
            hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 0), 0.17));
            hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 0.33));
            hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 255), 0.5));
            hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 0.67));
            hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 255), 0.83));
            hueBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 1.0));
            hueTrack.Background = hueBrush;

            _hueCanvas = new Canvas { Width = 216, Height = 14 };
            _hueThumb = new Border
            {
                Width = 8,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.5 }
            };
            _hueCanvas.Children.Add(_hueThumb);
            hueTrack.Child = _hueCanvas;

            hueTrack.MouseLeftButtonDown += OnHueMouseDown;
            hueTrack.MouseMove += OnHueMouseMove;
            hueTrack.MouseLeftButtonUp += (s, e) =>
            {
                var fe = s as FrameworkElement;
                if (fe != null && fe.IsMouseCaptured) fe.ReleaseMouseCapture();
            };

            mainStack.Children.Add(hueTrack);

            // 3. Alpha / Opacity Slider
            var alphaTrack = new Border
            {
                Width = 216,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Margin = new Thickness(0, 8, 0, 0),
                ClipToBounds = true,
                Cursor = Cursors.Hand
            };

            _alphaBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            _alphaBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0.0));
            _alphaBrush.GradientStops.Add(new GradientStop(Color.FromRgb(56, 189, 248), 1.0));
            alphaTrack.Background = _alphaBrush;

            _alphaCanvas = new Canvas { Width = 216, Height = 14 };
            _alphaThumb = new Border
            {
                Width = 8,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.5 }
            };
            _alphaCanvas.Children.Add(_alphaThumb);
            alphaTrack.Child = _alphaCanvas;

            alphaTrack.MouseLeftButtonDown += OnAlphaMouseDown;
            alphaTrack.MouseMove += OnAlphaMouseMove;
            alphaTrack.MouseLeftButtonUp += (s, e) =>
            {
                var fe = s as FrameworkElement;
                if (fe != null && fe.IsMouseCaptured) fe.ReleaseMouseCapture();
            };

            mainStack.Children.Add(alphaTrack);

            // 4. Hex Input & Color Preview Swatch
            var inputRow = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = true };

            _previewBox = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 70, 95)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0)
            };
            DockPanel.SetDock(_previewBox, Dock.Left);
            inputRow.Children.Add(_previewBox);

            _hexInput = new TextBox
            {
                Height = 28,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(28, 33, 48)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 56, 82)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _hexInput.TextChanged += OnHexInputChanged;
            inputRow.Children.Add(_hexInput);

            mainStack.Children.Add(inputRow);

            // 5. Curated Figma Swatch Palette
            var swatchesLabel = new TextBlock
            {
                Text = "PRESETS",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 155)),
                Margin = new Thickness(0, 10, 0, 6)
            };
            mainStack.Children.Add(swatchesLabel);

            var swatchesWrap = new WrapPanel { Width = 216 };
            string[] presets = new string[]
            {
                "#FFFFFF", "#000000", "#64748B", "#EF4444", "#F97316",
                "#F59E0B", "#10B981", "#06B6D4", "#3B82F6", "#6366F1",
                "#8B5CF6", "#EC4899", "#1E293B", "#0F172A"
            };

            foreach (var hex in presets)
            {
                string presetHex = hex;
                var swatchBtn = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(ParseColor(presetHex)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    BorderThickness = new Thickness(0.8),
                    Margin = new Thickness(2),
                    Cursor = Cursors.Hand
                };
                swatchBtn.MouseLeftButtonDown += (s, e) =>
                {
                    SetColor(ParseColor(presetHex));
                    NotifyChanged();
                };
                swatchesWrap.Children.Add(swatchBtn);
            }
            mainStack.Children.Add(swatchesWrap);

            Content = mainStack;
        }

        public void SetColor(Color c)
        {
            _isUpdatingUi = true;
            try
            {
                RgbToHsv(c.R, c.G, c.B, out _hue, out _saturation, out _value);
                _alpha = c.A / 255.0;

                UpdateSvBackground();
                UpdateThumbPositions();
                UpdatePreviewAndText();
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        public void SetColor(string hex)
        {
            SetColor(ParseColor(hex));
        }

        private void UpdateSvBackground()
        {
            var baseHueColor = HsvToRgb(_hue, 1.0, 1.0, 1.0);

            var group = new DrawingGroup();

            // 1. Solid hue background
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush(baseHueColor),
                null,
                new RectangleGeometry(new Rect(0, 0, 216, 140))));

            // 2. White to Transparent Horizontal Gradient (Saturation)
            var satBrush = new LinearGradientBrush(Colors.White, Colors.Transparent, 0.0);
            group.Children.Add(new GeometryDrawing(
                satBrush,
                null,
                new RectangleGeometry(new Rect(0, 0, 216, 140))));

            // 3. Transparent to Black Vertical Gradient (Value)
            var valBrush = new LinearGradientBrush(Colors.Transparent, Colors.Black, 90.0);
            group.Children.Add(new GeometryDrawing(
                valBrush,
                null,
                new RectangleGeometry(new Rect(0, 0, 216, 140))));

            _svArea.Background = new DrawingBrush(group);

            // Update alpha track gradient target color
            if (_alphaBrush != null && _alphaBrush.GradientStops.Count == 2)
            {
                _alphaBrush.GradientStops[1].Color = HsvToRgb(_hue, _saturation, _value, 1.0);
            }
        }

        private void UpdateThumbPositions()
        {
            // SV Thumb
            double x = _saturation * 216.0;
            double y = (1.0 - _value) * 140.0;
            Canvas.SetLeft(_svThumb, Math.Max(-7, Math.Min(209, x - 7)));
            Canvas.SetTop(_svThumb, Math.Max(-7, Math.Min(133, y - 7)));

            // Hue Thumb
            double hueX = (_hue / 360.0) * (216.0 - 8.0);
            Canvas.SetLeft(_hueThumb, Math.Max(0, Math.Min(208, hueX)));

            // Alpha Thumb
            double alphaX = _alpha * (216.0 - 8.0);
            Canvas.SetLeft(_alphaThumb, Math.Max(0, Math.Min(208, alphaX)));
        }

        private void UpdatePreviewAndText()
        {
            var col = SelectedColor;
            _previewBox.Background = new SolidColorBrush(col);
            _hexInput.Text = SelectedHex;
        }

        private void OnSvMouseDown(object sender, MouseButtonEventArgs e)
        {
            _svArea.CaptureMouse();
            UpdateSvFromMouse(e.GetPosition(_svArea));
        }

        private void OnSvMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _svArea.IsMouseCaptured)
            {
                UpdateSvFromMouse(e.GetPosition(_svArea));
            }
            else if (_svArea.IsMouseCaptured)
            {
                _svArea.ReleaseMouseCapture();
            }
        }

        private void UpdateSvFromMouse(Point pt)
        {
            double s = Math.Max(0.0, Math.Min(1.0, pt.X / 216.0));
            double v = Math.Max(0.0, Math.Min(1.0, 1.0 - (pt.Y / 140.0)));

            _saturation = s;
            _value = v;

            UpdateThumbPositions();
            UpdatePreviewAndText();
            NotifyChanged();
        }

        private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
        {
            var trk = sender as FrameworkElement;
            if (trk != null)
            {
                trk.CaptureMouse();
                UpdateHueFromMouse(e.GetPosition(trk));
            }
        }

        private void OnHueMouseMove(object sender, MouseEventArgs e)
        {
            var trk = sender as FrameworkElement;
            if (trk != null && e.LeftButton == MouseButtonState.Pressed && trk.IsMouseCaptured)
            {
                UpdateHueFromMouse(e.GetPosition(trk));
            }
            else if (trk != null && trk.IsMouseCaptured)
            {
                trk.ReleaseMouseCapture();
            }
        }

        private void UpdateHueFromMouse(Point pt)
        {
            double ratio = Math.Max(0.0, Math.Min(1.0, pt.X / 216.0));
            _hue = ratio * 360.0;
            if (_hue >= 360.0) _hue = 359.9;

            UpdateSvBackground();
            UpdateThumbPositions();
            UpdatePreviewAndText();
            NotifyChanged();
        }

        private void OnAlphaMouseDown(object sender, MouseButtonEventArgs e)
        {
            var trk = sender as FrameworkElement;
            if (trk != null)
            {
                trk.CaptureMouse();
                UpdateAlphaFromMouse(e.GetPosition(trk));
            }
        }

        private void OnAlphaMouseMove(object sender, MouseEventArgs e)
        {
            var trk = sender as FrameworkElement;
            if (trk != null && e.LeftButton == MouseButtonState.Pressed && trk.IsMouseCaptured)
            {
                UpdateAlphaFromMouse(e.GetPosition(trk));
            }
            else if (trk != null && trk.IsMouseCaptured)
            {
                trk.ReleaseMouseCapture();
            }
        }

        private void UpdateAlphaFromMouse(Point pt)
        {
            double a = Math.Max(0.0, Math.Min(1.0, pt.X / 216.0));
            _alpha = a;

            UpdateThumbPositions();
            UpdatePreviewAndText();
            NotifyChanged();
        }

        private void OnHexInputChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            string txt = _hexInput.Text.Trim();
            if (txt.StartsWith("#") && (txt.Length == 7 || txt.Length == 9 || txt.Length == 4))
            {
                try
                {
                    var col = ParseColor(txt);
                    _isUpdatingUi = true;
                    try
                    {
                        RgbToHsv(col.R, col.G, col.B, out _hue, out _saturation, out _value);
                        _alpha = col.A / 255.0;
                        UpdateSvBackground();
                        UpdateThumbPositions();
                        _previewBox.Background = new SolidColorBrush(col);
                    }
                    finally
                    {
                        _isUpdatingUi = false;
                    }
                    NotifyChanged();
                }
                catch { }
            }
        }

        private void NotifyChanged()
        {
            if (ColorChanged != null)
            {
                ColorChanged(SelectedHex, SelectedColor);
            }
        }

        public static Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Colors.White;
            hex = hex.Trim().TrimStart('#');

            try
            {
                if (hex.Length == 3)
                {
                    byte r = byte.Parse(new string(hex[0], 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(new string(hex[1], 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(new string(hex[2], 2), NumberStyles.HexNumber);
                    return Color.FromRgb(r, g, b);
                }
                if (hex.Length == 6)
                {
                    byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                    return Color.FromRgb(r, g, b);
                }
                if (hex.Length == 8)
                {
                    byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                    byte a = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch { }

            return Colors.White;
        }

        public static Color HsvToRgb(double h, double s, double v, double a)
        {
            while (h < 0) h += 360;
            while (h >= 360) h -= 360;

            double c = v * s;
            double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            double m = v - c;

            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            byte br = (byte)Math.Max(0, Math.Min(255, (r + m) * 255.0));
            byte bg = (byte)Math.Max(0, Math.Min(255, (g + m) * 255.0));
            byte bb = (byte)Math.Max(0, Math.Min(255, (b + m) * 255.0));
            byte ba = (byte)Math.Max(0, Math.Min(255, a * 255.0));

            return Color.FromArgb(ba, br, bg, bb);
        }

        public static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double dr = r / 255.0;
            double dg = g / 255.0;
            double db = b / 255.0;

            double max = Math.Max(dr, Math.Max(dg, db));
            double min = Math.Min(dr, Math.Min(dg, db));
            double delta = max - min;

            v = max;
            s = (max > 1e-6) ? (delta / max) : 0.0;

            if (delta < 1e-6)
            {
                h = 0;
            }
            else
            {
                if (Math.Abs(max - dr) < 1e-6)
                    h = 60.0 * (((dg - db) / delta) % 6.0);
                else if (Math.Abs(max - dg) < 1e-6)
                    h = 60.0 * (((db - dr) / delta) + 2.0);
                else
                    h = 60.0 * (((dr - dg) / delta) + 4.0);

                if (h < 0) h += 360.0;
            }
        }
    }

    /// <summary>
    /// Dropdown popup wrapping ColorPickerControl with cinematic shadow and reliable stay-open lifecycle.
    /// Does not auto-dismiss on tap; closes cleanly on Esc, close button, or outside click.
    /// </summary>
    public class ColorPickerPopup : Popup
    {
        private static ColorPickerPopup _activePopup = null;
        private static UIElement _activeTarget = null;

        public static ColorPickerPopup ActivePopup { get { return _activePopup; } }

        public static void CloseActive()
        {
            if (_activePopup != null)
            {
                _activePopup.IsOpen = false;
                _activePopup = null;
                _activeTarget = null;
            }
        }

        private readonly ColorPickerControl _picker;

        public ColorPickerControl Picker { get { return _picker; } }

        public ColorPickerPopup()
        {
            AllowsTransparency = true;
            StaysOpen = true; // StaysOpen = true prevents premature dismissal on tap release!
            Placement = PlacementMode.Bottom;

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 53, 76)),
                BorderThickness = new Thickness(1.2),
                Background = new SolidColorBrush(Color.FromRgb(20, 23, 34)),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 6,
                    Opacity = 0.75
                }
            };

            _picker = new ColorPickerControl();
            _picker.RequestClose += () => { this.IsOpen = false; };
            border.Child = _picker;
            Child = border;
        }

        public static ColorPickerPopup ShowAt(UIElement target, string initialHex, Action<string> onColorChanged)
        {
            if (_activePopup != null)
            {
                bool isSameTarget = (_activeTarget == target);
                CloseActive();
                if (isSameTarget)
                {
                    // Tapping the same swatch toggles it closed
                    return null;
                }
            }

            var popup = new ColorPickerPopup();
            popup.PlacementTarget = target;
            popup.Picker.SetColor(initialHex);
            popup.Picker.ColorChanged += (hex, col) =>
            {
                if (onColorChanged != null)
                {
                    onColorChanged(hex);
                }
            };

            _activePopup = popup;
            _activeTarget = target;
            popup.Closed += (s, e) =>
            {
                if (_activePopup == popup)
                {
                    _activePopup = null;
                    _activeTarget = null;
                }
            };

            popup.IsOpen = true;

            // Hook window events for outside click and Escape key dismissal
            var win = Window.GetWindow(target);
            if (win != null)
            {
                MouseButtonEventHandler winMouseDown = null;
                KeyEventHandler winKeyDown = null;

                winMouseDown = (s, e) =>
                {
                    if (!popup.IsOpen)
                    {
                        win.PreviewMouseDown -= winMouseDown;
                        return;
                    }

                    var feTarget = target as FrameworkElement;
                    if (feTarget != null)
                    {
                        Point ptTarget = e.GetPosition(feTarget);
                        if (ptTarget.X >= 0 && ptTarget.X <= feTarget.ActualWidth &&
                            ptTarget.Y >= 0 && ptTarget.Y <= feTarget.ActualHeight)
                        {
                            return; // Clicked swatch itself, let toggle handle it
                        }
                    }

                    var childFe = popup.Child as FrameworkElement;
                    if (childFe != null && childFe.IsLoaded)
                    {
                        try
                        {
                            Point screenPos = win.PointToScreen(e.GetPosition(win));
                            Point ptPopup = childFe.PointFromScreen(screenPos);
                            if (ptPopup.X >= 0 && ptPopup.X <= childFe.ActualWidth &&
                                ptPopup.Y >= 0 && ptPopup.Y <= childFe.ActualHeight)
                            {
                                return; // Clicked inside popup
                            }
                        }
                        catch
                        {
                            // Fallback
                        }
                    }

                    // Click was outside popup
                    CloseActive();
                    win.PreviewMouseDown -= winMouseDown;
                };

                winKeyDown = (s, e) =>
                {
                    if (e.Key == Key.Escape && popup.IsOpen)
                    {
                        CloseActive();
                        e.Handled = true;
                        win.PreviewKeyDown -= winKeyDown;
                    }
                };

                win.PreviewMouseDown += winMouseDown;
                win.PreviewKeyDown += winKeyDown;

                popup.Closed += (s, e) =>
                {
                    win.PreviewMouseDown -= winMouseDown;
                    win.PreviewKeyDown -= winKeyDown;
                };
            }

            return popup;
        }
    }
}
