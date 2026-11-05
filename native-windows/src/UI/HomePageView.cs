using System;
using System.Collections.Generic;
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
    /// Kerf Studio official Home / Project Hub View.
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
                Padding = new Thickness(48, 24, 48, 48)
            };

            var bodyStack = new StackPanel { MaxWidth = 1200, HorizontalAlignment = HorizontalAlignment.Left };

            // Welcome Hero Card
            bodyStack.Children.Add(CreateHeroBanner());

            // Templates Section
            bodyStack.Children.Add(CreateSectionTitle("Starter Templates"));
            bodyStack.Children.Add(CreateTemplatesRow());

            // Recent Projects Section
            bodyStack.Children.Add(CreateRecentProjectsHeader());
            _recentProjectsPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            _emptyStateText = new TextBlock
            {
                Text = "No local projects found. Click '+ New Design' or open a template above to get started.",
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
                Padding = new Thickness(32, 0, 32, 0)
            };

            var dock = new DockPanel { LastChildFill = false };

            // Left: Official Logo & Title
            var logoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logo = KerfLogo.CreateIconElement(26, 32, fillBrush: KerfBrand.MintBrush, withGlow: true);
            logo.Margin = new Thickness(0, 0, 12, 0);
            logoStack.Children.Add(logo);

            var titleText = new TextBlock
            {
                Text = "Kerf Studio",
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
                Text = "v3.0.0",
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
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 16, 0)
            };
            gpuBadge.Child = new TextBlock
            {
                Text = isHw ? "Direct3D 11 GPU Active" : "Software Rasterizer (Tier " + tier + ")",
                FontSize = 12,
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
                Background = KerfBrand.DarkSlateBrush,
                Foreground = KerfBrand.TextWhiteBrush,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13
            };
            _searchBox.GotFocus += (s, e) => _searchBox.BorderBrush = KerfBrand.MintBrush;
            _searchBox.LostFocus += (s, e) => _searchBox.BorderBrush = KerfBrand.TealBrush;
            _searchBox.TextChanged += (s, e) => FilterProjects();
            DockPanel.SetDock(_searchBox, Dock.Right);
            dock.Children.Add(_searchBox);

            return dock;
        }

        public void RefreshProjects()
        {
            StorageManager.EnsureDefaultTemplates();
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
                MessageBox.Show("Failed to open project: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFileDialogPrompt()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Kerf Design Files (*.kerf)|*.kerf|All Files (*.*)|*.*",
                Title = "Open Kerf Design File",
                InitialDirectory = StorageManager.ProjectsFolder
            };

            if (dlg.ShowDialog() == true)
            {
                OpenProjectFile(dlg.FileName);
            }
        }
    }
}
