using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Kerf.UI
{
    /// <summary>
    /// Tactile, Figma-style scrubbable numeric fields and smooth sliders.
    /// Supports horizontal label scrubbing with Shift (10x) and Alt (0.1x) modifiers,
    /// direct keyboard input, and 60 FPS live canvas updates.
    /// Styled strictly in accordance with Kerf Brand Identity.
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
            string suffix = "",
            string tooltip = null)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 6), ToolTip = tooltip ?? label };

            var container = new Border
            {
                Background = KerfBrand.DarkSlateBrush,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Height = 28,
                ToolTip = tooltip ?? label
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
                Foreground = KerfBrand.TextMutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.SizeWE,
                ToolTip = tooltip ?? label
            };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            // 2. Value TextBox
            var tb = new TextBox
            {
                Text = Math.Round(initialValue, 1).ToString("0.#", CultureInfo.InvariantCulture) + suffix,
                FontSize = 11.5,
                Foreground = KerfBrand.TextWhiteBrush,
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
                container.BorderBrush = KerfBrand.MintBrush;
            };

            lbl.MouseMove += (s, e) =>
            {
                if (isScrubbing && e.LeftButton == MouseButtonState.Pressed && lbl.IsMouseCaptured)
                {
                    try
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

                        tb.Text = Math.Round(newVal, 1).ToString("0.#", CultureInfo.InvariantCulture) + (suffix ?? string.Empty);

                        if (onChanged != null)
                        {
                            onChanged(newVal);
                        }
                    }
                    catch (Exception)
                    {
                        // Fail-safe to avoid crash during live scrubbing
                    }
                }
                else if (lbl.IsMouseCaptured)
                {
                    lbl.ReleaseMouseCapture();
                    isScrubbing = false;
                    container.BorderBrush = KerfBrand.TealBrush;
                }
            };

            lbl.MouseLeftButtonUp += (s, e) =>
            {
                if (lbl.IsMouseCaptured)
                {
                    lbl.ReleaseMouseCapture();
                }
                isScrubbing = false;
                container.BorderBrush = KerfBrand.TealBrush;
            };

            Action commitText = () =>
            {
                try
                {
                    string raw = !string.IsNullOrEmpty(suffix) ? tb.Text.Replace(suffix, "").Trim() : tb.Text.Trim();
                    double parsed;
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                        double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
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
                }
                catch (Exception)
                {
                    // Fail-safe to guarantee studio never crashes on text commit
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

        private static Style _sharedSliderStyle;
        public static Style GetSharedSliderStyle()
        {
            if (_sharedSliderStyle != null) return _sharedSliderStyle;

            string xaml = @"
<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
       TargetType=""Slider"">
    <Setter Property=""IsMoveToPointEnabled"" Value=""True""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
        <Setter.Value>
            <ControlTemplate TargetType=""Slider"">
                <Grid VerticalAlignment=""Center"">
                    <Border Height=""5"" CornerRadius=""2.5"" Background=""#0C4247"" BorderBrush=""#0A717B"" BorderThickness=""1"" VerticalAlignment=""Center""/>
                    <Track x:Name=""PART_Track"">
                        <Track.Thumb>
                            <Thumb Cursor=""Hand"">
                                <Thumb.Template>
                                    <ControlTemplate TargetType=""Thumb"">
                                        <Border Width=""14"" Height=""14"" CornerRadius=""7"" Background=""#FFFFFF"" BorderBrush=""#46EFB1"" BorderThickness=""2"">
                                            <Border.Effect>
                                                <DropShadowEffect Color=""#000000"" BlurRadius=""4"" ShadowDepth=""1"" Opacity=""0.5""/>
                                            </Border.Effect>
                                        </Border>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";

            try
            {
                _sharedSliderStyle = (Style)System.Windows.Markup.XamlReader.Parse(xaml);
            }
            catch
            {
                _sharedSliderStyle = new Style(typeof(Slider));
                _sharedSliderStyle.Setters.Add(new Setter(Slider.IsMoveToPointEnabledProperty, true));
                _sharedSliderStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            }

            return _sharedSliderStyle;
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
            string suffix = "",
            string tooltip = null)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };

            // Label
            bool isShort = (label != null && label.Length <= 2);
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = isShort ? 12 : 11,
                Foreground = KerfBrand.TextMutedBrush,
                Width = isShort ? 24 : 54,
                TextAlignment = isShort ? TextAlignment.Center : TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = tooltip ?? label
            };
            DockPanel.SetDock(lbl, Dock.Left);
            dock.Children.Add(lbl);

            // Readout Badge
            var badge = new TextBlock
            {
                Text = Math.Round(initialValue, 0) + suffix,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush,
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
                IsMoveToPointEnabled = true,
                Cursor = Cursors.Hand,
                Style = GetSharedSliderStyle(),
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
