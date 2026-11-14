using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Kerf.Engine;
using Kerf.IO;
using Kerf.Model;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace Kerf.UI
{
    /// <summary>
    /// Kerf official Home / Project Hub View.
    /// Strictly adheres to the Kerf brand palette:
    /// - Primary: #46EFB1 (Electric Mint), #0A717B (Deep Cyan), #0C4247 (Dark Slate), #1E082E (Obsidian Plum)
    /// - Secondary: #F6B322 (Warm Amber Gold)
    /// </summary>
    public class HomePageView : UserControl
    {
        public event Action<KerfDocument, string> DocumentOpened;
        public event Action NewDocumentRequested;

        private WrapPanel _recentProjectsPanel;
        private TextBox _searchBox;
        private List<ProjectMetadata> _allProjects;
        private TextBlock _emptyStateText;

        public HomePageView()
        {
            _allProjects = new List<ProjectMetadata>();
            Resources[typeof(TextBox)] = KerfBrand.CreateDarkTextBoxStyle(6);
            Resources[typeof(ComboBox)] = KerfBrand.CreateDarkComboBoxStyle(6);
            InitializeUI();
            Loaded += (s, e) => RefreshProjects();
        }

        private void InitializeUI()
        {
            Background = KerfBrand.ObsidianBrush; // #1E082E

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

            // 1. Top Header
            var header = CreateHeader();
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // 2. Scrollable Body
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(30)
            };

            var bodyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

            FrameworkElement welcomeBanner;
            try
            {
                welcomeBanner = new KerfRuntimeBannerView();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Kerf home banner failed to initialize: " + ex);
                welcomeBanner = CreateHeroBanner();
            }
            welcomeBanner.HorizontalAlignment = HorizontalAlignment.Stretch;
            welcomeBanner.Margin = new Thickness(0, 0, 0, 24);
            bodyStack.Children.Add(welcomeBanner);

            // Recent Projects Section
            bodyStack.Children.Add(CreateRecentProjectsHeader());
            _recentProjectsPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            _emptyStateText = new TextBlock
            {
                Text = "No recent projects yet. Create a new design or open an existing file to get started.",
                Foreground = KerfBrand.TextMutedBrush,
                FontSize = 14,
                Margin = new Thickness(0, 24, 0, 24),
                Visibility = Visibility.Collapsed
            };
            bodyStack.Children.Add(_emptyStateText);
            bodyStack.Children.Add(_recentProjectsPanel);

            scroll.Content = bodyStack;
            Grid.SetRow(scroll, 1);
            mainGrid.Children.Add(scroll);

            Content = mainGrid;
        }

        private FrameworkElement CreateHeader()
        {
            var border = new Border
            {
                Background = KerfBrand.DarkSlateBrush, // #0C4247
                BorderBrush = KerfBrand.TealBrush,     // #0A717B
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 0, 24, 0)
            };

            var dock = new DockPanel { LastChildFill = false };

            // Left: Official Logo & Title
            var logoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logo = KerfLogo.CreateIconElement(26, 32, fillBrush: KerfBrand.MintBrush, withGlow: true);
            logo.Margin = new Thickness(0, 0, 10, 0);
            logoStack.Children.Add(logo);

            var titleText = new TextBlock
            {
                Text = ProductInfo.Name,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            var versionTag = new Border
            {
                Background = KerfBrand.TealBrush,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            versionTag.Child = new TextBlock
            {
                Text = ProductInfo.Version,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush
            };
            logoStack.Children.Add(titleText);
            logoStack.Children.Add(versionTag);
            DockPanel.SetDock(logoStack, Dock.Left);
            dock.Children.Add(logoStack);

            // Right: GPU Status badge & Actions
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            int tier = RenderCapability.Tier >> 16;
            bool isHw = tier >= 2;
            var gpuBadge = new Border
            {
                Background = isHw ? KerfBrand.DarkSlateBrush : KerfBrand.DarkSlateBrush,
                BorderBrush = isHw ? KerfBrand.MintBrush : KerfBrand.AmberBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(9, 3, 9, 3),
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = isHw ? "Direct3D 11 hardware acceleration is active" : "Software rendering is active (tier " + tier + ")"
            };
            gpuBadge.Child = new TextBlock
            {
                Text = isHw ? "GPU ready" : "Software mode",
                FontSize = 10,
                Foreground = isHw ? KerfBrand.MintBrush : KerfBrand.AmberBrush,
                FontWeight = FontWeights.Medium
            };
            rightStack.Children.Add(gpuBadge);

            // Open File button (Secondary Button: #0C4247 background, 1px #46EFB1 border, white text)
            var openBtn = KerfBrand.CreateSecondaryButton("Open File...", () => OpenFileDialogPrompt(), 8);
            openBtn.Margin = new Thickness(0, 0, 10, 0);
            rightStack.Children.Add(openBtn);

            // New File button (Primary Button: #46EFB1 background, #0C4247 text)
            var newBtn = KerfBrand.CreatePrimaryButton("+ New Design", () =>
            {
                if (NewDocumentRequested != null) NewDocumentRequested();
            }, 8);
            rightStack.Children.Add(newBtn);

            // Help / Runtime Documentation button (rounded ? button)
            var helpBtn = new Button
            {
                Content = "?",
                Width = 32,
                Height = 32,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush,
                Background = KerfBrand.DarkSlateBrush,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Runtime Integration Guide (React, Next.js, Web, Native)",
                Template = KerfBrand.CreateRoundedButtonTemplate(16),
                Margin = new Thickness(10, 0, 0, 0)
            };
            helpBtn.MouseEnter += (s, e) =>
            {
                helpBtn.BorderBrush = KerfBrand.MintBrush;
                helpBtn.Background = new SolidColorBrush(Color.FromRgb(36, 42, 58));
            };
            helpBtn.MouseLeave += (s, e) =>
            {
                helpBtn.BorderBrush = KerfBrand.TealBrush;
                helpBtn.Background = KerfBrand.DarkSlateBrush;
            };
            helpBtn.Click += (s, e) => ShowRuntimeGuideDialog();
            rightStack.Children.Add(helpBtn);

            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

            border.Child = dock;
            return border;
        }

        private FrameworkElement CreateHeroBanner()
        {
            var banner = new Border
            {
                Margin = new Thickness(0, 12, 0, 32),
                Padding = new Thickness(36, 32, 36, 32),
                CornerRadius = new CornerRadius(16),
                Background = new LinearGradientBrush(KerfBrand.DarkSlate, KerfBrand.Obsidian, 90),
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1.2)
            };

            var stack = new StackPanel();

            var h1 = new TextBlock
            {
                Text = "Design, Prototype & Compose at Native Speed",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.TextWhiteBrush,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var sub = new TextBlock
            {
                Text = "High-performance vector design environment inspired by modern workflows. Features retained Direct3D scene graph, real-time vector animation engine, separable Gaussian blurs, and crash-safe storage.",
                FontSize = 14,
                Foreground = KerfBrand.TextMutedBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                MaxWidth = 800,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            stack.Children.Add(h1);
            stack.Children.Add(sub);
            banner.Child = stack;
            return banner;
        }

        private FrameworkElement CreateMotionFeatureBanner()
        {
            var banner = new Border
            {
                Margin = new Thickness(0, 0, 0, 32),
                Padding = new Thickness(32, 28, 32, 28),
                CornerRadius = new CornerRadius(16),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x0C, 0x33, 0x38),
                    Color.FromRgb(0x05, 0x1A, 0x1D),
                    90),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x7A, 0x82)),
                BorderThickness = new Thickness(1.2)
            };

            var stack = new StackPanel();

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x46, 0xEF, 0xB1)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = "NEW RUNTIME & .KFX EXPORT",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47))
            };
            var tagText = new TextBlock
            {
                Text = "KERF MOTION ENGINE v4.0",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x46, 0xEF, 0xB1)),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerStack.Children.Add(badge);
            headerStack.Children.Add(tagText);
            stack.Children.Add(headerStack);

            var title = new TextBlock
            {
                Text = "Interactive State Machines & Standalone .kfx Runtime Export",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.TextWhiteBrush,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(title);

            var sub = new TextBlock
            {
                Text = "Create rich vector animations with keyframes, Bézier easing curves, and state machines. Export to standalone .kfx packages and embed them anywhere—Web, Desktop, or Mobile—using the zero-overhead Kerf Motion Runtime with interactive inputs and pointer listeners.",
                FontSize = 13,
                Foreground = KerfBrand.TextMutedBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                MaxWidth = 900,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            stack.Children.Add(sub);

            var cardsGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var card1 = CreateMotionFeatureCard(
                "Standalone .kfx Packages",
                "ZIP container with scene.json, animations.json, and state-machines.json. Platform-agnostic, embeddable like Rive.",
                "#46EFB1");
            Grid.SetColumn(card1, 0);
            cardsGrid.Children.Add(card1);

            var card2 = CreateMotionFeatureCard(
                "Interactive State Machine",
                "Multi-state nodes, Boolean / Number / Trigger inputs, pointer hover/click listeners, and smooth Bézier cross-fading.",
                "#0A717B");
            Grid.SetColumn(card2, 1);
            cardsGrid.Children.Add(card2);

            var card3 = CreateMotionFeatureCard(
                "Unified Export Window",
                "Dedicated export modal: .kfx packages, standalone HTML5 web player runner, After Effects MP4 video, animated GIF, SVG, PDF, and PNG.",
                "#F6B322");
            Grid.SetColumn(card3, 2);
            cardsGrid.Children.Add(card3);

            stack.Children.Add(cardsGrid);

            var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
            var docsBtn = KerfBrand.CreatePrimaryButton("📖 View React & Web Runtime Integration Guide", () => ShowRuntimeGuideDialog(), 8);
            docsBtn.Height = 32;
            docsBtn.Padding = new Thickness(18, 0, 18, 0);
            actionRow.Children.Add(docsBtn);
            stack.Children.Add(actionRow);

            banner.Child = stack;
            return banner;
        }

        private FrameworkElement CreateMotionFeatureCard(string title, string desc, string accentHex)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x60, 0x08, 0x22, 0x26)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x14, 0x5C, 0x63)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 14, 0)
            };

            var stack = new StackPanel();

            var topRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(GpuRenderer.ParseHexColor(accentHex, 1.0)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            topRow.Children.Add(dot);
            topRow.Children.Add(titleBlock);

            var descBlock = new TextBlock
            {
                Text = desc,
                FontSize = 11,
                Foreground = KerfBrand.TextMutedBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            };

            stack.Children.Add(topRow);
            stack.Children.Add(descBlock);
            card.Child = stack;
            return card;
        }

        private FrameworkElement CreateSectionTitle(string title)
        {
            var tb = new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.TextWhiteBrush,
                Margin = new Thickness(0, 16, 0, 12)
            };
            return tb;
        }

        private FrameworkElement CreateTemplatesRow()
        {
            var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 24) };

            panel.Children.Add(CreateTemplateCard("Mobile UI Design", "iPhone 16 Pro frames, Auto Layout stacks & modern components", "#46EFB1", "#0A717B", () =>
            {
                var doc = StorageManager.CreateStarterMobileTemplate();
                if (DocumentOpened != null) DocumentOpened(doc, null);
            }));

            panel.Children.Add(CreateTemplateCard("Analytics Dashboard", "Desktop 1280px canvas, responsive metric cards & sidebar layout", "#0A717B", "#0C4247", () =>
            {
                var doc = StorageManager.CreateStarterDashboardTemplate();
                if (DocumentOpened != null) DocumentOpened(doc, null);
            }));

            panel.Children.Add(CreateTemplateCard("SaaS Landing Page", "Modern hero headline, CTA buttons, navigation bar & app preview", "#F6B322", "#0A717B", () =>
            {
                var doc = StorageManager.CreateStarterLandingPageTemplate();
                if (DocumentOpened != null) DocumentOpened(doc, null);
            }));

            panel.Children.Add(CreateTemplateCard("Design System & UI Kit", "Color token swatches, button states, badge chips & cards", "#46EFB1", "#F6B322", () =>
            {
                var doc = StorageManager.CreateStarterDesignSystemTemplate();
                if (DocumentOpened != null) DocumentOpened(doc, null);
            }));

            panel.Children.Add(CreateTemplateCard("Blank Canvas", "Start fresh with an infinite Direct3D Retained Scene Graph", "#46EFB1", "#0C4247", () =>
            {
                if (NewDocumentRequested != null) NewDocumentRequested();
            }));

            return panel;
        }

        private FrameworkElement CreateTemplateCard(string title, string desc, string c1, string c2, Action onClick)
        {
            var card = new Border
            {
                Width = 280,
                Height = 160,
                CornerRadius = new CornerRadius(12),
                Background = KerfBrand.DarkSlateBrush,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 20, 16),
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var stripe = new Border
            {
                Background = new LinearGradientBrush(
                    GpuRenderer.ParseHexColor(c1, 1.0),
                    GpuRenderer.ParseHexColor(c2, 1.0),
                    0)
            };
            Grid.SetRow(stripe, 0);
            grid.Children.Add(stripe);

            var content = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.TextWhiteBrush,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var descBlock = new TextBlock
            {
                Text = desc,
                FontSize = 12,
                Foreground = KerfBrand.TextMutedBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };

            content.Children.Add(titleBlock);
            content.Children.Add(descBlock);
            Grid.SetRow(content, 1);
            grid.Children.Add(content);

            card.Child = grid;

            card.MouseEnter += (s, e) =>
            {
                card.Background = KerfBrand.FigmaBorderBrush;
                card.BorderBrush = KerfBrand.MintBrush;
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = KerfBrand.FigmaPanelBrush;
                card.BorderBrush = KerfBrand.FigmaBorderBrush;
            };
            card.MouseLeftButtonUp += (s, e) => onClick();

            return card;
        }

        private FrameworkElement CreateRecentProjectsHeader()
        {
            var dock = new DockPanel { Margin = new Thickness(0, 24, 0, 0), LastChildFill = false };

            var title = new TextBlock
            {
                Text = "Recent Projects",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(title, Dock.Left);
            dock.Children.Add(title);

            // Search box
            _searchBox = new TextBox
            {
                Width = 240,
                Height = 32,
                VerticalAlignment = VerticalAlignment.Center
            };
            KerfBrand.ApplyDarkRoundedTextBoxStyle(_searchBox, 8, 13);
            _searchBox.TextChanged += (s, e) => FilterProjects();
            DockPanel.SetDock(_searchBox, Dock.Right);
            dock.Children.Add(_searchBox);

            return dock;
        }

        public void RefreshProjects()
        {
            _allProjects = StorageManager.GetRecentProjects();
            FilterProjects();
        }

        private void FilterProjects()
        {
            string q = (_searchBox != null && !string.IsNullOrEmpty(_searchBox.Text)) ? _searchBox.Text.Trim().ToLowerInvariant() : "";
            var filtered = string.IsNullOrEmpty(q)
                ? _allProjects
                : _allProjects.Where(p => p.Name.ToLowerInvariant().Contains(q) || p.FilePath.ToLowerInvariant().Contains(q)).ToList();

            _recentProjectsPanel.Children.Clear();

            if (filtered.Count == 0)
            {
                _emptyStateText.Visibility = Visibility.Visible;
            }
            else
            {
                _emptyStateText.Visibility = Visibility.Collapsed;
                foreach (var p in filtered)
                {
                    _recentProjectsPanel.Children.Add(CreateProjectCard(p));
                }
            }
        }

        private FrameworkElement CreateProjectCard(ProjectMetadata meta)
        {
            var card = new Border
            {
                Width = 280,
                Height = 140,
                CornerRadius = new CornerRadius(12),
                Background = KerfBrand.DarkSlateBrush,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 20, 20),
                Cursor = Cursors.Hand,
                Padding = new Thickness(18, 16, 18, 16)
            };

            var stack = new StackPanel();

            // Name + ext tag
            var topDock = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
            var nameBlock = new TextBlock
            {
                Text = meta.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.TextWhiteBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180
            };
            DockPanel.SetDock(nameBlock, Dock.Left);
            topDock.Children.Add(nameBlock);

            var tag = new Border
            {
                Background = KerfBrand.TealBrush,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            tag.Child = new TextBlock
            {
                Text = ".KERF",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush
            };
            DockPanel.SetDock(tag, Dock.Right);
            topDock.Children.Add(tag);
            stack.Children.Add(topDock);

            // Path snippet
            var pathBlock = new TextBlock
            {
                Text = meta.FilePath,
                FontSize = 11,
                Foreground = KerfBrand.TextMutedBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(pathBlock);

            // Bottom metadata: date & size
            var bottomDock = new DockPanel { LastChildFill = false };
            var dateBlock = new TextBlock
            {
                Text = meta.LastModified.ToString("g"),
                FontSize = 11,
                Foreground = KerfBrand.TextMutedBrush
            };
            DockPanel.SetDock(dateBlock, Dock.Left);
            bottomDock.Children.Add(dateBlock);

            var sizeBlock = new TextBlock
            {
                Text = string.Format("{0:F1} KB", meta.FileSizeBytes / 1024.0),
                FontSize = 11,
                Foreground = KerfBrand.TextMutedBrush
            };
            DockPanel.SetDock(sizeBlock, Dock.Right);
            bottomDock.Children.Add(sizeBlock);
            stack.Children.Add(bottomDock);

            card.Child = stack;

            card.MouseEnter += (s, e) =>
            {
                card.Background = KerfBrand.FigmaBorderBrush;
                card.BorderBrush = KerfBrand.MintBrush;
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = KerfBrand.FigmaPanelBrush;
                card.BorderBrush = KerfBrand.FigmaBorderBrush;
            };
            card.MouseLeftButtonUp += (s, e) => OpenProjectFile(meta.FilePath);

            return card;
        }

        private void OpenProjectFile(string path)
        {
            try
            {
                var doc = StorageManager.LoadDocument(path);
                if (DocumentOpened != null)
                {
                    DocumentOpened(doc, path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open project: " + ex.Message, "Kerf", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFileDialogPrompt()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Kerf Projects and Packages (*.kerf;*.kfx;*.json)|*.kerf;*.kfx;*.json|Kerf Design Files (*.kerf)|*.kerf|Kerf Animation Packages (*.kfx)|*.kfx|JSON Projects (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Open Kerf Project or Animation Package",
                InitialDirectory = StorageManager.ProjectsFolder
            };

            if (dlg.ShowDialog() == true)
            {
                OpenProjectFile(dlg.FileName);
            }
        }

        private void ShowRuntimeGuideDialog()
        {
            var dlg = new Dialogs.RuntimeGuideDialog();
            var win = Window.GetWindow(this);
            if (win != null) dlg.Owner = win;
            dlg.ShowDialog();
        }
    }
}
