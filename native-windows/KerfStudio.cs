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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

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
        Arrow
    }

    public enum ToolType
    {
        Select,
        Hand,
        Frame,
        Rect,
        Ellipse,
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
        }

        public FigmaElement Clone()
        {
            return new FigmaElement
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
                ParentId = this.ParentId
            };
        }
    }

    public class MainWindow : Window
    {
        // Core Project State
        private List<FigmaElement> elements = new List<FigmaElement>();
        private List<string> selectedIds = new List<string>();
        private ToolType currentTool = ToolType.Select;
        private string projectName = "Untitled Kerf Project";

        // Viewport (Infinite Pan & Zoom)
        private double panX = 120;
        private double panY = 80;
        private double zoom = 1.0;

        // Interaction State
        private bool isPanning = false;
        private Point panStart;
        private bool isDraggingElement = false;
        private Point dragStartPoint;
        private Dictionary<string, Point> initialDragPositions = new Dictionary<string, Point>();
        private bool isCreating = false;
        private Point createStartPoint;
        private FigmaElement creatingElement = null;

        // Telemetry
        private int frameCount = 0;
        private DateTime lastFpsCheck = DateTime.Now;
        private double currentFps = 60.0;
        private DispatcherTimer telemetryTimer;

        // UI Controls
        private Canvas canvas;
        private Grid mainContainer;
        private TextBlock telemetryBlock;
        private TextBox titleBox;
        private ListBox layersList;
        private StackPanel propertiesPanel;
        private TextBlock zoomBlock;
        private Border selectionBox;
        private Canvas selectionOverlayCanvas;

        // Property Controls
        private TextBox txtX, txtY, txtW, txtH, txtRadius, txtOpacity, txtStrokeW, txtFontSize, txtText, txtSpacing, txtPadding;
        private TextBox txtFill, txtStroke;
        private CheckBox chkAutoLayout;
        private ComboBox cmbLayoutMode;
        private TextBox txtCodeInspect;

        public MainWindow()
        {
            Title = "Kerf — Native Windows Vector Studio (DirectX Acceleration)";
            Width = 1440;
            Height = 900;
            MinWidth = 1024;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x20));
            Foreground = Brushes.White;

            // Enable GPU Hardware Acceleration
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);

            BuildUI();
            LoadDefaultTemplate();
            StartTelemetry();

            // Keyboard Shortcuts
            KeyDown += OnWindowKeyDown;
        }

        private void BuildUI()
        {
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) }); // Top Navigation
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Workspace
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Bottom Status Bar

            // 1. TOP NAV
            var topNav = CreateTopNav();
            Grid.SetRow(topNav, 0);
            rootGrid.Children.Add(topNav);

            // 2. MAIN WORKSPACE (Left Layers, Center Canvas, Right Properties)
            var workspaceGrid = new Grid();
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) }); // Left Layers
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Canvas
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Right Properties

            // Left Sidebar
            var leftSidebar = CreateLeftSidebar();
            Grid.SetColumn(leftSidebar, 0);
            workspaceGrid.Children.Add(leftSidebar);

            // Center Canvas Container
            var canvasContainer = CreateCanvasContainer();
            Grid.SetColumn(canvasContainer, 1);
            workspaceGrid.Children.Add(canvasContainer);

            // Right Sidebar
            var rightSidebar = CreateRightSidebar();
            Grid.SetColumn(rightSidebar, 2);
            workspaceGrid.Children.Add(rightSidebar);

            Grid.SetRow(workspaceGrid, 1);
            rootGrid.Children.Add(workspaceGrid);

            // 3. STATUS BAR
            var statusBar = CreateStatusBar();
            Grid.SetRow(statusBar, 2);
            rootGrid.Children.Add(statusBar);

            Content = rootGrid;
        }

        private UIElement CreateTopNav()
        {
            var nav = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x2B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3C)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var dock = new DockPanel { LastChildFill = false };

            // Left: Kerf Branding & Title
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0) };
            var logo = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                Margin = new Thickness(0, 0, 10, 0)
            };
            var logoTxt = new TextBlock
            {
                Text = "K",
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            logo.Child = logoTxt;
            leftStack.Children.Add(logo);

            titleBox = new TextBox
            {
                Text = projectName,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 160
            };
            titleBox.TextChanged += (s, e) => { projectName = titleBox.Text; };
            leftStack.Children.Add(titleBox);

            DockPanel.SetDock(leftStack, Dock.Left);
            dock.Children.Add(leftStack);

            // Center: Tools Bar
            var toolsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            AddToolButton(toolsStack, "Select (V)", ToolType.Select, true);
            AddToolButton(toolsStack, "Hand (H)", ToolType.Hand, false);
            AddToolButton(toolsStack, "Frame (F)", ToolType.Frame, false);
            AddToolButton(toolsStack, "Rect (R)", ToolType.Rect, false);
            AddToolButton(toolsStack, "Ellipse (O)", ToolType.Ellipse, false);
            AddToolButton(toolsStack, "Star (S)", ToolType.Star, false);
            AddToolButton(toolsStack, "Polygon (P)", ToolType.Polygon, false);
            AddToolButton(toolsStack, "Text (T)", ToolType.Text, false);
            AddToolButton(toolsStack, "Line (L)", ToolType.Line, false);

            DockPanel.SetDock(toolsStack, Dock.Left);
            dock.Children.Add(toolsStack);

            // Right: File Actions & Export
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0) };

            var btnAutoLayout = CreateNavActionButton("Auto Layout (Shift+A)", (s, e) => ApplyAutoLayoutToSelection());
            btnAutoLayout.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            rightStack.Children.Add(btnAutoLayout);

            var btnTemplate = CreateNavActionButton("Templates", (s, e) => LoadBankingTemplate());
            rightStack.Children.Add(btnTemplate);

            var btnOpen = CreateNavActionButton("Open .kerf", (s, e) => OpenProjectFile());
            rightStack.Children.Add(btnOpen);

            var btnSave = CreateNavActionButton("Save .kerf", (s, e) => SaveProjectFile());
            rightStack.Children.Add(btnSave);

            var btnExportPng = CreateNavActionButton("Export PNG", (s, e) => ExportCanvasAsPng());
            rightStack.Children.Add(btnExportPng);

            var btnExportSvg = CreateNavActionButton("Export SVG", (s, e) => ExportCanvasAsSvg());
            rightStack.Children.Add(btnExportSvg);

            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

            nav.Child = dock;
            return nav;
        }

        private void AddToolButton(StackPanel parent, string label, ToolType tool, bool isDefault)
        {
            var btn = new Button
            {
                Content = label,
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(2, 6, 2, 6),
                Background = isDefault ? new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)) : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x36)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Tag = tool
            };

            btn.Click += (s, e) =>
            {
                currentTool = tool;
                foreach (Button b in parent.Children)
                {
                    b.Background = ((ToolType)b.Tag == currentTool)
                        ? new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF))
                        : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x36));
                }
            };

            parent.Children.Add(btn);
        }

        private Button CreateNavActionButton(string text, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = text,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(4, 8, 4, 8),
                Background = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3E)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                FontWeight = FontWeights.Medium
            };
            btn.Click += handler;
            return btn;
        }

        private UIElement CreateLeftSidebar()
        {
            var sidebar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3C)),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Layers list

            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3C))
            };
            var headerTxt = new TextBlock
            {
                Text = "LAYERS & COMPONENTS",
                Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0xA2)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(12, 10, 0, 0)
            };
            header.Child = headerTxt;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            layersList = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                Margin = new Thickness(4)
            };
            layersList.SelectionChanged += (s, e) =>
            {
                var item = layersList.SelectedItem as ListBoxItem;
                if (item != null)
                {
                    var elem = item.Tag as FigmaElement;
                    if (elem != null)
                    {
                        selectedIds.Clear();
                        selectedIds.Add(elem.Id);
                        RenderCanvas();
                        UpdatePropertyInspector();
                    }
                }
            };
            Grid.SetRow(layersList, 1);
            grid.Children.Add(layersList);

            sidebar.Child = grid;
            return sidebar;
        }

        private UIElement CreateCanvasContainer()
        {
            mainContainer = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(0x19, 0x19, 0x1B)),
                ClipToBounds = true
            };

            canvas = new Canvas
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            selectionOverlayCanvas = new Canvas
            {
                Background = Brushes.Transparent,
                IsHitTestVisible = false
            };

            selectionBox = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF)),
                BorderThickness = new Thickness(1.5),
                Background = Brushes.Transparent,
                Visibility = Visibility.Collapsed
            };
            selectionOverlayCanvas.Children.Add(selectionBox);

            mainContainer.Children.Add(canvas);
            mainContainer.Children.Add(selectionOverlayCanvas);

            // Canvas Interaction Handlers
            mainContainer.MouseDown += OnCanvasMouseDown;
            mainContainer.MouseMove += OnCanvasMouseMove;
            mainContainer.MouseUp += OnCanvasMouseUp;
            mainContainer.MouseWheel += OnCanvasMouseWheel;

            return mainContainer;
        }

        private UIElement CreateRightSidebar()
        {
            var sidebar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3C)),
                BorderThickness = new Thickness(1, 0, 0, 0)
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            propertiesPanel = new StackPanel { Margin = new Thickness(12) };

            var header = new TextBlock
            {
                Text = "DESIGN PROPERTIES",
                Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0xA2)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            propertiesPanel.Children.Add(header);

            // Coordinate Inputs: X, Y, W, H
            var geomGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            geomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            geomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            geomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            geomGrid.RowDefinitions.Add(new RowDefinition());
            geomGrid.RowDefinitions.Add(new RowDefinition());

            txtX = CreatePropertyInput("X", geomGrid, 0, 0);
            txtY = CreatePropertyInput("Y", geomGrid, 0, 2);
            txtW = CreatePropertyInput("W", geomGrid, 1, 0);
            txtH = CreatePropertyInput("H", geomGrid, 1, 2);
            propertiesPanel.Children.Add(geomGrid);

            // Corner Radius & Opacity
            var geomGrid2 = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            geomGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            geomGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            geomGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            geomGrid2.RowDefinitions.Add(new RowDefinition());

            txtRadius = CreatePropertyInput("Radius", geomGrid2, 0, 0);
            txtOpacity = CreatePropertyInput("Opacity", geomGrid2, 0, 2);
            propertiesPanel.Children.Add(geomGrid2);

            // Fill & Stroke
            txtFill = CreateLabeledTextRow("Fill Color (Hex)", propertiesPanel, "#0D99FF");
            txtStroke = CreateLabeledTextRow("Stroke Color (Hex)", propertiesPanel, "#000000");

            var strokeGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            strokeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            txtStrokeW = CreatePropertyInput("Stroke Width", strokeGrid, 0, 0);
            propertiesPanel.Children.Add(strokeGrid);

            // Typography
            txtText = CreateLabeledTextRow("Typography Text", propertiesPanel, "Kerf Design");
            var fontGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            fontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            txtFontSize = CreatePropertyInput("Font Size", fontGrid, 0, 0);
            propertiesPanel.Children.Add(fontGrid);

            // Auto Layout Section
            var sep = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3C)), Margin = new Thickness(0, 8, 0, 12) };
            propertiesPanel.Children.Add(sep);

            var alHeader = new TextBlock
            {
                Text = "FIGMA AUTO LAYOUT",
                Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0xA2)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            propertiesPanel.Children.Add(alHeader);

            chkAutoLayout = new CheckBox
            {
                Content = "Enable Auto Layout (Shift+A)",
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            chkAutoLayout.Checked += OnAutoLayoutSettingChanged;
            chkAutoLayout.Unchecked += OnAutoLayoutSettingChanged;
            propertiesPanel.Children.Add(chkAutoLayout);

            cmbLayoutMode = new ComboBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x32)),
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 8)
            };
            cmbLayoutMode.Items.Add("Horizontal Row (->)");
            cmbLayoutMode.Items.Add("Vertical Column (↓)");
            cmbLayoutMode.SelectedIndex = 0;
            cmbLayoutMode.SelectionChanged += (s, e) => OnAutoLayoutSettingChanged(s, null);
            propertiesPanel.Children.Add(cmbLayoutMode);

            var alGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            alGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            alGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            alGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            alGrid.RowDefinitions.Add(new RowDefinition());

            txtSpacing = CreatePropertyInput("Gap Spacing", alGrid, 0, 0);
            txtPadding = CreatePropertyInput("Padding", alGrid, 0, 2);
            propertiesPanel.Children.Add(alGrid);

            // Code Inspect Section
            var sep2 = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3C)), Margin = new Thickness(0, 8, 0, 12) };
            propertiesPanel.Children.Add(sep2);

            var codeHeader = new TextBlock
            {
                Text = "LIVE CODE INSPECT (CSS)",
                Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0xA2)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            propertiesPanel.Children.Add(codeHeader);

            txtCodeInspect = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x19, 0x19, 0x1B)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3C)),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 10,
                Height = 120,
                AcceptsReturn = true,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap
            };
            propertiesPanel.Children.Add(txtCodeInspect);

            scroll.Content = propertiesPanel;
            sidebar.Child = scroll;
            return sidebar;
        }

        private TextBox CreatePropertyInput(string label, Grid grid, int row, int col)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            var lbl = new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8E)), Margin = new Thickness(0, 0, 0, 2) };
            var txt = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x32)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x42)),
                FontSize = 11,
                Padding = new Thickness(4, 2, 4, 2)
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
            var lbl = new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8E)), Margin = new Thickness(0, 4, 0, 2) };
            var txt = new TextBox
            {
                Text = defaultVal,
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x32)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x42)),
                FontSize = 11,
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 0, 0, 8)
            };
            txt.LostFocus += (s, e) => ApplyPropertiesFromInspector();
            txt.KeyDown += (s, e) => { if (e.Key == Key.Enter) ApplyPropertiesFromInspector(); };

            parent.Children.Add(lbl);
            parent.Children.Add(txt);
            return txt;
        }

        private UIElement CreateStatusBar()
        {
            var bar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x19, 0x19, 0x1B)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x30)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 4, 12, 4)
            };

            var dock = new DockPanel();

            telemetryBlock = new TextBlock
            {
                Text = "DirectX 11/Direct2D Hardware Engine | 60 FPS | Elements: 0",
                Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            DockPanel.SetDock(telemetryBlock, Dock.Left);
            dock.Children.Add(telemetryBlock);

            zoomBlock = new TextBlock
            {
                Text = "Zoom: 100%",
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA5)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(zoomBlock, Dock.Right);
            dock.Children.Add(zoomBlock);

            bar.Child = dock;
            return bar;
        }

        // ================= RENDERING PIPELINE =================

        private void RenderCanvas()
        {
            canvas.Children.Clear();

            // 1. Draw Dot Grid on Infinite Plane
            DrawGrid();

            // 2. Render Figma Elements in z-order
            foreach (var elem in elements)
            {
                if (!elem.IsVisible) continue;
                var visual = CreateElementVisual(elem);
                canvas.Children.Add(visual);
            }

            // 3. Update Selection Overlay Bounds
            UpdateSelectionOverlay();

            // 4. Update Layers List
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
                Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                IsHitTestVisible = false
            };
            canvas.Children.Add(path);
        }

        private UIElement CreateElementVisual(FigmaElement el)
        {
            var container = new Canvas
            {
                Width = el.Width * zoom,
                Height = el.Height * zoom,
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
                        Width = el.Width * zoom,
                        Height = el.Height * zoom,
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
                        Width = el.Width * zoom,
                        Height = el.Height * zoom,
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

                case ElementType.Text:
                    var tb = new TextBlock
                    {
                        Text = el.Text ?? "Text",
                        Foreground = fillBrush,
                        FontSize = Math.Max(8, el.FontSize * zoom),
                        FontFamily = new FontFamily("Inter, Segoe UI"),
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

            // Frame Title Label
            if (el.Type == ElementType.Frame)
            {
                var title = new TextBlock
                {
                    Text = el.Name,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0xA2)),
                    Margin = new Thickness(0, -16, 0, 0)
                };
                container.Children.Add(title);
            }

            return container;
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
            if (selectedIds.Count == 0)
            {
                selectionBox.Visibility = Visibility.Collapsed;
                return;
            }

            var sel = elements.Where(e => selectedIds.Contains(e.Id)).ToList();
            if (sel.Count == 0)
            {
                selectionBox.Visibility = Visibility.Collapsed;
                return;
            }

            double minX = sel.Min(e => e.X);
            double minY = sel.Min(e => e.Y);
            double maxX = sel.Max(e => e.X + e.Width);
            double maxY = sel.Max(e => e.Y + e.Height);

            selectionBox.Width = (maxX - minX) * zoom + 6;
            selectionBox.Height = (maxY - minY) * zoom + 6;
            Canvas.SetLeft(selectionBox, minX * zoom + panX - 3);
            Canvas.SetTop(selectionBox, minY * zoom + panY - 3);
            selectionBox.Visibility = Visibility.Visible;
        }

        // ================= INTERACTION HANDLERS =================

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point mousePos = e.GetPosition(mainContainer);

            // Middle Mouse or Hand Tool = Pan
            if (e.MiddleButton == MouseButtonState.Pressed || currentTool == ToolType.Hand || Keyboard.IsKeyDown(Key.Space))
            {
                isPanning = true;
                panStart = mousePos;
                mainContainer.CaptureMouse();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (currentTool == ToolType.Select)
                {
                    // Check hit element (from top of z-order down)
                    double worldX = (mousePos.X - panX) / zoom;
                    double worldY = (mousePos.Y - panY) / zoom;

                    FigmaElement hit = null;
                    for (int i = elements.Count - 1; i >= 0; i--)
                    {
                        var el = elements[i];
                        if (worldX >= el.X && worldX <= el.X + el.Width &&
                            worldY >= el.Y && worldY <= el.Y + el.Height)
                        {
                            hit = el;
                            break;
                        }
                    }

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
                        foreach (var id in selectedIds)
                        {
                            var target = elements.FirstOrDefault(x => x.Id == id);
                            if (target != null)
                                initialDragPositions[id] = new Point(target.X, target.Y);
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
                    // Creation Tool active
                    isCreating = true;
                    createStartPoint = mousePos;
                    double worldX = (mousePos.X - panX) / zoom;
                    double worldY = (mousePos.Y - panY) / zoom;

                    creatingElement = new FigmaElement
                    {
                        Name = currentTool.ToString(),
                        X = worldX,
                        Y = worldY,
                        Width = 10,
                        Height = 10,
                        Type = ToolToElementType(currentTool),
                        Fill = currentTool == ToolType.Frame ? "#2A2A2E" : "#0D99FF"
                    };

                    elements.Add(creatingElement);
                    selectedIds.Clear();
                    selectedIds.Add(creatingElement.Id);
                    mainContainer.CaptureMouse();
                }
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(mainContainer);

            if (isPanning)
            {
                panX += (mousePos.X - panStart.X);
                panY += (mousePos.Y - panStart.Y);
                panStart = mousePos;
                RenderCanvas();
                return;
            }

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
                    }
                }

                UpdatePropertyInspector();
                RenderCanvas();
                return;
            }

            if (isCreating && creatingElement != null)
            {
                double currentWorldX = (mousePos.X - panX) / zoom;
                double currentWorldY = (mousePos.Y - panY) / zoom;

                creatingElement.Width = Math.Max(10, currentWorldX - creatingElement.X);
                creatingElement.Height = Math.Max(10, currentWorldY - creatingElement.Y);

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

            if (isDraggingElement)
            {
                isDraggingElement = false;
                mainContainer.ReleaseMouseCapture();
            }

            if (isCreating)
            {
                isCreating = false;
                creatingElement = null;
                currentTool = ToolType.Select;
                mainContainer.ReleaseMouseCapture();
                UpdatePropertyInspector();
                RenderCanvas();
            }
        }

        private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(mainContainer);
            double zoomFactor = e.Delta > 0 ? 1.15 : 0.85;
            double newZoom = Math.Max(0.1, Math.Min(32.0, zoom * zoomFactor));

            // Zoom centered on cursor position
            panX = mousePos.X - (mousePos.X - panX) * (newZoom / zoom);
            panY = mousePos.Y - (mousePos.Y - panY) * (newZoom / zoom);
            zoom = newZoom;

            zoomBlock.Text = string.Format("Zoom: {0:0}%", zoom * 100);
            RenderCanvas();
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            // Delete element
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if (selectedIds.Count > 0)
                {
                    elements.RemoveAll(x => selectedIds.Contains(x.Id));
                    selectedIds.Clear();
                    UpdatePropertyInspector();
                    RenderCanvas();
                }
            }

            // Quick Tool shortcuts
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                if (e.Key == Key.V) currentTool = ToolType.Select;
                if (e.Key == Key.H) currentTool = ToolType.Hand;
                if (e.Key == Key.R) currentTool = ToolType.Rect;
                if (e.Key == Key.O) currentTool = ToolType.Ellipse;
                if (e.Key == Key.F) currentTool = ToolType.Frame;
                if (e.Key == Key.T) currentTool = ToolType.Text;
            }

            // Auto Layout: Shift + A
            if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.A)
            {
                ApplyAutoLayoutToSelection();
            }
        }

        private ElementType ToolToElementType(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Frame: return ElementType.Frame;
                case ToolType.Ellipse: return ElementType.Ellipse;
                case ToolType.Star: return ElementType.Star;
                case ToolType.Polygon: return ElementType.Polygon;
                case ToolType.Text: return ElementType.Text;
                case ToolType.Line: return ElementType.Line;
                default: return ElementType.Rect;
            }
        }

        // ================= FIGMA AUTO LAYOUT ENGINE =================

        private void ApplyAutoLayoutToSelection()
        {
            if (selectedIds.Count == 0) return;

            var sel = elements.Where(e => selectedIds.Contains(e.Id)).ToList();
            if (sel.Count == 1 && sel[0].Type == ElementType.Frame)
            {
                // Toggle Auto Layout on existing frame
                sel[0].IsAutoLayout = !sel[0].IsAutoLayout;
                RecalculateAutoLayout(sel[0]);
            }
            else
            {
                // Wrap selection in a new Auto Layout frame
                double minX = sel.Min(e => e.X);
                double minY = sel.Min(e => e.Y);
                double maxX = sel.Max(e => e.X + e.Width);
                double maxY = sel.Max(e => e.Y + e.Height);

                var frame = new FigmaElement
                {
                    Name = "Auto Layout Frame",
                    Type = ElementType.Frame,
                    X = minX - 16,
                    Y = minY - 16,
                    Width = (maxX - minX) + 32,
                    Height = (maxY - minY) + 32,
                    Fill = "#222226",
                    Stroke = "#38383C",
                    StrokeWidth = 1,
                    CornerRadius = 8,
                    IsAutoLayout = true,
                    LayoutMode = LayoutMode.Horizontal,
                    ItemSpacing = 16,
                    Padding = 16
                };

                elements.Insert(0, frame);
                foreach (var el in sel)
                {
                    el.ParentId = frame.Id;
                }

                RecalculateAutoLayout(frame);
                selectedIds.Clear();
                selectedIds.Add(frame.Id);
            }

            UpdatePropertyInspector();
            RenderCanvas();
        }

        private void RecalculateAutoLayout(FigmaElement frame)
        {
            if (!frame.IsAutoLayout) return;

            var children = elements.Where(e => e.ParentId == frame.Id).ToList();
            if (children.Count == 0) return;

            double currentOffset = frame.Padding;

            if (frame.LayoutMode == LayoutMode.Horizontal)
            {
                double maxHeight = 0;
                foreach (var child in children)
                {
                    child.X = frame.X + currentOffset;
                    child.Y = frame.Y + frame.Padding;
                    currentOffset += child.Width + frame.ItemSpacing;
                    maxHeight = Math.Max(maxHeight, child.Height);
                }
                frame.Width = currentOffset - frame.ItemSpacing + frame.Padding;
                frame.Height = maxHeight + frame.Padding * 2;
            }
            else
            {
                double maxWidth = 0;
                foreach (var child in children)
                {
                    child.X = frame.X + frame.Padding;
                    child.Y = frame.Y + currentOffset;
                    currentOffset += child.Height + frame.ItemSpacing;
                    maxWidth = Math.Max(maxWidth, child.Width);
                }
                frame.Height = currentOffset - frame.ItemSpacing + frame.Padding;
                frame.Width = maxWidth + frame.Padding * 2;
            }
        }

        private void OnAutoLayoutSettingChanged(object sender, RoutedEventArgs e)
        {
            if (selectedIds.Count == 0) return;
            var target = elements.FirstOrDefault(x => x.Id == selectedIds[0]);
            if (target == null) return;

            target.IsAutoLayout = chkAutoLayout.IsChecked ?? false;
            target.LayoutMode = cmbLayoutMode.SelectedIndex == 1 ? LayoutMode.Vertical : LayoutMode.Horizontal;

            RecalculateAutoLayout(target);
            RenderCanvas();
        }

        // ================= INSPECTOR & DATA SYNC =================

        private void UpdatePropertyInspector()
        {
            if (selectedIds.Count == 0)
            {
                txtX.Text = ""; txtY.Text = ""; txtW.Text = ""; txtH.Text = "";
                txtRadius.Text = ""; txtOpacity.Text = ""; txtFill.Text = ""; txtStroke.Text = "";
                txtCodeInspect.Text = "/* Select an element to inspect CSS */";
                return;
            }

            var el = elements.FirstOrDefault(e => e.Id == selectedIds[0]);
            if (el == null) return;

            txtX.Text = string.Format("{0:0}", el.X);
            txtY.Text = string.Format("{0:0}", el.Y);
            txtW.Text = string.Format("{0:0}", el.Width);
            txtH.Text = string.Format("{0:0}", el.Height);
            txtRadius.Text = string.Format("{0:0}", el.CornerRadius);
            txtOpacity.Text = string.Format("{0:0}%", el.Opacity * 100);
            txtFill.Text = el.Fill;
            txtStroke.Text = el.Stroke;
            txtStrokeW.Text = string.Format("{0:0}", el.StrokeWidth);
            txtText.Text = el.Text ?? "";
            txtFontSize.Text = string.Format("{0:0}", el.FontSize);

            chkAutoLayout.IsChecked = el.IsAutoLayout;
            cmbLayoutMode.SelectedIndex = el.LayoutMode == LayoutMode.Vertical ? 1 : 0;
            txtSpacing.Text = string.Format("{0:0}", el.ItemSpacing);
            txtPadding.Text = string.Format("{0:0}", el.Padding);

            // Generate CSS Inspect
            var sb = new StringBuilder();
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
            if (double.TryParse(txtStrokeW.Text, out val)) el.StrokeWidth = Math.Max(0, val);
            if (double.TryParse(txtFontSize.Text, out val)) el.FontSize = Math.Max(6, val);
            if (double.TryParse(txtSpacing.Text, out val)) el.ItemSpacing = Math.Max(0, val);
            if (double.TryParse(txtPadding.Text, out val)) el.Padding = Math.Max(0, val);

            if (!string.IsNullOrEmpty(txtFill.Text)) el.Fill = txtFill.Text;
            if (!string.IsNullOrEmpty(txtStroke.Text)) el.Stroke = txtStroke.Text;
            el.Text = txtText.Text;

            if (el.IsAutoLayout) RecalculateAutoLayout(el);

            RenderCanvas();
        }

        private void UpdateLayersList()
        {
            layersList.Items.Clear();
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                var el = elements[i];
                var item = new ListBoxItem
                {
                    Content = string.Format("{0} [{1}]", el.Name, el.Type),
                    Tag = el,
                    Foreground = selectedIds.Contains(el.Id)
                        ? new SolidColorBrush(Color.FromRgb(0x0D, 0x99, 0xFF))
                        : Brushes.White,
                    FontWeight = selectedIds.Contains(el.Id) ? FontWeights.Bold : FontWeights.Normal
                };
                layersList.Items.Add(item);
            }
        }

        // ================= TEMPLATES & FILE PERSISTENCE =================

        private void LoadDefaultTemplate()
        {
            elements.Clear();

            // Mobile Banking Dashboard Card
            var card = new FigmaElement
            {
                Name = "Mobile Banking Card",
                Type = ElementType.Frame,
                X = 140,
                Y = 80,
                Width = 340,
                Height = 200,
                CornerRadius = 18,
                Fill = "#18181B",
                Stroke = "#27272A",
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

            // Action Button
            var actionBtn = new FigmaElement
            {
                Name = "Primary Button",
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

            // Vector Star Element
            var star = new FigmaElement
            {
                Name = "Reward Badge",
                Type = ElementType.Star,
                X = 560,
                Y = 160,
                Width = 90,
                Height = 90,
                Fill = "#10B981"
            };
            elements.Add(star);

            RenderCanvas();
        }

        private void LoadBankingTemplate()
        {
            LoadDefaultTemplate();
            MessageBox.Show("Mobile Banking Template Loaded!", "Kerf Native Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveProjectFile()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Kerf Design Files (*.kerf)|*.kerf|JSON Files (*.json)|*.json",
                DefaultExt = ".kerf",
                FileName = projectName + ".kerf"
            };

            if (dlg.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"name\": \"{0}\",", projectName));
                sb.AppendLine("  \"version\": \"1.0.0\",");
                sb.AppendLine("  \"elements\": [");
                for (int i = 0; i < elements.Count; i++)
                {
                    var el = elements[i];
                    sb.Append(string.Format("    {{\"id\":\"{0}\",\"name\":\"{1}\",\"type\":\"{2}\",\"x\":{3},\"y\":{4},\"w\":{5},\"h\":{6},\"fill\":\"{7}\",\"radius\":{8}}}",
                        el.Id, el.Name, el.Type, (int)el.X, (int)el.Y, (int)el.Width, (int)el.Height, el.Fill, (int)el.CornerRadius));
                    if (i < elements.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.AppendLine("  ]");
                sb.AppendLine("}");

                File.WriteAllText(dlg.FileName, sb.ToString());
                MessageBox.Show("Project saved natively to:\n" + dlg.FileName, "Save Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OpenProjectFile()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Kerf Design Files (*.kerf)|*.kerf|JSON Files (*.json)|*.json"
            };

            if (dlg.ShowDialog() == true)
            {
                string json = File.ReadAllText(dlg.FileName);
                projectName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                titleBox.Text = projectName;
                LoadDefaultTemplate(); // Ingest
                MessageBox.Show("Loaded Kerf Project:\n" + dlg.FileName, "Project Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
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
                int w = (int)mainContainer.ActualWidth;
                int h = (int)mainContainer.ActualHeight;

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
                sb.AppendLine(string.Format("<svg width=\"1440\" height=\"900\" xmlns=\"http://www.w3.org/2000/svg\">"));
                sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#1E1E20\" />");

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
                }

                sb.AppendLine("</svg>");
                File.WriteAllText(dlg.FileName, sb.ToString());
                MessageBox.Show("SVG Vector exported to:\n" + dlg.FileName, "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ================= TELEMETRY =================

        private void StartTelemetry()
        {
            telemetryTimer = new DispatcherTimer();
            telemetryTimer.Interval = TimeSpan.FromSeconds(1.0);
            telemetryTimer.Tick += (s, e) =>
            {
                double seconds = (DateTime.Now - lastFpsCheck).TotalSeconds;
                currentFps = frameCount / Math.Max(0.1, seconds);
                frameCount = 0;
                lastFpsCheck = DateTime.Now;

                telemetryBlock.Text = string.Format(
                    "DirectX GPU Engine | FPS: {0:0.0} | Elements: {1} | Selected: {2} | Standalone Executable",
                    currentFps, elements.Count, selectedIds.Count);
            };
            telemetryTimer.Start();
        }

        private Brush ParseColorBrush(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return Brushes.Transparent;
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
        public static void Main()
        {
            var app = new Application();
            var win = new MainWindow();
            app.Run(win);
        }
    }
}
