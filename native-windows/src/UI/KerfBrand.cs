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
        // Actual Figma UI Dark Palette Colors
        public static readonly Color FigmaPanel = Color.FromRgb(0x2C, 0x2C, 0x2C);   // #2C2C2C (Actual Figma UI Panel)
        public static readonly Color FigmaCanvas = Color.FromRgb(0x1E, 0x1E, 0x1E);  // #1E1E1E (Actual Figma Canvas)
        public static readonly Color FigmaBorder = Color.FromRgb(0x38, 0x38, 0x38);  // #383838 (Actual Figma Divider)
        public static readonly Color FigmaCard = Color.FromRgb(0x22, 0x22, 0x22);    // #222222 (Card / Sub-panel)

        // Primary Brand Colors (Light Green Accent per Button Mockup)
        public static readonly Color Mint = Color.FromRgb(0x46, 0xEF, 0xB1);       // #46EFB1 (Light Green Accent)
        public static readonly Color Teal = Color.FromRgb(0x0A, 0x71, 0x7B);       // #0A717B
        public static readonly Color DarkSlate = FigmaPanel;                       // Mapped to actual Figma panel #2C2C2C
        public static readonly Color Obsidian = FigmaCanvas;                      // Mapped to actual Figma canvas #1E1E1E

        // Secondary Palette Color
        public static readonly Color Amber = Color.FromRgb(0xF6, 0xB3, 0x22);      // #F6B322 (Keyframes)

        // Standard Text Colors
        public static readonly Color TextWhite = Colors.White;
        public static readonly Color TextBlack = Color.FromRgb(0x11, 0x18, 0x27);  // #111827 / Black for light green buttons & menu
        public static readonly Color TextMuted = Color.FromRgb(0xB3, 0xB3, 0xB3);  // #B3B3B3 (Figma Label Gray)
        public static readonly Color BorderSubtle = FigmaBorder;                   // #383838 (Figma Divider)
        public static readonly Color PanelSurface = FigmaPanel;                    // #2C2C2C
        public static readonly Color AppBackground = FigmaCanvas;                  // #1E1E1E

        // Brushes (Frozen for optimal WPF rendering)
        public static readonly SolidColorBrush FigmaPanelBrush = FreezeBrush(new SolidColorBrush(FigmaPanel));
        public static readonly SolidColorBrush FigmaCanvasBrush = FreezeBrush(new SolidColorBrush(FigmaCanvas));
        public static readonly SolidColorBrush FigmaBorderBrush = FreezeBrush(new SolidColorBrush(FigmaBorder));
        public static readonly SolidColorBrush FigmaCardBrush = FreezeBrush(new SolidColorBrush(FigmaCard));

        public static readonly SolidColorBrush MintBrush = FreezeBrush(new SolidColorBrush(Mint));
        public static readonly SolidColorBrush BrandTealBrush = FreezeBrush(new SolidColorBrush(Teal));
        public static readonly SolidColorBrush TealBrush = FigmaBorderBrush;       // Subtly dark gray Figma divider (#383838)
        public static readonly SolidColorBrush DarkSlateBrush = FigmaPanelBrush;   // #2C2C2C
        public static readonly SolidColorBrush ObsidianBrush = FigmaCanvasBrush;   // #1E1E1E
        public static readonly SolidColorBrush AmberBrush = FreezeBrush(new SolidColorBrush(Amber));
        public static readonly SolidColorBrush TextWhiteBrush = Brushes.White;
        public static readonly SolidColorBrush TextBlackBrush = FreezeBrush(new SolidColorBrush(TextBlack));
        public static readonly SolidColorBrush TextMutedBrush = FreezeBrush(new SolidColorBrush(TextMuted));
        public static readonly SolidColorBrush BorderSubtleBrush = FigmaBorderBrush;
        public static readonly SolidColorBrush PanelSurfaceBrush = FigmaPanelBrush;
        public static readonly SolidColorBrush AppBackgroundBrush = FigmaCanvasBrush;

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
        /// Creates a sleek Primary Button matching the brand specification (#46EFB1 light green fill, black text).
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
                Foreground = TextBlackBrush, // Black text per user instruction & button mockup
                BorderBrush = MintBrush,
                BorderThickness = new Thickness(0),
                Template = CreateRoundedButtonTemplate(cornerRadius)
            };

            btn.MouseEnter += (s, e) =>
            {
                btn.Background = FreezeBrush(new SolidColorBrush(Color.FromRgb(0x62, 0xF7, 0xC0)));
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
        /// Creates a sleek Secondary Button matching the brand specification (#2C2C2C Figma dark fill, #46EFB1 1px border, white text).
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
                Background = FigmaPanelBrush,
                Foreground = Brushes.White,
                BorderBrush = MintBrush,
                BorderThickness = new Thickness(1),
                Template = CreateRoundedButtonTemplate(cornerRadius)
            };

            btn.MouseEnter += (s, e) =>
            {
                btn.Background = FigmaBorderBrush;
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.Background = FigmaPanelBrush;
            };

            if (onClick != null)
            {
                btn.Click += (s, e) => onClick();
            }
            return btn;
        }
    }
}
