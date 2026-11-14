using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Kerf.UI
{
    /// <summary>
    /// Official Kerf Brand Identity & Color System.
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

        #region Dark Rounded TextBox & ComboBox Styling

        private static ControlTemplate _darkRoundedTextBoxTemplate;

        public static ControlTemplate GetDarkRoundedTextBoxTemplate(double cornerRadius = 6)
        {
            if (_darkRoundedTextBoxTemplate == null)
            {
                var template = new ControlTemplate(typeof(TextBox));
                var border = new FrameworkElementFactory(typeof(Border), "tbBorder");
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
                border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
                border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
                border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
                border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
                border.SetValue(Border.SnapsToDevicePixelsProperty, true);

                var scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
                scrollViewer.SetValue(ScrollViewer.MarginProperty, new Thickness(0));
                // Allow the content host to fill the padded text area. Centering the
                // ScrollViewer itself clips glyphs in short inspector and dialog fields.
                scrollViewer.SetValue(ScrollViewer.VerticalAlignmentProperty, VerticalAlignment.Stretch);
                border.AppendChild(scrollViewer);

                template.VisualTree = border;

                var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, MintBrush, "tbBorder"));
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(36, 42, 58)), "tbBorder"));
                template.Triggers.Add(hoverTrigger);

                var focusTrigger = new Trigger { Property = UIElement.IsFocusedProperty, Value = true };
                focusTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, MintBrush, "tbBorder"));
                focusTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(40, 48, 66)), "tbBorder"));
                template.Triggers.Add(focusTrigger);

                _darkRoundedTextBoxTemplate = template;
            }
            return _darkRoundedTextBoxTemplate;
        }

        public static void ApplyDarkRoundedTextBoxStyle(TextBox textBox, double cornerRadius = 6, double fontSize = 11.5)
        {
            if (textBox == null) return;
            textBox.Template = GetDarkRoundedTextBoxTemplate(cornerRadius);
            textBox.Background = new SolidColorBrush(Color.FromRgb(28, 33, 46));
            textBox.Foreground = Brushes.White;
            textBox.CaretBrush = Brushes.White;
            textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 66, 92));
            textBox.BorderThickness = new Thickness(1);
            textBox.Padding = new Thickness(9, 2, 9, 2);
            textBox.VerticalContentAlignment = VerticalAlignment.Center;
            textBox.FontSize = fontSize;
            textBox.MinHeight = 30;
        }

        public static TextBox CreateDarkRoundedTextBox(string text = "", double width = double.NaN, double height = 28, double cornerRadius = 6)
        {
            var tb = new TextBox
            {
                Text = text,
                Height = height
            };
            if (!double.IsNaN(width)) tb.Width = width;
            ApplyDarkRoundedTextBoxStyle(tb, cornerRadius);
            return tb;
        }

        public static Style CreateDarkTextBoxStyle(double cornerRadius = 6)
        {
            var style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(Control.TemplateProperty, GetDarkRoundedTextBoxTemplate(cornerRadius)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(28, 33, 46))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(TextBox.CaretBrushProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(56, 66, 92))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 2, 9, 2)));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 30.0));
            return style;
        }

        private static ControlTemplate _darkComboTemplate;
        private static Style _darkComboItemStyle;

        public static void ApplyDarkRoundedComboBoxStyle(ComboBox combo, double cornerRadius = 6)
        {
            if (combo == null) return;

            if (_darkComboTemplate == null)
            {
                // 1. ToggleButton Template
                var tbTemplate = new ControlTemplate(typeof(ToggleButton));
                var tbBorder = new FrameworkElementFactory(typeof(Border), "tbBorder");
                tbBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
                tbBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(28, 33, 46)));
                tbBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(56, 66, 92)));
                tbBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));

                var tbGrid = new FrameworkElementFactory(typeof(Grid));
                var c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
                c0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
                var c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
                c1.SetValue(ColumnDefinition.WidthProperty, new GridLength(22));
                tbGrid.AppendChild(c0);
                tbGrid.AppendChild(c1);

                var arrow = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
                arrow.SetValue(Grid.ColumnProperty, 1);
                arrow.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0 0 L 4 4 L 8 0"));
                arrow.SetValue(System.Windows.Shapes.Path.StrokeProperty, MintBrush);
                arrow.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.6);
                arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                tbGrid.AppendChild(arrow);

                tbBorder.AppendChild(tbGrid);
                tbTemplate.VisualTree = tbBorder;

                var tbHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                tbHover.Setters.Add(new Setter(Border.BorderBrushProperty, MintBrush, "tbBorder"));
                tbHover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(36, 42, 58)), "tbBorder"));
                tbTemplate.Triggers.Add(tbHover);

                var tbChecked = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
                tbChecked.Setters.Add(new Setter(Border.BorderBrushProperty, MintBrush, "tbBorder"));
                tbChecked.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(40, 48, 66)), "tbBorder"));
                tbTemplate.Triggers.Add(tbChecked);

                // 2. ComboBox Template
                _darkComboTemplate = new ControlTemplate(typeof(ComboBox));
                var grid = new FrameworkElementFactory(typeof(Grid));

                var toggleBtn = new FrameworkElementFactory(typeof(ToggleButton), "toggleButton");
                toggleBtn.SetValue(ToggleButton.ClickModeProperty, ClickMode.Press);
                toggleBtn.SetValue(UIElement.FocusableProperty, false);
                toggleBtn.SetValue(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
                    Mode = BindingMode.TwoWay
                });
                toggleBtn.SetValue(Control.TemplateProperty, tbTemplate);
                grid.AppendChild(toggleBtn);

                var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter), "ContentSite");
                contentPresenter.SetValue(UIElement.IsHitTestVisibleProperty, false);
                contentPresenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
                contentPresenter.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
                contentPresenter.SetValue(ContentPresenter.ContentTemplateSelectorProperty, new TemplateBindingExtension(ComboBox.ItemTemplateSelectorProperty));
                contentPresenter.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, 24, 0));
                contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                contentPresenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                grid.AppendChild(contentPresenter);

                var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
                popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
                popup.SetValue(Popup.IsOpenProperty, new TemplateBindingExtension(ComboBox.IsDropDownOpenProperty));
                popup.SetValue(Popup.AllowsTransparencyProperty, true);
                popup.SetValue(UIElement.FocusableProperty, false);
                popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Slide);

                var popBorder = new FrameworkElementFactory(typeof(Border), "DropDownBorder");
                popBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
                popBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(24, 28, 40)));
                popBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(56, 66, 92)));
                popBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                popBorder.SetValue(Border.PaddingProperty, new Thickness(4));
                popBorder.SetValue(FrameworkElement.MaxHeightProperty, 240.0);
                popBorder.SetValue(FrameworkElement.MinWidthProperty, new Binding("ActualWidth")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
                });

                var popShadow = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 6,
                    Direction = 270,
                    Color = Colors.Black,
                    Opacity = 0.65
                };
                popBorder.SetValue(UIElement.EffectProperty, popShadow);

                var scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
                scrollViewer.SetValue(ScrollViewer.CanContentScrollProperty, true);
                scrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
                scrollViewer.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);

                var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
                itemsPresenter.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Contained);
                scrollViewer.AppendChild(itemsPresenter);
                popBorder.AppendChild(scrollViewer);
                popup.AppendChild(popBorder);

                grid.AppendChild(popup);
                _darkComboTemplate.VisualTree = grid;

                // 3. ComboBoxItem Style
                _darkComboItemStyle = new Style(typeof(ComboBoxItem));
                _darkComboItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
                _darkComboItemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
                _darkComboItemStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));

                var itemTemplate = new ControlTemplate(typeof(ComboBoxItem));
                var itemBorder = new FrameworkElementFactory(typeof(Border), "ItemBorder");
                itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
                itemBorder.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));
                itemBorder.SetValue(Border.PaddingProperty, new Thickness(8, 5, 8, 5));
                itemBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);

                var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                itemBorder.AppendChild(cp);
                itemTemplate.VisualTree = itemBorder;

                var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(42, 50, 72)), "ItemBorder"));
                hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, MintBrush));
                itemTemplate.Triggers.Add(hoverTrigger);

                var selTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
                selTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(56, 66, 92)), "ItemBorder"));
                selTrigger.Setters.Add(new Setter(Control.ForegroundProperty, MintBrush));
                itemTemplate.Triggers.Add(selTrigger);

                _darkComboItemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
            }

            combo.Template = _darkComboTemplate;
            combo.ItemContainerStyle = _darkComboItemStyle;
            combo.Foreground = Brushes.White;
            combo.Background = new SolidColorBrush(Color.FromRgb(28, 33, 46));
            combo.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 66, 92));
            combo.Height = 28;
            combo.Cursor = Cursors.Hand;
        }

        public static ComboBox CreateDarkComboBox(double width = 300, double height = 28, Thickness margin = default(Thickness))
        {
            var combo = new ComboBox
            {
                Width = width,
                Height = height,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = margin,
                FontSize = 11.5
            };
            ApplyDarkRoundedComboBoxStyle(combo);
            return combo;
        }

        public static Style CreateDarkComboBoxStyle(double cornerRadius = 6)
        {
            var style = new Style(typeof(ComboBox));
            var dummy = new ComboBox();
            ApplyDarkRoundedComboBoxStyle(dummy, cornerRadius);
            style.Setters.Add(new Setter(Control.TemplateProperty, _darkComboTemplate));
            style.Setters.Add(new Setter(ItemsControl.ItemContainerStyleProperty, _darkComboItemStyle));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(28, 33, 46))));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(56, 66, 92))));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 28.0));
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            return style;
        }

        #endregion
    }
}
