using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kerf.Engine;
using Kerf.Engine.Animation;
using Kerf.IO;
using Kerf.Model;
using Microsoft.Win32;
using IOPath = System.IO.Path;
using Page = Kerf.Model.Page;

namespace Kerf.UI.Dialogs
{
    public enum ExportFormatType
    {
        KfxRuntime = 0,
        Png = 1,
        Jpeg = 2,
        Svg = 3,
        Pdf = 4,
        Mp4 = 5
    }

    /// <summary>
    /// Dedicated Kerf Studio Export Window.
    /// Provides a polished UI format selector for .kfx runtime packages, PNG, JPEG, SVG, PDF, and MP4.
    /// Strictly adheres to C# 5.0 compatibility.
    /// </summary>
    public class ExportDialog : Window
    {
        private readonly KerfDocument _document;
        private readonly StudioView _studioView;
        private ExportFormatType _selectedFormat = ExportFormatType.KfxRuntime;

        // UI references
        private StackPanel _formatListPanel;
        private ContentControl _optionsContainer;
        private TextBlock _statusTextBlock;
        private Button _actionButton;

        // Option controls
        private CheckBox _kfxIncludeMetadataCheck;
        private CheckBox _kfxGenerateWebRunnerCheck;
        private ComboBox _pngScaleCombo;
        private ComboBox _pngScopeCombo;
        private CheckBox _pngTransparentCheck;
        private Slider _jpegQualitySlider;
        private TextBlock _jpegQualityText;
        private ComboBox _jpegScaleCombo;
        private ComboBox _jpegScopeCombo;
        private ComboBox _svgScopeCombo;
        private CheckBox _svgBackgroundCheck;
        private ComboBox _pdfDpiCombo;
        private ComboBox _pdfScopeCombo;
        private ComboBox _mp4ClipCombo;
        private ComboBox _mp4FpsCombo;
        private ProgressBar _mp4ProgressBar;

        public ExportDialog(KerfDocument doc, StudioView studioView = null)
        {
            _document = doc ?? new KerfDocument();
            _studioView = studioView;

            Title = "Export Document & Animations — Kerf Studio";
            Width = 820;
            Height = 580;
            MinWidth = 760;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x07, 0x20)); // Obsidian #140720
            Foreground = Brushes.White;
            FontFamily = new FontFamily("-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif");

