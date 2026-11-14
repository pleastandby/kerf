using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Kerf.Model;

namespace Kerf.UI.Dialogs
{
    /// <summary>
    /// Comprehensive in-app developer documentation window explaining how to embed,
    /// configure, and control the Kerf .kfx Motion Runtime in React, Next.js, Vue,
    /// Web Components, and Vanilla JavaScript projects.
    /// Strictly adheres to C# 5.0 compatibility.
    /// </summary>
    public class RuntimeGuideDialog : Window
    {
        private class GuideTopic
        {
            public string Id;
            public string Title;
            public string Badge;
            public string Subtitle;
            public Func<FrameworkElement> ContentBuilder;
        }

        private List<GuideTopic> _topics;
        private string _activeTopicId;
        private StackPanel _topicListPanel;
        private ScrollViewer _contentScroll;
        private ContentControl _contentContainer;
        private TextBox _searchBox;

        public RuntimeGuideDialog()
        {
            Title = "Kerf Motion Runtime — Developer Integration Guide";
            Width = 1000;
            Height = 700;
            MinWidth = 860;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x07, 0x20)); // Obsidian Plum #140720
            Foreground = Brushes.White;
            FontFamily = new FontFamily("-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif");

            Resources[typeof(TextBox)] = KerfBrand.CreateDarkTextBoxStyle(6);
            Resources[typeof(ComboBox)] = KerfBrand.CreateDarkComboBoxStyle(6);

