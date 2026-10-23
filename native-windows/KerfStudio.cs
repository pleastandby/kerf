using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace Kerf.NativeStudio
{
    public enum ElementType
    {
        Rect,
        Ellipse,
        Frame,
        Text,
        Star,
        Polygon,
        Line,
        Arrow,
        Path
    }

    public enum ToolType
    {
        Select,
        Hand,
        Frame,
        Rect,
        Ellipse,
        Pen,
        Pencil,
        Star,
        Polygon,
        Text,
        Line
    }

    public enum LayoutMode
    {
        Horizontal,
        Vertical
    }

    public enum AppView
    {
        Home,
        Studio
    }

    public class PointData
    {
        public double X { get; set; }
        public double Y { get; set; }

        public PointData() { }
        public PointData(double x, double y)
        {
            X = x;
            Y = y;
        }

        public Point ToPoint()
        {
            return new Point(X, Y);
        }

        public static PointData FromPoint(Point p)
        {
            return new PointData(p.X, p.Y);
        }
    }

    public class FigmaElement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ElementType Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public double CornerRadius { get; set; }
        public double Opacity { get; set; }
        public string Fill { get; set; }
        public string Stroke { get; set; }
        public double StrokeWidth { get; set; }
        public bool IsVisible { get; set; }
        public bool IsLocked { get; set; }
        public string Text { get; set; }
        public double FontSize { get; set; }
        public bool IsAutoLayout { get; set; }
        public LayoutMode LayoutMode { get; set; }
        public double ItemSpacing { get; set; }
        public double Padding { get; set; }
        public string ParentId { get; set; }
        public string SvgPathData { get; set; }
        public List<PointData> Points { get; set; }
        public bool IsClosed { get; set; }

        public FigmaElement()
        {
            Id = Guid.NewGuid().ToString().Substring(0, 8);
            Name = "Element";
            Type = ElementType.Rect;
            X = 100;
            Y = 100;
            Width = 140;
            Height = 90;
            Rotation = 0;
            CornerRadius = 0;
            Opacity = 1.0;
            Fill = "#0D99FF";
            Stroke = "#000000";
            StrokeWidth = 0;
            IsVisible = true;
            IsLocked = false;
            Text = "Typography";
            FontSize = 18;
            IsAutoLayout = false;
            LayoutMode = LayoutMode.Horizontal;
            ItemSpacing = 12;
            Padding = 16;
            ParentId = null;
            SvgPathData = null;
            Points = new List<PointData>();
            IsClosed = false;
        }

        public FigmaElement Clone()
        {
            var el = new FigmaElement
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = this.Name + " Copy",
                Type = this.Type,
                X = this.X + 24,
                Y = this.Y + 24,
                Width = this.Width,
                Height = this.Height,
                Rotation = this.Rotation,
                CornerRadius = this.CornerRadius,
                Opacity = this.Opacity,
                Fill = this.Fill,
                Stroke = this.Stroke,
                StrokeWidth = this.StrokeWidth,
                IsVisible = this.IsVisible,
                IsLocked = this.IsLocked,
                Text = this.Text,
                FontSize = this.FontSize,
                IsAutoLayout = this.IsAutoLayout,
                LayoutMode = this.LayoutMode,
                ItemSpacing = this.ItemSpacing,
                Padding = this.Padding,
                ParentId = this.ParentId,
                SvgPathData = this.SvgPathData,
                IsClosed = this.IsClosed,
                Points = new List<PointData>()
            };
            if (this.Points != null)
            {
                foreach (var p in this.Points)
                {
                    el.Points.Add(new PointData(p.X, p.Y));
                }
            }
            return el;
        }
    }

    public class ProjectFileModel
    {
        public string Format { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
        public long Timestamp { get; set; }
        public double PanX { get; set; }
        public double PanY { get; set; }
        public double Zoom { get; set; }
        public List<FigmaElement> Elements { get; set; }

        public ProjectFileModel()
        {
            Format = "kerf-vector-design";
            Version = "2.4.0";
            Elements = new List<FigmaElement>();
        }
    }

    public class MainWindow : Window
    {
        // Core View Mode: Home Page opens first!
        private AppView currentView = AppView.Home;
        private Grid rootGrid;
        private Grid homeViewContainer;
        private Grid studioViewContainer;

        // Core Project State
        private List<FigmaElement> elements = new List<FigmaElement>();
        private List<string> selectedIds = new List<string>();
        private ToolType currentTool = ToolType.Select;
        private string projectName = "Untitled Kerf Project";
        private string currentFilePath = null;
        private bool isDirty = false;

        // Viewport (Infinite Pan & Zoom)
        private double panX = 140;
        private double panY = 90;
        private double zoom = 1.0;

        // Interaction State
        private bool isPanning = false;
        private Point panStart;
        private bool isDraggingElement = false;
        private Point dragStartPoint;
        private Dictionary<string, Point> initialDragPositions = new Dictionary<string, Point>();
        private Dictionary<string, List<PointData>> initialDragPoints = new Dictionary<string, List<PointData>>();
        private bool isCreating = false;
        private Point createStartPoint;
        private FigmaElement creatingElement = null;

        // Pen & Pencil State
        private bool isPenDrawing = false;
        private List<Point> penPoints = new List<Point>();
        private FigmaElement activePenElement = null;
        private Point liveMousePos;
        private bool isPencilDrawing = false;
        private List<Point> pencilPoints = new List<Point>();
        private FigmaElement activePencilElement = null;

        // Telemetry & Autosave
        private int frameCount = 0;
        private DateTime lastFpsCheck = DateTime.Now;
        private double currentFps = 60.0;
        private DispatcherTimer telemetryTimer;
        private DispatcherTimer autosaveTimer;

        // Studio UI Containers
        private Canvas canvas;
        private Grid mainContainer;
        private TextBlock telemetryBlock;
        private TextBox titleBox;
        private StackPanel layersContainer;
        private StackPanel propertiesPanel;
        private TextBlock zoomBlock;
        private TextBlock toolHintBlock;
        private Border selectionBox;
        private TextBlock layerCountBadge;

        // Home Page Containers
        private WrapPanel homeRecentGrid;
        private TextBox homeSearchBox;

        // Tool Buttons Dictionary
        private Dictionary<ToolType, Button> toolButtons = new Dictionary<ToolType, Button>();

        // Property Controls
        private TextBox txtX, txtY, txtW, txtH, txtRadius, txtOpacity, txtStrokeW, txtFontSize, txtText, txtSpacing, txtPadding;
        private TextBox txtFill, txtStroke;
        private Border swatchFillPreview, swatchStrokePreview;
        private CheckBox chkAutoLayout;
        private ComboBox cmbLayoutMode;
        private TextBox txtCodeInspect;
        private TextBlock statusDot;

        // Local Storage Path
        private string localProjectsDir;
        private string autosavePath;

        public MainWindow(string initialFile = null)
        {
            InitializeLocalStorage();

            Title = "Kerf Studio — Native DirectX Vector Design";
            Width = 1440;
            Height = 900;
            MinWidth = 1080;
            MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x14));
            Foreground = Brushes.White;

            // Frameless modern window with Aero Snap & resizing support
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;

            var chrome = new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0)
            };
            WindowChrome.SetWindowChrome(this, chrome);

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

            // Hook closing event to save before closing!
            Closing += OnWindowClosing;

            BuildAppShell();

            // Open Home Page first unless a file argument was passed
            if (!string.IsNullOrEmpty(initialFile) && File.Exists(initialFile))
            {
                LoadKerfFile(initialFile);
                SwitchToView(AppView.Studio);
            }
            else
            {
                SwitchToView(AppView.Home);
            }

            StartTelemetry();
            StartAutosave();

            KeyDown += OnWindowKeyDown;
        }

        private void InitializeLocalStorage()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string kerfDir = IOPath.Combine(appData, "KerfStudio");
                localProjectsDir = IOPath.Combine(kerfDir, "Projects");
                autosavePath = IOPath.Combine(kerfDir, "autosave.kerf");

                if (!Directory.Exists(localProjectsDir))
                {
                    Directory.CreateDirectory(localProjectsDir);
                }

                // Create default template files if not exist
                string welcomeFile = IOPath.Combine(localProjectsDir, "Mobile Banking UI.kerf");
                if (!File.Exists(welcomeFile))
                {
                    SaveDefaultBankingTemplateToFile(welcomeFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Storage Init Error: " + ex.Message);
            }
        }

        private void BuildAppShell()
        {
            rootGrid = new Grid();

            // View 1: Home Page View (Opens first!)
            homeViewContainer = CreateHomePageView();
            rootGrid.Children.Add(homeViewContainer);

            // View 2: Studio Canvas Workspace View
            studioViewContainer = CreateStudioView();
            rootGrid.Children.Add(studioViewContainer);

            Content = rootGrid;
        }

        private void SwitchToView(AppView view)
        {
            currentView = view;
            if (currentView == AppView.Home)
            {
                homeViewContainer.Visibility = Visibility.Visible;
                studioViewContainer.Visibility = Visibility.Collapsed;
                RefreshHomeRecentProjects();
            }
            else
            {
                homeViewContainer.Visibility = Visibility.Collapsed;
                studioViewContainer.Visibility = Visibility.Visible;
                RenderCanvas();
            }
        }

        // ================= SAVE BEFORE CLOSING LOGIC =================

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if (isDirty)
            {
                var result = MessageBox.Show(
                    string.Format("Do you want to save changes to \"{0}\" before closing?", projectName),
                    "Kerf Studio — Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    bool saved = SaveProjectCurrentOrDialog();
                    if (!saved)
                    {
                        e.Cancel = true; // User cancelled the Save dialog, abort close!
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true; // Abort close!
                }
            }
        }

        private bool SaveProjectCurrentOrDialog()
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                SaveKerfFile(currentFilePath);
                isDirty = false;
                UpdateStatusDot(false);
                return true;
            }
            else
            {
                return SaveProjectFileDialog();
            }
        }

        // =============================================================
        //  VIEW 1: HOME PAGE / PROJECT LAUNCHER (OPENS FIRST)
        // =============================================================

        private Grid CreateHomePageView()
        {
            var grid = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x14))
            };

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) }); // Home Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Home Content

            // Top Header
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 46
            };
            header.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) ToggleMaximizeRestore(); else try { DragMove(); } catch { } };

            var dock = new DockPanel { LastChildFill = false };

            // Left: Logo & Search
            var leftStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };

            var logo = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(6),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x0D, 0x99, 0xFF),
                    Color.FromRgb(0x63, 0x66, 0xF1),
                    new Point(0, 0),
                    new Point(1, 1)
                ),
                Margin = new Thickness(0, 0, 10, 0)
            };
            logo.Child = new TextBlock
            {
                Text = "K",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(logo);

            var titleBlock = new TextBlock
            {
                Text = "KERF STUDIO",
                FontWeight = FontWeights.ExtraBold,
                FontSize = 13,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 20, 0)
            };
            leftStack.Children.Add(titleBlock);

            // Search Box
            var searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x27)),
                CornerRadius = new CornerRadius(6),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x36)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 2, 8, 2),
                Width = 260
            };
            homeSearchBox = new TextBox
            {
                Text = "",
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            homeSearchBox.TextChanged += (s, e) => RefreshHomeRecentProjects();
            searchBorder.Child = homeSearchBox;
            leftStack.Children.Add(searchBorder);

            DockPanel.SetDock(leftStack, Dock.Left);
            dock.Children.Add(leftStack);

            // Right: New File, Open File & Window Controls
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var btnNewFile = new Button
            {
                Content = "+ New Design File",
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 8, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnNewFile.Click += (s, e) => StartNewBlankCanvas();
            rightStack.Children.Add(btnNewFile);

            var btnOpenKerf = CreateNavActionButton("Open .kerf", "M 3 5 L 8 5 L 10 7 L 15 7 C 16 7 16 8 16 9 L 14 15 L 2 15 L 2 6 C 2 5 2 5 3 5 Z", (s, e) =>
            {
                OpenProjectFileDialog();
                if (elements.Count > 0) SwitchToView(AppView.Studio);
            });
            rightStack.Children.Add(btnOpenKerf);

            var div = new Border { Width = 1, Height = 22, Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x3A)), Margin = new Thickness(8, 0, 8, 0) };
            rightStack.Children.Add(div);

            // Window Controls (Minimize, Maximize, Close)
            rightStack.Children.Add(CreateWindowControlButton("M 0 0 L 10 0", "Minimize", (s, e) => WindowState = WindowState.Minimized));
            rightStack.Children.Add(CreateWindowControlButton("M 1 1 L 9 1 L 9 9 L 1 9 Z", "Maximize", (s, e) => ToggleMaximizeRestore()));
            rightStack.Children.Add(CreateWindowControlButton("M 1 1 L 9 9 M 9 1 L 1 9", "Close", (s, e) => Close(), true));

            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

            header.Child = dock;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Body: Left Navigation Column + Center Content ScrollViewer
            var bodyGrid = new Grid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left Navigation
            var leftNav = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x19)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(16)
            };

            var leftNavStack = new StackPanel();

            var btnCreatePrimary = new Button
            {
                Content = "+ Create New Design",
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x0D, 0x99, 0xFF),
                    Color.FromRgb(0x3B, 0x82, 0xF6),
                    new Point(0, 0),
                    new Point(1, 0)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Padding = new Thickness(0, 10, 0, 10),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 20)
            };
            btnCreatePrimary.Click += (s, e) => StartNewBlankCanvas();
            leftNavStack.Children.Add(btnCreatePrimary);

            AddHomeNavCategory(leftNavStack, "DESIGN WORKSPACE");
            AddHomeNavItem(leftNavStack, "Recent Designs", true, () => { });
            AddHomeNavItem(leftNavStack, "Starter Templates", false, () => LoadBankingTemplate());
            AddHomeNavItem(leftNavStack, "Local Storage Files", false, () => Process.Start("explorer.exe", localProjectsDir));

            // Bottom Storage Info Card
            var storageCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24)),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 40, 0, 0)
            };
            var scStack = new StackPanel();
            scStack.Children.Add(new TextBlock
            {
                Text = "LOCAL STORAGE (.KERF)",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            scStack.Children.Add(new TextBlock
            {
                Text = "All projects are saved locally on this machine with 100% vector fidelity.",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
            var btnShowExplorer = new Button
            {
                Content = "Open Storage in Explorer",
                FontSize = 10,
                Padding = new Thickness(0, 4, 0, 4),
                Background = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnShowExplorer.Click += (s, e) => Process.Start("explorer.exe", localProjectsDir);
            scStack.Children.Add(btnShowExplorer);
            storageCard.Child = scStack;
            leftNavStack.Children.Add(storageCard);

            leftNav.Child = leftNavStack;
            Grid.SetColumn(leftNav, 0);
            bodyGrid.Children.Add(leftNav);

            // Center Content
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(32, 24, 32, 24)
            };

            var mainStack = new StackPanel();

            // Quick Start Templates Row
            var tHeader = new TextBlock
            {
                Text = "QUICK START TEMPLATES",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            mainStack.Children.Add(tHeader);

            var tGrid = new Grid { Margin = new Thickness(0, 0, 0, 28) };
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            tGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var cardBlank = CreateQuickTemplateCard("Blank Vector Canvas", "Clean slate with infinite canvas & grid", "#0D99FF", () => StartNewBlankCanvas());
            Grid.SetColumn(cardBlank, 0);
            tGrid.Children.Add(cardBlank);

            var cardBanking = CreateQuickTemplateCard("Mobile Banking Card", "Auto layout card with chips and balance", "#10B981", () => LoadBankingTemplate());
            Grid.SetColumn(cardBanking, 2);
            tGrid.Children.Add(cardBanking);

            var cardVector = CreateQuickTemplateCard("Vector Path & Pen Art", "Sample vector wave with bezier anchor points", "#8B5CF6", () => LoadVectorPathTemplate());
            Grid.SetColumn(cardVector, 4);
            tGrid.Children.Add(cardVector);

            mainStack.Children.Add(tGrid);

            // Recent Files Section
            var rHeader = new TextBlock
            {
                Text = "RECENT DESIGNS (.KERF)",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            mainStack.Children.Add(rHeader);

            homeRecentGrid = new WrapPanel();
            mainStack.Children.Add(homeRecentGrid);

            scroll.Content = mainStack;
            Grid.SetColumn(scroll, 1);
            bodyGrid.Children.Add(scroll);

            Grid.SetRow(bodyGrid, 1);
            grid.Children.Add(bodyGrid);

            return grid;
        }

        private void AddHomeNavCategory(StackPanel parent, string title)
        {
            var txt = new TextBlock
            {
                Text = title,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x78)),
                Margin = new Thickness(8, 8, 0, 6)
            };
            parent.Children.Add(txt);
        }

        private void AddHomeNavItem(StackPanel parent, string title, bool isActive, Action action)
        {
            var btn = new Button
            {
                Content = title,
                FontSize = 11,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xAA)),
                Background = isActive ? new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x2C)) : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand
            };
            btn.Click += (s, e) => action();
            parent.Children.Add(btn);
        }

        private Border CreateQuickTemplateCard(string title, string desc, string accentHex, Action onClick)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1F)),
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Cursor = Cursors.Hand
            };

            card.MouseEnter += (s, e) => card.BorderBrush = ParseColorBrush(accentHex);
            card.MouseLeave += (s, e) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E));
            card.MouseLeftButtonDown += (s, e) => onClick();

            var stack = new StackPanel();

            var topRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var iconBox = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = ParseColorBrush(accentHex)
            };
            DockPanel.SetDock(iconBox, Dock.Left);
            topRow.Children.Add(iconBox);

            var tagPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2)
            };
            tagPill.Child = new TextBlock { Text = ".KERF", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            DockPanel.SetDock(tagPill, Dock.Right);
            topRow.Children.Add(tagPill);

            stack.Children.Add(topRow);

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            });

            stack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98)),
                TextWrapping = TextWrapping.Wrap
            });

            card.Child = stack;
            return card;
        }

        private void RefreshHomeRecentProjects()
        {
            if (homeRecentGrid == null) return;
            homeRecentGrid.Children.Clear();

            string query = homeSearchBox != null ? homeSearchBox.Text.Trim().ToLower() : "";
            string[] files = Directory.GetFiles(localProjectsDir, "*.kerf");

            int matchCount = 0;
            foreach (var file in files)
            {
                string fileName = IOPath.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrEmpty(query) && !fileName.ToLower().Contains(query))
                    continue;

                matchCount++;
                var fi = new FileInfo(file);
                var card = CreateRecentFileCard(fileName, fi.LastWriteTime, fi.Length, file);
                homeRecentGrid.Children.Add(card);
            }

            if (matchCount == 0)
            {
                var empty = new TextBlock
                {
                    Text = "No projects found. Click '+ New Design File' or choose a template above!",
                    FontSize = 12,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                homeRecentGrid.Children.Add(empty);
            }
        }

        private Border CreateRecentFileCard(string name, DateTime modified, long bytes, string fullPath)
        {
            var card = new Border
            {
                Width = 200,
                Height = 160,
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1F)),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 16, 16),
                Padding = new Thickness(12),
                Cursor = Cursors.Hand
            };

            card.MouseEnter += (s, e) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF));
            card.MouseLeave += (s, e) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E));

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) }); // Preview Thumbnail Box
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Info
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Buttons

            // Thumbnail
            var thumb = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x28)),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var thumbText = new TextBlock
            {
                Text = "⚡ .KERF",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            thumb.Child = thumbText;
            Grid.SetRow(thumb, 0);
            grid.Children.Add(thumb);

            // Info
            var infoStack = new StackPanel();
            var titleTxt = new TextBlock
            {
                Text = name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            infoStack.Children.Add(titleTxt);

            var metaTxt = new TextBlock
            {
                Text = string.Format("{0:MM/dd HH:mm} | {1:0.0} KB", modified, bytes / 1024.0),
                FontSize = 9,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoStack.Children.Add(metaTxt);

            Grid.SetRow(infoStack, 1);
            grid.Children.Add(infoStack);

            // Action Buttons
            var btnDock = new DockPanel();
            var btnOpen = new Button
            {
                Content = "Open",
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Padding = new Thickness(10, 3, 10, 3),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnOpen.Click += (s, e) =>
            {
                LoadKerfFile(fullPath);
                currentFilePath = fullPath;
                projectName = name;
                titleBox.Text = projectName;
                isDirty = false;
                SwitchToView(AppView.Studio);
            };
            DockPanel.SetDock(btnOpen, Dock.Left);
            btnDock.Children.Add(btnOpen);

            var btnDel = new Button
            {
                Content = "Del",
                Background = Brushes.Transparent,
                Foreground = Brushes.Gray,
                FontSize = 10,
                Padding = new Thickness(6, 3, 6, 3),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnDel.Click += (s, e) =>
            {
                try
                {
                    if (MessageBox.Show("Delete " + name + ".kerf?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        File.Delete(fullPath);
                        RefreshHomeRecentProjects();
                    }
                }
                catch { }
            };
            DockPanel.SetDock(btnDel, Dock.Right);
            btnDock.Children.Add(btnDel);

            Grid.SetRow(btnDock, 2);
            grid.Children.Add(btnDock);

            card.Child = grid;
            return card;
        }

        private void StartNewBlankCanvas()
        {
            elements.Clear();
            selectedIds.Clear();
            projectName = "Untitled Project";
            titleBox.Text = projectName;
            currentFilePath = null;
            isDirty = false;
            SwitchToView(AppView.Studio);
        }

        // =============================================================
        //  VIEW 2: STUDIO CANVAS WORKSPACE VIEW
        // =============================================================

        private Grid CreateStudioView()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) }); // Top Nav with Window Controls
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Workspace
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Status Bar

            // Top Nav
            var topNav = CreateTopNav();
            Grid.SetRow(topNav, 0);
            grid.Children.Add(topNav);

            // Workspace (Left Layers, Center Canvas, Right Properties)
            var wsGrid = new Grid();
            wsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            wsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });

            var leftSidebar = CreateLeftSidebar();
            Grid.SetColumn(leftSidebar, 0);
            wsGrid.Children.Add(leftSidebar);

            var canvasContainer = CreateCanvasContainer();
            Grid.SetColumn(canvasContainer, 1);
            wsGrid.Children.Add(canvasContainer);

            var rightSidebar = CreateRightSidebar();
            Grid.SetColumn(rightSidebar, 2);
            wsGrid.Children.Add(rightSidebar);

            Grid.SetRow(wsGrid, 1);
            grid.Children.Add(wsGrid);

            // Status Bar
            var statusBar = CreateStatusBar();
            Grid.SetRow(statusBar, 2);
            grid.Children.Add(statusBar);

            return grid;
        }

        // ================= STUDIO TOP NAV =================

        private UIElement CreateTopNav()
        {
            var nav = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 46
            };
            nav.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2) ToggleMaximizeRestore(); else try { DragMove(); } catch { } };

            var dock = new DockPanel { LastChildFill = false };

            // Left: Home Button + Brand + Project Title Pill
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };

            // Home Button to return to Home Page Launcher
            var btnHome = new Button
            {
                Content = "🏠 Home",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x30)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Back to Home Page Launcher"
            };
            btnHome.Click += (s, e) => SwitchToView(AppView.Home);
            leftStack.Children.Add(btnHome);

            // Project Title Pill
            var pillBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x28)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            var pillStack = new StackPanel { Orientation = Orientation.Horizontal };

            statusDot = new TextBlock
            {
                Text = "●",
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            pillStack.Children.Add(statusDot);

            titleBox = new TextBox
            {
                Text = projectName,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 140
            };
            titleBox.TextChanged += (s, e) =>
            {
                projectName = titleBox.Text;
                isDirty = true;
                UpdateStatusDot(true);
            };
            pillStack.Children.Add(titleBox);

            var extBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x32, 0x32, 0x3C)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(6, 0, 0, 0)
            };
            extBadge.Child = new TextBlock { Text = ".kerf", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)) };
            pillStack.Children.Add(extBadge);

            pillBorder.Child = pillStack;
            leftStack.Children.Add(pillBorder);

            DockPanel.SetDock(leftStack, Dock.Left);
            dock.Children.Add(leftStack);

            // Center: Segmented Vector Tool Island
            var toolsIsland = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x27)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };

            var toolsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            AddToolIconButton(toolsStack, ToolType.Select, "Select Tool (V)", "M 2 2 L 2 17 L 6.5 13 L 9.5 19.5 L 12 18.5 L 9 12 L 15 12 Z", true, true);
            AddToolIconButton(toolsStack, ToolType.Hand, "Hand Tool (H)", "M 5 9 L 5 4 C 5 3 6 3 6 4 L 6 8 M 6 4 C 6 2.5 7.5 2.5 7.5 4 L 7.5 8 M 7.5 4 C 7.5 2.5 9 2.5 9 4 L 9 8 M 9 5 C 9 3.5 10.5 3.5 10.5 5 L 10.5 10 C 10.5 14 8 16 6 16 C 4 16 2 13 2 10 L 2 8 C 2 7 3 7 3 8 L 3 10", false, false);
            AddToolIconButton(toolsStack, ToolType.Frame, "Frame Tool (F)", "M 5 2 L 5 18 M 13 2 L 13 18 M 2 5 L 18 5 M 2 13 L 18 13", false, false);
            AddToolIconButton(toolsStack, ToolType.Rect, "Rectangle Tool (R)", "M 3 4 C 3 3.5 3.5 3 4 3 L 14 3 C 14.5 3 15 3.5 15 4 L 15 14 C 15 14.5 14.5 15 14 15 L 4 15 C 3.5 15 3 14.5 3 14 Z", false, false);
            AddToolIconButton(toolsStack, ToolType.Ellipse, "Ellipse Tool (O)", "M 9 2 A 7 7 0 1 1 8.99 2 Z", false, false);

            // Pen and Pencil
            AddToolIconButton(toolsStack, ToolType.Pen, "Pen Tool (P) — Vector Bézier Path", "M 12 2 L 16 6 L 8 14 L 3 15 L 4 10 Z M 7 11 L 11 7", false, false);
            AddToolIconButton(toolsStack, ToolType.Pencil, "Pencil Tool (B) — Freehand Vector Draw", "M 14 2.5 L 15.5 4 L 5.5 14 L 2.5 15.5 L 4 12.5 Z M 12 4.5 L 13.5 6", false, false);

            AddToolIconButton(toolsStack, ToolType.Star, "Star Tool (S)", "M 9 2 L 11.2 6.5 L 16 7.2 L 12.5 10.6 L 13.3 15.5 L 9 13.2 L 4.7 15.5 L 5.5 10.6 L 2 7.2 L 6.8 6.5 Z", true, false);
            AddToolIconButton(toolsStack, ToolType.Polygon, "Polygon / Triangle (T)", "M 9 3 L 16 15 L 2 15 Z", false, false);
            AddToolIconButton(toolsStack, ToolType.Text, "Text Tool (T)", "M 3 4 L 15 4 M 9 4 L 9 16 M 6 16 L 12 16", false, false);
            AddToolIconButton(toolsStack, ToolType.Line, "Line Tool (L)", "M 3 15 L 15 3", false, false);

            toolsIsland.Child = toolsStack;
            DockPanel.SetDock(toolsIsland, Dock.Left);
            dock.Children.Add(toolsIsland);

            // Right: Auto Layout, Save, Open, Export & Window Controls
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var btnAutoLayout = CreateNavActionButton("Auto Layout", "M 2 3 L 16 3 M 4 6 L 14 6 M 6 9 L 12 9", (s, e) => ApplyAutoLayoutToSelection());
            rightStack.Children.Add(btnAutoLayout);

            var btnOpen = CreateNavActionButton("Open .kerf", "M 3 5 L 8 5 L 10 7 L 15 7 C 16 7 16 8 16 9 L 14 15 L 2 15 L 2 6 C 2 5 2 5 3 5 Z", (s, e) => OpenProjectFileDialog());
            rightStack.Children.Add(btnOpen);

            var btnSave = CreateNavActionButton("Save .kerf", "M 3 3 L 13 3 L 16 6 L 16 15 C 16 16 15 16 15 16 L 3 16 C 2 16 2 15 2 14 Z M 5 3 L 5 7 L 12 7 L 12 3 M 5 11 L 13 11 L 13 15 L 5 15 Z", (s, e) => SaveProjectFileDialog());
            btnSave.Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF));
            rightStack.Children.Add(btnSave);

            var btnExport = CreateNavActionButton("Export", "M 9 2 L 9 12 M 5 8 L 9 12 L 13 8 M 3 14 L 15 14", (s, e) => ShowExportContextMenu(btnSave));
            rightStack.Children.Add(btnExport);

            var div = new Border { Width = 1, Height = 24, Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x3A)), Margin = new Thickness(8, 0, 8, 0) };
            rightStack.Children.Add(div);

            // Window Controls (Minimize, Maximize, Close with save warning)
            rightStack.Children.Add(CreateWindowControlButton("M 0 0 L 10 0", "Minimize", (s, e) => WindowState = WindowState.Minimized));
            rightStack.Children.Add(CreateWindowControlButton("M 1 1 L 9 1 L 9 9 L 1 9 Z", "Maximize", (s, e) => ToggleMaximizeRestore()));
            rightStack.Children.Add(CreateWindowControlButton("M 1 1 L 9 9 M 9 1 L 1 9", "Close", (s, e) => Close(), true));

            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

            nav.Child = dock;
            return nav;
        }

        private void AddToolIconButton(StackPanel parent, ToolType tool, string tooltip, string svgData, bool isFill, bool isDefault)
        {
            var btn = new Button
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(1),
                Background = isDefault ? new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)) : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = tooltip,
                Tag = tool,
                Cursor = Cursors.Hand
            };

            var iconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(svgData),
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isFill)
            {
                iconPath.Fill = Brushes.White;
                iconPath.Stroke = Brushes.Transparent;
            }
            else
            {
                iconPath.Stroke = Brushes.White;
                iconPath.StrokeThickness = 1.3;
                iconPath.Fill = Brushes.Transparent;
            }

            btn.Content = iconPath;
            btn.Click += (s, e) => SwitchTool(tool);

            toolButtons[tool] = btn;
            parent.Children.Add(btn);
        }

        private void SwitchTool(ToolType tool)
        {
            if (isPenDrawing && tool != ToolType.Pen)
            {
                FinalizePenPath();
            }

            currentTool = tool;

            foreach (var kvp in toolButtons)
            {
                var b = kvp.Value;
                bool isActive = (kvp.Key == currentTool);
                b.Background = isActive ? new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)) : Brushes.Transparent;

                var path = b.Content as System.Windows.Shapes.Path;
                if (path != null)
                {
                    var color = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xAB));
                    if (path.Fill != Brushes.Transparent) path.Fill = color;
                    if (path.Stroke != Brushes.Transparent) path.Stroke = color;
                }
            }

            if (toolHintBlock != null)
            {
                toolHintBlock.Text = "Active Tool: " + currentTool.ToString();
            }
        }

        private Button CreateNavActionButton(string label, string svgData, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(3, 0, 3, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            if (!string.IsNullOrEmpty(svgData))
            {
                var icon = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse(svgData),
                    Width = 12,
                    Height = 12,
                    Stretch = Stretch.Uniform,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.2,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                sp.Children.Add(icon);
            }

            var txt = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(txt);

            btn.Content = sp;
            btn.Click += handler;
            return btn;
        }

        private Button CreateWindowControlButton(string svgData, string tooltip, RoutedEventHandler handler, bool isClose = false)
        {
            var btn = new Button
            {
                Width = 38,
                Height = 32,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = tooltip,
                Cursor = Cursors.Hand
            };

            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(svgData),
                Width = 10,
                Height = 10,
                Stretch = Stretch.Uniform,
                Stroke = new SolidColorBrush(Color.FromRgb(0xC4, 0xC4, 0xCC)),
                StrokeThickness = 1.2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.Content = path;

            btn.MouseEnter += (s, e) =>
            {
                if (isClose)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
                    path.Stroke = Brushes.White;
                }
                else
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x36));
                    path.Stroke = Brushes.White;
                }
            };

            btn.MouseLeave += (s, e) =>
            {
                btn.Background = Brushes.Transparent;
                path.Stroke = new SolidColorBrush(Color.FromRgb(0xC4, 0xC4, 0xCC));
            };

            btn.Click += handler;
            return btn;
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void UpdateStatusDot(bool dirty)
        {
            if (statusDot != null)
            {
                statusDot.Foreground = dirty
                    ? new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B))
                    : new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
            }
        }

        // =============================================================
        //  LEFT SIDEBAR: PROPER LAYER SYSTEM UI WITH BORDERS & PADDING
        // =============================================================

        private UIElement CreateLeftSidebar()
        {
            var sidebar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) }); // Action Toolbar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Scrollable Cards List

            // 1. Header with Badge
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 0, 14, 0)
            };
            var headerDock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

            var headerTitle = new TextBlock
            {
                Text = "LAYERS & OBJECTS",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(headerTitle, Dock.Left);
            headerDock.Children.Add(headerTitle);

            layerCountBadge = new TextBlock
            {
                Text = "0 layers",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(layerCountBadge, Dock.Right);
            headerDock.Children.Add(layerCountBadge);

            header.Child = headerDock;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // 2. Action Toolbar with borders and padding
            var actionBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 5, 10, 5)
            };
            var actionStack = new StackPanel { Orientation = Orientation.Horizontal };

            actionStack.Children.Add(CreateSmallLayerButton("Del", (s, e) => DeleteSelectedElements()));
            actionStack.Children.Add(CreateSmallLayerButton("Duplicate (Ctrl+D)", (s, e) => DuplicateSelectedElements()));
            actionStack.Children.Add(CreateSmallLayerButton("↑ Up", (s, e) => MoveLayerOrder(1)));
            actionStack.Children.Add(CreateSmallLayerButton("↓ Down", (s, e) => MoveLayerOrder(-1)));

            actionBorder.Child = actionStack;
            Grid.SetRow(actionBorder, 1);
            grid.Children.Add(actionBorder);

            // 3. Scrollable StackPanel for Layer Cards
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 6, 0, 6)
            };

            layersContainer = new StackPanel();
            scroll.Content = layersContainer;

            Grid.SetRow(scroll, 2);
            grid.Children.Add(scroll);

            sidebar.Child = grid;
            return sidebar;
        }

        private Button CreateSmallLayerButton(string text, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 10,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btn.Click += handler;
            return btn;
        }

        private void UpdateLayersList()
        {
            if (layersContainer == null) return;
            layersContainer.Children.Clear();

            if (layerCountBadge != null)
            {
                layerCountBadge.Text = string.Format("{0} layer{1}", elements.Count, elements.Count == 1 ? "" : "s");
            }

            if (elements.Count == 0)
            {
                var emptyBorder = new Border
                {
                    Padding = new Thickness(16),
                    Margin = new Thickness(12),
                    Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1E)),
                    CornerRadius = new CornerRadius(8),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                    BorderThickness = new Thickness(1)
                };
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "No Layers Yet",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0xA8)),
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Select a shape or pen tool above to start",
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyBorder.Child = emptyStack;
                layersContainer.Children.Add(emptyBorder);
                return;
            }

            // Top to bottom in list = topmost to bottommost element
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                var el = elements[i];
                bool isSel = selectedIds.Contains(el.Id);

                // Layer Card Border with proper padding & borders
                var card = new Border
                {
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(0x20, 0x28, 0x38)) : new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x20)),
                    BorderBrush = isSel ? new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)) : new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x30)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(8, 2, 8, 2),
                    Padding = new Thickness(8, 5, 8, 5),
                    Cursor = Cursors.Hand,
                    Tag = el
                };

                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Icon badge
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name & Type
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); // Eye visibility
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); // Lock
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Delete

                // 1. Icon Badge
                var iconBadge = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(4),
                    Background = GetLayerTypeBrush(el.Type),
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var iconPath = CreateSmallIconForType(el.Type);
                iconBadge.Child = iconPath;
                Grid.SetColumn(iconBadge, 0);
                rowGrid.Children.Add(iconBadge);

                // 2. Name & Type Badge
                var nameStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 4, 0)
                };
                var nameTxt = new TextBlock
                {
                    Text = el.Name,
                    FontSize = 11,
                    FontWeight = isSel ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = isSel ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD6)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 110,
                    VerticalAlignment = VerticalAlignment.Center
                };
                nameStack.Children.Add(nameTxt);

                var typePill = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(3, 1, 3, 1),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                typePill.Child = new TextBlock
                {
                    Text = el.Type.ToString().ToUpper(),
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xAB))
                };
                nameStack.Children.Add(typePill);

                Grid.SetColumn(nameStack, 1);
                rowGrid.Children.Add(nameStack);

                // 3. Eye Button (Visibility)
                string targetId = el.Id;
                var btnEye = new Button
                {
                    Content = el.IsVisible ? "👁" : "⊘",
                    FontSize = 10,
                    Foreground = el.IsVisible ? new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xAB)) : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    ToolTip = el.IsVisible ? "Hide Layer" : "Show Layer",
                    Cursor = Cursors.Hand
                };
                btnEye.Click += (s, e) =>
                {
                    el.IsVisible = !el.IsVisible;
                    isDirty = true;
                    RenderCanvas();
                };
                Grid.SetColumn(btnEye, 2);
                rowGrid.Children.Add(btnEye);

                // 4. Lock Button
                var btnLock = new Button
                {
                    Content = el.IsLocked ? "🔒" : "🔓",
                    FontSize = 10,
                    Foreground = el.IsLocked ? new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)) : new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x78)),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    ToolTip = el.IsLocked ? "Unlock Layer" : "Lock Layer",
                    Cursor = Cursors.Hand
                };
                btnLock.Click += (s, e) =>
                {
                    el.IsLocked = !el.IsLocked;
                    isDirty = true;
                    RenderCanvas();
                };
                Grid.SetColumn(btnLock, 3);
                rowGrid.Children.Add(btnLock);

                // 5. Delete Button
                var btnDel = new Button
                {
                    Content = "✕",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x88)),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    ToolTip = "Delete Layer",
                    Cursor = Cursors.Hand
                };
                btnDel.MouseEnter += (s, e) => btnDel.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                btnDel.MouseLeave += (s, e) => btnDel.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x88));
                btnDel.Click += (s, e) =>
                {
                    elements.RemoveAll(x => x.Id == targetId);
                    selectedIds.Remove(targetId);
                    isDirty = true;
                    UpdatePropertyInspector();
                    RenderCanvas();
                };
                Grid.SetColumn(btnDel, 4);
                rowGrid.Children.Add(btnDel);

                // Click selection
                card.MouseLeftButtonDown += (s, e) =>
                {
                    if (!Keyboard.IsKeyDown(Key.LeftShift)) selectedIds.Clear();
                    selectedIds.Add(targetId);
                    UpdatePropertyInspector();
                    RenderCanvas();
                };

                card.Child = rowGrid;
                layersContainer.Children.Add(card);
            }
        }

        private Brush GetLayerTypeBrush(ElementType type)
        {
            switch (type)
            {
                case ElementType.Frame: return new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
                case ElementType.Rect: return new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF));
                case ElementType.Ellipse: return new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                case ElementType.Path: return new SolidColorBrush(Color.FromRgb(0xEC, 0x48, 0x99));
                case ElementType.Star: return new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                case ElementType.Text: return new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6));
                default: return new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            }
        }

        private UIElement CreateSmallIconForType(ElementType type)
        {
            string svg;
            bool isFill = false;
            switch (type)
            {
                case ElementType.Frame: svg = "M 3 1 L 3 13 M 9 1 L 9 13 M 1 3 L 13 3 M 1 9 L 13 9"; break;
                case ElementType.Rect: svg = "M 1 1 L 11 1 L 11 11 L 1 11 Z"; break;
                case ElementType.Ellipse: svg = "M 6 1 A 5 5 0 1 1 5.99 1 Z"; break;
                case ElementType.Path: svg = "M 8 1 L 11 4 L 5 10 L 2 11 L 3 8 Z"; break;
                case ElementType.Star: svg = "M 6 1 L 7.5 4.5 L 11 5 L 8.5 7.5 L 9 11 L 6 9.5 L 3 11 L 3.5 7.5 L 1 5 L 4.5 4.5 Z"; isFill = true; break;
                case ElementType.Text: svg = "M 1 2 L 11 2 M 6 2 L 6 11 M 4 11 L 8 11"; break;
                default: svg = "M 1 1 L 11 11"; break;
            }

            var p = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(svg),
                Width = 10,
                Height = 10,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (isFill) p.Fill = Brushes.White;
            else { p.Stroke = Brushes.White; p.StrokeThickness = 1.2; }
            return p;
        }

        // ================= CENTER CANVAS CONTAINER =================

        private UIElement CreateCanvasContainer()
        {
            mainContainer = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x17)),
                ClipToBounds = true,
                Focusable = true
            };

            canvas = new Canvas();
            mainContainer.Children.Add(canvas);

            // Canvas Mouse Events
            mainContainer.MouseDown += OnCanvasMouseDown;
            mainContainer.MouseMove += OnCanvasMouseMove;
            mainContainer.MouseUp += OnCanvasMouseUp;
            mainContainer.MouseWheel += OnCanvasMouseWheel;

            // Floating Zoom Pill Overlay
            var zoomPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(210, 30, 30, 35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x42)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 16, 16)
            };

            var zoomStack = new StackPanel { Orientation = Orientation.Horizontal };
            zoomStack.Children.Add(CreatePillButton("－", (s, e) => SetZoomLevel(zoom * 0.85)));
            zoomStack.Children.Add(CreatePillButton("100%", (s, e) => SetZoomLevel(1.0)));
            zoomStack.Children.Add(CreatePillButton("＋", (s, e) => SetZoomLevel(zoom * 1.15)));
            zoomStack.Children.Add(CreatePillButton("Fit", (s, e) => FitCanvasToElements()));
            zoomPill.Child = zoomStack;

            mainContainer.Children.Add(zoomPill);
            return mainContainer;
        }

        private Button CreatePillButton(string text, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 10,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand
            };
            btn.Click += handler;
            return btn;
        }

        // ================= RIGHT SIDEBAR (INSPECTOR) =================

        private UIElement CreateRightSidebar()
        {
            var sidebar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(1, 0, 0, 0)
            };

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            propertiesPanel = new StackPanel { Margin = new Thickness(14, 12, 14, 16) };

            // Alignment Card
            propertiesPanel.Children.Add(CreateAlignmentBar());

            // Geometry Card
            var geomCard = CreateInspectorCard("GEOMETRY & POSITION");
            var geomGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            geomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            geomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            geomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            geomGrid.RowDefinitions.Add(new RowDefinition());
            geomGrid.RowDefinitions.Add(new RowDefinition());

            txtX = CreatePropertyInput("X", geomGrid, 0, 0);
            txtY = CreatePropertyInput("Y", geomGrid, 0, 2);
            txtW = CreatePropertyInput("Width", geomGrid, 1, 0);
            txtH = CreatePropertyInput("Height", geomGrid, 1, 2);
            geomCard.Children.Add(geomGrid);

            var geomGrid2 = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            geomGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            geomGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            geomGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            geomGrid2.RowDefinitions.Add(new RowDefinition());

            txtRadius = CreatePropertyInput("Corner Radius", geomGrid2, 0, 0);
            txtOpacity = CreatePropertyInput("Opacity (0-1)", geomGrid2, 0, 2);
            geomCard.Children.Add(geomGrid2);
            propertiesPanel.Children.Add(geomCard);

            // Fill & Stroke Card
            var fillCard = CreateInspectorCard("FILL & STROKE");
            txtFill = CreateLabeledColorRow("Fill Color (Hex)", fillCard, "#0D99FF", out swatchFillPreview);
            txtStroke = CreateLabeledColorRow("Stroke Color (Hex)", fillCard, "#000000", out swatchStrokePreview);

            var strokeGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            strokeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            txtStrokeW = CreatePropertyInput("Stroke Width (px)", strokeGrid, 0, 0);
            fillCard.Children.Add(strokeGrid);

            // Quick Colors Row
            var paletteRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            string[] quickColors = new string[] { "#0D99FF", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#18181B", "#FFFFFF" };
            foreach (var col in quickColors)
            {
                var chip = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(4),
                    Background = ParseColorBrush(col),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = col
                };
                string capturedCol = col;
                chip.MouseLeftButtonDown += (s, e) =>
                {
                    txtFill.Text = capturedCol;
                    ApplyPropertiesFromInspector();
                };
                paletteRow.Children.Add(chip);
            }
            fillCard.Children.Add(paletteRow);
            propertiesPanel.Children.Add(fillCard);

            // Typography Card
            var typeCard = CreateInspectorCard("TYPOGRAPHY");
            txtText = CreateLabeledTextRow("Content Text", typeCard, "Kerf Design");
            var fontGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            fontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            txtFontSize = CreatePropertyInput("Font Size (pt)", fontGrid, 0, 0);
            typeCard.Children.Add(fontGrid);
            propertiesPanel.Children.Add(typeCard);

            // Auto Layout Card
            var alCard = CreateInspectorCard("FIGMA AUTO LAYOUT");
            chkAutoLayout = new CheckBox
            {
                Content = "Enable Auto Layout (Shift+A)",
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            chkAutoLayout.Checked += OnAutoLayoutSettingChanged;
            chkAutoLayout.Unchecked += OnAutoLayoutSettingChanged;
            alCard.Children.Add(chkAutoLayout);

            cmbLayoutMode = new ComboBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 8)
            };
            cmbLayoutMode.Items.Add("Horizontal Flow (->)");
            cmbLayoutMode.Items.Add("Vertical Column (↓)");
            cmbLayoutMode.SelectedIndex = 0;
            cmbLayoutMode.SelectionChanged += (s, e) => OnAutoLayoutSettingChanged(s, null);
            alCard.Children.Add(cmbLayoutMode);

            var alGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            alGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            alGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            alGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            alGrid.RowDefinitions.Add(new RowDefinition());

            txtSpacing = CreatePropertyInput("Item Gap", alGrid, 0, 0);
            txtPadding = CreatePropertyInput("Padding", alGrid, 0, 2);
            alCard.Children.Add(alGrid);
            propertiesPanel.Children.Add(alCard);

            // Live Code Inspect Card
            var codeCard = CreateInspectorCard("LIVE CODE INSPECT (CSS)");
            txtCodeInspect = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x36)),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 10,
                Height = 110,
                AcceptsReturn = true,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            codeCard.Children.Add(txtCodeInspect);

            var btnCopyCode = new Button
            {
                Content = "Copy CSS Code",
                FontSize = 10,
                Padding = new Thickness(0, 4, 0, 4),
                Background = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnCopyCode.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(txtCodeInspect.Text);
                    MessageBox.Show("CSS copied to clipboard!", "Kerf Inspector", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }
            };
            codeCard.Children.Add(btnCopyCode);
            propertiesPanel.Children.Add(codeCard);

            scroll.Content = propertiesPanel;
            sidebar.Child = scroll;
            return sidebar;
        }

        private UIElement CreateAlignmentBar()
        {
            var bar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x25)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            AddAlignButton(stack, "Align Left", "M 2 2 L 2 16 M 4 5 L 12 5 L 12 8 L 4 8 Z M 4 10 L 15 10 L 15 13 L 4 13 Z", () => AlignSelected(0));
            AddAlignButton(stack, "Align Center H", "M 9 2 L 9 16 M 4 5 L 14 5 L 14 8 L 4 8 Z M 6 10 L 12 10 L 12 13 L 6 13 Z", () => AlignSelected(1));
            AddAlignButton(stack, "Align Right", "M 16 2 L 16 16 M 6 5 L 14 5 L 14 8 L 6 8 Z M 3 10 L 14 10 L 14 13 L 3 13 Z", () => AlignSelected(2));
            AddAlignButton(stack, "Align Top", "M 2 2 L 16 2 M 5 4 L 8 4 L 8 12 L 5 12 Z M 10 4 L 13 4 L 13 15 L 10 15 Z", () => AlignSelected(3));
            AddAlignButton(stack, "Align Middle V", "M 2 9 L 16 9 M 5 4 L 8 4 L 8 14 L 5 14 Z M 10 6 L 13 6 L 13 12 L 10 12 Z", () => AlignSelected(4));
            AddAlignButton(stack, "Align Bottom", "M 2 16 L 16 16 M 5 6 L 8 6 L 8 14 L 5 14 Z M 10 3 L 13 3 L 13 14 L 10 14 Z", () => AlignSelected(5));

            bar.Child = stack;
            return bar;
        }

        private void AddAlignButton(StackPanel parent, string tip, string svgData, Action action)
        {
            var btn = new Button
            {
                Width = 28,
                Height = 24,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = tip,
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 2, 0)
            };

            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(svgData),
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
                Stroke = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xC4)),
                StrokeThickness = 1.2
            };
            btn.Content = path;
            btn.Click += (s, e) => action();
            parent.Children.Add(btn);
        }

        private void AlignSelected(int mode)
        {
            if (selectedIds.Count < 2) return;
            var sel = elements.Where(e => selectedIds.Contains(e.Id)).ToList();
            if (sel.Count == 0) return;

            switch (mode)
            {
                case 0:
                    double minX = sel.Min(e => e.X);
                    foreach (var el in sel) el.X = minX;
                    break;
                case 1:
                    double avgCenter = sel.Average(e => e.X + e.Width / 2);
                    foreach (var el in sel) el.X = avgCenter - el.Width / 2;
                    break;
                case 2:
                    double maxR = sel.Max(e => e.X + e.Width);
                    foreach (var el in sel) el.X = maxR - el.Width;
                    break;
                case 3:
                    double minY = sel.Min(e => e.Y);
                    foreach (var el in sel) el.Y = minY;
                    break;
                case 4:
                    double avgMid = sel.Average(e => e.Y + e.Height / 2);
                    foreach (var el in sel) el.Y = avgMid - el.Height / 2;
                    break;
                case 5:
                    double maxB = sel.Max(e => e.Y + e.Height);
                    foreach (var el in sel) el.Y = maxB - el.Height;
                    break;
            }

            isDirty = true;
            UpdatePropertyInspector();
            RenderCanvas();
        }

        private StackPanel CreateInspectorCard(string title)
        {
            var card = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            var header = new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            card.Children.Add(header);
            return card;
        }

        private TextBox CreatePropertyInput(string label, Grid grid, int row, int col)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            var lbl = new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x94)), Margin = new Thickness(0, 0, 0, 2) };
            var txt = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x28)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x32, 0x32, 0x3C)),
                FontSize = 11,
                Padding = new Thickness(5, 3, 5, 3)
            };
            txt.LostFocus += (s, e) => ApplyPropertiesFromInspector();
            txt.KeyDown += (s, e) => { if (e.Key == Key.Enter) ApplyPropertiesFromInspector(); };

            stack.Children.Add(lbl);
            stack.Children.Add(txt);

            Grid.SetRow(stack, row);
            Grid.SetColumn(stack, col);
            grid.Children.Add(stack);
            return txt;
        }

        private TextBox CreateLabeledTextRow(string label, StackPanel parent, string defaultVal)
        {
            var lbl = new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x94)), Margin = new Thickness(0, 2, 0, 2) };
            var txt = new TextBox
            {
                Text = defaultVal,
                Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x28)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x32, 0x32, 0x3C)),
                FontSize = 11,
                Padding = new Thickness(5, 3, 5, 3),
                Margin = new Thickness(0, 0, 0, 6)
            };
            txt.LostFocus += (s, e) => ApplyPropertiesFromInspector();
            txt.KeyDown += (s, e) => { if (e.Key == Key.Enter) ApplyPropertiesFromInspector(); };

            parent.Children.Add(lbl);
            parent.Children.Add(txt);
            return txt;
        }

        private TextBox CreateLabeledColorRow(string label, StackPanel parent, string defaultVal, out Border previewBox)
        {
            var lbl = new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x94)), Margin = new Thickness(0, 2, 0, 2) };

            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            previewBox = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                Background = ParseColorBrush(defaultVal),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x4C)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(previewBox, 0);
            row.Children.Add(previewBox);

            var txt = new TextBox
            {
                Text = defaultVal,
                Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x28)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x32, 0x32, 0x3C)),
                FontSize = 11,
                Padding = new Thickness(5, 3, 5, 3)
            };
            txt.LostFocus += (s, e) => ApplyPropertiesFromInspector();
            txt.KeyDown += (s, e) => { if (e.Key == Key.Enter) ApplyPropertiesFromInspector(); };
            Grid.SetColumn(txt, 2);
            row.Children.Add(txt);

            parent.Children.Add(lbl);
            parent.Children.Add(row);
            return txt;
        }

        private UIElement CreateStatusBar()
        {
            var bar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0x27, 0x2E)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 4, 14, 4)
            };

            var dock = new DockPanel();

            telemetryBlock = new TextBlock
            {
                Text = "DirectX 11 Engine | 60 FPS | Elements: 0",
                Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            DockPanel.SetDock(telemetryBlock, Dock.Left);
            dock.Children.Add(telemetryBlock);

            toolHintBlock = new TextBlock
            {
                Text = "Active Tool: Select (V) | Local Storage Enabled",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98)),
                FontSize = 11,
                Margin = new Thickness(24, 0, 0, 0)
            };
            DockPanel.SetDock(toolHintBlock, Dock.Left);
            dock.Children.Add(toolHintBlock);

            zoomBlock = new TextBlock
            {
                Text = "Zoom: 100%",
                Foreground = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xC4)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(zoomBlock, Dock.Right);
            dock.Children.Add(zoomBlock);

            bar.Child = dock;
            return bar;
        }

        // ================= RENDERING PIPELINE & VECTOR DRAWING =================

        private void RenderCanvas()
        {
            if (canvas == null) return;
            canvas.Children.Clear();

            // 1. Dotted Infinite Grid
            DrawGrid();

            // 2. Render Figma Elements
            foreach (var elem in elements)
            {
                if (!elem.IsVisible) continue;
                var visual = CreateElementVisual(elem);
                canvas.Children.Add(visual);
            }

            // 3. Render Live Pen Tool Overlay
            if (isPenDrawing && penPoints != null && penPoints.Count > 0)
            {
                RenderPenToolOverlay();
            }

            // 4. Update Selection Overlay Bounds & Handles
            UpdateSelectionOverlay();

            // 5. Update Layers List in Sidebar
            UpdateLayersList();

            frameCount++;
        }

        private void DrawGrid()
        {
            double step = 32.0 * zoom;
            if (step < 12) return;

            double startX = (panX % step);
            double startY = (panY % step);

            var group = new GeometryGroup();
            for (double x = startX; x < mainContainer.ActualWidth; x += step)
            {
                for (double y = startY; y < mainContainer.ActualHeight; y += step)
                {
                    group.Children.Add(new RectangleGeometry(new Rect(x, y, 1.5, 1.5)));
                }
            }

            var path = new System.Windows.Shapes.Path
            {
                Data = group,
                Fill = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                IsHitTestVisible = false
            };
            canvas.Children.Add(path);
        }

        private UIElement CreateElementVisual(FigmaElement el)
        {
            var container = new Canvas
            {
                Width = Math.Max(4, el.Width * zoom),
                Height = Math.Max(4, el.Height * zoom),
                Tag = el
            };

            double screenX = el.X * zoom + panX;
            double screenY = el.Y * zoom + panY;
            Canvas.SetLeft(container, screenX);
            Canvas.SetTop(container, screenY);

            var fillBrush = ParseColorBrush(el.Fill);
            var strokeBrush = el.StrokeWidth > 0 ? ParseColorBrush(el.Stroke) : Brushes.Transparent;

            Shape shape = null;

            switch (el.Type)
            {
                case ElementType.Rect:
                case ElementType.Frame:
                    var r = new Rectangle
                    {
                        Width = Math.Max(2, el.Width * zoom),
                        Height = Math.Max(2, el.Height * zoom),
                        Fill = fillBrush,
                        Stroke = strokeBrush,
                        StrokeThickness = el.StrokeWidth * zoom,
                        RadiusX = el.CornerRadius * zoom,
                        RadiusY = el.CornerRadius * zoom,
                        Opacity = el.Opacity
                    };
                    shape = r;
                    break;

                case ElementType.Ellipse:
                    var elp = new Ellipse
                    {
                        Width = Math.Max(2, el.Width * zoom),
                        Height = Math.Max(2, el.Height * zoom),
                        Fill = fillBrush,
                        Stroke = strokeBrush,
                        StrokeThickness = el.StrokeWidth * zoom,
                        Opacity = el.Opacity
                    };
                    shape = elp;
                    break;

                case ElementType.Star:
                    shape = CreateStarShape(el.Width * zoom, el.Height * zoom, fillBrush, strokeBrush, el.StrokeWidth * zoom);
                    break;

                case ElementType.Polygon:
                    shape = CreatePolygonShape(el.Width * zoom, el.Height * zoom, fillBrush, strokeBrush, el.StrokeWidth * zoom);
                    break;

                case ElementType.Line:
                case ElementType.Arrow:
                    var line = new Line
                    {
                        X1 = 0,
                        Y1 = 0,
                        X2 = el.Width * zoom,
                        Y2 = el.Height * zoom,
                        Stroke = fillBrush,
                        StrokeThickness = Math.Max(2, el.StrokeWidth * zoom),
                        Opacity = el.Opacity
                    };
                    shape = line;
                    break;

                case ElementType.Path:
                    var pathShape = CreateVectorPathShape(el);
                    container.Children.Add(pathShape);
                    break;

                case ElementType.Text:
                    var tb = new TextBlock
                    {
                        Text = el.Text ?? "Text",
                        Foreground = fillBrush,
                        FontSize = Math.Max(8, el.FontSize * zoom),
                        FontFamily = new FontFamily("Inter, Segoe UI, Arial"),
                        FontWeight = FontWeights.SemiBold,
                        Width = el.Width * zoom,
                        Height = el.Height * zoom,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = el.Opacity
                    };
                    container.Children.Add(tb);
                    break;
            }

            if (shape != null)
            {
                container.Children.Add(shape);
            }

            if (el.Type == ElementType.Frame)
            {
                var title = new TextBlock
                {
                    Text = el.Name,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x98)),
                    Margin = new Thickness(0, -18, 0, 0)
                };
                container.Children.Add(title);
            }

            return container;
        }

        private Shape CreateVectorPathShape(FigmaElement el)
        {
            var path = new System.Windows.Shapes.Path
            {
                Fill = el.IsClosed ? ParseColorBrush(el.Fill) : Brushes.Transparent,
                Stroke = ParseColorBrush(el.Stroke),
                StrokeThickness = Math.Max(1.5, el.StrokeWidth * zoom),
                Opacity = el.Opacity
            };

            if (el.Points != null && el.Points.Count > 0)
            {
                var geom = new PathGeometry();
                var fig = new PathFigure
                {
                    StartPoint = new Point((el.Points[0].X - el.X) * zoom, (el.Points[0].Y - el.Y) * zoom),
                    IsClosed = el.IsClosed
                };

                for (int i = 1; i < el.Points.Count; i++)
                {
                    fig.Segments.Add(new LineSegment(
                        new Point((el.Points[i].X - el.X) * zoom, (el.Points[i].Y - el.Y) * zoom),
                        true));
                }

                geom.Figures.Add(fig);
                path.Data = geom;
            }
            else if (!string.IsNullOrEmpty(el.SvgPathData))
            {
                try
                {
                    path.Data = Geometry.Parse(el.SvgPathData);
                }
                catch { }
            }

            return path;
        }

        private void RenderPenToolOverlay()
        {
            Point lastPt = penPoints[penPoints.Count - 1];
            double screenLastX = lastPt.X * zoom + panX;
            double screenLastY = lastPt.Y * zoom + panY;

            var rubberLine = new Line
            {
                X1 = screenLastX,
                Y1 = screenLastY,
                X2 = liveMousePos.X,
                Y2 = liveMousePos.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            canvas.Children.Add(rubberLine);

            for (int i = 0; i < penPoints.Count; i++)
            {
                Point pt = penPoints[i];
                double sx = pt.X * zoom + panX;
                double sy = pt.Y * zoom + panY;

                var node = new Rectangle
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.White,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                    StrokeThickness = 1.8
                };
                Canvas.SetLeft(node, sx - 4);
                Canvas.SetTop(node, sy - 4);
                canvas.Children.Add(node);

                if (i == 0 && penPoints.Count > 2)
                {
                    double dist = Math.Sqrt(Math.Pow(liveMousePos.X - sx, 2) + Math.Pow(liveMousePos.Y - sy, 2));
                    if (dist < 18)
                    {
                        var halo = new Ellipse
                        {
                            Width = 20,
                            Height = 20,
                            Stroke = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                            StrokeThickness = 2
                        };
                        Canvas.SetLeft(halo, sx - 10);
                        Canvas.SetTop(halo, sy - 10);
                        canvas.Children.Add(halo);
                    }
                }
            }
        }

        private Shape CreateStarShape(double w, double h, Brush fill, Brush stroke, double strokeW)
        {
            var poly = new Polygon
            {
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = strokeW
            };

            Point center = new Point(w / 2, h / 2);
            double rOuter = Math.Min(w, h) / 2;
            double rInner = rOuter * 0.45;

            for (int i = 0; i < 10; i++)
            {
                double angle = i * Math.PI / 5 - Math.PI / 2;
                double r = (i % 2 == 0) ? rOuter : rInner;
                poly.Points.Add(new Point(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle)));
            }

            return poly;
        }

        private Shape CreatePolygonShape(double w, double h, Brush fill, Brush stroke, double strokeW)
        {
            var poly = new Polygon
            {
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = strokeW
            };

            poly.Points.Add(new Point(w / 2, 0));
            poly.Points.Add(new Point(w, h));
            poly.Points.Add(new Point(0, h));

            return poly;
        }

        private void UpdateSelectionOverlay()
        {
            if (selectionBox != null)
            {
                canvas.Children.Remove(selectionBox);
                selectionBox = null;
            }

            if (selectedIds.Count == 0) return;

            var sel = elements.Where(e => selectedIds.Contains(e.Id)).ToList();
            if (sel.Count == 0) return;

            double minX = sel.Min(e => e.X);
            double minY = sel.Min(e => e.Y);
            double maxX = sel.Max(e => e.X + e.Width);
            double maxY = sel.Max(e => e.Y + e.Height);

            selectionBox = new Border
            {
                Width = (maxX - minX) * zoom + 8,
                Height = (maxY - minY) * zoom + 8,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                BorderThickness = new Thickness(1.5),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(selectionBox, minX * zoom + panX - 4);
            Canvas.SetTop(selectionBox, minY * zoom + panY - 4);
            canvas.Children.Add(selectionBox);
        }

        // ================= CANVAS MOUSE & DRAG LOGIC (FIXED) =================

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            mainContainer.Focus();
            Point mousePos = e.GetPosition(mainContainer);
            liveMousePos = mousePos;

            // Middle Mouse or Spacebar = Pan
            if (e.MiddleButton == MouseButtonState.Pressed || currentTool == ToolType.Hand)
            {
                isPanning = true;
                panStart = mousePos;
                mainContainer.CaptureMouse();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // 1. PEN TOOL
                if (currentTool == ToolType.Pen)
                {
                    double worldX = (mousePos.X - panX) / zoom;
                    double worldY = (mousePos.Y - panY) / zoom;
                    Point worldPt = new Point(worldX, worldY);

                    if (!isPenDrawing)
                    {
                        isPenDrawing = true;
                        penPoints = new List<Point> { worldPt };

                        activePenElement = new FigmaElement
                        {
                            Name = "Vector Path " + (elements.Count + 1),
                            Type = ElementType.Path,
                            X = worldX,
                            Y = worldY,
                            Width = 2,
                            Height = 2,
                            Stroke = "#0D99FF",
                            StrokeWidth = 2,
                            Fill = "#0D99FF",
                            Points = new List<PointData> { PointData.FromPoint(worldPt) },
                            IsClosed = false
                        };

                        elements.Add(activePenElement);
                        selectedIds.Clear();
                        selectedIds.Add(activePenElement.Id);
                    }
                    else
                    {
                        Point startPt = penPoints[0];
                        double dist = Math.Sqrt(Math.Pow(worldX - startPt.X, 2) + Math.Pow(worldY - startPt.Y, 2));

                        if (penPoints.Count > 2 && dist < (18 / zoom))
                        {
                            activePenElement.IsClosed = true;
                            FinalizePenPath();
                            return;
                        }

                        penPoints.Add(worldPt);
                        activePenElement.Points.Add(PointData.FromPoint(worldPt));
                        RecalculatePathBounds(activePenElement);
                    }

                    isDirty = true;
                    UpdateStatusDot(true);
                    RenderCanvas();
                    return;
                }

                // 2. PENCIL TOOL
                if (currentTool == ToolType.Pencil)
                {
                    isPencilDrawing = true;
                    double worldX = (mousePos.X - panX) / zoom;
                    double worldY = (mousePos.Y - panY) / zoom;
                    Point worldPt = new Point(worldX, worldY);

                    pencilPoints = new List<Point> { worldPt };
                    activePencilElement = new FigmaElement
                    {
                        Name = "Freehand Path " + (elements.Count + 1),
                        Type = ElementType.Path,
                        X = worldX,
                        Y = worldY,
                        Width = 2,
                        Height = 2,
                        Stroke = "#10B981",
                        StrokeWidth = 2.5,
                        Fill = "#00000000",
                        Points = new List<PointData> { PointData.FromPoint(worldPt) },
                        IsClosed = false
                    };
                    elements.Add(activePencilElement);
                    selectedIds.Clear();
                    selectedIds.Add(activePencilElement.Id);
                    mainContainer.CaptureMouse();
                    return;
                }

                // 3. SELECT TOOL: Moving Shapes AND Paths
                if (currentTool == ToolType.Select)
                {
                    var hit = HitTestElement(mousePos);
                    if (hit != null)
                    {
                        if (!selectedIds.Contains(hit.Id))
                        {
                            if (!Keyboard.IsKeyDown(Key.LeftShift)) selectedIds.Clear();
                            selectedIds.Add(hit.Id);
                        }

                        isDraggingElement = true;
                        dragStartPoint = mousePos;
                        initialDragPositions.Clear();
                        initialDragPoints.Clear();

                        foreach (var id in selectedIds)
                        {
                            var target = elements.FirstOrDefault(x => x.Id == id);
                            if (target != null)
                            {
                                initialDragPositions[id] = new Point(target.X, target.Y);

                                // Save points copy for vector paths so path and bounding box move in lockstep!
                                if (target.Points != null && target.Points.Count > 0)
                                {
                                    var ptsCopy = new List<PointData>();
                                    foreach (var pt in target.Points)
                                    {
                                        ptsCopy.Add(new PointData(pt.X, pt.Y));
                                    }
                                    initialDragPoints[id] = ptsCopy;
                                }
                            }
                        }

                        mainContainer.CaptureMouse();
                        UpdatePropertyInspector();
                        RenderCanvas();
                        return;
                    }
                    else
                    {
                        selectedIds.Clear();
                        UpdatePropertyInspector();
                        RenderCanvas();
                    }
                }
                else
                {
                    // 4. CREATION TOOLS (Rect, Ellipse, Frame, Star, Polygon, Text, Line)
                    // Properly anchor start point so dragging in any direction dynamically expands the shape & bounding box!
                    isCreating = true;
                    createStartPoint = mousePos;
                    double worldX = (mousePos.X - panX) / zoom;
                    double worldY = (mousePos.Y - panY) / zoom;

                    creatingElement = new FigmaElement
                    {
                        Name = currentTool.ToString(),
                        X = worldX,
                        Y = worldY,
                        Width = 4,
                        Height = 4,
                        Type = ToolToElementType(currentTool),
                        Fill = currentTool == ToolType.Frame ? "#1E1E22" : "#0D99FF"
                    };

                    elements.Add(creatingElement);
                    selectedIds.Clear();
                    selectedIds.Add(creatingElement.Id);
                    mainContainer.CaptureMouse();
                    RenderCanvas();
                }
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(mainContainer);
            liveMousePos = mousePos;

            if (isPanning)
            {
                panX += (mousePos.X - panStart.X);
                panY += (mousePos.Y - panStart.Y);
                panStart = mousePos;
                RenderCanvas();
                return;
            }

            if (isPenDrawing)
            {
                RenderCanvas();
                return;
            }

            if (isPencilDrawing && activePencilElement != null)
            {
                double worldX = (mousePos.X - panX) / zoom;
                double worldY = (mousePos.Y - panY) / zoom;
                Point worldPt = new Point(worldX, worldY);
                pencilPoints.Add(worldPt);
                activePencilElement.Points.Add(PointData.FromPoint(worldPt));
                RecalculatePathBounds(activePencilElement);
                RenderCanvas();
                return;
            }

            // DRAGGING EXISTING ELEMENTS (Shapes & Paths)
            if (isDraggingElement)
            {
                double dx = (mousePos.X - dragStartPoint.X) / zoom;
                double dy = (mousePos.Y - dragStartPoint.Y) / zoom;

                foreach (var id in selectedIds)
                {
                    var target = elements.FirstOrDefault(x => x.Id == id);
                    if (target != null && initialDragPositions.ContainsKey(id))
                    {
                        target.X = initialDragPositions[id].X + dx;
                        target.Y = initialDragPositions[id].Y + dy;

                        // Also translate path points so the vector and bounding box move together!
                        if (target.Points != null && initialDragPoints.ContainsKey(id))
                        {
                            var origPts = initialDragPoints[id];
                            for (int i = 0; i < target.Points.Count && i < origPts.Count; i++)
                            {
                                target.Points[i].X = origPts[i].X + dx;
                                target.Points[i].Y = origPts[i].Y + dy;
                            }
                        }
                    }
                }

                isDirty = true;
                UpdatePropertyInspector();
                RenderCanvas();
                return;
            }

            // CREATING SHAPES: Smooth resizing in any direction with real-time bounding box!
            if (isCreating && creatingElement != null)
            {
                double startWorldX = (createStartPoint.X - panX) / zoom;
                double startWorldY = (createStartPoint.Y - panY) / zoom;
                double curWorldX = (mousePos.X - panX) / zoom;
                double curWorldY = (mousePos.Y - panY) / zoom;

                creatingElement.X = Math.Min(startWorldX, curWorldX);
                creatingElement.Y = Math.Min(startWorldY, curWorldY);
                creatingElement.Width = Math.Max(4, Math.Abs(curWorldX - startWorldX));
                creatingElement.Height = Math.Max(4, Math.Abs(curWorldY - startWorldY));

                isDirty = true;
                RenderCanvas();
            }
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isPanning)
            {
                isPanning = false;
                mainContainer.ReleaseMouseCapture();
            }

            if (isPencilDrawing)
            {
                isPencilDrawing = false;
                activePencilElement = null;
                mainContainer.ReleaseMouseCapture();
                SwitchTool(ToolType.Select);
                RenderCanvas();
            }

            if (isDraggingElement)
            {
                isDraggingElement = false;
                mainContainer.ReleaseMouseCapture();
            }

            if (isCreating)
            {
                isCreating = false;
                creatingElement = null;
                SwitchTool(ToolType.Select);
                mainContainer.ReleaseMouseCapture();
                UpdatePropertyInspector();
                RenderCanvas();
            }
        }

        private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(mainContainer);
            double delta = e.Delta > 0 ? 1.15 : 0.87;
            double newZoom = Math.Max(0.1, Math.Min(10.0, zoom * delta));

            panX = mousePos.X - (mousePos.X - panX) * (newZoom / zoom);
            panY = mousePos.Y - (mousePos.Y - panY) * (newZoom / zoom);
            zoom = newZoom;

            zoomBlock.Text = string.Format("Zoom: {0:0}%", zoom * 100);
            RenderCanvas();
        }

        private void FinalizePenPath()
        {
            isPenDrawing = false;
            if (activePenElement != null)
            {
                RecalculatePathBounds(activePenElement);
                activePenElement = null;
            }
            SwitchTool(ToolType.Select);
            UpdatePropertyInspector();
            RenderCanvas();
        }

        private void RecalculatePathBounds(FigmaElement el)
        {
            if (el.Points == null || el.Points.Count == 0) return;
            double minX = el.Points.Min(p => p.X);
            double minY = el.Points.Min(p => p.Y);
            double maxX = el.Points.Max(p => p.X);
            double maxY = el.Points.Max(p => p.Y);

            el.X = minX;
            el.Y = minY;
            el.Width = Math.Max(4, maxX - minX);
            el.Height = Math.Max(4, maxY - minY);
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V) SwitchTool(ToolType.Select);
            else if (e.Key == Key.H) SwitchTool(ToolType.Hand);
            else if (e.Key == Key.F) SwitchTool(ToolType.Frame);
            else if (e.Key == Key.R) SwitchTool(ToolType.Rect);
            else if (e.Key == Key.O) SwitchTool(ToolType.Ellipse);
            else if (e.Key == Key.P) SwitchTool(ToolType.Pen);
            else if (e.Key == Key.B) SwitchTool(ToolType.Pencil);
            else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == 0) SwitchTool(ToolType.Star);
            else if (e.Key == Key.T) SwitchTool(ToolType.Text);
            else if (e.Key == Key.L) SwitchTool(ToolType.Line);
            else if ((e.Key == Key.Escape || e.Key == Key.Enter) && isPenDrawing)
            {
                FinalizePenPath();
            }
            else if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                DeleteSelectedElements();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.S) SaveProjectFileDialog();
                else if (e.Key == Key.O) OpenProjectFileDialog();
                else if (e.Key == Key.D) DuplicateSelectedElements();
            }
        }

        private FigmaElement HitTestElement(Point screenPos)
        {
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                var el = elements[i];
                if (!el.IsVisible || el.IsLocked) continue;

                double elScreenX = el.X * zoom + panX;
                double elScreenY = el.Y * zoom + panY;
                double elScreenW = el.Width * zoom;
                double elScreenH = el.Height * zoom;

                var rect = new Rect(elScreenX, elScreenY, elScreenW, elScreenH);
                if (rect.Contains(screenPos)) return el;
            }
            return null;
        }

        private ElementType ToolToElementType(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Frame: return ElementType.Frame;
                case ToolType.Rect: return ElementType.Rect;
                case ToolType.Ellipse: return ElementType.Ellipse;
                case ToolType.Star: return ElementType.Star;
                case ToolType.Polygon: return ElementType.Polygon;
                case ToolType.Text: return ElementType.Text;
                case ToolType.Line: return ElementType.Line;
                case ToolType.Pen:
                case ToolType.Pencil:
                    return ElementType.Path;
                default:
                    return ElementType.Rect;
            }
        }

        // ================= PROPERTY INSPECTOR LOGIC =================

        private void UpdatePropertyInspector()
        {
            if (selectedIds.Count == 0)
            {
                txtX.Text = "";
                txtY.Text = "";
                txtW.Text = "";
                txtH.Text = "";
                txtRadius.Text = "";
                txtOpacity.Text = "";
                txtStrokeW.Text = "";
                txtFontSize.Text = "";
                txtText.Text = "";
                txtSpacing.Text = "";
                txtPadding.Text = "";
                txtCodeInspect.Text = "/* Select an element to inspect CSS & properties */";
                return;
            }

            var el = elements.FirstOrDefault(e => e.Id == selectedIds[0]);
            if (el == null) return;

            txtX.Text = string.Format("{0:0}", el.X);
            txtY.Text = string.Format("{0:0}", el.Y);
            txtW.Text = string.Format("{0:0}", el.Width);
            txtH.Text = string.Format("{0:0}", el.Height);
            txtRadius.Text = string.Format("{0:0}", el.CornerRadius);
            txtOpacity.Text = string.Format("{0:0.00}", el.Opacity);
            txtFill.Text = el.Fill;
            txtStroke.Text = el.Stroke;
            txtStrokeW.Text = string.Format("{0:0}", el.StrokeWidth);
            txtFontSize.Text = string.Format("{0:0}", el.FontSize);
            txtText.Text = el.Text ?? "";
            txtSpacing.Text = string.Format("{0:0}", el.ItemSpacing);
            txtPadding.Text = string.Format("{0:0}", el.Padding);

            if (swatchFillPreview != null) swatchFillPreview.Background = ParseColorBrush(el.Fill);
            if (swatchStrokePreview != null) swatchStrokePreview.Background = ParseColorBrush(el.Stroke);

            chkAutoLayout.IsChecked = el.IsAutoLayout;
            cmbLayoutMode.SelectedIndex = el.LayoutMode == LayoutMode.Horizontal ? 0 : 1;

            var sb = new StringBuilder();
            sb.AppendLine(string.Format("/* {0} [{1}] */", el.Name, el.Type));
            sb.AppendLine(string.Format("width: {0:0}px;", el.Width));
            sb.AppendLine(string.Format("height: {0:0}px;", el.Height));
            sb.AppendLine(string.Format("background: {0};", el.Fill));
            if (el.CornerRadius > 0) sb.AppendLine(string.Format("border-radius: {0:0}px;", el.CornerRadius));
            if (el.StrokeWidth > 0) sb.AppendLine(string.Format("border: {0:0}px solid {1};", el.StrokeWidth, el.Stroke));
            if (el.Opacity < 1.0) sb.AppendLine(string.Format("opacity: {0:0.00};", el.Opacity));
            if (el.IsAutoLayout)
            {
                sb.AppendLine("display: flex;");
                sb.AppendLine(string.Format("flex-direction: {0};", el.LayoutMode == LayoutMode.Horizontal ? "row" : "column"));
                sb.AppendLine(string.Format("gap: {0:0}px;", el.ItemSpacing));
                sb.AppendLine(string.Format("padding: {0:0}px;", el.Padding));
            }
            txtCodeInspect.Text = sb.ToString();
        }

        private void ApplyPropertiesFromInspector()
        {
            if (selectedIds.Count == 0) return;
            var el = elements.FirstOrDefault(e => e.Id == selectedIds[0]);
            if (el == null) return;

            double val;
            if (double.TryParse(txtX.Text, out val)) el.X = val;
            if (double.TryParse(txtY.Text, out val)) el.Y = val;
            if (double.TryParse(txtW.Text, out val)) el.Width = Math.Max(4, val);
            if (double.TryParse(txtH.Text, out val)) el.Height = Math.Max(4, val);
            if (double.TryParse(txtRadius.Text, out val)) el.CornerRadius = Math.Max(0, val);
            if (double.TryParse(txtOpacity.Text, out val)) el.Opacity = Math.Max(0.0, Math.Min(1.0, val));
            if (double.TryParse(txtStrokeW.Text, out val)) el.StrokeWidth = Math.Max(0, val);
            if (double.TryParse(txtFontSize.Text, out val)) el.FontSize = Math.Max(6, val);
            if (double.TryParse(txtSpacing.Text, out val)) el.ItemSpacing = Math.Max(0, val);
            if (double.TryParse(txtPadding.Text, out val)) el.Padding = Math.Max(0, val);

            if (!string.IsNullOrEmpty(txtFill.Text)) el.Fill = txtFill.Text;
            if (!string.IsNullOrEmpty(txtStroke.Text)) el.Stroke = txtStroke.Text;
            el.Text = txtText.Text;

            if (swatchFillPreview != null) swatchFillPreview.Background = ParseColorBrush(el.Fill);
            if (swatchStrokePreview != null) swatchStrokePreview.Background = ParseColorBrush(el.Stroke);

            if (el.IsAutoLayout) RecalculateAutoLayout(el);

            isDirty = true;
            UpdateStatusDot(true);
            RenderCanvas();
        }

        private void OnAutoLayoutSettingChanged(object sender, RoutedEventArgs e)
        {
            if (selectedIds.Count == 0) return;
            var el = elements.FirstOrDefault(x => x.Id == selectedIds[0]);
            if (el == null) return;

            el.IsAutoLayout = chkAutoLayout.IsChecked == true;
            el.LayoutMode = cmbLayoutMode.SelectedIndex == 0 ? LayoutMode.Horizontal : LayoutMode.Vertical;
            RecalculateAutoLayout(el);
            isDirty = true;
            RenderCanvas();
        }

        private void ApplyAutoLayoutToSelection()
        {
            if (selectedIds.Count == 0) return;
            var el = elements.FirstOrDefault(x => x.Id == selectedIds[0]);
            if (el == null) return;

            el.IsAutoLayout = !el.IsAutoLayout;
            RecalculateAutoLayout(el);
            isDirty = true;
            UpdatePropertyInspector();
            RenderCanvas();
        }

        private void RecalculateAutoLayout(FigmaElement parent)
        {
            var children = elements.Where(e => e.ParentId == parent.Id).ToList();
            if (children.Count == 0) return;

            double curX = parent.X + parent.Padding;
            double curY = parent.Y + parent.Padding;

            foreach (var child in children)
            {
                child.X = curX;
                child.Y = curY;

                if (parent.LayoutMode == LayoutMode.Horizontal)
                    curX += child.Width + parent.ItemSpacing;
                else
                    curY += child.Height + parent.ItemSpacing;
            }

            if (parent.LayoutMode == LayoutMode.Horizontal)
                parent.Width = Math.Max(parent.Width, curX - parent.X + parent.Padding - parent.ItemSpacing);
            else
                parent.Height = Math.Max(parent.Height, curY - parent.Y + parent.Padding - parent.ItemSpacing);
        }

        private void DeleteSelectedElements()
        {
            if (selectedIds.Count == 0) return;
            elements.RemoveAll(e => selectedIds.Contains(e.Id));
            selectedIds.Clear();
            isDirty = true;
            UpdateStatusDot(true);
            UpdatePropertyInspector();
            RenderCanvas();
        }

        private void DuplicateSelectedElements()
        {
            if (selectedIds.Count == 0) return;
            var newIds = new List<string>();
            foreach (var id in selectedIds)
            {
                var original = elements.FirstOrDefault(e => e.Id == id);
                if (original != null)
                {
                    var clone = original.Clone();
                    elements.Add(clone);
                    newIds.Add(clone.Id);
                }
            }
            selectedIds = newIds;
            isDirty = true;
            UpdateStatusDot(true);
            UpdatePropertyInspector();
            RenderCanvas();
        }

        private void MoveLayerOrder(int delta)
        {
            if (selectedIds.Count == 0) return;
            string targetId = selectedIds[0];
            int idx = elements.FindIndex(e => e.Id == targetId);
            if (idx < 0) return;

            int newIdx = idx + delta;
            if (newIdx >= 0 && newIdx < elements.Count)
            {
                var el = elements[idx];
                elements.RemoveAt(idx);
                elements.Insert(newIdx, el);
                RenderCanvas();
            }
        }

        // ================= LOCAL STORAGE & .KERF PERSISTENCE =================

        private bool SaveProjectFileDialog()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Kerf Design Files (*.kerf)|*.kerf|All Files (*.*)|*.*",
                DefaultExt = ".kerf",
                FileName = projectName + ".kerf",
                InitialDirectory = localProjectsDir
            };

            if (dlg.ShowDialog() == true)
            {
                SaveKerfFile(dlg.FileName);
                currentFilePath = dlg.FileName;
                projectName = IOPath.GetFileNameWithoutExtension(dlg.FileName);
                titleBox.Text = projectName;
                isDirty = false;
                UpdateStatusDot(false);
                MessageBox.Show("Project successfully saved natively to:\n" + dlg.FileName, "Kerf Studio Save Success", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            return false;
        }

        private void OpenProjectFileDialog()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Kerf Design Files (*.kerf)|*.kerf|All Supported (*.kerf;*.json)|*.kerf;*.json",
                InitialDirectory = localProjectsDir
            };

            if (dlg.ShowDialog() == true)
            {
                LoadKerfFile(dlg.FileName);
                currentFilePath = dlg.FileName;
                projectName = IOPath.GetFileNameWithoutExtension(dlg.FileName);
                titleBox.Text = projectName;
                isDirty = false;
                UpdateStatusDot(false);
            }
        }

        private void SaveKerfFile(string filepath)
        {
            try
            {
                var model = new ProjectFileModel
                {
                    Name = projectName,
                    Timestamp = DateTime.UtcNow.Ticks,
                    PanX = panX,
                    PanY = panY,
                    Zoom = zoom,
                    Elements = elements
                };

                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                string json = serializer.Serialize(model);
                File.WriteAllText(filepath, json, Encoding.UTF8);

                // Mirror to Local Storage cache
                string localMirror = IOPath.Combine(localProjectsDir, IOPath.GetFileName(filepath));
                if (!filepath.Equals(localMirror, StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(localMirror, json, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving .kerf file: " + ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadKerfFile(string filepath)
        {
            try
            {
                string json = File.ReadAllText(filepath, Encoding.UTF8);
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var model = serializer.Deserialize<ProjectFileModel>(json);

                if (model != null && model.Elements != null)
                {
                    elements = model.Elements;
                    panX = model.PanX != 0 ? model.PanX : 140;
                    panY = model.PanY != 0 ? model.PanY : 90;
                    zoom = model.Zoom != 0 ? model.Zoom : 1.0;
                    if (!string.IsNullOrEmpty(model.Name)) projectName = model.Name;

                    titleBox.Text = projectName;
                    selectedIds.Clear();
                    UpdatePropertyInspector();
                    RenderCanvas();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading .kerf file: " + ex.Message, "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDefaultBankingTemplateToFile(string path)
        {
            LoadDefaultTemplate();
            SaveKerfFile(path);
        }

        // ================= TEMPLATES & EXPORT =================

        private void LoadBankingTemplate()
        {
            LoadDefaultTemplate();
            projectName = "Mobile Banking UI";
            titleBox.Text = projectName;
            currentFilePath = null;
            isDirty = true;
            SwitchToView(AppView.Studio);
        }

        private void LoadVectorPathTemplate()
        {
            elements.Clear();
            projectName = "Vector Pen Art";
            titleBox.Text = projectName;

            var wave = new FigmaElement
            {
                Name = "Vector Wave",
                Type = ElementType.Path,
                X = 140,
                Y = 180,
                Width = 420,
                Height = 120,
                Stroke = "#0D99FF",
                StrokeWidth = 3,
                Fill = "#0D99FF",
                Points = new List<PointData>
                {
                    new PointData(140, 240),
                    new PointData(220, 180),
                    new PointData(300, 260),
                    new PointData(380, 200),
                    new PointData(480, 250),
                    new PointData(560, 190)
                },
                IsClosed = false
            };
            elements.Add(wave);

            var star = new FigmaElement
            {
                Name = "Accent Star",
                Type = ElementType.Star,
                X = 580,
                Y = 160,
                Width = 60,
                Height = 60,
                Fill = "#10B981"
            };
            elements.Add(star);

            currentFilePath = null;
            isDirty = true;
            SwitchToView(AppView.Studio);
        }

        private void LoadDefaultTemplate()
        {
            elements.Clear();

            // Mobile Banking Card Frame
            var card = new FigmaElement
            {
                Name = "Mobile Banking Card",
                Type = ElementType.Frame,
                X = 140,
                Y = 80,
                Width = 340,
                Height = 200,
                CornerRadius = 18,
                Fill = "#1A1A22",
                Stroke = "#2C2C36",
                StrokeWidth = 1,
                IsAutoLayout = true,
                LayoutMode = LayoutMode.Vertical,
                ItemSpacing = 16,
                Padding = 24
            };
            elements.Add(card);

            var chip = new FigmaElement
            {
                Name = "Chip Icon",
                Type = ElementType.Rect,
                X = 164,
                Y = 104,
                Width = 44,
                Height = 32,
                CornerRadius = 6,
                Fill = "#F59E0B",
                ParentId = card.Id
            };
            elements.Add(chip);

            var balance = new FigmaElement
            {
                Name = "Balance Header",
                Type = ElementType.Text,
                Text = "$84,290.50 USD",
                FontSize = 26,
                X = 164,
                Y = 152,
                Width = 260,
                Height = 36,
                Fill = "#FFFFFF",
                ParentId = card.Id
            };
            elements.Add(balance);

            var cardNum = new FigmaElement
            {
                Name = "Card Number",
                Type = ElementType.Text,
                Text = "**** **** **** 4892",
                FontSize = 14,
                X = 164,
                Y = 204,
                Width = 220,
                Height = 24,
                Fill = "#A1A1AA",
                ParentId = card.Id
            };
            elements.Add(cardNum);

            RecalculateAutoLayout(card);

            // Primary Action Button
            var actionBtn = new FigmaElement
            {
                Name = "Primary Action Button",
                Type = ElementType.Rect,
                X = 520,
                Y = 80,
                Width = 180,
                Height = 48,
                CornerRadius = 24,
                Fill = "#0D99FF"
            };
            elements.Add(actionBtn);

            var btnText = new FigmaElement
            {
                Name = "Button Label",
                Type = ElementType.Text,
                Text = "Transfer Funds",
                FontSize = 14,
                X = 545,
                Y = 94,
                Width = 140,
                Height = 24,
                Fill = "#FFFFFF"
            };
            elements.Add(btnText);

            var star = new FigmaElement
            {
                Name = "Reward Star",
                Type = ElementType.Star,
                X = 560,
                Y = 160,
                Width = 90,
                Height = 90,
                Fill = "#10B981"
            };
            elements.Add(star);

            var samplePath = new FigmaElement
            {
                Name = "Sample Vector Wave",
                Type = ElementType.Path,
                X = 140,
                Y = 320,
                Width = 280,
                Height = 80,
                Stroke = "#6366F1",
                StrokeWidth = 3,
                Fill = "#6366F1",
                Points = new List<PointData>
                {
                    new PointData(140, 360),
                    new PointData(200, 330),
                    new PointData(260, 380),
                    new PointData(340, 320),
                    new PointData(420, 370)
                },
                IsClosed = false
            };
            elements.Add(samplePath);

            RenderCanvas();
        }

        private void SetZoomLevel(double z)
        {
            zoom = Math.Max(0.1, Math.Min(10.0, z));
            zoomBlock.Text = string.Format("Zoom: {0:0}%", zoom * 100);
            RenderCanvas();
        }

        private void FitCanvasToElements()
        {
            if (elements.Count == 0) return;
            double minX = elements.Min(e => e.X);
            double minY = elements.Min(e => e.Y);
            double maxX = elements.Max(e => e.X + e.Width);
            double maxY = elements.Max(e => e.Y + e.Height);

            double w = maxX - minX;
            double h = maxY - minY;

            zoom = Math.Min(mainContainer.ActualWidth / (w + 100), mainContainer.ActualHeight / (h + 100));
            zoom = Math.Max(0.2, Math.Min(3.0, zoom));

            panX = (mainContainer.ActualWidth - w * zoom) / 2 - minX * zoom;
            panY = (mainContainer.ActualHeight - h * zoom) / 2 - minY * zoom;

            zoomBlock.Text = string.Format("Zoom: {0:0}%", zoom * 100);
            RenderCanvas();
        }

        private void ShowExportContextMenu(Button anchor)
        {
            var menu = new ContextMenu();

            var itemPng = new MenuItem { Header = "Export High-Res PNG (.png)" };
            itemPng.Click += (s, e) => ExportCanvasAsPng();
            menu.Items.Add(itemPng);

            var itemSvg = new MenuItem { Header = "Export Vector SVG (.svg)" };
            itemSvg.Click += (s, e) => ExportCanvasAsSvg();
            menu.Items.Add(itemSvg);

            menu.PlacementTarget = anchor;
            menu.IsOpen = true;
        }

        private void ExportCanvasAsPng()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                DefaultExt = ".png",
                FileName = projectName + ".png"
            };

            if (dlg.ShowDialog() == true)
            {
                int w = (int)Math.Max(800, mainContainer.ActualWidth);
                int h = (int)Math.Max(600, mainContainer.ActualHeight);

                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(canvas);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var fs = File.OpenWrite(dlg.FileName))
                {
                    encoder.Save(fs);
                }

                MessageBox.Show("High-Resolution PNG exported natively to:\n" + dlg.FileName, "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ExportCanvasAsSvg()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "SVG Vector File (*.svg)|*.svg",
                DefaultExt = ".svg",
                FileName = projectName + ".svg"
            };

            if (dlg.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.AppendLine("<svg width=\"1440\" height=\"900\" xmlns=\"http://www.w3.org/2000/svg\">");
                sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#121214\" />");

                foreach (var el in elements)
                {
                    if (el.Type == ElementType.Rect || el.Type == ElementType.Frame)
                    {
                        sb.AppendLine(string.Format("  <rect x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"{3}\" rx=\"{4}\" fill=\"{5}\" />",
                            el.X, el.Y, el.Width, el.Height, el.CornerRadius, el.Fill));
                    }
                    else if (el.Type == ElementType.Ellipse)
                    {
                        sb.AppendLine(string.Format("  <ellipse cx=\"{0}\" cy=\"{1}\" rx=\"{2}\" ry=\"{3}\" fill=\"{4}\" />",
                            el.X + el.Width / 2, el.Y + el.Height / 2, el.Width / 2, el.Height / 2, el.Fill));
                    }
                    else if (el.Type == ElementType.Text)
                    {
                        sb.AppendLine(string.Format("  <text x=\"{0}\" y=\"{1}\" fill=\"{2}\" font-size=\"{3}\" font-family=\"Inter, Arial\">{4}</text>",
                            el.X, el.Y + el.FontSize, el.Fill, el.FontSize, el.Text));
                    }
                    else if (el.Type == ElementType.Path && el.Points != null && el.Points.Count > 1)
                    {
                        var psb = new StringBuilder();
                        psb.Append(string.Format("M {0} {1} ", el.Points[0].X, el.Points[0].Y));
                        for (int i = 1; i < el.Points.Count; i++)
                        {
                            psb.Append(string.Format("L {0} {1} ", el.Points[i].X, el.Points[i].Y));
                        }
                        if (el.IsClosed) psb.Append("Z");
                        sb.AppendLine(string.Format("  <path d=\"{0}\" stroke=\"{1}\" stroke-width=\"{2}\" fill=\"{3}\" />",
                            psb.ToString(), el.Stroke, el.StrokeWidth, el.IsClosed ? el.Fill : "none"));
                    }
                }

                sb.AppendLine("</svg>");
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("SVG Vector exported to:\n" + dlg.FileName, "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void StartTelemetry()
        {
            telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
            telemetryTimer.Tick += (s, e) =>
            {
                double seconds = (DateTime.Now - lastFpsCheck).TotalSeconds;
                currentFps = frameCount / Math.Max(0.1, seconds);
                frameCount = 0;
                lastFpsCheck = DateTime.Now;

                if (telemetryBlock != null)
                {
                    telemetryBlock.Text = string.Format(
                        "DirectX GPU Engine | FPS: {0:0.0} | Elements: {1} | Selected: {2}",
                        currentFps, elements.Count, selectedIds.Count);
                }
            };
            telemetryTimer.Start();
        }

        private void StartAutosave()
        {
            autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.0) };
            autosaveTimer.Tick += (s, e) =>
            {
                if (isDirty)
                {
                    SaveKerfFile(autosavePath);
                    isDirty = false;
                    UpdateStatusDot(false);
                }
            };
            autosaveTimer.Start();
        }

        private Brush ParseColorBrush(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex) || hex.Equals("transparent", StringComparison.OrdinalIgnoreCase))
                    return Brushes.Transparent;
                if (!hex.StartsWith("#")) hex = "#" + hex;
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF));
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            var app = new Application();
            string initialFile = args != null && args.Length > 0 ? args[0] : null;
            var win = new MainWindow(initialFile);
            app.Run(win);
        }
    }
}
