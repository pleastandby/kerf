using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kerf.UI
{
    /// <summary>
    /// Official Kerf Studio Brand Identity & Color System.
    /// Strictly constrained to the official brand palette:
    /// Primary:
    ///   - #46EFB1: Electric Mint / Neon Teal (Hero accent, selection, primary buttons, active badges)
    ///   - #0A717B: Deep Cyan / Teal Accent (Hover states, interactive borders, secondary accents)
    ///   - #0C4247: Dark Teal Slate / Pine (Panels, cards, tool shelf, secondary button fill)
    ///   - #1E082E: Obsidian Plum / Charcoal (Deep canvas surround, main app window, root chrome)
    /// Secondary:
    ///   - #F6B322: Warm Amber Gold (Keyframe markers ◆, telemetry alerts, warm highlights)
    /// </summary>
    public static class KerfBrand
    {
        // Primary Palette Colors
        public static readonly Color Mint = Color.FromRgb(0x46, 0xEF, 0xB1);       // #46EFB1
        public static readonly Color Teal = Color.FromRgb(0x0A, 0x71, 0x7B);       // #0A717B
        public static readonly Color DarkSlate = Color.FromRgb(0x0C, 0x42, 0x47);  // #0C4247
        public static readonly Color Obsidian = Color.FromRgb(0x1E, 0x08, 0x2E);   // #1E082E

        // Secondary Palette Color
        public static readonly Color Amber = Color.FromRgb(0xF6, 0xB3, 0x22);      // #F6B322

        // Derived & Neutral Colors
        public static readonly Color TextWhite = Colors.White;
        public static readonly Color TextMuted = Color.FromRgb(0xA2, 0xC0, 0xC3);  // Soft pale teal-slate
        public static readonly Color BorderSubtle = Color.FromRgb(0x0A, 0x71, 0x7B);
        public static readonly Color PanelSurface = Color.FromRgb(0x0C, 0x42, 0x47);
        public static readonly Color AppBackground = Color.FromRgb(0x1E, 0x08, 0x2E);

        // Brushes (Frozen for optimal WPF rendering)
        public static readonly SolidColorBrush MintBrush = FreezeBrush(new SolidColorBrush(Mint));
        public static readonly SolidColorBrush TealBrush = FreezeBrush(new SolidColorBrush(Teal));
        public static readonly SolidColorBrush DarkSlateBrush = FreezeBrush(new SolidColorBrush(DarkSlate));
        public static readonly SolidColorBrush ObsidianBrush = FreezeBrush(new SolidColorBrush(Obsidian));
        public static readonly SolidColorBrush AmberBrush = FreezeBrush(new SolidColorBrush(Amber));
        public static readonly SolidColorBrush TextWhiteBrush = Brushes.White;
        public static readonly SolidColorBrush TextMutedBrush = FreezeBrush(new SolidColorBrush(TextMuted));
        public static readonly SolidColorBrush BorderSubtleBrush = FreezeBrush(new SolidColorBrush(BorderSubtle));

        public static SolidColorBrush FreezeBrush(SolidColorBrush brush)
        {
            if (brush != null && brush.CanFreeze) brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Creates a reusable rounded button template with customizable corner radius.
        /// </summary>
        public static ControlTemplate CreateRoundedButtonTemplate(double cornerRadius)
        {
            var t = new ControlTemplate(typeof(Button));
            var b = new FrameworkElementFactory(typeof(Border), "btnBorder");
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            b.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            b.AppendChild(cp);

            t.VisualTree = b;
            return t;
        }

        /// <summary>
        /// Creates a sleek Primary Button matching the brand specification (#46EFB1 fill, dark text).
        /// </summary>
        public static Button CreatePrimaryButton(string text, Action onClick = null, double cornerRadius = 10)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(16, 7, 16, 7),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = MintBrush,
                Foreground = DarkSlateBrush,
                BorderBrush = MintBrush,
                BorderThickness = new Thickness(0),
                Template = CreateRoundedButtonTemplate(cornerRadius)
            };

            btn.MouseEnter += (s, e) =>
            {
                btn.Background = FreezeBrush(new SolidColorBrush(Color.FromRgb(0x5A, 0xF5, 0xBE)));
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.Background = MintBrush;
            };

            if (onClick != null)
            {
                btn.Click += (s, e) => onClick();
            }
            return btn;
        }

        /// <summary>
        /// Creates a sleek Secondary Button matching the brand specification (#0C4247 fill, #46EFB1 1px border, white text).
        /// </summary>
        public static Button CreateSecondaryButton(string text, Action onClick = null, double cornerRadius = 10)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Padding = new Thickness(16, 7, 16, 7),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = DarkSlateBrush,
                Foreground = Brushes.White,
                BorderBrush = MintBrush,
                BorderThickness = new Thickness(1),
                Template = CreateRoundedButtonTemplate(cornerRadius)
            };

            btn.MouseEnter += (s, e) =>
            {
                btn.Background = TealBrush;
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.Background = DarkSlateBrush;
            };

            if (onClick != null)
            {
                btn.Click += (s, e) => onClick();
            }
            return btn;
        }
    }
}