            InitializeTopics();
            InitializeUI();
            SelectTopic("react");
        }

        private void InitializeTopics()
        {
            _topics = new List<GuideTopic>();

            _topics.Add(new GuideTopic
            {
                Id = "react",
                Title = "React Integration",
                Badge = "REACT",
                Subtitle = "Declarative <KerfPlayer /> component, custom useKerfPlayer hook & TypeScript API",
                ContentBuilder = BuildReactGuide
            });

            _topics.Add(new GuideTopic
            {
                Id = "nextjs",
                Title = "Next.js & SSR",
                Badge = "NEXT.JS",
                Subtitle = "Next.js App Router, dynamic client hydration, public assets & SSR safety",
                ContentBuilder = BuildNextJsGuide
            });

            _topics.Add(new GuideTopic
            {
                Id = "webcomponent",
                Title = "HTML5 & Web Component",
                Badge = "VANILLA",
                Subtitle = "Zero-dependency <kerf-player> custom element for HTML & vanilla JS sites",
                ContentBuilder = BuildWebComponentGuide
            });

            _topics.Add(new GuideTopic
            {
                Id = "jssdk",
                Title = "JavaScript SDK API",
                Badge = "JS SDK",
                Subtitle = "Direct KerfPlayerSDK class for programmatic canvas rendering & controls",
                ContentBuilder = BuildJsSdkGuide
            });

            _topics.Add(new GuideTopic
            {
                Id = "statemachine",
                Title = "State Machine & Inputs",
                Badge = "INTERACTIVE",
                Subtitle = "Drive animations via code: Booleans, Numbers, Triggers & transition listeners",
                ContentBuilder = BuildStateMachineGuide
            });

            _topics.Add(new GuideTopic
            {
                Id = "vue",
                Title = "Vue 3 & Vite",
                Badge = "VUE",
                Subtitle = "Vue 3 Composition API (<script setup>), Vite asset bundling & lifecycle hooks",
                ContentBuilder = BuildVueGuide
            });

            _topics.Add(new GuideTopic
            {
                Id = "kfxformat",
                Title = ".kfx File Format Specs",
                Badge = "SPECS",
                Subtitle = "ZIP package container structure: manifest, scene, animations & state graphs",
                ContentBuilder = BuildKfxFormatGuide
            });

            _topics.Add(new GuideTopic
            {
                Id = "native",
                Title = "Native & Mobile (iOS / Android)",
                Badge = "NATIVE",
                Subtitle = "C-ABI Rust Core headless engine, Metal, Vulkan & Direct3D backends",
                ContentBuilder = BuildNativeGuide
            });
        }

        private void InitializeUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main Body (2 columns)
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) }); // Footer

            // 1. Header
            var header = CreateHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // 2. Body Grid
            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) }); // Left navigation
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right documentation content

            // Left Navigation Sidebar
            var leftBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x05, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(14, 14, 14, 14)
            };

            var leftDock = new DockPanel { LastChildFill = true };

            // Sidebar Title & Search
            var searchHeader = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var searchLabel = new TextBlock
            {
                Text = "INTEGRATION GUIDES",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush,
                Margin = new Thickness(2, 0, 0, 6)
            };
            searchHeader.Children.Add(searchLabel);

            _searchBox = new TextBox
            {
                Height = 28,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 6)
            };
            KerfBrand.ApplyDarkRoundedTextBoxStyle(_searchBox, 6, 11.5);
            _searchBox.TextChanged += (s, e) => FilterTopics();
            searchHeader.Children.Add(_searchBox);

            DockPanel.SetDock(searchHeader, Dock.Top);
            leftDock.Children.Add(searchHeader);

            var leftScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _topicListPanel = new StackPanel();
            BuildTopicList();
            leftScroll.Content = _topicListPanel;
            leftDock.Children.Add(leftScroll);

            leftBorder.Child = leftDock;
            Grid.SetColumn(leftBorder, 0);
            body.Children.Add(leftBorder);

            // Right Documentation View Area
            var rightBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x08, 0x24))
            };
            _contentScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(36, 24, 36, 32)
            };
            _contentContainer = new ContentControl();
            _contentScroll.Content = _contentContainer;
            rightBorder.Child = _contentScroll;
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
                Text = "Kerf Motion Runtime — Developer Integration Guide",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            var subtitle = new TextBlock
            {
                Text = "Official documentation, code snippets & API reference for embedding interactive .kfx animations",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleStack.Children.Add(title);
            titleStack.Children.Add(subtitle);
            leftStack.Children.Add(titleStack);

            DockPanel.SetDock(leftStack, Dock.Left);
            dock.Children.Add(leftStack);

            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47)),
                BorderBrush = KerfBrand.MintBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 12, 0)
            };
            badge.Child = new TextBlock
            {
                Text = ".kfx Runtime v4.0",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush
            };
            rightStack.Children.Add(badge);

            var closeBtn = KerfBrand.CreateSecondaryButton("Close", () => Close(), 6);
            closeBtn.Height = 28;
            closeBtn.Padding = new Thickness(14, 2, 14, 2);
            rightStack.Children.Add(closeBtn);

            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

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

            var hint = new TextBlock
            {
                Text = "💡 All .kfx packages exported via File > Export Runtime Package (.kfx) are directly compatible with these integration libraries.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(hint, Dock.Left);
            dock.Children.Add(hint);

            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var copyCdnBtn = KerfBrand.CreateSecondaryButton("Copy Runtime CDN URL", () =>
            {
                try
                {
                    Clipboard.SetText("https://unpkg.com/@kerf/runtime@4.0.0/dist/kerf-player.js");
                    MessageBox.Show("Runtime CDN URL copied to clipboard:\nhttps://unpkg.com/@kerf/runtime@4.0.0/dist/kerf-player.js", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }
            }, 6);
            copyCdnBtn.Height = 28;
            copyCdnBtn.Padding = new Thickness(12, 2, 12, 2);
            copyCdnBtn.Margin = new Thickness(0, 0, 10, 0);
            btnStack.Children.Add(copyCdnBtn);

            var okBtn = KerfBrand.CreatePrimaryButton("Done", () => Close(), 6);
            okBtn.Height = 28;
            okBtn.Padding = new Thickness(16, 2, 16, 2);
            btnStack.Children.Add(okBtn);

            DockPanel.SetDock(btnStack, Dock.Right);
            dock.Children.Add(btnStack);

            border.Child = dock;
            return border;
        }

        private void BuildTopicList(string filter = "")
        {
            _topicListPanel.Children.Clear();
            string query = (filter != null) ? filter.Trim().ToLowerInvariant() : "";

            for (int i = 0; i < _topics.Count; i++)
            {
                var topic = _topics[i];
                if (topic == null) continue;

                if (!string.IsNullOrEmpty(query))
                {
                    bool match = topic.Title.ToLowerInvariant().Contains(query) ||
                                 topic.Subtitle.ToLowerInvariant().Contains(query) ||
                                 topic.Badge.ToLowerInvariant().Contains(query);
                    if (!match) continue;
                }

                _topicListPanel.Children.Add(CreateTopicCard(topic));
            }
        }

        private void FilterTopics()
        {
            string q = (_searchBox != null && !string.IsNullOrEmpty(_searchBox.Text)) ? _searchBox.Text : "";
            BuildTopicList(q);
        }

        private FrameworkElement CreateTopicCard(GuideTopic topic)
        {
            bool isSelected = string.Equals(_activeTopicId, topic.Id, StringComparison.OrdinalIgnoreCase);

            var border = new Border
            {
                Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0x0E, 0x35))
                    : new SolidColorBrush(Color.FromRgb(0x16, 0x07, 0x22)),
                BorderBrush = isSelected
                    ? KerfBrand.MintBrush
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x12, 0x3E)),
                BorderThickness = new Thickness(isSelected ? 1.5 : 1.0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel();

            // Top row: Title and Badge
            var topRow = new DockPanel { LastChildFill = false };
            var titleBlock = new TextBlock
            {
                Text = topic.Title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = isSelected ? KerfBrand.MintBrush : Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(titleBlock, Dock.Left);
            topRow.Children.Add(titleBlock);

            var badge = new Border
            {
                Background = isSelected ? KerfBrand.MintBrush : new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            var badgeText = new TextBlock
            {
                Text = topic.Badge,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = isSelected ? new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47)) : KerfBrand.MintBrush
            };
            badge.Child = badgeText;
            DockPanel.SetDock(badge, Dock.Right);
            topRow.Children.Add(badge);
            stack.Children.Add(topRow);

            // Subtitle
            var subBlock = new TextBlock
            {
                Text = topic.Subtitle,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x99, 0xB7)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                LineHeight = 15
            };
            stack.Children.Add(subBlock);

            border.Child = stack;

            border.MouseEnter += (s, e) =>
            {
                if (!string.Equals(_activeTopicId, topic.Id, StringComparison.OrdinalIgnoreCase))
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(0x1D, 0x0B, 0x2C));
                    border.BorderBrush = KerfBrand.TealBrush;
                }
            };
            border.MouseLeave += (s, e) =>
            {
                if (!string.Equals(_activeTopicId, topic.Id, StringComparison.OrdinalIgnoreCase))
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x07, 0x22));
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x12, 0x3E));
                }
            };

            border.MouseDown += (s, e) => SelectTopic(topic.Id);

            return border;
        }

        private void SelectTopic(string topicId)
        {
            _activeTopicId = topicId;
            FilterTopics();

            var topic = _topics.FirstOrDefault(t => string.Equals(t.Id, topicId, StringComparison.OrdinalIgnoreCase));
            if (topic != null && topic.ContentBuilder != null)
            {
                _contentContainer.Content = topic.ContentBuilder();
                if (_contentScroll != null) _contentScroll.ScrollToTop();
            }
        }

        // =========================================================================
        // UI Helpers for Guide Content
        // =========================================================================

        private FrameworkElement CreateSectionHeading(string title, string badge = null)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = false };

            var text = new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(text, Dock.Left);
            dock.Children.Add(text);

            if (!string.IsNullOrEmpty(badge))
            {
                var b = new Border
                {
                    Background = KerfBrand.TealBrush,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                b.Child = new TextBlock
                {
                    Text = badge,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = KerfBrand.MintBrush
                };
                DockPanel.SetDock(b, Dock.Left);
                dock.Children.Add(b);
            }

            return dock;
        }

        private FrameworkElement CreateSubHeading(string title)
        {
            return new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.MintBrush,
                Margin = new Thickness(0, 18, 0, 6)
            };
        }

        private FrameworkElement CreateParagraph(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0xDF, 0xD7, 0xEB)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 19,
                Margin = new Thickness(0, 0, 0, 10)
            };
        }

        private FrameworkElement CreateTipBox(string title, string text)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x2A, 0x2E)),
                BorderBrush = KerfBrand.MintBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 12, 0, 16)
            };

            var stack = new StackPanel();
            var head = new TextBlock
            {
                Text = "💡 " + title,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(head);

            var body = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xEB, 0xE5)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17
            };
            stack.Children.Add(body);

            border.Child = stack;
            return border;
        }

        private FrameworkElement CreateCodeBlock(string language, string fileName, string code)
        {
            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x05, 0x12)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x15, 0x3B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 8, 0, 16)
            };

            var stack = new StackPanel();

            // Header bar
            var bar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x13, 0x07, 0x1F)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x15, 0x3B)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Padding = new Thickness(14, 6, 10, 6)
            };

            var dock = new DockPanel { LastChildFill = false };

            var fileStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var langBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x42, 0x47)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            langBadge.Child = new TextBlock
            {
                Text = language,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.MintBrush
            };
            fileStack.Children.Add(langBadge);

            if (!string.IsNullOrEmpty(fileName))
            {
                var fileText = new TextBlock
                {
                    Text = fileName,
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xAA, 0xC8)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                fileStack.Children.Add(fileText);
            }
            DockPanel.SetDock(fileStack, Dock.Left);
            dock.Children.Add(fileStack);

            // Copy button
            Button copyBtn = null;
            copyBtn = KerfBrand.CreateSecondaryButton("📋 Copy Code", () =>
            {
                try
                {
                    Clipboard.SetText(code);
                    if (copyBtn != null)
                    {
                        copyBtn.Content = "✓ Copied!";
                        copyBtn.Foreground = KerfBrand.MintBrush;
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
                        timer.Tick += (ts, te) =>
                        {
                            copyBtn.Content = "📋 Copy Code";
                            copyBtn.Foreground = Brushes.White;
                            timer.Stop();
                        };
                        timer.Start();
                    }
                }
                catch { }
            }, 4);
            copyBtn.Height = 24;
            copyBtn.FontSize = 10.5;
            copyBtn.Padding = new Thickness(8, 0, 8, 0);

            DockPanel.SetDock(copyBtn, Dock.Right);
            dock.Children.Add(copyBtn);

            bar.Child = dock;
            stack.Children.Add(bar);

            // Code Content
            var tb = new TextBox
            {
                Text = code,
                FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xE8, 0xF6)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = true,
                Padding = new Thickness(14, 12, 14, 12),
                Cursor = Cursors.IBeam
            };
            stack.Children.Add(tb);

            container.Child = stack;
            return container;
        }

        private FrameworkElement CreateApiTable(string[] headers, string[][] rows)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x05, 0x16)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x15, 0x3B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 8, 0, 16)
            };

            var grid = new Grid();
            for (int h = 0; h < headers.Length; h++)
            {
                var col = new ColumnDefinition();
                if (h == 0) col.Width = new GridLength(160);
                else if (h == 1) col.Width = new GridLength(140);
                else col.Width = new GridLength(1, GridUnitType.Star);
                grid.ColumnDefinitions.Add(col);
            }

            // Header row
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int h = 0; h < headers.Length; h++)
            {
                var cell = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x09, 0x2A)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x15, 0x3B)),
                    BorderThickness = new Thickness(0, 0, (h < headers.Length - 1 ? 1 : 0), 1),
                    Padding = new Thickness(10, 8, 10, 8)
                };
                var t = new TextBlock
                {
                    Text = headers[h],
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = KerfBrand.MintBrush
                };
                cell.Child = t;
                Grid.SetColumn(cell, h);
                Grid.SetRow(cell, 0);
                grid.Children.Add(cell);
            }

            // Data rows
            for (int r = 0; r < rows.Length; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var rowData = rows[r];
                var rowBg = (r % 2 == 0) ? new SolidColorBrush(Color.FromRgb(0x12, 0x06, 0x1B)) : new SolidColorBrush(Color.FromRgb(0x0E, 0x05, 0x16));

                for (int c = 0; c < rowData.Length && c < headers.Length; c++)
                {
                    var cell = new Border
                    {
                        Background = rowBg,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x15, 0x3B)),
                        BorderThickness = new Thickness(0, 0, (c < headers.Length - 1 ? 1 : 0), (r < rows.Length - 1 ? 1 : 0)),
                        Padding = new Thickness(10, 7, 10, 7)
                    };
                    var t = new TextBlock
                    {
                        Text = rowData[c],
                        FontSize = 11,
                        Foreground = (c == 0) ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xDF, 0xD7, 0xEB)),
                        FontFamily = (c == 0 || c == 1) ? new FontFamily("Consolas, Courier New, monospace") : new FontFamily("-apple-system, sans-serif"),
                        FontWeight = (c == 0) ? FontWeights.SemiBold : FontWeights.Normal,
                        TextWrapping = TextWrapping.Wrap
                    };
                    cell.Child = t;
                    Grid.SetColumn(cell, c);
                    Grid.SetRow(cell, r + 1);
                    grid.Children.Add(cell);
                }
            }

            border.Child = grid;
            return border;
        }

        // =========================================================================
        // Specific Guide Builders
        // =========================================================================

        private FrameworkElement BuildReactGuide()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionHeading("Integrating Kerf Motion in React", "RECOMMENDED"));
            stack.Children.Add(CreateParagraph(
                "Kerf animations exported as .kfx packages provide a rich, interactive alternative to Rive and Lottie with full vector rendering, Bézier easing curves, and state machines. You can embed them in any React application (Vite, Next.js, Remix, Create React App) using declarative components or hooks."));

            stack.Children.Add(CreateSubHeading("Step 1: Install the Package"));
            stack.Children.Add(CreateParagraph("Install the official Kerf Motion React runtime via npm, yarn, or pnpm:"));
            stack.Children.Add(CreateCodeBlock("Terminal", "bash",
                "npm install @kerf/runtime-react\n" +
                "# or using pnpm\n" +
                "pnpm add @kerf/runtime-react\n" +
                "# or using yarn\n" +
                "yarn add @kerf/runtime-react"));

            stack.Children.Add(CreateSubHeading("Step 2: Place Your Exported .kfx File"));
            stack.Children.Add(CreateParagraph(
                "Export your animation from Kerf via 'Export > Kerf Animation (.kfx)' and place the generated file into your project's public folder, e.g. `public/animations/hero.kfx`. In Vite or Create React App, files in `public/` are accessible directly at the root URL `/animations/hero.kfx`."));

            stack.Children.Add(CreateSubHeading("Step 3: Declarative Component (<KerfPlayer />)"));
            stack.Children.Add(CreateParagraph(
                "The simplest way to embed a .kfx animation is using the <KerfPlayer /> component. You can attach a ref to query states or fire triggers dynamically:"));

            string reactCode =
                "import React, { useRef } from 'react';\n" +
                "import { KerfPlayer, KerfPlayerRef } from '@kerf/runtime-react';\n\n" +
                "export function InteractiveHero() {\n" +
                "  const playerRef = useRef<KerfPlayerRef>(null);\n\n" +
                "  const handleHover = () => {\n" +
                "    // Set state machine Boolean input\n" +
                "    playerRef.current?.setBool('isHovered', true);\n" +
                "  };\n\n" +
                "  const handleUnhover = () => {\n" +
                "    playerRef.current?.setBool('isHovered', false);\n" +
                "  };\n\n" +
                "  const handleAction = () => {\n" +
                "    // Fire state machine Trigger impulse\n" +
                "    playerRef.current?.fireTrigger('PulseEffect');\n" +
                "  };\n\n" +
                "  return (\n" +
                "    <div className=\"hero-banner\" onMouseEnter={handleHover} onMouseLeave={handleUnhover}>\n" +
                "      <KerfPlayer\n" +
                "        ref={playerRef}\n" +
                "        src=\"/animations/hero.kfx\"\n" +
                "        autoplay={true}\n" +
                "        loop={true}\n" +
                "        stateMachine=\"MainStateMachine\"\n" +
                "        style={{ width: '100%', height: '480px' }}\n" +
                "        onLoad={(manifest) => console.log('Loaded animation:', manifest.name)}\n" +
                "        onStateChange={(state) => console.log('State changed to:', state)}\n" +
                "      />\n" +
                "      <button onClick={handleAction}>Trigger Animation</button>\n" +
                "    </div>\n" +
                "  );\n" +
                "}";

            stack.Children.Add(CreateCodeBlock("TypeScript / React", "src/components/InteractiveHero.tsx", reactCode));

            stack.Children.Add(CreateSubHeading("Step 4: Hook-based Integration (useKerfPlayer)"));
            stack.Children.Add(CreateParagraph(
                "For deeper control over canvas element sizing, custom render loops, or state-driven inputs, use the `useKerfPlayer` hook:"));

            string hookCode =
                "import React, { useRef } from 'react';\n" +
                "import { useKerfPlayer } from '@kerf/runtime-react';\n\n" +
                "export function GaugeWidget({ score }: { score: number }) {\n" +
                "  const canvasRef = useRef<HTMLCanvasElement>(null);\n" +
                "  const { isLoaded, activeState, fireTrigger, setNumber } = useKerfPlayer({\n" +
                "    canvasRef,\n" +
                "    src: '/animations/gauge.kfx',\n" +
                "    autoplay: true,\n" +
                "    stateMachine: 'GaugeLogic',\n" +
                "    inputs: {\n" +
                "      scoreValue: score,\n" +
                "      isWarning: score < 30\n" +
                "    }\n" +
                "  });\n\n" +
                "  return (\n" +
                "    <div className=\"gauge-card\">\n" +
                "      <canvas ref={canvasRef} width={500} height={350} />\n" +
                "      <span>Active State: {activeState}</span>\n" +
                "      <button onClick={() => fireTrigger('Reset')}>Recalibrate</button>\n" +
                "    </div>\n" +
                "  );\n" +
                "}";

            stack.Children.Add(CreateCodeBlock("TypeScript / React Hook", "src/components/GaugeWidget.tsx", hookCode));

            stack.Children.Add(CreateTipBox(
                "High-DPI / Retina Crispness",
                "Kerf automatically matches window.devicePixelRatio so vector strokes and shapes stay pin-sharp on 4K and Retina displays. You don't need manual DPI compensation."));

            return stack;
        }

        private FrameworkElement BuildNextJsGuide()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionHeading("Next.js (App Router & Pages Router)", "SSR BEST PRACTICES"));
            stack.Children.Add(CreateParagraph(
                "Next.js executes React components on the server by default (Server Components). Because vector canvas rendering and interactive animation runtimes require DOM APIs like HTMLCanvasElement and WebGL/2D contexts, Kerf players must be loaded on the client."));

            stack.Children.Add(CreateSubHeading("App Router (app/page.tsx) with 'use client'"));
            stack.Children.Add(CreateParagraph(
                "Mark your motion wrapper component with `'use client'` at the top, or use `next/dynamic` with `ssr: false` to avoid hydration mismatches:"));

            string nextCode =
                "'use client';\n\n" +
                "import dynamic from 'next/dynamic';\n\n" +
                "// Dynamically load KerfPlayer on the client only\n" +
                "const KerfPlayer = dynamic(\n" +
                "  () => import('@kerf/runtime-react').then((m) => m.KerfPlayer),\n" +
                "  { ssr: false, loading: () => <div className=\"h-[400px] bg-slate-900 rounded-lg animate-pulse\" /> }\n" +
                ");\n\n" +
                "export default function HomePage() {\n" +
                "  return (\n" +
                "    <main className=\"container mx-auto py-12\">\n" +
                "      <h1 className=\"text-3xl font-bold mb-6\">Next.js + Kerf Motion Runtime</h1>\n" +
                "      <div className=\"w-full max-w-2xl h-[400px] border border-cyan-800 rounded-xl overflow-hidden\">\n" +
                "        {/* Place hero.kfx in public/animations/hero.kfx */}\n" +
                "        <KerfPlayer\n" +
                "          src=\"/animations/hero.kfx\"\n" +
                "          autoplay={true}\n" +
                "          loop={true}\n" +
                "          stateMachine=\"HeroInteractive\"\n" +
                "        />\n" +
                "      </div>\n" +
                "    </main>\n" +
                "  );\n" +
                "}";

            stack.Children.Add(CreateCodeBlock("Next.js App Router (TypeScript)", "app/page.tsx", nextCode));

            stack.Children.Add(CreateSubHeading("Public Asset Routing in Next.js"));
            stack.Children.Add(CreateParagraph(
                "Next.js automatically serves files placed in the `public/` folder at the root path:"));
            stack.Children.Add(CreateCodeBlock("Folder Layout", "Next.js Project",
                "my-nextjs-app/\n" +
                "├── public/\n" +
                "│   └── animations/\n" +
                "│       └── hero.kfx        <-- Accessible at fetch('/animations/hero.kfx')\n" +
                "├── app/\n" +
                "│   └── page.tsx\n" +
                "└── package.json"));

            stack.Children.Add(CreateTipBox(
                "Asset Preloading & CDN Caching",
                "Because .kfx is a standard ZIP container with a manifest header, you can add `<link rel=\"preload\" href=\"/animations/hero.kfx\" as=\"fetch\" crossOrigin=\"anonymous\" />` in your Next.js layout.tsx for instant zero-latency playback."));

            return stack;
        }

        private FrameworkElement BuildWebComponentGuide()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionHeading("HTML5 Custom Element (<kerf-player>)", "ZERO FRAMEWORK"));
            stack.Children.Add(CreateParagraph(
                "You can embed .kfx animations in any static website, WordPress page, Webflow project, or CMS without any JavaScript build tools or frameworks. Simply import the standalone runner script."));

            stack.Children.Add(CreateSubHeading("HTML Embedding Example"));

            string htmlCode =
                "<!DOCTYPE html>\n" +
                "<html lang=\"en\">\n" +
                "<head>\n" +
                "  <meta charset=\"UTF-8\">\n" +
                "  <title>Kerf Motion Web Component Demo</title>\n" +
                "  <!-- 1. Load the lightweight Kerf runtime (12KB gzipped) -->\n" +
                "  <script src=\"https://unpkg.com/@kerf/runtime/dist/kerf-player.js\"></script>\n" +
                "</head>\n" +
                "<body style=\"background: #0E0517; color: #FFF; font-family: sans-serif;\">\n" +
                "  <h1>Interactive Kerf Animation</h1>\n\n" +
                "  <!-- 2. Use the declarative custom element -->\n" +
                "  <kerf-player\n" +
                "    id=\"avatarPlayer\"\n" +
                "    src=\"./animations/avatar.kfx\"\n" +
                "    autoplay\n" +
                "    loop\n" +
                "    state-machine=\"IdleLookAround\"\n" +
                "    style=\"width: 480px; height: 360px; display: block; border-radius: 12px;\">\n" +
                "  </kerf-player>\n\n" +
                "  <!-- 3. Control via standard DOM JavaScript -->\n" +
                "  <div style=\"margin-top: 16px;\">\n" +
                "    <button onclick=\"document.getElementById('avatarPlayer').fireTrigger('Wave')\">Wave</button>\n" +
                "    <button onclick=\"document.getElementById('avatarPlayer').setBool('isHappy', true)\">Smile</button>\n" +
                "    <button onclick=\"document.getElementById('avatarPlayer').setBool('isHappy', false)\">Neutral</button>\n" +
                "  </div>\n\n" +
                "  <script>\n" +
                "    const player = document.getElementById('avatarPlayer');\n" +
                "    player.addEventListener('statechange', (event) => {\n" +
                "      console.log('State transitioned to:', event.detail.stateName);\n" +
                "    });\n" +
                "  </script>\n" +
                "</body>\n" +
                "</html>";

            stack.Children.Add(CreateCodeBlock("HTML5 / Vanilla JS", "index.html", htmlCode));

            stack.Children.Add(CreateSubHeading("Supported HTML Element Attributes"));
            string[][] attrRows = new string[][]
            {
                new[] { "src", "string (URL)", "Path or CDN URL to the .kfx package container" },
                new[] { "autoplay", "boolean", "Starts playback immediately upon container load (default: false)" },
                new[] { "loop", "boolean", "Continuously loops active clips or states (default: true)" },
                new[] { "state-machine", "string", "Initial active State Machine name to bind and execute" },
                new[] { "clip", "string", "Specific animation clip name to play (for timeline playback)" },
                new[] { "fit", "contain | cover | fill", "Scaling mode inside the canvas container (default: contain)" }
            };
            stack.Children.Add(CreateApiTable(new[] { "Attribute", "Type", "Description" }, attrRows));

            return stack;
        }

        private FrameworkElement BuildJsSdkGuide()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionHeading("Standalone JavaScript SDK (KerfPlayerSDK)", "CORE ENGINE"));
            stack.Children.Add(CreateParagraph(
                "If you want direct low-level control over rendering loops, custom canvas contexts, or offscreen rendering, you can instantiate the KerfPlayerSDK class directly in TypeScript or JavaScript."));

            stack.Children.Add(CreateSubHeading("Core SDK Usage"));

            string jsCode =
                "import { KerfPlayerSDK } from '@kerf/runtime';\n\n" +
                "// 1. Reference an existing canvas\n" +
                "const canvas = document.getElementById('myCanvas') as HTMLCanvasElement;\n\n" +
                "// 2. Initialize the player\n" +
                "const player = new KerfPlayerSDK({\n" +
                "  canvas,\n" +
                "  src: '/animations/loader.kfx',\n" +
                "  autoplay: true,\n" +
                "  stateMachine: 'LoadingProcess',\n" +
                "  dpr: window.devicePixelRatio || 1,\n" +
                "});\n\n" +
                "// 3. Register event callbacks\n" +
                "player.on('load', (manifest) => {\n" +
                "  console.log('Package version:', manifest.version, 'Author:', manifest.author);\n" +
                "});\n\n" +
                "player.on('stateChange', (newState, previousState) => {\n" +
                "  console.log(`Transitioned from ${previousState} to ${newState}`);\n" +
                "});\n\n" +
                "player.on('loop', (clipName) => {\n" +
                "  console.log('Loop completed for:', clipName);\n" +
                "});\n\n" +
                "// 4. Drive inputs programmatically\n" +
                "player.setNumber('downloadPercentage', 42.5);\n" +
                "player.setBool('isNetworkOnline', true);\n" +
                "player.fireTrigger('CompletePulse');\n\n" +
                "// 5. Cleanup on route unmount\n" +
                "// player.destroy();";

            stack.Children.Add(CreateCodeBlock("TypeScript / ES Module", "src/motionEngine.ts", jsCode));

            stack.Children.Add(CreateSubHeading("Runtime Methods Reference"));
            string[][] methodRows = new string[][]
            {
                new[] { "fireTrigger(name)", "name: string", "Fires a one-shot trigger impulse that resets after evaluation" },
                new[] { "setBool(name, val)", "name: string, val: boolean", "Updates boolean input value for state machine transitions" },
                new[] { "setNumber(name, val)", "name: string, val: number", "Sets numeric scalar input value (continuous or threshold)" },
                new[] { "getBool(name)", "name: string -> boolean", "Retrieves current boolean input value" },
                new[] { "getNumber(name)", "name: string -> number", "Retrieves current numeric input value" },
                new[] { "play()", "void", "Resumes animation playback from current position" },
                new[] { "pause()", "void", "Pauses animation execution" },
                new[] { "reset()", "void", "Rewinds animation time to 0.0s and restores default state" },
                new[] { "seek(timeSeconds)", "timeSeconds: number", "Jumps timeline playback to specific timestamp" },
                new[] { "destroy()", "void", "Stops animation loop, unregisters canvas listeners and frees memory" }
            };
            stack.Children.Add(CreateApiTable(new[] { "Method", "Signature", "Description" }, methodRows));

            return stack;
        }

        private FrameworkElement BuildStateMachineGuide()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionHeading("Interactive State Machine Runtime", "INTERACTIVITY"));
            stack.Children.Add(CreateParagraph(
                "Kerf State Machines enable complex interactive animation logic inside a single .kfx package. Instead of stitching multiple video clips or complex CSS keyframes together, you define multi-state transitions in Kerf and trigger them from code with smooth Bézier rest-pose blending."));

            stack.Children.Add(CreateSubHeading("Input Types Explained"));
            string[][] inputRows = new string[][]
            {
                new[] { "Trigger", "Impulse", "Activates for exactly one evaluation frame and resets automatically to false. Perfect for button clicks, gestures, jumps, and pulses." },
                new[] { "Boolean", "true / false", "Persistent flag. Ideal for hover states (isHovered), toggles (isDarkMode), switches, and visibility." },
                new[] { "Number", "Float / Scalar", "Continuous scalar value. Ideal for health bars, progress bars, scroll position, audio levels, and physics velocity." }
            };
            stack.Children.Add(CreateApiTable(new[] { "Input Type", "Category", "Recommended Use Case" }, inputRows));

            stack.Children.Add(CreateSubHeading("Pointer Event Listeners on Layers"));
            stack.Children.Add(CreateParagraph(
                "When authoring in Kerf's Animate Mode, you can attach pointer listeners directly to vector layers. The web runtime automatically maps canvas mouse and touch events to the exact geometry boundaries:"));

            string pointerCode =
                "// Inside .kfx package state-machines.json (configured via Kerf UI):\n" +
                "// Target layer 'SubmitButton' -> Event: 'PointerDown' -> Fires Trigger 'SubmitClicked'\n" +
                "// Target layer 'SubmitButton' -> Event: 'PointerEnter' -> Sets Bool 'isHovered' = true\n" +
                "// Target layer 'SubmitButton' -> Event: 'PointerExit' -> Sets Bool 'isHovered' = false\n\n" +
                "// The runtime automatically intercepts canvas pointer events:\n" +
                "// You do NOT need manual hit-testing code in your React component!";

            stack.Children.Add(CreateCodeBlock("State Machine Architecture", "Pointer Listeners", pointerCode));

            stack.Children.Add(CreateTipBox(
                "Smooth Transition Blending",
                "When state machine transitions fire mid-animation, the Kerf runtime captures the live transform snapshot of all bones and vector layers and interpolates smoothly into the target state without popping or jerking."));

            return stack;
        }

        private FrameworkElement BuildVueGuide()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionHeading("Vue 3 & Vite Integration", "VUE ECOSYSTEM"));
            stack.Children.Add(CreateParagraph(
                "Using the Kerf Motion runtime in Vue 3 with the Composition API (<script setup>) and TypeScript is straightforward using a canvas template reference."));

            string vueCode =
                "<template>\n" +
                "  <div class=\"animation-wrapper\">\n" +
                "    <canvas ref=\"canvasRef\" width=\"600\" height=\"400\" />\n" +
                "    <div class=\"button-controls\">\n" +
                "      <button @click=\"onPulse\">Trigger Pulse</button>\n" +
                "      <button @click=\"toggleActive\">{{ isActive ? 'Turn Off' : 'Turn On' }}</button>\n" +
                "    </div>\n" +
                "  </div>\n" +
                "</template>\n\n" +
                "<script setup lang=\"ts\">\n" +
                "import { ref, onMounted, onUnmounted } from 'vue';\n" +
                "import { KerfPlayerSDK } from '@kerf/runtime';\n\n" +
                "// Vite static asset resolution with ?url\n" +
                "import animUrl from '@/assets/animations/interactive-card.kfx?url';\n\n" +
                "const canvasRef = ref<HTMLCanvasElement | null>(null);\n" +
                "const isActive = ref(false);\n" +
                "let player: KerfPlayerSDK | null = null;\n\n" +
                "onMounted(() => {\n" +
                "  if (!canvasRef.current) return;\n" +
                "  player = new KerfPlayerSDK({\n" +
                "    canvas: canvasRef.current,\n" +
                "    src: animUrl,\n" +
                "    autoplay: true,\n" +
                "    stateMachine: 'CardInteraction',\n" +
                "  });\n" +
                "});\n\n" +
                "const onPulse = () => {\n" +
                "  player?.fireTrigger('Pulse');\n" +
                "};\n\n" +
                "const toggleActive = () => {\n" +
                "  isActive.value = !isActive.value;\n" +
                "  player?.setBool('isActive', isActive.value);\n" +
                "};\n\n" +
                "onUnmounted(() => {\n" +
                "  player?.destroy();\n" +
                "});\n" +
                "</script>";

            stack.Children.Add(CreateCodeBlock("Vue 3 (<script setup>)", "src/components/MotionCard.vue", vueCode));

            return stack;
        }

        private FrameworkElement BuildKfxFormatGuide()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionHeading(".kfx Container Format Specifications", "STANDALONE SPEC"));
            stack.Children.Add(CreateParagraph(
                "A .kfx (Kerf Motion Package) file is a secure, platform-independent ZIP container storing the scene geometry, animation keyframes, state graphs, and binary assets. It is completely independent of React and JavaScript."));

            string tree =
                "animation.kfx\n" +
                "├── manifest.json        Format version, canvas bounds, entry points & assets index\n" +
                "├── scene.json           Optimized vector geometry hierarchy, styling & paths\n" +
                "├── animations.json      Clips, tracks, keyframes, Bézier easing curves\n" +
                "├── state-machines.json  State nodes, transitions, inputs & pointer event listeners\n" +
                "├── metadata.json        Author, timestamp, tags, thumbnail base64\n" +
                "└── assets/              Isolated fonts, raster images, and external audio/svg\n" +
                "    ├── images/\n" +
                "    ├── vectors/\n" +
                "    └── fonts/";

            stack.Children.Add(CreateCodeBlock("Package Container Structure", "ZIP Layout", tree));

            stack.Children.Add(CreateSubHeading("Core Specifications & Security Guarantees"));
            stack.Children.Add(CreateParagraph(
                "1. Pure Declarative JSON: No executable JavaScript or arbitrary code execution within the container.\n" +
                "2. Dual-Payload Persistence: Fully round-trippable back into Kerf while optimized for lightweight web streaming.\n" +
                "3. Safe Relative Paths: Directory traversal attempts ('../') are rejected by the runtime unpacker.\n" +
                "4. High-DPI Vector Fidelity: Infinite zoom with resolution-independent Bézier rasterization."));

            return stack;
        }

        private FrameworkElement BuildNativeGuide()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateSectionHeading("Native Platforms (iOS, Android, C++, Rust)", "NATIVE RUNTIME"));
            stack.Children.Add(CreateParagraph(
                "The Kerf runtime architecture is designed for multi-platform native deployment beyond the web browser. The high-performance animation math and state machine solver are compiled directly to native machine code via the Rust Core (crates/kerf_core)."));

            stack.Children.Add(CreateSubHeading("Rust Native Core Architecture"));
            string cAbi =
                "// C-ABI Exported Functions in kerf_core (available for iOS, Android NDK, C++):\n" +
                "kerf_anim_context_t kerf_anim_context_create();\n" +
                "uint32_t            kerf_anim_clip_create(ctx, id, name, duration, fps, loop);\n" +
                "uint32_t            kerf_anim_clip_add_track(ctx, clip, nodeId, propKey);\n" +
                "void                kerf_anim_track_add_keyframe(ctx, clip, track, time, val, interp, x1, y1, x2, y2);\n" +
                "float               kerf_anim_track_sample(ctx, clip, track, time);\n" +
                "uint32_t            kerf_sm_create(ctx, name);\n" +
                "uint32_t            kerf_sm_add_input(ctx, sm, name, type, defaultVal);\n" +
                "uint32_t            kerf_sm_advance(ctx, sm, dt);\n" +
                "void                kerf_anim_context_destroy(ctx);";

            stack.Children.Add(CreateCodeBlock("C / C++ Native Header", "include/kerf_core.h", cAbi));

            stack.Children.Add(CreateTipBox(
                "Target Renderers",
                "• Windows Desktop: Direct3D 11 GPU retained scene graph & WPF.\n" +
                "• iOS / macOS: Metal 2D vector renderer.\n" +
                "• Android / Linux: Vulkan & Skia GPU hardware surface.\n" +
                "• Web: HTML5 2D Canvas & WebGL with WebAssembly (WASM) Rust core."));

            return stack;
        }
    }
}
