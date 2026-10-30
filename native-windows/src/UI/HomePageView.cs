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
            Background = new SolidColorBrush(Color.FromRgb(30, 34, 48)); // #1E2230

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
                Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)),
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
                Background = new SolidColorBrush(Color.FromRgb(37, 41, 58)), // #25293A
                BorderBrush = new SolidColorBrush(Color.FromRgb(54, 62, 86)), // #363E56
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(32, 0, 32, 0)
            };

            var dock = new DockPanel { LastChildFill = false };

            // Left: Logo & Title
            var logoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logo = KerfLogo.CreateLogoElement(32, withGlow: true);
            logo.Margin = new Thickness(0, 0, 12, 0);
            logoStack.Children.Add(logo);

            var titleText = new TextBlock
            {
                Text = "Kerf Studio",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            var versionTag = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 42, 62)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            versionTag.Child = new TextBlock
            {
                Text = "v3.0.0",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 145, 180))
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
                Background = new SolidColorBrush(isHw ? Color.FromArgb(40, 16, 185, 129) : Color.FromArgb(40, 245, 158, 11)),
                BorderBrush = new SolidColorBrush(isHw ? Color.FromRgb(16, 185, 129) : Color.FromRgb(245, 158, 11)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 16, 0)
            };
            gpuBadge.Child = new TextBlock
            {
                Text = isHw ? "Direct3D 11 GPU Active" : "Software Rasterizer (Tier " + tier + ")",
                FontSize = 12,
                Foreground = isHw ? new SolidColorBrush(Color.FromRgb(52, 211, 153)) : new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                FontWeight = FontWeights.Medium
            };
            rightStack.Children.Add(gpuBadge);

            // Open File button
            var openBtn = CreateButton("Open File...", false);
            openBtn.Margin = new Thickness(0, 0, 10, 0);
            openBtn.Click += (s, e) => OpenFileDialogPrompt();
            rightStack.Children.Add(openBtn);

            // New File button
            var newBtn = CreateButton("+ New Design", true);
            newBtn.Click += (s, e) =>
            {
                if (NewDocumentRequested != null) NewDocumentRequested();
            };
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
                Background = new LinearGradientBrush(Color.FromRgb(22, 26, 42), Color.FromRgb(16, 19, 30), 90),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 48, 72)),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel();

            var h1 = new TextBlock
            {
                Text = "Design, Prototype & Compose at Native Speed",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var sub = new TextBlock
            {
                Text = "Dependable Windows desktop vector environment inspired by Figma and Illustrator. Features retained Direct3D scene graph, separable Gaussian blurs, inner shadows, and transactional crash-safe local storage.",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 165, 195)),
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
                Foreground = Brushes.White,
                Margin = new Thickness(0, 16, 0, 12)
            };
            return tb;
        }

        private FrameworkElement CreateTemplatesRow()
        {
            var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 24) };

            panel.Children.Add(CreateTemplateCard("Mobile UI Design", "iPhone 16 Pro frames, Auto Layout stacks & modern components", "#6366F1", "#A855F7", () =>
            {
                string path = IOPath.Combine(StorageManager.ProjectsFolder, "Mobile UI Design.kerf");
                StorageManager.EnsureDefaultTemplates();
                if (File.Exists(path))
                {
                    OpenProjectFile(path);
                }
                else
                {
                    var doc = new KerfDocument { Name = "Mobile UI Design" };
                    if (DocumentOpened != null) DocumentOpened(doc, null);
                }
            }));

            panel.Children.Add(CreateTemplateCard("Analytics Dashboard", "Desktop 1280px canvas, responsive metric cards & sidebar layout", "#0EA5E9", "#3B82F6", () =>
            {
                string path = IOPath.Combine(StorageManager.ProjectsFolder, "Analytics Dashboard.kerf");
                StorageManager.EnsureDefaultTemplates();
                if (File.Exists(path))
                {
                    OpenProjectFile(path);
                }
                else
                {
                    var doc = new KerfDocument { Name = "Analytics Dashboard" };
                    if (DocumentOpened != null) DocumentOpened(doc, null);
                }
            }));

            panel.Children.Add(CreateTemplateCard("Blank Canvas", "Start fresh with an infinite Direct3D Retained Scene Graph", "#10B981", "#059669", () =>
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
                Background = new SolidColorBrush(Color.FromRgb(22, 26, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 45, 64)),
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
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var descBlock = new TextBlock
            {
                Text = desc,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 175)),
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
                card.Background = new SolidColorBrush(Color.FromRgb(28, 33, 48));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(22, 26, 38));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(38, 45, 64));
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
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(title, Dock.Left);
            dock.Children.Add(title);

            // Search box
            _searchBox = new TextBox
            {
                Width = 240,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(22, 26, 38)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 48, 70)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13
            };
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
                Background = new SolidColorBrush(Color.FromRgb(22, 26, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 45, 64)),
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
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180
            };
            DockPanel.SetDock(nameBlock, Dock.Left);
            topDock.Children.Add(nameBlock);

            var tag = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 42, 60)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            tag.Child = new TextBlock
            {
                Text = ".KERF",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241))
            };
            DockPanel.SetDock(tag, Dock.Right);
            topDock.Children.Add(tag);
            stack.Children.Add(topDock);

            // Path snippet
            var pathBlock = new TextBlock
            {
                Text = meta.FilePath,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 165))
            };
            DockPanel.SetDock(dateBlock, Dock.Left);
            bottomDock.Children.Add(dateBlock);

            var sizeBlock = new TextBlock
            {
                Text = string.Format("{0:F1} KB", meta.FileSizeBytes / 1024.0),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 165))
            };
            DockPanel.SetDock(sizeBlock, Dock.Right);
            bottomDock.Children.Add(sizeBlock);
            stack.Children.Add(bottomDock);

            card.Child = stack;

            card.MouseEnter += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(28, 33, 48));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(22, 26, 38));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(38, 45, 64));
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

        private Button CreateButton(string text, bool isPrimary)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Padding = new Thickness(16, 8, 16, 8),
                Cursor = Cursors.Hand,
                Background = isPrimary
                    ? (Brush)new LinearGradientBrush(Color.FromRgb(99, 102, 241), Color.FromRgb(79, 70, 229), 90)
                    : (Brush)new SolidColorBrush(Color.FromRgb(30, 35, 50)),
                Foreground = Brushes.White,
                BorderBrush = isPrimary
                    ? new SolidColorBrush(Color.FromRgb(129, 140, 248))
                    : new SolidColorBrush(Color.FromRgb(50, 60, 85)),
                BorderThickness = new Thickness(1),
                Template = CreateRoundedButtonTemplate(8)
            };
            return btn;
        }
    }
}
