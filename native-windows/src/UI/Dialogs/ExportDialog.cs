using System;
using System.Collections.Generic;
using System.Globalization;
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
        Mp4 = 5,
        Gif = 6
    }

    /// <summary>
    /// Dedicated Kerf Export Window.
    /// Provides a polished UI format selector for .kfx runtime packages, PNG, JPEG, SVG, PDF, MP4, and animated GIF.
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

        // Option controls - KFX
        private CheckBox _kfxIncludeMetadataCheck;
        private CheckBox _kfxGenerateWebRunnerCheck;

        // Option controls - PNG
        private ComboBox _pngScaleCombo;
        private ComboBox _pngScopeCombo;
        private CheckBox _pngTransparentCheck;

        // Option controls - JPEG
        private Slider _jpegQualitySlider;
        private TextBlock _jpegQualityText;
        private ComboBox _jpegScaleCombo;
        private ComboBox _jpegScopeCombo;

        // Option controls - SVG
        private ComboBox _svgScopeCombo;
        private CheckBox _svgBackgroundCheck;

        // Option controls - PDF
        private ComboBox _pdfDpiCombo;
        private ComboBox _pdfScopeCombo;

        // Option controls - MP4 (After Effects style)
        private ComboBox _mp4ClipCombo;
        private ComboBox _mp4FpsPresetCombo;
        private TextBox _mp4CustomFpsBox;
        private FrameworkElement _mp4CustomFpsRow;
        private ComboBox _mp4BitrateModeCombo;
        private ComboBox _mp4TargetBitrateCombo;
        private TextBox _mp4TargetBitrateBox;
        private FrameworkElement _mp4CustomBitrateRow;
        private TextBox _mp4MaxBitrateBox;
        private ComboBox _mp4ProfileCombo;
        private ComboBox _mp4KeyframeIntervalCombo;
        private ComboBox _mp4ScaleCombo;
        private ComboBox _mp4TimeRangeCombo;
        private TextBox _mp4InPointBox;
        private TextBox _mp4OutPointBox;
        private FrameworkElement _mp4WorkAreaRow;
        private ComboBox _mp4BackgroundCombo;
        private ProgressBar _mp4ProgressBar;

        // Option controls - Animated GIF
        private ComboBox _gifClipCombo;
        private ComboBox _gifFpsCombo;
        private TextBox _gifCustomFpsBox;
        private FrameworkElement _gifCustomFpsRow;
        private ComboBox _gifLoopCombo;
        private ComboBox _gifColorDepthCombo;
        private CheckBox _gifDitherCheck;
        private CheckBox _gifTransparentCheck;
        private ComboBox _gifScaleCombo;
        private ComboBox _gifTimeRangeCombo;
        private TextBox _gifInPointBox;
        private TextBox _gifOutPointBox;
        private FrameworkElement _gifWorkAreaRow;
        private ProgressBar _gifProgressBar;

        public ExportDialog(KerfDocument doc, StudioView studioView = null)
        {
            _document = doc ?? new KerfDocument();
            _studioView = studioView;

            Title = "Export Document & Animations — Kerf";
            Width = 840;
            Height = 620;
            MinWidth = 780;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x07, 0x20)); // Obsidian #140720
            Foreground = Brushes.White;
            FontFamily = new FontFamily("-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif");
            Resources[typeof(TextBox)] = KerfBrand.CreateDarkTextBoxStyle(6);
            Resources[typeof(ComboBox)] = KerfBrand.CreateDarkComboBoxStyle(6);

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

            // Right: Dynamic options panel (with smooth scroll)
            var rightBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x08, 0x24))
            };
            var rightScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(24, 20, 24, 20)
            };
            _optionsContainer = new ContentControl();
            rightScroll.Content = _optionsContainer;
            rightBorder.Child = rightScroll;
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
                Text = "Export production vector assets, video renders, animated GIFs, or standalone .kfx runtime packages",
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
            _actionButton.Width = 190;
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
                "After Effects-grade H.264 video with custom FPS, bitrate, and GOP controls.",
                new SolidColorBrush(Color.FromRgb(0x4D, 0x96, 0xFF))));

            _formatListPanel.Children.Add(CreateFormatCard(
                ExportFormatType.Gif,
                "Animated GIF (.gif)",
                "ANIMATION",
                "Looping animated GIF with custom FPS, palette quantization, and Floyd-Steinberg dithering.",
                new SolidColorBrush(Color.FromRgb(0xFF, 0x5D, 0xA2))));
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

            // Top row: Title and Badge
            var topRow = new DockPanel { LastChildFill = false };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = isSelected ? KerfBrand.MintBrush : Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(titleBlock, Dock.Left);
            topRow.Children.Add(titleBlock);

            var badge = new Border
            {
                Background = tagBrush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            var badgeText = new TextBlock
            {
                Text = tag,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47))
            };
            badge.Child = badgeText;
            DockPanel.SetDock(badge, Dock.Right);
            topRow.Children.Add(badge);

            stack.Children.Add(topRow);

            // Description
            var descBlock = new TextBlock
            {
                Text = description,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                LineHeight = 15
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

            switch (type)
            {
                case ExportFormatType.KfxRuntime:
                    _optionsContainer.Content = CreateKfxOptionsPanel();
                    _actionButton.Content = "Export .kfx Package";
                    break;
                case ExportFormatType.Png:
                    _optionsContainer.Content = CreatePngOptionsPanel();
                    _actionButton.Content = "Export PNG Image";
                    break;
                case ExportFormatType.Jpeg:
                    _optionsContainer.Content = CreateJpegOptionsPanel();
                    _actionButton.Content = "Export JPEG Image";
                    break;
                case ExportFormatType.Svg:
                    _optionsContainer.Content = CreateSvgOptionsPanel();
                    _actionButton.Content = "Export SVG Vector";
                    break;
                case ExportFormatType.Pdf:
                    _optionsContainer.Content = CreatePdfOptionsPanel();
                    _actionButton.Content = "Export PDF Document";
                    break;
                case ExportFormatType.Mp4:
                    _optionsContainer.Content = CreateMp4OptionsPanel();
                    _actionButton.Content = "Render MP4 Video";
                    break;
                case ExportFormatType.Gif:
                    _optionsContainer.Content = CreateGifOptionsPanel();
                    _actionButton.Content = "Export Animated GIF";
                    break;
            }
        }

        // =========================================================================
        // Format-specific Options Panels
        // =========================================================================

        private FrameworkElement CreateKfxOptionsPanel()
        {
            var stack = new StackPanel();

            var title = new TextBlock
            {
                Text = "Kerf Animation Package (.kfx)",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush,
                Margin = new Thickness(0, 0, 0, 6)
            };
            stack.Children.Add(title);

            var subtitle = new TextBlock
            {
                Text = "Zero-dependency ZIP runtime package containing optimized vector scene hierarchy, animation keyframe tracks, and interactive state machines. Compatible with web, mobile, and native desktop runtimes just like Rive.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18,
                Margin = new Thickness(0, 0, 0, 20)
            };
            stack.Children.Add(subtitle);

            // Settings
            stack.Children.Add(CreateFieldLabel("Package Settings:"));

            _kfxIncludeMetadataCheck = new CheckBox
            {
                Content = "Include Kerf editor metadata (preserves UI guides, grids, and editor state)",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 6, 0, 10)
            };
            stack.Children.Add(_kfxIncludeMetadataCheck);

            _kfxGenerateWebRunnerCheck = new CheckBox
            {
                Content = "Generate Standalone HTML5 Web Runner (.html) alongside package for immediate preview",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 20)
            };
            stack.Children.Add(_kfxGenerateWebRunnerCheck);

            // Statistics Card
            var statsBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x09, 0x2A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var statsStack = new StackPanel();
            statsStack.Children.Add(new TextBlock
            {
                Text = "ANIMATION PACKAGE CONTENTS",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush,
                Margin = new Thickness(0, 0, 0, 8)
            });

            int clipCount = (_document.Animation != null && _document.Animation.Clips != null) ? _document.Animation.Clips.Count : 0;
            int trackCount = (_document.Animation != null && _document.Animation.Clips != null) ? _document.Animation.Clips.Sum(c => (c.Tracks != null) ? c.Tracks.Count : 0) : 0;
            int kfCount = (_document.Animation != null && _document.Animation.Clips != null) ? _document.Animation.Clips.Sum(c => (c.Tracks != null) ? c.Tracks.Sum(t => (t.Keys != null) ? t.Keys.Count : 0) : 0) : 0;
            bool hasSm = (_document.Animation != null && _document.Animation.StateMachine != null);
            int inputCount = (hasSm && _document.Animation.StateMachine.Inputs != null) ? _document.Animation.StateMachine.Inputs.Count : 0;
            int listenerCount = (hasSm && _document.Animation.StateMachine.Listeners != null) ? _document.Animation.StateMachine.Listeners.Count : 0;

            statsStack.Children.Add(CreateStatRow("Animation Clips:", clipCount.ToString()));
            statsStack.Children.Add(CreateStatRow("Property Tracks:", trackCount.ToString()));
            statsStack.Children.Add(CreateStatRow("Total Keyframes:", kfCount.ToString()));
            statsStack.Children.Add(CreateStatRow("State Machine:", hasSm ? "Active (" + _document.Animation.StateMachine.Name + ")" : "None"));
            statsStack.Children.Add(CreateStatRow("Inputs & Listeners:", string.Format("{0} inputs, {1} listeners", inputCount, listenerCount)));
            statsBox.Child = statsStack;
            stack.Children.Add(statsBox);

            return stack;
        }

        private FrameworkElement CreatePngOptionsPanel()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "PNG Image Export",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x71, 0x7B)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            stack.Children.Add(CreateFieldLabel("Resolution Scale:"));
            _pngScaleCombo = CreateStyledComboBox(260, new Thickness(0, 4, 0, 14));
            _pngScaleCombo.Items.Add("1x (Standard Resolution)");
            _pngScaleCombo.Items.Add("2x (High DPI / Retina)");
            _pngScaleCombo.Items.Add("3x (Ultra Mobile)");
            _pngScaleCombo.Items.Add("4x (Print / Ultra HD)");
            _pngScaleCombo.SelectedIndex = 1; // 2x default
            stack.Children.Add(_pngScaleCombo);

            stack.Children.Add(CreateFieldLabel("Export Scope:"));
            _pngScopeCombo = CreateStyledComboBox(260, new Thickness(0, 4, 0, 14));
            _pngScopeCombo.Items.Add("Entire Active Page");
            _pngScopeCombo.Items.Add("Selected Objects Only");
            _pngScopeCombo.SelectedIndex = (_studioView != null && _studioView.SelectedLayersCount > 0) ? 1 : 0;
            stack.Children.Add(_pngScopeCombo);

            _pngTransparentCheck = new CheckBox
            {
                Content = "Transparent background (uncheck to render canvas background fill)",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 6, 0, 10)
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
                Foreground = new SolidColorBrush(Color.FromRgb(0x7A, 0x82, 0x99)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            stack.Children.Add(CreateFieldLabel("Compression Quality:"));
            var qualityRow = new DockPanel { Margin = new Thickness(0, 4, 0, 14), LastChildFill = false };
            _jpegQualitySlider = new Slider
            {
                Minimum = 10,
                Maximum = 100,
                Value = 90,
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center
            };
            _jpegQualityText = new TextBlock
            {
                Text = "90%",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _jpegQualitySlider.ValueChanged += (s, e) =>
            {
                if (_jpegQualityText != null) _jpegQualityText.Text = ((int)_jpegQualitySlider.Value) + "%";
            };
            qualityRow.Children.Add(_jpegQualitySlider);
            qualityRow.Children.Add(_jpegQualityText);
            stack.Children.Add(qualityRow);

            stack.Children.Add(CreateFieldLabel("Resolution Scale:"));
            _jpegScaleCombo = CreateStyledComboBox(260, new Thickness(0, 4, 0, 14));
            _jpegScaleCombo.Items.Add("1x (Standard Resolution)");
            _jpegScaleCombo.Items.Add("2x (High DPI / Retina)");
            _jpegScaleCombo.Items.Add("3x (Ultra Mobile)");
            _jpegScaleCombo.SelectedIndex = 1;
            stack.Children.Add(_jpegScaleCombo);

            stack.Children.Add(CreateFieldLabel("Export Scope:"));
            _jpegScopeCombo = CreateStyledComboBox(260, new Thickness(0, 4, 0, 14));
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
            _svgScopeCombo = CreateStyledComboBox(260, new Thickness(0, 4, 0, 14));
            _svgScopeCombo.Items.Add("Entire Active Page");
            _svgScopeCombo.Items.Add("Selected Objects Only");
            _svgScopeCombo.SelectedIndex = (_studioView != null && _studioView.SelectedLayersCount > 0) ? 1 : 0;
            stack.Children.Add(_svgScopeCombo);

            _svgBackgroundCheck = new CheckBox
            {
                Content = "Include page background color as root rect element",
                IsChecked = false,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 6, 0, 10)
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
            _pdfDpiCombo = CreateStyledComboBox(260, new Thickness(0, 4, 0, 14));
            _pdfDpiCombo.Items.Add("150 DPI (Screen / Presentation)");
            _pdfDpiCombo.Items.Add("300 DPI (High-Resolution Print)");
            _pdfDpiCombo.SelectedIndex = 1;
            stack.Children.Add(_pdfDpiCombo);

            stack.Children.Add(CreateFieldLabel("Export Scope:"));
            _pdfScopeCombo = CreateStyledComboBox(260, new Thickness(0, 4, 0, 14));
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
                Text = "MP4 Video Animation Render (After Effects Grade)",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4D, 0x96, 0xFF)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Target Clip
            stack.Children.Add(CreateFieldLabel("Target Animation Clip:"));
            _mp4ClipCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 12));
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

            // Framerate (FPS) with Custom FPS option
            stack.Children.Add(CreateFieldLabel("Frame Rate (FPS):"));
            _mp4FpsPresetCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 4));
            _mp4FpsPresetCombo.Items.Add("12 fps (Stop Motion)");
            _mp4FpsPresetCombo.Items.Add("15 fps (Stylized)");
            _mp4FpsPresetCombo.Items.Add("23.976 fps (Cinematic Film)");
            _mp4FpsPresetCombo.Items.Add("24 fps (Standard Film)");
            _mp4FpsPresetCombo.Items.Add("25 fps (PAL Video)");
            _mp4FpsPresetCombo.Items.Add("29.97 fps (NTSC Video)");
            _mp4FpsPresetCombo.Items.Add("30 fps (Web / Smooth)");
            _mp4FpsPresetCombo.Items.Add("50 fps (High Frame Rate PAL)");
            _mp4FpsPresetCombo.Items.Add("59.94 fps (NTSC High Frame Rate)");
            _mp4FpsPresetCombo.Items.Add("60 fps (Full Motion 60fps - Recommended)");
            _mp4FpsPresetCombo.Items.Add("120 fps (Ultra Smooth / Gaming)");
            _mp4FpsPresetCombo.Items.Add("Custom FPS...");
            _mp4FpsPresetCombo.SelectedIndex = 9; // 60 fps default
            stack.Children.Add(_mp4FpsPresetCombo);

            var customFpsDock = new DockPanel { Margin = new Thickness(0, 4, 0, 12), LastChildFill = false };
            customFpsDock.Children.Add(new TextBlock { Text = "Custom FPS Value: ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)), VerticalAlignment = VerticalAlignment.Center });
            _mp4CustomFpsBox = CreateDarkTextBox("60.0", 80);
            customFpsDock.Children.Add(_mp4CustomFpsBox);
            _mp4CustomFpsRow = customFpsDock;
            _mp4CustomFpsRow.Visibility = Visibility.Collapsed;
            _mp4FpsPresetCombo.SelectionChanged += (s, e) =>
            {
                if (_mp4CustomFpsRow != null)
                {
                    _mp4CustomFpsRow.Visibility = (_mp4FpsPresetCombo.SelectedIndex == 11) ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            stack.Children.Add(_mp4CustomFpsRow);

            // Bitrate & Encoding Mode (After Effects / Media Encoder style)
            stack.Children.Add(CreateFieldLabel("Bitrate Encoding Mode:"));
            _mp4BitrateModeCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 12));
            _mp4BitrateModeCombo.Items.Add("VBR, 1 Pass (Variable Bit Rate)");
            _mp4BitrateModeCombo.Items.Add("CBR (Constant Bit Rate)");
            _mp4BitrateModeCombo.Items.Add("VBR, 2 Pass (High Quality)");
            _mp4BitrateModeCombo.SelectedIndex = 0;
            stack.Children.Add(_mp4BitrateModeCombo);

            stack.Children.Add(CreateFieldLabel("Target Bitrate (Quality):"));
            _mp4TargetBitrateCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 4));
            _mp4TargetBitrateCombo.Items.Add("Draft / Low (2 Mbps)");
            _mp4TargetBitrateCombo.Items.Add("Medium Web (6 Mbps)");
            _mp4TargetBitrateCombo.Items.Add("High Definition (12 Mbps - Recommended)");
            _mp4TargetBitrateCombo.Items.Add("1080p Studio (18 Mbps)");
            _mp4TargetBitrateCombo.Items.Add("4K UHD / Master (28 Mbps)");
            _mp4TargetBitrateCombo.Items.Add("Production Master (50 Mbps)");
            _mp4TargetBitrateCombo.Items.Add("Custom Bitrate...");
            _mp4TargetBitrateCombo.SelectedIndex = 2; // 12 Mbps
            stack.Children.Add(_mp4TargetBitrateCombo);

            var customBitrateDock = new DockPanel { Margin = new Thickness(0, 4, 0, 8), LastChildFill = false };
            customBitrateDock.Children.Add(new TextBlock { Text = "Target (Mbps): ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)), VerticalAlignment = VerticalAlignment.Center });
            _mp4TargetBitrateBox = CreateDarkTextBox("12.0", 70);
            customBitrateDock.Children.Add(_mp4TargetBitrateBox);
            customBitrateDock.Children.Add(new TextBlock { Text = "  Max (Mbps): ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)), VerticalAlignment = VerticalAlignment.Center });
            _mp4MaxBitrateBox = CreateDarkTextBox("18.0", 70);
            customBitrateDock.Children.Add(_mp4MaxBitrateBox);
            _mp4CustomBitrateRow = customBitrateDock;
            _mp4CustomBitrateRow.Visibility = Visibility.Collapsed;
            _mp4TargetBitrateCombo.SelectionChanged += (s, e) =>
            {
                if (_mp4CustomBitrateRow != null)
                {
                    _mp4CustomBitrateRow.Visibility = (_mp4TargetBitrateCombo.SelectedIndex == 6) ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            stack.Children.Add(_mp4CustomBitrateRow);

            // H.264 Profile & GOP
            stack.Children.Add(CreateFieldLabel("H.264 Profile:"));
            _mp4ProfileCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 12));
            _mp4ProfileCombo.Items.Add("High Profile (CABAC, 8x8 Transform - Recommended)");
            _mp4ProfileCombo.Items.Add("Main Profile (Broadcast & Web standard)");
            _mp4ProfileCombo.Items.Add("Baseline Profile (Legacy & Mobile)");
            _mp4ProfileCombo.SelectedIndex = 0;
            stack.Children.Add(_mp4ProfileCombo);

            stack.Children.Add(CreateFieldLabel("Keyframe Distance (GOP Interval):"));
            _mp4KeyframeIntervalCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 12));
            _mp4KeyframeIntervalCombo.Items.Add("Automatic (Every 1 second / 1x FPS)");
            _mp4KeyframeIntervalCombo.Items.Add("Every 2 seconds (Web streaming)");
            _mp4KeyframeIntervalCombo.Items.Add("Every 0.5 seconds (Fast seeking)");
            _mp4KeyframeIntervalCombo.Items.Add("All-Intra (Every frame keyframe / Highest Edit Quality)");
            _mp4KeyframeIntervalCombo.SelectedIndex = 0;
            stack.Children.Add(_mp4KeyframeIntervalCombo);

            // Output Scale & Resolution
            stack.Children.Add(CreateFieldLabel("Resolution Scale:"));
            _mp4ScaleCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 12));
            _mp4ScaleCombo.Items.Add("1.0x (100% Canvas Resolution)");
            _mp4ScaleCombo.Items.Add("0.5x (Half Resolution)");
            _mp4ScaleCombo.Items.Add("1.5x (1.5x Scaling)");
            _mp4ScaleCombo.Items.Add("2.0x (2x Retina / 4K UHD)");
            _mp4ScaleCombo.Items.Add("4.0x (Ultra 4K/8K)");
            _mp4ScaleCombo.SelectedIndex = 0;
            stack.Children.Add(_mp4ScaleCombo);

            // Time Range / Work Area
            stack.Children.Add(CreateFieldLabel("Time Range:"));
            _mp4TimeRangeCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 4));
            _mp4TimeRangeCombo.Items.Add("Entire Clip Duration");
            _mp4TimeRangeCombo.Items.Add("Work Area / Custom Range");
            _mp4TimeRangeCombo.SelectedIndex = 0;
            stack.Children.Add(_mp4TimeRangeCombo);

            var workAreaDock = new DockPanel { Margin = new Thickness(0, 4, 0, 12), LastChildFill = false };
            workAreaDock.Children.Add(new TextBlock { Text = "In Point (s): ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)), VerticalAlignment = VerticalAlignment.Center });
            _mp4InPointBox = CreateDarkTextBox("0.00", 65);
            workAreaDock.Children.Add(_mp4InPointBox);
            workAreaDock.Children.Add(new TextBlock { Text = "  Out Point (s): ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)), VerticalAlignment = VerticalAlignment.Center });
            double clipDur = (_document.Animation != null && _document.Animation.Clips != null && _document.Animation.Clips.Count > 0) ? _document.Animation.Clips[0].Duration : 2.0;
            _mp4OutPointBox = CreateDarkTextBox(clipDur.ToString("F2", CultureInfo.InvariantCulture), 65);
            workAreaDock.Children.Add(_mp4OutPointBox);
            _mp4WorkAreaRow = workAreaDock;
            _mp4WorkAreaRow.Visibility = Visibility.Collapsed;
            _mp4TimeRangeCombo.SelectionChanged += (s, e) =>
            {
                if (_mp4WorkAreaRow != null)
                {
                    _mp4WorkAreaRow.Visibility = (_mp4TimeRangeCombo.SelectedIndex == 1) ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            stack.Children.Add(_mp4WorkAreaRow);

            // Background Color
            stack.Children.Add(CreateFieldLabel("Background Color:"));
            _mp4BackgroundCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 14));
            _mp4BackgroundCombo.Items.Add("Canvas Background Color");
            _mp4BackgroundCombo.Items.Add("Solid Black (#000000)");
            _mp4BackgroundCombo.Items.Add("Solid White (#FFFFFF)");
            _mp4BackgroundCombo.SelectedIndex = 0;
            stack.Children.Add(_mp4BackgroundCombo);

            _mp4ProgressBar = new ProgressBar
            {
                Width = 300,
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 10),
                Visibility = Visibility.Collapsed
            };
            stack.Children.Add(_mp4ProgressBar);

            return stack;
        }

        private FrameworkElement CreateGifOptionsPanel()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Animated GIF Export",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x5D, 0xA2)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Target Clip
            stack.Children.Add(CreateFieldLabel("Target Animation Clip:"));
            _gifClipCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 12));
            if (_document.Animation != null && _document.Animation.Clips != null && _document.Animation.Clips.Count > 0)
            {
                foreach (var c in _document.Animation.Clips)
                {
                    if (c != null) _gifClipCombo.Items.Add(string.Format("{0} ({1:F2}s)", c.Name, c.Duration));
                }
            }
            else
            {
                _gifClipCombo.Items.Add("Default Animation (2.00s)");
            }
            _gifClipCombo.SelectedIndex = 0;
            stack.Children.Add(_gifClipCombo);

            // Framerate (FPS)
            stack.Children.Add(CreateFieldLabel("Frame Rate (FPS):"));
            _gifFpsCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 4));
            _gifFpsCombo.Items.Add("10 fps (Compact File Size)");
            _gifFpsCombo.Items.Add("12 fps (Standard Animation)");
            _gifFpsCombo.Items.Add("15 fps (Smooth Web GIF - Recommended)");
            _gifFpsCombo.Items.Add("20 fps (Very Smooth)");
            _gifFpsCombo.Items.Add("24 fps (Cinema Frame Rate)");
            _gifFpsCombo.Items.Add("30 fps (High Smoothness)");
            _gifFpsCombo.Items.Add("Custom FPS...");
            _gifFpsCombo.SelectedIndex = 2; // 15 fps default
            stack.Children.Add(_gifFpsCombo);

            var customGifFpsDock = new DockPanel { Margin = new Thickness(0, 4, 0, 12), LastChildFill = false };
            customGifFpsDock.Children.Add(new TextBlock { Text = "Custom FPS: ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)), VerticalAlignment = VerticalAlignment.Center });
            _gifCustomFpsBox = CreateDarkTextBox("15.0", 80);
            customGifFpsDock.Children.Add(_gifCustomFpsBox);
            _gifCustomFpsRow = customGifFpsDock;
            _gifCustomFpsRow.Visibility = Visibility.Collapsed;
            _gifFpsCombo.SelectionChanged += (s, e) =>
            {
                if (_gifCustomFpsRow != null)
                {
                    _gifCustomFpsRow.Visibility = (_gifFpsCombo.SelectedIndex == 6) ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            stack.Children.Add(_gifCustomFpsRow);

            // Looping Mode
            stack.Children.Add(CreateFieldLabel("Looping Count:"));
            _gifLoopCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 12));
            _gifLoopCombo.Items.Add("Loop Forever (Infinite)");
            _gifLoopCombo.Items.Add("Play Once (No Loop)");
            _gifLoopCombo.Items.Add("Loop 2 Times");
            _gifLoopCombo.Items.Add("Loop 3 Times");
            _gifLoopCombo.Items.Add("Loop 5 Times");
            _gifLoopCombo.SelectedIndex = 0;
            stack.Children.Add(_gifLoopCombo);

            // Color Palette Depth
            stack.Children.Add(CreateFieldLabel("Color Palette Quality:"));
            _gifColorDepthCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 12));
            _gifColorDepthCombo.Items.Add("256 Colors (Maximum Quality)");
            _gifColorDepthCombo.Items.Add("128 Colors (Balanced Web)");
            _gifColorDepthCombo.Items.Add("64 Colors (Small File Size)");
            _gifColorDepthCombo.Items.Add("32 Colors (Retro / Micro)");
            _gifColorDepthCombo.SelectedIndex = 0;
            stack.Children.Add(_gifColorDepthCombo);

            _gifDitherCheck = new CheckBox
            {
                Content = "Apply Floyd-Steinberg error diffusion dithering (smooth gradients)",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(_gifDitherCheck);

            _gifTransparentCheck = new CheckBox
            {
                Content = "Preserve alpha transparency (cutout background)",
                IsChecked = true,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(_gifTransparentCheck);

            // Resolution Scale
            stack.Children.Add(CreateFieldLabel("Resolution Scale:"));
            _gifScaleCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 12));
            _gifScaleCombo.Items.Add("1.0x (Original Resolution)");
            _gifScaleCombo.Items.Add("0.5x (Half Size - Recommended for Web)");
            _gifScaleCombo.Items.Add("0.25x (Quarter Size / Thumbnail)");
            _gifScaleCombo.Items.Add("1.5x");
            _gifScaleCombo.Items.Add("2.0x");
            _gifScaleCombo.SelectedIndex = 1; // 0.5x default for GIF
            stack.Children.Add(_gifScaleCombo);

            // Time Range
            stack.Children.Add(CreateFieldLabel("Time Range:"));
            _gifTimeRangeCombo = CreateStyledComboBox(300, new Thickness(0, 4, 0, 4));
            _gifTimeRangeCombo.Items.Add("Entire Clip Duration");
            _gifTimeRangeCombo.Items.Add("Work Area / Custom Range");
            _gifTimeRangeCombo.SelectedIndex = 0;
            stack.Children.Add(_gifTimeRangeCombo);

            var gifWorkAreaDock = new DockPanel { Margin = new Thickness(0, 4, 0, 12), LastChildFill = false };
            gifWorkAreaDock.Children.Add(new TextBlock { Text = "In (s): ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)), VerticalAlignment = VerticalAlignment.Center });
            _gifInPointBox = CreateDarkTextBox("0.00", 65);
            gifWorkAreaDock.Children.Add(_gifInPointBox);
            gifWorkAreaDock.Children.Add(new TextBlock { Text = "  Out (s): ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)), VerticalAlignment = VerticalAlignment.Center });
            double gClipDur = (_document.Animation != null && _document.Animation.Clips != null && _document.Animation.Clips.Count > 0) ? _document.Animation.Clips[0].Duration : 2.0;
            _gifOutPointBox = CreateDarkTextBox(gClipDur.ToString("F2", CultureInfo.InvariantCulture), 65);
            gifWorkAreaDock.Children.Add(_gifOutPointBox);
            _gifWorkAreaRow = gifWorkAreaDock;
            _gifWorkAreaRow.Visibility = Visibility.Collapsed;
            _gifTimeRangeCombo.SelectionChanged += (s, e) =>
            {
                if (_gifWorkAreaRow != null)
                {
                    _gifWorkAreaRow.Visibility = (_gifTimeRangeCombo.SelectedIndex == 1) ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            stack.Children.Add(_gifWorkAreaRow);

            _gifProgressBar = new ProgressBar
            {
                Width = 300,
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 10),
                Visibility = Visibility.Collapsed
            };
            stack.Children.Add(_gifProgressBar);

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
                MessageBox.Show("Active page not found in document.", "Kerf", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                case ExportFormatType.Gif:
                    ExecuteGifExport(defaultName);
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
                        "Kerf Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    _statusTextBlock.Text = "Export failed.";
                    MessageBox.Show("Export failed: " + ex.Message, "Kerf", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        switch (_pngScaleCombo.SelectedIndex)
                        {
                            case 0: scale = 1.0; break;
                            case 1: scale = 2.0; break;
                            case 2: scale = 3.0; break;
                            case 3: scale = 4.0; break;
                        }
                    }

                    bool transparent = _pngTransparentCheck == null || _pngTransparentCheck.IsChecked == true;
                    IEnumerable<LayerNode> targets = ResolveExportTargets(_pngScopeCombo);
                    HighQualityImageRenderer.ExportToFile(page, targets, dlg.FileName, RasterFormat.Png, scale);

                    _statusTextBlock.Text = "PNG exported successfully.";
                    MessageBox.Show("Exported PNG successfully: " + dlg.FileName, "Kerf", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("PNG export failed: " + ex.Message, "Kerf", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteJpegExport(Page page, string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "JPEG Image (*.jpg)|*.jpg;*.jpeg",
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
                        switch (_jpegScaleCombo.SelectedIndex)
                        {
                            case 0: scale = 1.0; break;
                            case 1: scale = 2.0; break;
                            case 2: scale = 3.0; break;
                        }
                    }

                    IEnumerable<LayerNode> targets = ResolveExportTargets(_jpegScopeCombo);
                    HighQualityImageRenderer.ExportToFile(page, targets, dlg.FileName, RasterFormat.Jpeg, scale);

                    _statusTextBlock.Text = "JPEG exported successfully.";
                    MessageBox.Show("Exported JPEG successfully: " + dlg.FileName, "Kerf", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("JPEG export failed: " + ex.Message, "Kerf", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteSvgExport(Page page, string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Scalable Vector Graphics (*.svg)|*.svg",
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
                    MessageBox.Show("Exported SVG successfully: " + dlg.FileName, "Kerf", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("SVG export failed: " + ex.Message, "Kerf", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Exported PDF successfully: " + dlg.FileName, "Kerf", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("PDF export failed: " + ex.Message, "Kerf", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    Clip targetClip = null;
                    if (_document.Animation != null && _document.Animation.Clips != null && _document.Animation.Clips.Count > 0)
                    {
                        int idx = (_mp4ClipCombo != null) ? _mp4ClipCombo.SelectedIndex : 0;
                        if (idx >= 0 && idx < _document.Animation.Clips.Count) targetClip = _document.Animation.Clips[idx];
                        else targetClip = _document.Animation.Clips[0];
                    }
                    if (targetClip == null) targetClip = new Clip(100, "Default Animation", 2.0, 30.0, LoopMode.OneShot);

                    // Resolve FPS
                    double fps = 60.0;
                    if (_mp4FpsPresetCombo != null)
                    {
                        switch (_mp4FpsPresetCombo.SelectedIndex)
                        {
                            case 0: fps = 12.0; break;
                            case 1: fps = 15.0; break;
                            case 2: fps = 23.976; break;
                            case 3: fps = 24.0; break;
                            case 4: fps = 25.0; break;
                            case 5: fps = 29.97; break;
                            case 6: fps = 30.0; break;
                            case 7: fps = 50.0; break;
                            case 8: fps = 59.94; break;
                            case 9: fps = 60.0; break;
                            case 10: fps = 120.0; break;
                            case 11:
                                if (_mp4CustomFpsBox != null)
                                {
                                    double val;
                                    if (double.TryParse(_mp4CustomFpsBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                                        fps = Math.Max(1.0, Math.Min(240.0, val));
                                }
                                break;
                        }
                    }

                    // Resolve Bitrate
                    double targetBitrate = 12.0;
                    if (_mp4TargetBitrateCombo != null)
                    {
                        switch (_mp4TargetBitrateCombo.SelectedIndex)
                        {
                            case 0: targetBitrate = 2.0; break;
                            case 1: targetBitrate = 6.0; break;
                            case 2: targetBitrate = 12.0; break;
                            case 3: targetBitrate = 18.0; break;
                            case 4: targetBitrate = 28.0; break;
                            case 5: targetBitrate = 50.0; break;
                            case 6:
                                if (_mp4TargetBitrateBox != null)
                                {
                                    double val;
                                    if (double.TryParse(_mp4TargetBitrateBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                                        targetBitrate = Math.Max(0.5, Math.Min(200.0, val));
                                }
                                break;
                        }
                    }

                    double maxBitrate = targetBitrate * 1.5;
                    if (_mp4MaxBitrateBox != null)
                    {
                        double val;
                        if (double.TryParse(_mp4MaxBitrateBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                            maxBitrate = Math.Max(targetBitrate, Math.Min(300.0, val));
                    }

                    // Resolve Scale
                    double scale = 1.0;
                    if (_mp4ScaleCombo != null)
                    {
                        switch (_mp4ScaleCombo.SelectedIndex)
                        {
                            case 0: scale = 1.0; break;
                            case 1: scale = 0.5; break;
                            case 2: scale = 1.5; break;
                            case 3: scale = 2.0; break;
                            case 4: scale = 4.0; break;
                        }
                    }

                    // Resolve Time Range
                    double startT = 0.0;
                    double endT = targetClip.Duration;
                    if (_mp4TimeRangeCombo != null && _mp4TimeRangeCombo.SelectedIndex == 1)
                    {
                        if (_mp4InPointBox != null) double.TryParse(_mp4InPointBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out startT);
                        if (_mp4OutPointBox != null) double.TryParse(_mp4OutPointBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out endT);
                    }

                    string bgMode = (_mp4BackgroundCombo != null) ? _mp4BackgroundCombo.Text : "Canvas";

                    var mp4Options = new Mp4ExportOptions
                    {
                        Clip = targetClip,
                        Fps = fps,
                        Scale = scale,
                        TargetBitrateMbps = targetBitrate,
                        MaxBitrateMbps = maxBitrate,
                        BitrateMode = (_mp4BitrateModeCombo != null && _mp4BitrateModeCombo.SelectedIndex == 1) ? "CBR" : "VBR",
                        Profile = (_mp4ProfileCombo != null && _mp4ProfileCombo.SelectedIndex == 2) ? "Baseline" : ((_mp4ProfileCombo != null && _mp4ProfileCombo.SelectedIndex == 1) ? "Main" : "High"),
                        KeyframeIntervalFrames = (int)fps,
                        StartTimeSeconds = startT,
                        EndTimeSeconds = endT,
                        BackgroundColor = bgMode,
                        ProgressCallback = progress =>
                        {
                            if (_mp4ProgressBar != null) _mp4ProgressBar.Value = progress * 100;
                        }
                    };

                    if (_mp4ProgressBar != null)
                    {
                        _mp4ProgressBar.Visibility = Visibility.Visible;
                        _mp4ProgressBar.Value = 0;
                    }

                    Mp4Exporter.ExportClipToVideo(_document, mp4Options, dlg.FileName);

                    _statusTextBlock.Text = "MP4 video rendered successfully.";
                    MessageBox.Show("MP4 Video rendered successfully: " + dlg.FileName, "Kerf", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("MP4 render failed: " + ex.Message, "Kerf", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    if (_mp4ProgressBar != null) _mp4ProgressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ExecuteGifExport(string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Animated GIF (*.gif)|*.gif",
                FileName = defaultName + ".gif",
                Title = "Export Animated GIF"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    Clip targetClip = null;
                    if (_document.Animation != null && _document.Animation.Clips != null && _document.Animation.Clips.Count > 0)
                    {
                        int idx = (_gifClipCombo != null) ? _gifClipCombo.SelectedIndex : 0;
                        if (idx >= 0 && idx < _document.Animation.Clips.Count) targetClip = _document.Animation.Clips[idx];
                        else targetClip = _document.Animation.Clips[0];
                    }
                    if (targetClip == null) targetClip = new Clip(100, "Default Animation", 2.0, 30.0, LoopMode.Loop);

                    // Resolve FPS
                    double fps = 15.0;
                    if (_gifFpsCombo != null)
                    {
                        switch (_gifFpsCombo.SelectedIndex)
                        {
                            case 0: fps = 10.0; break;
                            case 1: fps = 12.0; break;
                            case 2: fps = 15.0; break;
                            case 3: fps = 20.0; break;
                            case 4: fps = 24.0; break;
                            case 5: fps = 30.0; break;
                            case 6:
                                if (_gifCustomFpsBox != null)
                                {
                                    double val;
                                    if (double.TryParse(_gifCustomFpsBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                                        fps = Math.Max(1.0, Math.Min(60.0, val));
                                }
                                break;
                        }
                    }

                    // Resolve Loop Count
                    int loopCount = 0; // 0 = infinite
                    if (_gifLoopCombo != null)
                    {
                        switch (_gifLoopCombo.SelectedIndex)
                        {
                            case 0: loopCount = 0; break;
                            case 1: loopCount = 1; break;
                            case 2: loopCount = 2; break;
                            case 3: loopCount = 3; break;
                            case 4: loopCount = 5; break;
                        }
                    }

                    // Resolve Colors
                    int maxColors = 256;
                    if (_gifColorDepthCombo != null)
                    {
                        switch (_gifColorDepthCombo.SelectedIndex)
                        {
                            case 0: maxColors = 256; break;
                            case 1: maxColors = 128; break;
                            case 2: maxColors = 64; break;
                            case 3: maxColors = 32; break;
                        }
                    }

                    // Resolve Scale
                    double scale = 0.5;
                    if (_gifScaleCombo != null)
                    {
                        switch (_gifScaleCombo.SelectedIndex)
                        {
                            case 0: scale = 1.0; break;
                            case 1: scale = 0.5; break;
                            case 2: scale = 0.25; break;
                            case 3: scale = 1.5; break;
                            case 4: scale = 2.0; break;
                        }
                    }

                    // Resolve Time Range
                    double startT = 0.0;
                    double endT = targetClip.Duration;
                    if (_gifTimeRangeCombo != null && _gifTimeRangeCombo.SelectedIndex == 1)
                    {
                        if (_gifInPointBox != null) double.TryParse(_gifInPointBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out startT);
                        if (_gifOutPointBox != null) double.TryParse(_gifOutPointBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out endT);
                    }

                    bool dither = _gifDitherCheck == null || _gifDitherCheck.IsChecked == true;
                    bool transparent = _gifTransparentCheck == null || _gifTransparentCheck.IsChecked == true;

                    var gifOptions = new GifExportOptions
                    {
                        Clip = targetClip,
                        Fps = fps,
                        Scale = scale,
                        LoopCount = loopCount,
                        PreserveTransparency = transparent,
                        MaxColors = maxColors,
                        ApplyDithering = dither,
                        StartTimeSeconds = startT,
                        EndTimeSeconds = endT,
                        ProgressCallback = progress =>
                        {
                            if (_gifProgressBar != null) _gifProgressBar.Value = progress * 100;
                        }
                    };

                    if (_gifProgressBar != null)
                    {
                        _gifProgressBar.Visibility = Visibility.Visible;
                        _gifProgressBar.Value = 0;
                    }

                    GifExporter.ExportClipToGif(_document, gifOptions, dlg.FileName);

                    _statusTextBlock.Text = "Animated GIF exported successfully.";
                    MessageBox.Show("Animated GIF exported successfully: " + dlg.FileName, "Kerf", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("GIF export failed: " + ex.Message, "Kerf", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    if (_gifProgressBar != null) _gifProgressBar.Visibility = Visibility.Collapsed;
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

        private TextBox CreateDarkTextBox(string initialText, double width = 120)
        {
            var tb = new TextBox
            {
                Text = initialText,
                Width = width,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 2)
            };
            KerfBrand.ApplyDarkRoundedTextBoxStyle(tb, 6);
            return tb;
        }

        private ComboBox CreateStyledComboBox(double width = 300, Thickness margin = default(Thickness))
        {
            var combo = new ComboBox
            {
                Width = width,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = margin,
                FontSize = 11.5
            };
            KerfBrand.ApplyDarkRoundedComboBoxStyle(combo, 6);
            return combo;
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
