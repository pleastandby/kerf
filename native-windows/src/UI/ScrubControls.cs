using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kerf.UI
{
    /// <summary>
    /// Tactile, Figma-style scrubbable numeric fields and smooth sliders.
    /// Supports horizontal label scrubbing with Shift (10x) and Alt (0.1x) modifiers,
    /// direct keyboard input, and 60 FPS live canvas updates.
    /// </summary>
    public static class ScrubControls
    {
        /// <summary>
        /// Creates a Figma-style numeric field where dragging horizontally over the label scrubs the value.
        /// </summary>
        public static FrameworkElement CreateScrubField(
            string label,
            double initialValue,
            Action<double> onChanged,
            double min = double.MinValue,
            double max = double.MaxValue,
            double step = 1.0,
            string suffix = "")
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 30, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 48, 70)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Height = 28
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 1. Scrub Label (SizeWE cursor, click & drag to scrub)
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(125, 140, 175)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.SizeWE
            };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            // 2. Value TextBox
            var tb = new TextBox
            {
                Text = Math.Round(initialValue, 1).ToString("0.#", CultureInfo.InvariantCulture) + suffix,
                FontSize = 11.5,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };
            Grid.SetColumn(tb, 1);
            grid.Children.Add(tb);

            container.Child = grid;
            dock.Children.Add(container);

            // State for scrubbing
            double currentValue = initialValue;
            Point startMouse = new Point(0, 0);
            double startVal = initialValue;
            bool isScrubbing = false;

            lbl.MouseLeftButtonDown += (s, e) =>
            {
                lbl.CaptureMouse();
                startMouse = e.GetPosition(lbl);
                startVal = currentValue;
                isScrubbing = true;
                container.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            };

            lbl.MouseMove += (s, e) =>
            {
                if (isScrubbing && e.LeftButton == MouseButtonState.Pressed && lbl.IsMouseCaptured)
                {
                    Point cur = e.GetPosition(lbl);
                    double dx = cur.X - startMouse.X;

                    double multiplier = step;
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        multiplier *= 10.0;
                    else if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                        multiplier *= 0.1;

                    double newVal = startVal + dx * multiplier;
                    newVal = Math.Max(min, Math.Min(max, newVal));
                    currentValue = newVal;

                    tb.Text = Math.Round(newVal, 1).ToString("0.#", CultureInfo.InvariantCulture) + suffix;

                    if (onChanged != null)
                    {
                        onChanged(newVal);
                    }
                }
                else if (lbl.IsMouseCaptured)
                {
                    lbl.ReleaseMouseCapture();
                    isScrubbing = false;
                    container.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 48, 70));
                }
            };

            lbl.MouseLeftButtonUp += (s, e) =>
            {
                if (lbl.IsMouseCaptured)
                {
                    lbl.ReleaseMouseCapture();
                }
                isScrubbing = false;
                container.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 48, 70));
            };

            Action commitText = () =>
            {
                string raw = tb.Text.Replace(suffix, "").Trim();
                double parsed;
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                    double.TryParse(raw, out parsed))
                {
                    parsed = Math.Max(min, Math.Min(max, parsed));
                    currentValue = parsed;
                    tb.Text = Math.Round(parsed, 1).ToString("0.#", CultureInfo.InvariantCulture) + suffix;
                    if (onChanged != null) onChanged(parsed);
                }
                else
                {
                    tb.Text = Math.Round(currentValue, 1).ToString("0.#", CultureInfo.InvariantCulture) + suffix;
                }
            };

            tb.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    commitText();
                    tb.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            };

            tb.LostFocus += (s, e) => commitText();

            return dock;
        }

        /// <summary>
        /// Creates a smooth, Figma-grade slider with real-time numeric badge and glowing track.
        /// </summary>
        public static FrameworkElement CreateSmoothSlider(
            string label,
            double initialValue,
            double min,
            double max,
            Action<double> onChanged,
            string suffix = "")
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };

            // Label
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 165)),
                Width = 54,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(lbl, Dock.Left);
            dock.Children.Add(lbl);

            // Readout Badge
            var badge = new TextBlock
            {
                Text = Math.Round(initialValue, 0) + suffix,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Width = 38,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            DockPanel.SetDock(badge, Dock.Right);
            dock.Children.Add(badge);

            // Smooth Slider Track
            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = initialValue,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };

            slider.ValueChanged += (s, e) =>
            {
                double val = slider.Value;
                badge.Text = Math.Round(val, 0) + suffix;
                if (onChanged != null)
                {
                    onChanged(val);
                }
            };

            dock.Children.Add(slider);
            return dock;
        }
    }
}