            InitializeUI();
            SelectFormat(ExportFormatType.KfxRuntime);
        }

        private void InitializeUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Body (Two columns)
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); // Footer

            // 1. Header
            var header = CreateHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // 2. Body Grid (Left format list, Right options & details)
            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Left formats
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right options

            // Left: Format selection list
            var leftBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x05, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(14, 16, 14, 16)
            };

            var leftScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _formatListPanel = new StackPanel();
            BuildFormatList();
            leftScroll.Content = _formatListPanel;
            leftBorder.Child = leftScroll;
            Grid.SetColumn(leftBorder, 0);
            body.Children.Add(leftBorder);

            // Right: Dynamic options panel
            var rightBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x08, 0x24)),
                Padding = new Thickness(24, 20, 24, 20)
            };
            _optionsContainer = new ContentControl();
            rightBorder.Child = _optionsContainer;
            Grid.SetColumn(rightBorder, 1);
            body.Children.Add(rightBorder);

            Grid.SetRow(body, 1);
            root.Children.Add(body);

            // 3. Footer
            var footer = CreateFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;
        }

        private FrameworkElement CreateHeader()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x08, 0x2E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0A, 0x71, 0x7B)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 0, 24, 0)
            };

            var dock = new DockPanel { LastChildFill = false };

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var icon = KerfLogo.CreateIconElement(24, 28, fillBrush: KerfBrand.MintBrush, withGlow: true);
            icon.Margin = new Thickness(0, 0, 12, 0);
            leftStack.Children.Add(icon);

            var titleStack = new StackPanel();
            var title = new TextBlock
            {
                Text = "Export Document & Animations",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            var subtitle = new TextBlock
            {
                Text = "Export production vector assets, video renders, or standalone cross-platform .kfx runtime packages",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleStack.Children.Add(title);
            titleStack.Children.Add(subtitle);
            leftStack.Children.Add(titleStack);

            DockPanel.SetDock(leftStack, Dock.Left);
            dock.Children.Add(leftStack);

            border.Child = dock;
            return border;
        }

        private FrameworkElement CreateFooter()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x05, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 0, 24, 0)
            };

            var dock = new DockPanel { LastChildFill = false };

            _statusTextBlock = new TextBlock
            {
                Text = "Ready to export.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x46, 0xEF, 0xB1)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_statusTextBlock, Dock.Left);
            dock.Children.Add(_statusTextBlock);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var cancelBtn = KerfBrand.CreateSecondaryButton("Cancel", () => Close(), 6);
            cancelBtn.Width = 90;
            cancelBtn.Height = 32;
            cancelBtn.Margin = new Thickness(0, 0, 12, 0);
            btnStack.Children.Add(cancelBtn);

            _actionButton = KerfBrand.CreatePrimaryButton("Export .kfx Package", () => ExecuteActiveExport(), 6);
            _actionButton.Width = 180;
            _actionButton.Height = 32;
            _actionButton.FontWeight = FontWeights.SemiBold;
            btnStack.Children.Add(_actionButton);

            DockPanel.SetDock(btnStack, Dock.Right);
            dock.Children.Add(btnStack);

            border.Child = dock;
            return border;
        }

        private void BuildFormatList()
        {
            _formatListPanel.Children.Clear();

            _formatListPanel.Children.Add(CreateFormatCard(
                ExportFormatType.KfxRuntime,
                "Kerf Animation (.kfx)",
                "RUNTIME",
                "Standalone cross-platform runtime package (like Rive) for Web, iOS, Android & Desktop.",
                KerfBrand.MintBrush,
                true));

            _formatListPanel.Children.Add(CreateFormatCard(
                ExportFormatType.Png,
                "PNG Image (.png)",
                "RASTER",
                "Lossless transparent bitmap. Multi-resolution scale options @1x, @2x, @3x, @4x.",
                new SolidColorBrush(Color.FromRgb(0x0A, 0x71, 0x7B))));

            _formatListPanel.Children.Add(CreateFormatCard(
                ExportFormatType.Jpeg,
                "JPEG Image (.jpg)",
                "RASTER",
                "Compressed image with adjustable quality and background color. Compact for web.",
                new SolidColorBrush(Color.FromRgb(0x7A, 0x82, 0x99))));

            _formatListPanel.Children.Add(CreateFormatCard(
                ExportFormatType.Svg,
                "Vector Graphic (.svg)",
                "VECTOR",
                "Scalable vector paths, gradients, strokes, and clean XML geometry.",
                KerfBrand.AmberBrush));

            _formatListPanel.Children.Add(CreateFormatCard(
                ExportFormatType.Pdf,
                "PDF Document (.pdf)",
                "DOCUMENT",
                "Print-ready high-resolution vector and document container.",
                new SolidColorBrush(Color.FromRgb(0xD4, 0x5D, 0x79))));

            _formatListPanel.Children.Add(CreateFormatCard(
                ExportFormatType.Mp4,
                "MP4 Video (.mp4)",
                "VIDEO",
                "H.264 video animation render of active clip or state machine timeline.",
                new SolidColorBrush(Color.FromRgb(0x4D, 0x96, 0xFF))));
        }

        private FrameworkElement CreateFormatCard(
            ExportFormatType type,
            string title,
            string tag,
            string description,
            Brush tagBrush,
            bool isProminent = false)
        {
            bool isSelected = _selectedFormat == type;

            var border = new Border
            {
                Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0x0E, 0x35))
                    : new SolidColorBrush(Color.FromRgb(0x16, 0x07, 0x22)),
                BorderBrush = isSelected
                    ? KerfBrand.MintBrush
                    : (isProminent ? KerfBrand.TealBrush : new SolidColorBrush(Color.FromRgb(0x2A, 0x12, 0x3E))),
                BorderThickness = new Thickness(isSelected ? 1.5 : 1.0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel();

            // Top row: Title + Tag badge
            var topRow = new DockPanel { LastChildFill = false };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = isSelected ? KerfBrand.MintBrush : Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(titleBlock, Dock.Left);
            topRow.Children.Add(titleBlock);

            var tagBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            tagBadge.Child = new TextBlock
            {
                Text = tag,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = tagBrush
            };
            DockPanel.SetDock(tagBadge, Dock.Right);
            topRow.Children.Add(tagBadge);
            stack.Children.Add(topRow);

            // Subtitle / description
            var descBlock = new TextBlock
            {
                Text = description,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x90, 0xAE)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(descBlock);

            border.Child = stack;

            border.MouseDown += (s, e) =>
            {
                SelectFormat(type);
            };

            return border;
        }

        private void SelectFormat(ExportFormatType type)
        {
            _selectedFormat = type;
            BuildFormatList();

            switch (_selectedFormat)
            {
                case ExportFormatType.KfxRuntime:
                    _actionButton.Content = "Export .kfx Package";
                    _optionsContainer.Content = CreateKfxOptionsPanel();
                    break;
                case ExportFormatType.Png:
                    _actionButton.Content = "Export PNG Image";
                    _optionsContainer.Content = CreatePngOptionsPanel();
                    break;
                case ExportFormatType.Jpeg:
                    _actionButton.Content = "Export JPEG Image";
                    _optionsContainer.Content = CreateJpegOptionsPanel();
                    break;
                case ExportFormatType.Svg:
                    _actionButton.Content = "Export SVG Vector";
                    _optionsContainer.Content = CreateSvgOptionsPanel();
                    break;
                case ExportFormatType.Pdf:
                    _actionButton.Content = "Export PDF Document";
                    _optionsContainer.Content = CreatePdfOptionsPanel();
                    break;
                case ExportFormatType.Mp4:
                    _actionButton.Content = "Render MP4 Video";
                    _optionsContainer.Content = CreateMp4OptionsPanel();
                    break;
            }
        }

        // =========================================================================
        // Options Panels
        // =========================================================================

        private FrameworkElement CreateKfxOptionsPanel()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Kerf Animation Package (.kfx) — Standalone Runtime",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush,
                Margin = new Thickness(0, 0, 0, 6)
            });

            stack.Children.Add(new TextBlock
            {
                Text = "Packages vector scene hierarchy, property animation tracks, and interactive state machine graphs into an optimized, platform-agnostic .kfx package. Can be loaded and evaluated in any Web, iOS, Android, or Desktop application without editor dependencies.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xAC, 0xC6)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18,
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Project statistics summary box
            var statsBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x0B, 0x2E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var statsStack = new StackPanel();
            string smName = (_document.Animation != null && _document.Animation.StateMachine != null) ? _document.Animation.StateMachine.Name : "Default StateMachine";
            int stateCount = (_document.Animation != null && _document.Animation.StateMachine != null && _document.Animation.StateMachine.Layers != null && _document.Animation.StateMachine.Layers.Count > 0 && _document.Animation.StateMachine.Layers[0].States != null)
                ? _document.Animation.StateMachine.Layers[0].States.Count : 0;
            int inputCount = (_document.Animation != null && _document.Animation.StateMachine != null && _document.Animation.StateMachine.Inputs != null)
                ? _document.Animation.StateMachine.Inputs.Count : 0;
            int listenerCount = (_document.Animation != null && _document.Animation.StateMachine != null && _document.Animation.StateMachine.Listeners != null)
                ? _document.Animation.StateMachine.Listeners.Count : 0;
            int clipCount = (_document.Animation != null && _document.Animation.Clips != null) ? _document.Animation.Clips.Count : 0;

            statsStack.Children.Add(CreateStatRow("Active State Machine:", string.Format("{0} ({1} states, {2} inputs, {3} listeners)", smName, stateCount, inputCount, listenerCount)));
            statsStack.Children.Add(CreateStatRow("Animation Clips:", string.Format("{0} clip(s) defined", clipCount)));
            statsStack.Children.Add(CreateStatRow("Format Specification:", "Native Kerf ZIP Container (FormatVersion 1.0.0, Schema 4.0.0)"));
            statsBox.Child = statsStack;
            stack.Children.Add(statsBox);

            // Package Configuration Checkboxes
            _kfxIncludeMetadataCheck = new CheckBox
            {
                Content = "Include source editor metadata (allows reopening & editing in Kerf Studio)",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(_kfxIncludeMetadataCheck);

            _kfxGenerateWebRunnerCheck = new CheckBox
            {
                Content = "Generate standalone HTML Web Player runner (double-click to test in any browser like Rive)",
                IsChecked = true,
                Foreground = KerfBrand.MintBrush,
                FontWeight = FontWeights.Medium,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10)
            };
            stack.Children.Add(_kfxGenerateWebRunnerCheck);

            return stack;
        }

        private FrameworkElement CreatePngOptionsPanel()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "PNG Image Export (Photoshop-Grade Rasterizer)",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 16)
            });

            stack.Children.Add(CreateFieldLabel("Resolution Scale:"));
            _pngScaleCombo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            _pngScaleCombo.Items.Add("1x (Standard 96 DPI)");
            _pngScaleCombo.Items.Add("2x (Retina @2x — Recommended)");
            _pngScaleCombo.Items.Add("3x (Ultra HD @3x)");
            _pngScaleCombo.Items.Add("4x (Print Quality @4x)");
            _pngScaleCombo.SelectedIndex = 1;
            stack.Children.Add(_pngScaleCombo);

            stack.Children.Add(CreateFieldLabel("Export Scope:"));
            _pngScopeCombo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            _pngScopeCombo.Items.Add("Entire Active Page");
            _pngScopeCombo.Items.Add("Selected Objects Only");
            _pngScopeCombo.SelectedIndex = (_studioView != null && _studioView.SelectedLayersCount > 0) ? 1 : 0;
            stack.Children.Add(_pngScopeCombo);

            _pngTransparentCheck = new CheckBox
            {
                Content = "Transparent background (omit canvas solid background)",
                IsChecked = false,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 10)
            };
            stack.Children.Add(_pngTransparentCheck);

            return stack;
        }

        private FrameworkElement CreateJpegOptionsPanel()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "JPEG Image Export",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 16)
            });

            stack.Children.Add(CreateFieldLabel("Quality Level:"));
            var qualityRow = new DockPanel { Width = 300, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            _jpegQualityText = new TextBlock { Text = "90%", FontSize = 12, Foreground = KerfBrand.MintBrush, Width = 45, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_jpegQualityText, Dock.Right);
            qualityRow.Children.Add(_jpegQualityText);

            _jpegQualitySlider = new Slider { Minimum = 10, Maximum = 100, Value = 90, TickFrequency = 5, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
            _jpegQualitySlider.ValueChanged += (s, e) =>
            {
                if (_jpegQualityText != null) _jpegQualityText.Text = ((int)_jpegQualitySlider.Value) + "%";
            };
            qualityRow.Children.Add(_jpegQualitySlider);
            stack.Children.Add(qualityRow);

            stack.Children.Add(CreateFieldLabel("Resolution Scale:"));
            _jpegScaleCombo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            _jpegScaleCombo.Items.Add("1x (Standard)");
            _jpegScaleCombo.Items.Add("2x (High Resolution @2x)");
            _jpegScaleCombo.Items.Add("3x (Ultra HD)");
            _jpegScaleCombo.SelectedIndex = 1;
            stack.Children.Add(_jpegScaleCombo);

            stack.Children.Add(CreateFieldLabel("Export Scope:"));
            _jpegScopeCombo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            _jpegScopeCombo.Items.Add("Entire Active Page");
            _jpegScopeCombo.Items.Add("Selected Objects Only");
            _jpegScopeCombo.SelectedIndex = (_studioView != null && _studioView.SelectedLayersCount > 0) ? 1 : 0;
            stack.Children.Add(_jpegScopeCombo);

            return stack;
        }

        private FrameworkElement CreateSvgOptionsPanel()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Vector SVG Export",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.AmberBrush,
                Margin = new Thickness(0, 0, 0, 16)
            });

            stack.Children.Add(CreateFieldLabel("Export Scope:"));
            _svgScopeCombo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            _svgScopeCombo.Items.Add("Entire Active Page");
            _svgScopeCombo.Items.Add("Selected Objects Only");
            _svgScopeCombo.SelectedIndex = (_studioView != null && _studioView.SelectedLayersCount > 0) ? 1 : 0;
            stack.Children.Add(_svgScopeCombo);

            _svgBackgroundCheck = new CheckBox
            {
                Content = "Include canvas background rect in SVG root",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 10)
            };
            stack.Children.Add(_svgBackgroundCheck);

            return stack;
        }

        private FrameworkElement CreatePdfOptionsPanel()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "PDF Document Export",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0x5D, 0x79)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            stack.Children.Add(CreateFieldLabel("Resolution & Print Quality:"));
            _pdfDpiCombo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            _pdfDpiCombo.Items.Add("150 DPI (Screen / Presentation)");
            _pdfDpiCombo.Items.Add("300 DPI (High-Resolution Print)");
            _pdfDpiCombo.SelectedIndex = 1;
            stack.Children.Add(_pdfDpiCombo);

            stack.Children.Add(CreateFieldLabel("Export Scope:"));
            _pdfScopeCombo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            _pdfScopeCombo.Items.Add("Entire Active Page");
            _pdfScopeCombo.Items.Add("Selected Objects Only");
            _pdfScopeCombo.SelectedIndex = (_studioView != null && _studioView.SelectedLayersCount > 0) ? 1 : 0;
            stack.Children.Add(_pdfScopeCombo);

            return stack;
        }

        private FrameworkElement CreateMp4OptionsPanel()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "MP4 Video Animation Render",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4D, 0x96, 0xFF)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            stack.Children.Add(CreateFieldLabel("Target Animation Clip:"));
            _mp4ClipCombo = new ComboBox { Width = 280, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            if (_document.Animation != null && _document.Animation.Clips != null && _document.Animation.Clips.Count > 0)
            {
                foreach (var c in _document.Animation.Clips)
                {
                    if (c != null) _mp4ClipCombo.Items.Add(string.Format("{0} ({1:F2}s)", c.Name, c.Duration));
                }
            }
            else
            {
                _mp4ClipCombo.Items.Add("Default Animation (2.00s)");
            }
            _mp4ClipCombo.SelectedIndex = 0;
            stack.Children.Add(_mp4ClipCombo);

            stack.Children.Add(CreateFieldLabel("Framerate:"));
            _mp4FpsCombo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 14) };
            _mp4FpsCombo.Items.Add("30 fps (Web / Smooth)");
            _mp4FpsCombo.Items.Add("60 fps (Full Motion / Native)");
            _mp4FpsCombo.SelectedIndex = 1;
            stack.Children.Add(_mp4FpsCombo);

            _mp4ProgressBar = new ProgressBar
            {
                Width = 280,
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 10, 0, 10),
                Visibility = Visibility.Collapsed
            };
            stack.Children.Add(_mp4ProgressBar);

            return stack;
        }

        // =========================================================================
        // Execution
        // =========================================================================

        private void ExecuteActiveExport()
        {
            var page = _document.GetActivePage();
            if (page == null)
            {
                MessageBox.Show("Active page not found in document.", "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string defaultName = !string.IsNullOrEmpty(_document.Name) ? _document.Name : "export";

            switch (_selectedFormat)
            {
                case ExportFormatType.KfxRuntime:
                    ExecuteKfxExport(defaultName);
                    break;
                case ExportFormatType.Png:
                    ExecutePngExport(page, defaultName);
                    break;
                case ExportFormatType.Jpeg:
                    ExecuteJpegExport(page, defaultName);
                    break;
                case ExportFormatType.Svg:
                    ExecuteSvgExport(page, defaultName);
                    break;
                case ExportFormatType.Pdf:
                    ExecutePdfExport(page, defaultName);
                    break;
                case ExportFormatType.Mp4:
                    ExecuteMp4Export(defaultName);
                    break;
            }
        }

        private void ExecuteKfxExport(string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Kerf Animation Package (*.kfx)|*.kfx",
                FileName = defaultName + ".kfx",
                Title = "Export Runtime Package (.kfx)"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    bool includeMetadata = _kfxIncludeMetadataCheck != null && _kfxIncludeMetadataCheck.IsChecked == true;
                    KfxPackage.ExportPackage(_document, dlg.FileName, includeMetadata);

                    // Generate accompanying standalone web runner if checked
                    if (_kfxGenerateWebRunnerCheck != null && _kfxGenerateWebRunnerCheck.IsChecked == true)
                    {
                        string runnerPath = IOPath.Combine(IOPath.GetDirectoryName(dlg.FileName), IOPath.GetFileNameWithoutExtension(dlg.FileName) + ".html");
                        WebPlayerGenerator.ExportHtmlPlayerToFile(_document, runnerPath, IOPath.GetFileName(dlg.FileName));
                    }

                    _statusTextBlock.Text = "Exported package successfully: " + IOPath.GetFileName(dlg.FileName);
                    MessageBox.Show(
                        "Kerf Animation Package (.kfx) exported successfully!\n\nLocation: " + dlg.FileName +
                        (_kfxGenerateWebRunnerCheck != null && _kfxGenerateWebRunnerCheck.IsChecked == true ? "\n\nStandalone HTML Web Runner created alongside package." : ""),
                        "Kerf Studio Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    _statusTextBlock.Text = "Export failed.";
                    MessageBox.Show("Export failed: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecutePngExport(Page page, string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                FileName = defaultName + ".png",
                Title = "Export PNG Image"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    double scale = 2.0;
                    if (_pngScaleCombo != null)
                    {
                        if (_pngScaleCombo.SelectedIndex == 0) scale = 1.0;
                        else if (_pngScaleCombo.SelectedIndex == 1) scale = 2.0;
                        else if (_pngScaleCombo.SelectedIndex == 2) scale = 3.0;
                        else if (_pngScaleCombo.SelectedIndex == 3) scale = 4.0;
                    }

                    IEnumerable<LayerNode> targets = ResolveExportTargets(_pngScopeCombo);
                    HighQualityImageRenderer.ExportToFile(page, targets, dlg.FileName, RasterFormat.Png, scale);

                    _statusTextBlock.Text = "PNG exported successfully.";
                    MessageBox.Show("Exported PNG successfully: " + dlg.FileName, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("PNG export failed: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteJpegExport(Page page, string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "JPEG Image (*.jpg)|*.jpg",
                FileName = defaultName + ".jpg",
                Title = "Export JPEG Image"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    double scale = 2.0;
                    if (_jpegScaleCombo != null)
                    {
                        if (_jpegScaleCombo.SelectedIndex == 0) scale = 1.0;
                        else if (_jpegScaleCombo.SelectedIndex == 1) scale = 2.0;
                        else if (_jpegScaleCombo.SelectedIndex == 2) scale = 3.0;
                    }

                    IEnumerable<LayerNode> targets = ResolveExportTargets(_jpegScopeCombo);
                    HighQualityImageRenderer.ExportToFile(page, targets, dlg.FileName, RasterFormat.Jpeg, scale);

                    _statusTextBlock.Text = "JPEG exported successfully.";
                    MessageBox.Show("Exported JPEG successfully: " + dlg.FileName, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("JPEG export failed: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteSvgExport(Page page, string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Vector SVG (*.svg)|*.svg",
                FileName = defaultName + ".svg",
                Title = "Export SVG Vector"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string svg = SvgExporter.ExportPage(page);
                    File.WriteAllText(dlg.FileName, svg, Encoding.UTF8);

                    _statusTextBlock.Text = "SVG exported successfully.";
                    MessageBox.Show("Exported SVG successfully: " + dlg.FileName, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("SVG export failed: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecutePdfExport(Page page, string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = defaultName + ".pdf",
                Title = "Export PDF Document"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    double scale = (_pdfDpiCombo != null && _pdfDpiCombo.SelectedIndex == 0) ? 1.5 : 3.125; // 150 DPI vs 300 DPI
                    IEnumerable<LayerNode> targets = ResolveExportTargets(_pdfScopeCombo);
                    PdfExporter.ExportToFile(page, targets, dlg.FileName, scale);

                    _statusTextBlock.Text = "PDF exported successfully.";
                    MessageBox.Show("Exported PDF successfully: " + dlg.FileName, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("PDF export failed: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteMp4Export(string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "MP4 Video (*.mp4)|*.mp4",
                FileName = defaultName + ".mp4",
                Title = "Render MP4 Video Animation"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    int fps = (_mp4FpsCombo != null && _mp4FpsCombo.SelectedIndex == 0) ? 30 : 60;
                    Clip targetClip = null;
                    if (_document.Animation != null && _document.Animation.Clips != null && _document.Animation.Clips.Count > 0)
                    {
                        int idx = (_mp4ClipCombo != null) ? _mp4ClipCombo.SelectedIndex : 0;
                        if (idx >= 0 && idx < _document.Animation.Clips.Count) targetClip = _document.Animation.Clips[idx];
                        else targetClip = _document.Animation.Clips[0];
                    }
                    if (targetClip == null) targetClip = new Clip(100, "Default Animation", 2.0, fps, LoopMode.OneShot);

                    if (_mp4ProgressBar != null)
                    {
                        _mp4ProgressBar.Visibility = Visibility.Visible;
                        _mp4ProgressBar.Value = 0;
                    }

                    Mp4Exporter.ExportClipToVideo(_document, targetClip, dlg.FileName, fps, 1.0, progress =>
                    {
                        if (_mp4ProgressBar != null) _mp4ProgressBar.Value = progress * 100;
                    });

                    _statusTextBlock.Text = "MP4 video rendered successfully.";
                    MessageBox.Show("MP4 Video rendered successfully: " + dlg.FileName, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("MP4 render failed: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    if (_mp4ProgressBar != null) _mp4ProgressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private IEnumerable<LayerNode> ResolveExportTargets(ComboBox scopeCombo)
        {
            if (scopeCombo != null && scopeCombo.SelectedIndex == 1 && _studioView != null && _studioView.SelectedLayersCount > 0)
            {
                return _studioView.SelectedLayers;
            }
            return null;
        }

        private FrameworkElement CreateFieldLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private FrameworkElement CreateStatRow(string label, string val)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x90, 0xAE)), Width = 140 };
            var v = new TextBlock { Text = val, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            DockPanel.SetDock(lbl, Dock.Left);
            dock.Children.Add(lbl);
            dock.Children.Add(v);
            return dock;
        }
    }
}
