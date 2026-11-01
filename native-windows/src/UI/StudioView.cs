using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Interop;
using Kerf.Engine;
using Kerf.Engine.Animation;
using AnimTrack = Kerf.Engine.Animation.Track;
using Kerf.IO;
using Kerf.Model;
using Kerf.Tools;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace Kerf.UI
{
    public enum DropPosition
    {
        None,
        Above,
        Below,
        Inside,
        Before = Above,
        After = Below
    }

    public class StudioView : UserControl
    {
        public event Action RequestHomeNavigation;
        public event Action<string> DocumentSaved;
        // Studio Mode (Design vs Animate)
        public enum StudioMode { Design, Animate }
        private StudioMode _currentStudioMode = StudioMode.Design;
        private Border _designTabButton;
        private Border _animateTabButton;
        private FrameworkElement _timelinePanel;
        private AnimationTimelineController _timelineController;
        private DispatcherTimer _animationTimer;

        // Timeline UI components
        private TextBlock _timelinePlayBtnText;
        private TextBlock _timelineTimeText;
        private Slider _timelineScrubber;
        private TextBlock _timelineClipText;
        private TextBlock _timelineLoopText;
        private TextBlock _timelineSpeedText;
        private StackPanel _timelineTracksStack;
        private Canvas _timelineCanvasHost;
        private FrameworkElement _timelineTracksContainer;
        private FrameworkElement _stateMachineContainer;
        private bool _isStateMachineMode = false;
        private double _timelinePixelsPerSecond = 260.0;
        private bool _isScrubbingTimeline = false;

        // Left Sidebar Tabs (Layers vs Assets)
        private bool _isAssetsTabActive = false;
        private Border _layersTabBtn;
        private Border _assetsTabBtn;
        private FrameworkElement _layersViewContainer;
        private FrameworkElement _assetsViewContainer;
        private StackPanel _assetsListStack;
        private TextBox _assetsSearchBox;
        private TextBlock _assetsCountBlock;

        // Interactive Vector State Machine Canvas Binding
        private uint _lastHoverNodeId = 0;
        private bool _interactiveCanvasEnabled = true;
        private TextBlock _stateMachineStatusBadge;
        private StackPanel _stateMachineInputsStack;
        private StackPanel _stateMachineStatesStack;
        private Border _canvasInteractBtn;
        private TextBlock _canvasInteractText;

        // Document & Engine
        public KerfDocument Document { get; private set; }
        public string CurrentFilePath { get; private set; }
        public CommandHistory History { get; private set; }

        private GpuRenderer _renderer;
        private RetainedSceneGraph _sceneGraph;
        private RetainedCanvasHost _canvasHost;

        // Viewport & Pan/Zoom
        private double _zoomScale = 1.0;
        private Point _panOffset = new Point(100, 80);
        public double ZoomScale { get { return _zoomScale; } set { _zoomScale = value; } }
        public Point PanOffset { get { return _panOffset; } set { _panOffset = value; } }
        private bool _isPanning = false;
        private Point _lastMousePos;
        private readonly List<LayerNode> _clipboardLayers = new List<LayerNode>();
        private Point _lastRightClickCanvasPoint = new Point(100, 100);

        // Selection & Tools
        private ToolType _activeTool = ToolType.Select;
        private readonly List<LayerNode> _selectedLayers = new List<LayerNode>();
        private HandleType _activeHandle = HandleType.None;
        private Rect _transformStartBounds;
        private double _transformStartRot;
        private Point _transformStartMouse;
        private Point _dragStartPoint;
        private bool _isTransforming = false;
        private bool _isDrawingNew = false;
        private LayerNode _drawingNode = null;

        // Pen tool & Vector Node Editing state
        private VectorGeometry _activePenGeometry = null;
        private LayerNode _activePenNode = null;
        private bool _isDraggingPenCurve = false;
        private int _activePenNodeIndex = -1;
        private Point _penHoverCanvasPoint = new Point(0, 0);
        private bool _hasPenHoverPoint = false;

        // Vector / Node Edit Mode
        private bool _isVectorEditing = false;
        private LayerNode _editingVectorNode = null;
        private int _selectedNodeIndex = -1;
        private bool _isDraggingNodeAnchor = false;
        private bool _isDraggingNodeHandle = false;
        private bool _draggingOutHandle = false;
        private bool _isDraggingSegment = false;
        private int _draggingSegmentIndex = -1;

        // Corner Radius interaction
        private double _startCornerRadius = 0.0;

        // Figma-style Gradient On-Canvas Rollers & Handles State
        private bool _isDraggingGradientHandle = false;
        private GradientHandleType _activeGradientHandle = GradientHandleType.None;
        private int _activeGradientStopIndex = -1;
        private Paint _activeGradientPaint = null;
        private LayerNode _activeGradientLayer = null;
        private Paint _editingGradientPaint = null;
        private LayerNode _editingGradientLayer = null;
        private GradientRampControl _activeGradientRamp = null;

        public void SetEditingGradient(LayerNode layer, Paint paint, GradientRampControl ramp = null)
        {
            _editingGradientLayer = layer;
            _editingGradientPaint = paint;
            _activeGradientRamp = ramp;
            RefreshCanvas();
        }

        public void ClearEditingGradient()
        {
            _editingGradientLayer = null;
            _editingGradientPaint = null;
            _activeGradientRamp = null;
            RefreshCanvas();
        }

        // UI Controls
        private TextBlock _titleBlock;
        private TextBox _titleTextBox;
        private TextBlock _telemetryBlock;
        private TextBlock _zoomPercentBlock;
        private StackPanel _layerListStack;
        private StackPanel _propertyInspectorStack;
        private Dictionary<ToolType, Button> _toolButtons;
        private StackPanel _booleanToolbar;
        private DispatcherTimer _autosaveTimer;

        // Layer Panel State
        private TextBlock _layerCountBlock;
        private TextBox _layerSearchBox;
        private string _layerSearchText = "";
        private string _layerFilterType = "all";
        private string _renamingLayerId = null;
        private Point _dragCardStartPoint;
        private int _layerGroupCounter = 1;

        // Adobe Illustrator Duplicate & Transform Engine State
        private Dictionary<LayerNode, Point> _dragInitialPositions = new Dictionary<LayerNode, Point>();
        private Dictionary<LayerNode, Rect> _dragInitialBounds = new Dictionary<LayerNode, Rect>();
        private Dictionary<LayerNode, double> _dragInitialRotations = new Dictionary<LayerNode, double>();
        private Dictionary<LayerNode, VectorGeometry> _dragInitialGeometries = new Dictionary<LayerNode, VectorGeometry>();
        private VectorGeometry _startVectorGeometry = null;
        private bool _hasAltDuplicated = false;
        private List<LayerNode> _altClonedLayers = new List<LayerNode>();
        private Point _lastTransformDelta = new Point(24, 24);

        // Rectangle / Marquee Selection State
        private bool _isMarqueeSelecting = false;
        private Point _marqueeStartPoint = new Point(0, 0);
        private Point _marqueeCurrentPoint = new Point(0, 0);

        // Canvas Overlay Container & In-Place Text Editing State
        private Grid _canvasContainer;
        private bool _isEditingText = false;
        private LayerNode _editingTextNode = null;
        private TextBox _inPlaceTextEditor = null;
        private TypographyData _initialTypographyBeforeEdit = null;
        private Rect _initialTextBoundsBeforeEdit;

        // Smart Snapping State
        public class SnapGuide
        {
            public bool IsVertical;
            public double Position;
            public double Start;
            public double End;
        }
        private readonly List<SnapGuide> _activeSnapGuides = new List<SnapGuide>();
        private bool _smartSnappingEnabled = true;

        public StudioView()
        {
            Document = new KerfDocument();
            History = new CommandHistory();
            _toolButtons = new Dictionary<ToolType, Button>();
            InitializeStudioUI();
            InitializeAutosave();
        }

        public void LoadDocument(KerfDocument doc, string filePath)
        {
            Document = doc ?? new KerfDocument();
            CurrentFilePath = filePath;
            History.Clear();
            _selectedLayers.Clear();

            _titleBlock.Text = Document.Name;
            _titleTextBox.Text = Document.Name;

            var page = Document.GetActivePage();
            _sceneGraph.Synchronize(page);
            RefreshCanvas();
            RefreshLayersList();
            RefreshPropertyInspector();
        }

        private void InitializeAutosave()
        {
            _autosaveTimer = new DispatcherTimer();
            _autosaveTimer.Interval = TimeSpan.FromSeconds(20);
            _autosaveTimer.Tick += (s, e) =>
            {
                if (Document != null && History.IsDirty)
                {
                    StorageManager.SaveAutosave(Document, CurrentFilePath);
                }
            };
            _autosaveTimer.Start();
        }

        private void InitializeStudioUI()
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 34, 48)); // #1E2230

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) }); // Top Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main Workspace

            // 1. Top Bar
            var topBar = CreateTopBar();
            Grid.SetRow(topBar, 0);
            rootGrid.Children.Add(topBar);

            // 2. Main Workspace (Left Layers | Canvas | Right Inspector)
            var workspaceGrid = new Grid();
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) }); // Left Panel (Layers)
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Canvas
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Right Panel (Inspector)

            // Left: Layers Panel
            var layersPanel = CreateLayersPanel();
            Grid.SetColumn(layersPanel, 0);
            workspaceGrid.Children.Add(layersPanel);

            // Center: Canvas Host + Floating Tool Island + Bottom Timeline Dock
            _canvasContainer = new Grid();
            _canvasContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _canvasContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var canvasGrid = new Grid();
            _renderer = new GpuRenderer();
            _sceneGraph = new RetainedSceneGraph(_renderer);

            _canvasHost = new RetainedCanvasHost(_sceneGraph, this);
            canvasGrid.Children.Add(_canvasHost);

            // Floating Tool Island at top-center of canvas
            var toolIsland = CreateToolIsland();
            toolIsland.HorizontalAlignment = HorizontalAlignment.Center;
            toolIsland.VerticalAlignment = VerticalAlignment.Top;
            toolIsland.Margin = new Thickness(0, 16, 0, 0);
            canvasGrid.Children.Add(toolIsland);

            Grid.SetRow(canvasGrid, 0);
            _canvasContainer.Children.Add(canvasGrid);

            _timelinePanel = CreateTimelinePanel();
            Grid.SetRow(_timelinePanel, 1);
            _canvasContainer.Children.Add(_timelinePanel);

            Grid.SetColumn(_canvasContainer, 1);
            workspaceGrid.Children.Add(_canvasContainer);

            // Right: Property Inspector
            var inspectorPanel = CreateInspectorPanel();
            Grid.SetColumn(inspectorPanel, 2);
            workspaceGrid.Children.Add(inspectorPanel);

            Grid.SetRow(workspaceGrid, 1);
            rootGrid.Children.Add(workspaceGrid);

            Content = rootGrid;

            // Keyboard Shortcuts
            KeyDown += OnStudioKeyDown;
            PreviewKeyDown += OnStudioKeyDown;
            Focusable = true;
            Loaded += (s, e) =>
            {
                Focus();
                HookWindowMessages();
            };
        }

        public void HandlePreviewKeyDown(KeyEventArgs e)
        {
            OnStudioKeyDown(this, e);
        }

        private void HookWindowMessages()
        {
            try
            {
                var source = PresentationSource.FromVisual(this) as HwndSource;
                if (source != null)
                {
                    source.AddHook(HwndHook);
                }
            }
            catch { }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEHWHEEL = 0x020E;
            if (msg == WM_MOUSEHWHEEL)
            {
                int tilt = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                _panOffset.X -= tilt;
                RefreshCanvas();
                handled = true;
            }
            return IntPtr.Zero;
        }

        #region Top Bar & Telemetry

        private FrameworkElement CreateTopBar()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 41, 58)), // #25293A
                BorderBrush = new SolidColorBrush(Color.FromRgb(54, 62, 86)), // #363E56
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 0, 16, 0)
            };

            var dock = new DockPanel { LastChildFill = false };

            // Left: Home button + Project Name + Undo/Redo + Save
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var homeBtn = CreateIconButton(VectorIcons.HomeGeometry, "Home (Back to Projects)", () =>
            {
                if (RequestHomeNavigation != null) RequestHomeNavigation();
            });
            leftStack.Children.Add(homeBtn);

            var sep1 = CreateSeparator();
            leftStack.Children.Add(sep1);

            _titleBlock = new TextBlock
            {
                Text = "Untitled Project",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 6, 0)
            };
            _titleTextBox = new TextBox
            {
                Text = "Untitled Project",
                FontSize = 13,
                Width = 160,
                Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(30, 35, 52)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center
            };
            _titleBlock.MouseLeftButtonUp += (s, e) =>
            {
                _titleBlock.Visibility = Visibility.Collapsed;
                _titleTextBox.Visibility = Visibility.Visible;
                _titleTextBox.Focus();
                _titleTextBox.SelectAll();
            };
            _titleTextBox.LostFocus += (s, e) => CommitTitleEdit();
            _titleTextBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitTitleEdit(); };

            leftStack.Children.Add(_titleBlock);
            leftStack.Children.Add(_titleTextBox);

            var sep2 = CreateSeparator();
            leftStack.Children.Add(sep2);

            var undoBtn = CreateIconButton(VectorIcons.UndoGeometry, "Undo (Ctrl+Z)", () => Undo());
            var redoBtn = CreateIconButton(VectorIcons.RedoGeometry, "Redo (Ctrl+Y)", () => Redo());
            var saveBtn = CreateIconButton(VectorIcons.SaveGeometry, "Save (Ctrl+S)", () => SaveCurrentDocument());

            var saveAsBtn = CreateTextButton("Save As", () => SaveAsCurrentDocument());
            saveAsBtn.ToolTip = "Save As Anywhere (Ctrl+Shift+S)";
            saveAsBtn.Margin = new Thickness(4, 0, 4, 0);

            var openBtn = CreateIconButton(VectorIcons.FolderGeometry, "Open Project (Ctrl+O)", () => OpenProjectFileDialog());

            leftStack.Children.Add(undoBtn);
            leftStack.Children.Add(redoBtn);
            leftStack.Children.Add(saveBtn);
            leftStack.Children.Add(saveAsBtn);
            leftStack.Children.Add(openBtn);

            var sepMode = CreateSeparator();
            leftStack.Children.Add(sepMode);

            var modeSwitcher = CreateModeSwitcher();
            leftStack.Children.Add(modeSwitcher);

            DockPanel.SetDock(leftStack, Dock.Left);
            dock.Children.Add(leftStack);

            // Center: Boolean operations bar (visible when >= 2 elements selected)
            _booleanToolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(24, 0, 0, 0)
            };
            _booleanToolbar.Children.Add(CreateIconButton(VectorIcons.UnionGeometry, "Boolean Union", () => ApplyBoolean(BooleanOp.Union)));
            _booleanToolbar.Children.Add(CreateIconButton(VectorIcons.SubtractGeometry, "Boolean Subtract", () => ApplyBoolean(BooleanOp.Subtract)));
            _booleanToolbar.Children.Add(CreateIconButton(VectorIcons.IntersectGeometry, "Boolean Intersect", () => ApplyBoolean(BooleanOp.Intersect)));
            _booleanToolbar.Children.Add(CreateIconButton(VectorIcons.ExcludeGeometry, "Boolean Exclude", () => ApplyBoolean(BooleanOp.Exclude)));
            dock.Children.Add(_booleanToolbar);

            // Right: Zoom controls, Telemetry badge, Export button
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Zoom percent & reset
            var zoomMinus = CreateTextButton("-", () => SetZoom(_zoomScale * 0.8));
            _zoomPercentBlock = new TextBlock
            {
                Text = "100%",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 200)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _zoomPercentBlock.MouseLeftButtonUp += (s, e) => ResetZoom();
            var zoomPlus = CreateTextButton("+", () => SetZoom(_zoomScale * 1.25));

            rightStack.Children.Add(zoomMinus);
            rightStack.Children.Add(_zoomPercentBlock);
            rightStack.Children.Add(zoomPlus);

            var sep3 = CreateSeparator();
            rightStack.Children.Add(sep3);

            // Real-time truthful telemetry
            _telemetryBlock = new TextBlock
            {
                Text = "GPU Tier 2 (Direct3D 11) | 0 µs",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 12, 0)
            };
            rightStack.Children.Add(_telemetryBlock);

            var exportBtn = CreateIconButton(VectorIcons.ExportGeometry, "Export Document (PNG/SVG)", () => ShowExportDialog());
            rightStack.Children.Add(exportBtn);

            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

            border.Child = dock;
            return border;
        }

        private void CommitTitleEdit()
        {
            _titleTextBox.Visibility = Visibility.Collapsed;
            _titleBlock.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(_titleTextBox.Text))
            {
                Document.Name = _titleTextBox.Text.Trim();
                _titleBlock.Text = Document.Name;
                History.MarkDirty();
            }
        }

        #endregion

        #region Tool Island

        private FrameworkElement CreateToolIsland()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 42, 48, 68)), // #2A3044
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 68, 78, 108)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 4, 6, 4),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 16,
                    ShadowDepth = 4,
                    Opacity = 0.45,
                    Color = Colors.Black
                }
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            stack.Children.Add(CreateToolButton(ToolType.Select, VectorIcons.SelectGeometry, "Select (V)"));
            stack.Children.Add(CreateToolButton(ToolType.Marquee, VectorIcons.MarqueeGeometry, "Rectangle Selection (M)"));
            stack.Children.Add(CreateToolButton(ToolType.Hand, VectorIcons.HandGeometry, "Hand Pan (H)"));
            stack.Children.Add(CreateSeparator());
            stack.Children.Add(CreateToolButton(ToolType.Frame, VectorIcons.FrameGeometry, "Frame (F)"));
            stack.Children.Add(CreateToolButton(ToolType.Rect, VectorIcons.RectGeometry, "Rectangle (R)"));
            stack.Children.Add(CreateToolButton(ToolType.Ellipse, VectorIcons.EllipseGeometry, "Ellipse (O)"));
            stack.Children.Add(CreateToolButton(ToolType.Star, VectorIcons.StarGeometry, "Star (S)"));
            stack.Children.Add(CreateToolButton(ToolType.Polygon, VectorIcons.PolygonGeometry, "Polygon (P)"));
            stack.Children.Add(CreateSeparator());
            stack.Children.Add(CreateToolButton(ToolType.Pen, VectorIcons.PenGeometry, "Bézier Pen (B)"));
            stack.Children.Add(CreateToolButton(ToolType.Text, VectorIcons.TextGeometry, "Text (T)"));
            stack.Children.Add(CreateToolButton(ToolType.Line, VectorIcons.LineGeometry, "Line (L)"));
            stack.Children.Add(CreateToolButton(ToolType.Arrow, VectorIcons.ArrowGeometry, "Arrow (A)"));

            border.Child = stack;
            SetActiveTool(ToolType.Select);
            return border;
        }

        private Button CreateToolButton(ToolType tool, Geometry iconGeo, string tooltip)
        {
            var btn = new Button
            {
                Width = 34,
                Height = 34,
                Margin = new Thickness(2, 0, 2, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Tag = tool,
                Template = CreateRoundedButtonTemplate(6)
            };

            bool isFilled = (tool == ToolType.Select);
            var icon = VectorIcons.CreateIcon(iconGeo, new SolidColorBrush(Color.FromRgb(170, 185, 210)), isFilled ? new SolidColorBrush(Color.FromRgb(170, 185, 210)) : null, 18);
            btn.Content = icon;

            btn.Click += (s, e) => SetActiveTool(tool);
            _toolButtons[tool] = btn;
            return btn;
        }

        public void SetActiveTool(ToolType tool)
        {
            if (tool != ToolType.Pen && _activePenNode != null)
            {
                FinishPenDrawing();
            }

            _activeTool = tool;
            foreach (var kvp in _toolButtons)
            {
                bool isActive = (kvp.Key == tool);
                var btn = kvp.Value;
                btn.Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(99, 102, 241))
                    : Brushes.Transparent;

                var path = btn.Content as System.Windows.Shapes.Path;
                if (path != null)
                {
                    var brush = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(170, 185, 210));
                    path.Stroke = brush;
                    if (kvp.Key == ToolType.Select)
                    {
                        path.Fill = brush;
                    }
                }
            }

            if (_canvasHost != null)
            {
                _canvasHost.Cursor = (tool == ToolType.Hand) ? Cursors.Hand :
                                     (tool == ToolType.Pen) ? Cursors.Pen :
                                     (tool == ToolType.Text) ? Cursors.IBeam :
                                     (tool == ToolType.Marquee) ? Cursors.Cross : Cursors.Arrow;
            }
        }

        #endregion

        #region Left Panel (Layer Hierarchy)

        private FrameworkElement CreateLayersPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(33, 37, 52)), // #212534
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 58, 80)), // #323A50
                BorderThickness = new Thickness(0, 0, 1, 0)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); // Header Toolbar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content View Host

            // 1. Header Toolbar with [ Layers ] | [ Assets ] Pill Switcher
            var headerDock = new DockPanel { Margin = new Thickness(8, 0, 8, 0), LastChildFill = false };

            var tabStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _layersTabBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(48, 56, 78)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            var layersTabTextStack = new StackPanel { Orientation = Orientation.Horizontal };
            var layersTitle = new TextBlock { Text = "Layers", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            _layerCountBlock = new TextBlock { Text = " (0)", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(140, 155, 185)), Margin = new Thickness(2, 0, 0, 0) };
            layersTabTextStack.Children.Add(layersTitle);
            layersTabTextStack.Children.Add(_layerCountBlock);
            _layersTabBtn.Child = layersTabTextStack;
            _layersTabBtn.MouseLeftButtonUp += (s, e) => SwitchLeftSidebarTab(false);
            tabStack.Children.Add(_layersTabBtn);

            _assetsTabBtn = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand
            };
            var assetsTabTextStack = new StackPanel { Orientation = Orientation.Horizontal };
            var assetsTitle = new TextBlock { Text = "Assets", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 205)) };
            _assetsCountBlock = new TextBlock { Text = " (0)", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(120, 135, 165)), Margin = new Thickness(2, 0, 0, 0) };
            assetsTabTextStack.Children.Add(assetsTitle);
            assetsTabTextStack.Children.Add(_assetsCountBlock);
            _assetsTabBtn.Child = assetsTabTextStack;
            _assetsTabBtn.MouseLeftButtonUp += (s, e) => SwitchLeftSidebarTab(true);
            tabStack.Children.Add(_assetsTabBtn);

            DockPanel.SetDock(tabStack, Dock.Left);
            headerDock.Children.Add(tabStack);

            // Layer actions
            var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            actionStack.Children.Add(CreateSmallButton("▲", "Bring Forward (Ctrl + ])", () => MoveSelectedLayerZ(true, false)));
            actionStack.Children.Add(CreateSmallButton("▼", "Send Backward (Ctrl + [)", () => MoveSelectedLayerZ(false, false)));
            actionStack.Children.Add(CreateSmallButton("⧉", "Duplicate (Ctrl + D)", () => DuplicateSelectedLayers()));
            actionStack.Children.Add(CreateSmallButton("📁", "Group (Ctrl + G)", () => GroupSelectedLayers()));
            actionStack.Children.Add(CreateSmallButton("🗑", "Delete (Delete)", () => DeleteSelectedLayers()));
            DockPanel.SetDock(actionStack, Dock.Right);
            headerDock.Children.Add(actionStack);

            Grid.SetRow(headerDock, 0);
            grid.Children.Add(headerDock);

            // 2. View Containers Host
            var contentHost = new Grid();

            // A. Layers View Container
            var layersGrid = new Grid();
            layersGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search / Filter
            layersGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            var filterStack = new StackPanel { Margin = new Thickness(10, 0, 10, 8) };
            var searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(42, 48, 66)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(62, 70, 96)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 6)
            };
            _layerSearchBox = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            _layerSearchBox.TextChanged += (s, e) =>
            {
                _layerSearchText = _layerSearchBox.Text.Trim();
                RefreshLayersList();
            };
            searchBorder.Child = _layerSearchBox;
            filterStack.Children.Add(searchBorder);

            // Filter Chips (All, Frames, Shapes, Text, Components)
            var chipPanel = new StackPanel { Orientation = Orientation.Horizontal };
            chipPanel.Children.Add(CreateFilterChip("All", "all"));
            chipPanel.Children.Add(CreateFilterChip("Frames", "frames"));
            chipPanel.Children.Add(CreateFilterChip("Shapes", "shapes"));
            chipPanel.Children.Add(CreateFilterChip("Text", "text"));
            chipPanel.Children.Add(CreateFilterChip("Components", "components"));
            filterStack.Children.Add(chipPanel);

            Grid.SetRow(filterStack, 0);
            layersGrid.Children.Add(filterStack);

            var scrollLayers = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _layerListStack = new StackPanel { Margin = new Thickness(8, 0, 8, 12) };
            scrollLayers.Content = _layerListStack;
            Grid.SetRow(scrollLayers, 1);
            layersGrid.Children.Add(scrollLayers);

            _layersViewContainer = layersGrid;
            contentHost.Children.Add(_layersViewContainer);

            // B. Assets / Components View Container
            var assetsGrid = new Grid();
            assetsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search / Create bar
            assetsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            var assetsFilterStack = new StackPanel { Margin = new Thickness(10, 0, 10, 8) };
            var assetsSearchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(42, 48, 66)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(62, 70, 96)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 6)
            };
            _assetsSearchBox = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            _assetsSearchBox.TextChanged += (s, e) => RefreshAssetsList();
            assetsSearchBorder.Child = _assetsSearchBox;
            assetsFilterStack.Children.Add(assetsSearchBorder);

            var newCompBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 123, 97, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(123, 97, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand
            };
            newCompBtn.Child = new TextBlock
            {
                Text = "❖ Create Component (Ctrl+Alt+K)",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(196, 181, 253)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            newCompBtn.MouseLeftButtonUp += (s, e) => CreateComponentFromSelection();
            assetsFilterStack.Children.Add(newCompBtn);

            Grid.SetRow(assetsFilterStack, 0);
            assetsGrid.Children.Add(assetsFilterStack);

            var scrollAssets = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _assetsListStack = new StackPanel { Margin = new Thickness(8, 0, 8, 12) };
            scrollAssets.Content = _assetsListStack;
            Grid.SetRow(scrollAssets, 1);
            assetsGrid.Children.Add(scrollAssets);

            _assetsViewContainer = assetsGrid;
            _assetsViewContainer.Visibility = Visibility.Collapsed;
            contentHost.Children.Add(_assetsViewContainer);

            Grid.SetRow(contentHost, 1);
            grid.Children.Add(contentHost);

            border.Child = grid;
            return border;
        }

        private void SwitchLeftSidebarTab(bool showAssets)
        {
            _isAssetsTabActive = showAssets;
            if (_layersTabBtn != null)
            {
                _layersTabBtn.Background = !showAssets ? new SolidColorBrush(Color.FromRgb(48, 56, 78)) : Brushes.Transparent;
            }
            if (_assetsTabBtn != null)
            {
                _assetsTabBtn.Background = showAssets ? new SolidColorBrush(Color.FromRgb(48, 56, 78)) : Brushes.Transparent;
            }
            if (_layersViewContainer != null)
            {
                _layersViewContainer.Visibility = !showAssets ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_assetsViewContainer != null)
            {
                _assetsViewContainer.Visibility = showAssets ? Visibility.Visible : Visibility.Collapsed;
            }
            if (showAssets)
            {
                RefreshAssetsList();
            }
        }

        private Button CreateFilterChip(string label, string filterType)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 10,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 0),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Template = CreateRoundedButtonTemplate(6)
            };

            Action updateStyle = () =>
            {
                bool active = _layerFilterType == filterType;
                btn.Background = active ? new SolidColorBrush(Color.FromArgb(50, 99, 102, 241)) : Brushes.Transparent;
                btn.Foreground = active ? new SolidColorBrush(Color.FromRgb(129, 140, 248)) : new SolidColorBrush(Color.FromRgb(130, 140, 160));
                btn.BorderBrush = active ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) : new SolidColorBrush(Color.FromRgb(36, 42, 60));
            };
            updateStyle();

            btn.Click += (s, e) =>
            {
                _layerFilterType = filterType;
                RefreshLayersList();
            };
            return btn;
        }

        private bool MatchesFilter(LayerNode node)
        {
            if (node == null) return false;

            if (!string.IsNullOrEmpty(_layerSearchText))
            {
                if (node.Name == null || node.Name.IndexOf(_layerSearchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            if (_layerFilterType == "frames")
            {
                return node.Type == ElementType.Frame;
            }
            if (_layerFilterType == "shapes")
            {
                return node.Type == ElementType.Rectangle || node.Type == ElementType.Ellipse ||
                       node.Type == ElementType.Star || node.Type == ElementType.Polygon ||
                       node.Type == ElementType.Line || node.Type == ElementType.Arrow ||
                       node.Type == ElementType.Path;
            }
            if (_layerFilterType == "text")
            {
                return node.Type == ElementType.Text;
            }
            if (_layerFilterType == "components")
            {
                return node.IsMasterComponent || node.Type == ElementType.ComponentInstance || !string.IsNullOrEmpty(node.ComponentId);
            }

            return true;
        }

        private Geometry GetIconForLayer(LayerNode layer)
        {
            if (layer != null)
            {
                if (layer.IsMasterComponent) return VectorIcons.ComponentGeometry;
                if (layer.Type == ElementType.ComponentInstance || !string.IsNullOrEmpty(layer.ComponentId)) return VectorIcons.InstanceGeometry;
                return GetIconForLayerType(layer.Type);
            }
            return VectorIcons.RectGeometry;
        }

        private Brush GetBadgeBrushForLayer(LayerNode layer)
        {
            if (layer != null)
            {
                if (layer.IsMasterComponent) return new SolidColorBrush(Color.FromRgb(123, 97, 255)); // Figma signature purple #7B61FF
                if (layer.Type == ElementType.ComponentInstance || !string.IsNullOrEmpty(layer.ComponentId)) return new SolidColorBrush(Color.FromRgb(168, 85, 247));
                return GetBadgeBrushForLayerType(layer.Type);
            }
            return new SolidColorBrush(Color.FromRgb(71, 85, 105));
        }

        private string GetTypeTagForLayer(LayerNode layer)
        {
            if (layer != null)
            {
                if (layer.IsMasterComponent) return "COMPONENT";
                if (layer.Type == ElementType.ComponentInstance || !string.IsNullOrEmpty(layer.ComponentId)) return "INSTANCE";
                return GetTypeTagText(layer.Type);
            }
            return "LAYER";
        }

        private Geometry GetIconForLayerType(ElementType type)
        {
            switch (type)
            {
                case ElementType.Frame: return VectorIcons.FrameGeometry;
                case ElementType.Group: return VectorIcons.FolderGeometry;
                case ElementType.Rectangle: return VectorIcons.RectGeometry;
                case ElementType.Ellipse: return VectorIcons.EllipseGeometry;
                case ElementType.Star: return VectorIcons.StarGeometry;
                case ElementType.Polygon: return VectorIcons.PolygonGeometry;
                case ElementType.Line: return VectorIcons.LineGeometry;
                case ElementType.Arrow: return VectorIcons.ArrowGeometry;
                case ElementType.Path: return VectorIcons.PenGeometry;
                case ElementType.Text: return VectorIcons.TextGeometry;
                default: return VectorIcons.RectGeometry;
            }
        }

        private Brush GetBadgeBrushForLayerType(ElementType type)
        {
            switch (type)
            {
                case ElementType.Frame: return new SolidColorBrush(Color.FromRgb(13, 153, 255));
                case ElementType.Group: return new SolidColorBrush(Color.FromRgb(245, 158, 11));
                case ElementType.Text: return new SolidColorBrush(Color.FromRgb(16, 185, 129));
                case ElementType.Path: return new SolidColorBrush(Color.FromRgb(168, 85, 247));
                case ElementType.Star:
                case ElementType.Polygon: return new SolidColorBrush(Color.FromRgb(245, 158, 11));
                default: return new SolidColorBrush(Color.FromRgb(71, 85, 105));
            }
        }

        private string GetTypeTagText(ElementType type)
        {
            switch (type)
            {
                case ElementType.Frame: return "FRAME";
                case ElementType.Group: return "GROUP";
                case ElementType.Rectangle: return "RECT";
                case ElementType.Ellipse: return "ELLIPSE";
                case ElementType.Text: return "TEXT";
                case ElementType.Path: return "PATH";
                case ElementType.Star: return "STAR";
                case ElementType.Polygon: return "POLYGON";
                case ElementType.Line: return "LINE";
                case ElementType.Arrow: return "ARROW";
                default: return type.ToString().ToUpper();
            }
        }

        public void RefreshAssetsList()
        {
            if (_assetsListStack == null || Document == null) return;
            _assetsListStack.Children.Clear();

            var components = Document.GetAllComponents();
            if (_assetsCountBlock != null)
            {
                _assetsCountBlock.Text = string.Format(" ({0})", components.Count);
            }

            string search = (_assetsSearchBox != null) ? _assetsSearchBox.Text.Trim() : "";
            if (!string.IsNullOrEmpty(search))
            {
                components = components.Where(c => c.Name != null && c.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            if (components.Count == 0)
            {
                var emptyCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 10, 0, 0)
                };
                var emptyStack = new StackPanel();
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "❖ No Components Yet",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(168, 85, 247)),
                    Margin = new Thickness(0, 0, 0, 6)
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(search)
                        ? "Select any layer or frame and press Ctrl+Alt+K (or right-click → Create Component) to create reusable master components."
                        : "No components match your search filter.",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                    TextWrapping = TextWrapping.Wrap
                });
                emptyCard.Child = emptyStack;
                _assetsListStack.Children.Add(emptyCard);
                return;
            }

            foreach (var comp in components)
            {
                var card = CreateAssetComponentCard(comp);
                if (card != null) _assetsListStack.Children.Add(card);
            }
        }

        private FrameworkElement CreateAssetComponentCard(LayerNode comp)
        {
            if (comp == null) return null;

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 46, 64)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 66, 92)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel { LastChildFill = true };

            // Left: Component Glyph (❖)
            var icon = VectorIcons.CreateIcon(VectorIcons.ComponentGeometry, new SolidColorBrush(Color.FromRgb(168, 85, 247)), null, 14, 1.4);
            icon.Margin = new Thickness(0, 2, 8, 0);
            DockPanel.SetDock(icon, Dock.Left);
            dock.Children.Add(icon);

            // Right: Insert Button
            var insertBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(123, 97, 255)), // Figma purple
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btnText = new TextBlock
            {
                Text = "+ Insert",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            insertBtn.Child = btnText;
            insertBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                CreateComponentInstance(comp);
            };
            DockPanel.SetDock(insertBtn, Dock.Right);
            dock.Children.Add(insertBtn);

            // Center: Name and Dimensions
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameBlock = new TextBlock
            {
                Text = comp.Name ?? "Component",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(nameBlock);

            var dimBlock = new TextBlock
            {
                Text = string.Format("{0:0} × {1:0} px", comp.Width, comp.Height),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 155, 185)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(dimBlock);
            dock.Children.Add(textStack);

            card.Child = dock;

            card.MouseEnter += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(48, 56, 78));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(123, 97, 255));
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(40, 46, 64));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(58, 66, 92));
            };

            card.MouseLeftButtonUp += (s, e) =>
            {
                _selectedLayers.Clear();
                _selectedLayers.Add(comp);
                OnSelectionChanged();
                RefreshCanvas();
            };

            return card;
        }



        public void RefreshLayersList()
        {
            if (_layerListStack == null) return;
            _layerListStack.Children.Clear();
            if (Document == null) return;
            var page = Document.GetActivePage();
            if (page == null || page.Layers == null) return;

            if (_layerCountBlock != null)
            {
                _layerCountBlock.Text = string.Format("({0})", page.CountAllLayers());
            }

            RefreshAssetsList();

            int count = 0;
            // Display in reverse order (topmost layer on top of UI list)
            for (int i = page.Layers.Count - 1; i >= 0; i--)
            {
                var layer = page.Layers[i];
                var card = CreateLayerCard(layer, 0);
                if (card != null)
                {
                    _layerListStack.Children.Add(card);
                    count++;
                }
            }

            if (count == 0)
            {
                var empty = new StackPanel
                {
                    Margin = new Thickness(12, 32, 12, 32),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                empty.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(_layerSearchText) && _layerFilterType == "all" ? "No layers yet" : "No matching layers",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });
                empty.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(_layerSearchText) && _layerFilterType == "all"
                        ? "Draw with tools (R, O, T, B)\nor add frames to design"
                        : "Clear search or select 'All'",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(90, 100, 120)),
                    TextAlignment = TextAlignment.Center
                });
                _layerListStack.Children.Add(empty);
            }
        }

        private FrameworkElement CreateLayerCard(LayerNode layer, int depth)
        {
            if (!MatchesFilter(layer)) return null;

            bool isSelected = _selectedLayers.Contains(layer);
            bool isContainer = layer.Type == ElementType.Frame || layer.Type == ElementType.Group;
            bool isRenaming = _renamingLayerId == layer.Id;

            var card = new Border
            {
                Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(48, 58, 88))
                    : new SolidColorBrush(Color.FromRgb(36, 42, 60)),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(129, 140, 248))
                    : new SolidColorBrush(Color.FromRgb(50, 58, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(4 + depth * 14, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 3),
                Cursor = Cursors.Hand,
                AllowDrop = true,
                Tag = layer
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // Caret (expand/collapse)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Icon badge
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name or Rename Box
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Type Tag
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); // Eye
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); // Lock
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); // Delete

            // 1. Caret for containers
            if (isContainer && layer.Children != null && layer.Children.Count > 0)
            {
                var caret = new TextBlock
                {
                    Text = layer.IsCollapsed ? "▶" : "▼",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand
                };
                caret.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    layer.IsCollapsed = !layer.IsCollapsed;
                    RefreshLayersList();
                };
                Grid.SetColumn(caret, 0);
                grid.Children.Add(caret);
            }

            // 2. Icon Badge
            var badge = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(4),
                Background = GetBadgeBrushForLayer(layer),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0)
            };
            var iconGeo = GetIconForLayer(layer);
            badge.Child = VectorIcons.CreateIcon(iconGeo, Brushes.White, null, 11, 1.2);
            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);

            bool isComp = layer.IsMasterComponent || !string.IsNullOrEmpty(layer.ComponentId);
            Brush nameBrush = isComp
                ? (isSelected ? new SolidColorBrush(Color.FromRgb(196, 181, 253)) : new SolidColorBrush(Color.FromRgb(167, 139, 250)))
                : (isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(190, 200, 220)));

            // 3. Name or Rename TextBox
            if (isRenaming)
            {
                var editBox = new TextBox
                {
                    Text = layer.Name,
                    FontSize = 11,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromRgb(28, 34, 52)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(2, 1, 2, 1),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                editBox.Loaded += (s, e) =>
                {
                    editBox.Focus();
                    editBox.SelectAll();
                };
                editBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        FinishRename(layer.Id, editBox.Text);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        _renamingLayerId = null;
                        RefreshLayersList();
                        e.Handled = true;
                    }
                };
                editBox.LostFocus += (s, e) =>
                {
                    FinishRename(layer.Id, editBox.Text);
                };
                Grid.SetColumn(editBox, 2);
                grid.Children.Add(editBox);
            }
            else
            {
                var nameBlock = new TextBlock
                {
                    Text = layer.Name,
                    FontSize = 11,
                    Foreground = nameBrush,
                    FontWeight = (isSelected || isComp) ? FontWeights.SemiBold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(6, 0, 4, 0)
                };
                Grid.SetColumn(nameBlock, 2);
                grid.Children.Add(nameBlock);
            }

            // 4. Type Tag
            var typeTag = new Border
            {
                Background = isComp ? new SolidColorBrush(Color.FromArgb(60, 123, 97, 255)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(2, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            typeTag.Child = new TextBlock
            {
                Text = GetTypeTagForLayer(layer),
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = isComp ? new SolidColorBrush(Color.FromRgb(196, 181, 253)) : new SolidColorBrush(Color.FromRgb(130, 140, 165))
            };
            Grid.SetColumn(typeTag, 3);
            grid.Children.Add(typeTag);

            // 5. Eye Button (Visibility)
            var eyeBtn = new TextBlock
            {
                Text = layer.IsVisible ? "👁" : "⊘",
                FontSize = 11,
                Foreground = layer.IsVisible ? new SolidColorBrush(Color.FromRgb(130, 145, 175)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = layer.IsVisible ? "Hide Layer" : "Show Layer"
            };
            eyeBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                layer.IsVisible = !layer.IsVisible;
                InvalidateAndRefresh(layer);
                RefreshLayersList();
            };
            Grid.SetColumn(eyeBtn, 4);
            grid.Children.Add(eyeBtn);

            // 6. Lock Button
            var lockBtn = new TextBlock
            {
                Text = layer.IsLocked ? "🔒" : "·",
                FontSize = 11,
                Foreground = layer.IsLocked ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(90, 100, 120)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = layer.IsLocked ? "Unlock Layer" : "Lock Layer"
            };
            lockBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                layer.IsLocked = !layer.IsLocked;
                RefreshLayersList();
            };
            Grid.SetColumn(lockBtn, 5);
            grid.Children.Add(lockBtn);

            // 7. Delete Button
            var delBtn = new TextBlock
            {
                Text = "✕",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 120, 140)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Delete Layer"
            };
            delBtn.MouseEnter += (s, e) => delBtn.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            delBtn.MouseLeave += (s, e) => delBtn.Foreground = new SolidColorBrush(Color.FromRgb(110, 120, 140));
            delBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                var page = Document.GetActivePage();
                if (page != null)
                {
                    History.Execute(new DeleteLayersCommand(page, new[] { layer }));
                    _selectedLayers.Remove(layer);
                    _sceneGraph.Synchronize(page);
                    OnSelectionChanged();
                }
            };
            Grid.SetColumn(delBtn, 6);
            grid.Children.Add(delBtn);

            card.Child = grid;

            // Selection click
            card.MouseLeftButtonUp += (s, e) =>
            {
                Focus();
                if (_renamingLayerId == layer.Id) return;
                bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

                if (isShift || isCtrl)
                {
                    if (_selectedLayers.Contains(layer)) _selectedLayers.Remove(layer);
                    else _selectedLayers.Add(layer);
                }
                else
                {
                    _selectedLayers.Clear();
                    _selectedLayers.Add(layer);
                }
                OnSelectionChanged();
            };

            // Double click to rename
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    StartRename(layer.Id);
                }
            };

            // Drag and Drop
            card.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _dragCardStartPoint = e.GetPosition(card);
            };

            card.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && _renamingLayerId == null)
                {
                    Point cur = e.GetPosition(card);
                    if (Math.Abs(cur.X - _dragCardStartPoint.X) > 4 || Math.Abs(cur.Y - _dragCardStartPoint.Y) > 4)
                    {
                        DragDrop.DoDragDrop(card, layer, DragDropEffects.Move);
                    }
                }
            };

            card.DragOver += (s, e) =>
            {
                e.Handled = true;
                var dropped = e.Data.GetData(typeof(LayerNode)) as LayerNode;
                if (dropped == null || dropped == layer)
                {
                    e.Effects = DragDropEffects.None;
                    return;
                }
                e.Effects = DragDropEffects.Move;

                double y = e.GetPosition(card).Y;
                double h = card.ActualHeight;

                if (isContainer)
                {
                    if (y > h * 0.25 && y < h * 0.75)
                    {
                        card.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                        card.BorderThickness = new Thickness(2);
                    }
                    else if (y <= h * 0.25)
                    {
                        card.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                        card.BorderThickness = new Thickness(1, 3, 1, 1);
                    }
                    else
                    {
                        card.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                        card.BorderThickness = new Thickness(1, 1, 1, 3);
                    }
                }
                else
                {
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                    card.BorderThickness = (y <= h * 0.5) ? new Thickness(1, 3, 1, 1) : new Thickness(1, 1, 1, 3);
                }
            };

            card.DragLeave += (s, e) =>
            {
                card.BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) : new SolidColorBrush(Color.FromRgb(32, 38, 56));
                card.BorderThickness = new Thickness(1);
            };

            card.Drop += (s, e) =>
            {
                e.Handled = true;
                card.BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) : new SolidColorBrush(Color.FromRgb(32, 38, 56));
                card.BorderThickness = new Thickness(1);

                var dropped = e.Data.GetData(typeof(LayerNode)) as LayerNode;
                if (dropped == null || dropped == layer || (dropped.Id != null && dropped.Id == layer.Id)) return;

                double y = e.GetPosition(card).Y;
                double h = card.ActualHeight;
                DropPosition pos;

                if (isContainer)
                {
                    if (y > h * 0.25 && y < h * 0.75) pos = DropPosition.Inside;
                    else if (y <= h * 0.25) pos = DropPosition.Above;
                    else pos = DropPosition.Below;
                }
                else
                {
                    pos = (y <= h * 0.5) ? DropPosition.Above : DropPosition.Below;
                }

                MoveLayerHierarchy(dropped, layer, pos);
            };

            // Right-Click Context Menu
            card.ContextMenu = CreateLayerContextMenu(layer);

            card.MouseRightButtonUp += (s, e) =>
            {
                if (!_selectedLayers.Contains(layer))
                {
                    _selectedLayers.Clear();
                    _selectedLayers.Add(layer);
                    OnSelectionChanged();
                }
            };

            var stack = new StackPanel();
            stack.Children.Add(card);

            // Children (if frame/group)
            if (isContainer && layer.Children != null && layer.Children.Count > 0 && !layer.IsCollapsed)
            {
                for (int c = layer.Children.Count - 1; c >= 0; c--)
                {
                    var childCard = CreateLayerCard(layer.Children[c], depth + 1);
                    if (childCard != null) stack.Children.Add(childCard);
                }
            }

            return stack;
        }

        private static ControlTemplate _modernMenuTemplate;
        private static ControlTemplate _modernMenuItemTemplate;

        private static void ApplyModernContextMenuStyle(ContextMenu cm)
        {
            if (_modernMenuTemplate == null)
            {
                _modernMenuTemplate = new ControlTemplate(typeof(ContextMenu));
                var borderFact = new FrameworkElementFactory(typeof(Border));
                borderFact.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
                borderFact.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(32, 37, 52)));
                borderFact.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(56, 66, 92)));
                borderFact.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                borderFact.SetValue(Border.PaddingProperty, new Thickness(5, 5, 5, 5));

                var dropShadow = new DropShadowEffect
                {
                    BlurRadius = 24,
                    ShadowDepth = 6,
                    Direction = 270,
                    Color = Colors.Black,
                    Opacity = 0.5
                };
                borderFact.SetValue(UIElement.EffectProperty, dropShadow);

                var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
                borderFact.AppendChild(itemsPresenter);
                _modernMenuTemplate.VisualTree = borderFact;
            }

            cm.Template = _modernMenuTemplate;
        }

        private static void ApplyModernMenuItemStyle(MenuItem mi)
        {
            if (_modernMenuItemTemplate == null)
            {
                _modernMenuItemTemplate = new ControlTemplate(typeof(MenuItem));
                var itemBorder = new FrameworkElementFactory(typeof(Border), "ItemBorder");
                itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                itemBorder.SetValue(Border.MarginProperty, new Thickness(1, 1, 1, 1));
                itemBorder.SetValue(Border.PaddingProperty, new Thickness(8, 6, 10, 6));
                itemBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);

                var grid = new FrameworkElementFactory(typeof(Grid));
                var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
                col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(24));
                var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
                col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
                var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
                col2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Auto));
                grid.AppendChild(col0);
                grid.AppendChild(col1);
                grid.AppendChild(col2);

                var iconPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
                iconPresenter.SetValue(Grid.ColumnProperty, 0);
                iconPresenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(MenuItem.IconProperty));
                iconPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                iconPresenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                grid.AppendChild(iconPresenter);

                var headerPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
                headerPresenter.SetValue(Grid.ColumnProperty, 1);
                headerPresenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(HeaderedItemsControl.HeaderProperty));
                headerPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                headerPresenter.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 16, 0));
                grid.AppendChild(headerPresenter);

                var gestureBlock = new FrameworkElementFactory(typeof(TextBlock));
                gestureBlock.SetValue(Grid.ColumnProperty, 2);
                gestureBlock.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(MenuItem.InputGestureTextProperty));
                gestureBlock.SetValue(TextBlock.FontSizeProperty, 10.5);
                gestureBlock.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(148, 163, 184)));
                gestureBlock.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                grid.AppendChild(gestureBlock);

                itemBorder.AppendChild(grid);
                _modernMenuItemTemplate.VisualTree = itemBorder;

                var highlightTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
                highlightTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(50, 58, 82)), "ItemBorder"));
                _modernMenuItemTemplate.Triggers.Add(highlightTrigger);
            }

            mi.Template = _modernMenuItemTemplate;
            mi.Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            mi.FontSize = 12.0;
            mi.FontWeight = FontWeights.Normal;
            mi.Cursor = Cursors.Hand;
        }

        private static Separator CreateModernSeparator()
        {
            var sep = new Separator
            {
                Margin = new Thickness(6, 4, 6, 4),
                Background = new SolidColorBrush(Color.FromRgb(48, 56, 78)),
                Height = 1
            };
            return sep;
        }

        private static ControlTemplate _modernComboTemplate;
        private static Style _modernComboItemStyle;

        public static void ApplyModernComboBoxStyle(ComboBox combo)
        {
            if (_modernComboTemplate == null)
            {
                // 1. ToggleButton Template
                var tbTemplate = new ControlTemplate(typeof(ToggleButton));
                var tbBorder = new FrameworkElementFactory(typeof(Border), "tbBorder");
                tbBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
                tbBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(33, 38, 52)));
                tbBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(56, 66, 92)));
                tbBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));

                var tbGrid = new FrameworkElementFactory(typeof(Grid));
                var c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
                c0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
                var c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
                c1.SetValue(ColumnDefinition.WidthProperty, new GridLength(20));
                tbGrid.AppendChild(c0);
                tbGrid.AppendChild(c1);

                var arrow = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
                arrow.SetValue(Grid.ColumnProperty, 1);
                arrow.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0 0 L 3.5 3.5 L 7 0"));
                arrow.SetValue(System.Windows.Shapes.Path.StrokeProperty, new SolidColorBrush(Color.FromRgb(148, 163, 184)));
                arrow.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.5);
                arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                tbGrid.AppendChild(arrow);

                tbBorder.AppendChild(tbGrid);
                tbTemplate.VisualTree = tbBorder;

                var tbHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                tbHover.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(99, 102, 241)), "tbBorder"));
                tbHover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(40, 46, 64)), "tbBorder"));
                tbTemplate.Triggers.Add(tbHover);

                var tbChecked = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
                tbChecked.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(56, 189, 248)), "tbBorder"));
                tbTemplate.Triggers.Add(tbChecked);

                // 2. ComboBox Template
                _modernComboTemplate = new ControlTemplate(typeof(ComboBox));
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
                contentPresenter.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 22, 0));
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
                popBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(30, 34, 48)));
                popBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(56, 66, 92)));
                popBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                popBorder.SetValue(Border.PaddingProperty, new Thickness(3, 4, 3, 4));
                popBorder.SetValue(FrameworkElement.MaxHeightProperty, 240.0);
                popBorder.SetValue(FrameworkElement.MinWidthProperty, new Binding("ActualWidth")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
                });

                var popShadow = new DropShadowEffect
                {
                    BlurRadius = 16,
                    ShadowDepth = 5,
                    Direction = 270,
                    Color = Colors.Black,
                    Opacity = 0.55
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
                _modernComboTemplate.VisualTree = grid;

                // 3. ComboBoxItem Style
                _modernComboItemStyle = new Style(typeof(ComboBoxItem));
                _modernComboItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(241, 245, 249))));
                _modernComboItemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
                _modernComboItemStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));

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
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(50, 58, 82)), "ItemBorder"));
                hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
                itemTemplate.Triggers.Add(hoverTrigger);

                var selTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
                selTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(79, 70, 229)), "ItemBorder"));
                selTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
                itemTemplate.Triggers.Add(selTrigger);

                _modernComboItemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
            }

            combo.Template = _modernComboTemplate;
            combo.ItemContainerStyle = _modernComboItemStyle;
            combo.Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            combo.Background = new SolidColorBrush(Color.FromRgb(33, 38, 52));
            combo.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 66, 92));
            combo.Cursor = Cursors.Hand;
        }

        public static ComboBox CreateModernComboBox(double height = 26, double width = double.NaN)
        {
            var combo = new ComboBox
            {
                Height = height,
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 6)
            };
            if (!double.IsNaN(width))
            {
                combo.Width = width;
            }
            ApplyModernComboBoxStyle(combo);
            return combo;
        }

        public static ComboBoxItem CreateModernComboBoxItem(object content, object tag = null)
        {
            return new ComboBoxItem
            {
                Content = content,
                Tag = tag,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                Background = Brushes.Transparent,
                FontSize = 11.5,
                Cursor = Cursors.Hand
            };
        }

        public static Button CreatePresetPill(string label, Action onClick)
        {
            var btn = new Button
            {
                Content = label,
                Height = 22,
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Background = new SolidColorBrush(Color.FromRgb(36, 42, 58)),
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 66, 92)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = Cursors.Hand
            };
            var t = new ControlTemplate(typeof(Button));
            var b = new FrameworkElementFactory(typeof(Border), "btnBorder");
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            b.AppendChild(cp);
            t.VisualTree = b;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(50, 58, 82)), "btnBorder"));
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(99, 102, 241)), "btnBorder"));
            t.Triggers.Add(hover);
            btn.Template = t;

            btn.Click += (s, e) => onClick();
            return btn;
        }

        private MenuItem CreateContextMenuItem(string header, Action onClick, string shortcut = null, Geometry iconGeo = null, Brush iconBrush = null)
        {
            var mi = new MenuItem
            {
                Header = header,
                InputGestureText = shortcut
            };
            ApplyModernMenuItemStyle(mi);
            if (iconGeo != null)
            {
                var brush = iconBrush ?? new SolidColorBrush(Color.FromRgb(203, 213, 225));
                mi.Icon = VectorIcons.CreateIcon(iconGeo, brush, null, 14, 1.4);
            }
            mi.Click += (s, e) => onClick();
            return mi;
        }

        private ContextMenu CreateLayerContextMenu(LayerNode layer = null)
        {
            var cm = new ContextMenu();
            ApplyModernContextMenuStyle(cm);

            var targetLayer = layer ?? _selectedLayers.FirstOrDefault();
            var purpleBrush = new SolidColorBrush(Color.FromRgb(167, 139, 250));
            var redBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            var blueBrush = new SolidColorBrush(Color.FromRgb(96, 165, 250));

            cm.Items.Add(CreateContextMenuItem("Cut", () => CutSelectedLayers(), "Ctrl + X", VectorIcons.CutGeometry));
            cm.Items.Add(CreateContextMenuItem("Copy", () => CopySelectedLayers(), "Ctrl + C", VectorIcons.CopyGeometry));
            cm.Items.Add(CreateContextMenuItem("Paste", () => PasteLayers(), "Ctrl + V", VectorIcons.PasteGeometry));
            cm.Items.Add(CreateContextMenuItem("Paste Here", () => PasteLayersHere(_lastRightClickCanvasPoint), "Ctrl + Shift + V", VectorIcons.PasteGeometry));
            cm.Items.Add(CreateContextMenuItem("Duplicate Layer", () => DuplicateSelectedLayers(), "Ctrl + D", VectorIcons.DuplicateGeometry));
            if (targetLayer != null)
            {
                cm.Items.Add(CreateContextMenuItem("Rename", () => StartRename(targetLayer.Id), "F2", VectorIcons.PenGeometry));
            }
            cm.Items.Add(CreateContextMenuItem("Delete Layer", () => DeleteSelectedLayers(), "Delete", VectorIcons.TrashGeometry, redBrush));
            cm.Items.Add(CreateModernSeparator());

            cm.Items.Add(CreateContextMenuItem("Frame Selection", () => FrameSelectedLayers(), "Ctrl + Alt + G", VectorIcons.FrameGeometry, blueBrush));
            cm.Items.Add(CreateContextMenuItem("Group Selection", () => GroupSelectedLayers(), "Ctrl + G", VectorIcons.GroupGeometry));
            cm.Items.Add(CreateContextMenuItem("Ungroup", () => UngroupSelectedLayers(), "Ctrl + Shift + G", VectorIcons.UngroupGeometry));

            if (targetLayer != null && targetLayer.IsMasterComponent)
            {
                cm.Items.Add(CreateContextMenuItem("❖ Create Instance", () => CreateComponentInstance(targetLayer), null, VectorIcons.ComponentGeometry, purpleBrush));
                cm.Items.Add(CreateContextMenuItem("Detach Component", () => DetachComponentInstance(targetLayer), null, VectorIcons.InstanceGeometry));
            }
            else if (targetLayer != null && (targetLayer.Type == ElementType.ComponentInstance || !string.IsNullOrEmpty(targetLayer.ComponentId)))
            {
                cm.Items.Add(CreateContextMenuItem("Go to Main Component", () => GoToMasterComponent(targetLayer.ComponentId), null, VectorIcons.ComponentGeometry, purpleBrush));
                cm.Items.Add(CreateContextMenuItem("Reset Overrides", () => ResetComponentOverrides(targetLayer), null, VectorIcons.UndoGeometry));
                cm.Items.Add(CreateContextMenuItem("Detach Instance", () => DetachComponentInstance(targetLayer), "Ctrl + Alt + B", VectorIcons.InstanceGeometry));
            }
            else
            {
                cm.Items.Add(CreateContextMenuItem("❖ Create Component", () => CreateComponentFromSelection(), "Ctrl + Alt + K", VectorIcons.ComponentGeometry, purpleBrush));
            }

            cm.Items.Add(CreateContextMenuItem("Add Auto Layout", () => AutoAlignSelectedLayers(), "Shift + A", VectorIcons.AutoLayoutGeometry, blueBrush));
            cm.Items.Add(CreateModernSeparator());

            cm.Items.Add(CreateContextMenuItem("Bring Forward", () => MoveSelectedLayerZ(true, false), "Ctrl + ]", VectorIcons.LayerUpGeometry));
            cm.Items.Add(CreateContextMenuItem("Send Backward", () => MoveSelectedLayerZ(false, false), "Ctrl + [", VectorIcons.LayerDownGeometry));
            cm.Items.Add(CreateContextMenuItem("Bring to Front", () => MoveSelectedLayerZ(true, true), "Ctrl + Shift + ]", VectorIcons.LayerUpGeometry));
            cm.Items.Add(CreateContextMenuItem("Send to Back", () => MoveSelectedLayerZ(false, true), "Ctrl + Shift + [", VectorIcons.LayerDownGeometry));
            cm.Items.Add(CreateModernSeparator());

            var alignSub = new MenuItem
            {
                Header = "Align"
            };
            ApplyModernMenuItemStyle(alignSub);
            alignSub.Icon = VectorIcons.CreateIcon(VectorIcons.AlignCenterGeometry, new SolidColorBrush(Color.FromRgb(203, 213, 225)), null, 14, 1.4);
            alignSub.Items.Add(CreateContextMenuItem("Align Left", () => AlignSelected(0), "Alt + A", VectorIcons.AlignLeftGeometry));
            alignSub.Items.Add(CreateContextMenuItem("Align Horizontal Center", () => AlignSelected(1), "Alt + H", VectorIcons.AlignCenterGeometry));
            alignSub.Items.Add(CreateContextMenuItem("Align Right", () => AlignSelected(2), "Alt + D", VectorIcons.AlignRightGeometry));
            alignSub.Items.Add(CreateContextMenuItem("Align Top", () => AlignSelected(3), "Alt + W", VectorIcons.AlignTopGeometry));
            alignSub.Items.Add(CreateContextMenuItem("Align Vertical Center", () => AlignSelected(4), "Alt + V", VectorIcons.AlignMiddleGeometry));
            alignSub.Items.Add(CreateContextMenuItem("Align Bottom", () => AlignSelected(5), "Alt + S", VectorIcons.AlignBottomGeometry));
            cm.Items.Add(alignSub);

            cm.Items.Add(CreateContextMenuItem("Flip Horizontal", () => FlipSelectedLayers(true), "Shift + H", VectorIcons.FlipHGeometry));
            cm.Items.Add(CreateContextMenuItem("Flip Vertical", () => FlipSelectedLayers(false), "Shift + V", VectorIcons.FlipVGeometry));
            cm.Items.Add(CreateModernSeparator());

            cm.Items.Add(CreateContextMenuItem("Lock / Unlock", () => ToggleLockSelectedLayers(), "Ctrl + Shift + L", VectorIcons.LockGeometry));
            cm.Items.Add(CreateContextMenuItem("Hide / Show", () => ToggleVisibilitySelectedLayers(), "Ctrl + Shift + H", VectorIcons.EyeGeometry));
            cm.Items.Add(CreateModernSeparator());
            cm.Items.Add(CreateContextMenuItem("Export Selection...", () => ShowExportDialog(), "Ctrl + Shift + E", VectorIcons.ExportGeometry));

            return cm;
        }

        private ContextMenu CreateCanvasContextMenu(Point canvasPt)
        {
            var cm = new ContextMenu();
            ApplyModernContextMenuStyle(cm);

            cm.Items.Add(CreateContextMenuItem("Paste", () => PasteLayers(), "Ctrl + V", VectorIcons.PasteGeometry));
            cm.Items.Add(CreateContextMenuItem("Paste Here", () => PasteLayersHere(canvasPt), "Ctrl + Shift + V", VectorIcons.PasteGeometry));
            cm.Items.Add(CreateContextMenuItem("Select All", () => SelectAllLayers(), "Ctrl + A", VectorIcons.SelectGeometry));
            cm.Items.Add(CreateModernSeparator());

            cm.Items.Add(CreateContextMenuItem("Zoom to 100%", () => ZoomTo100(), "Shift + 0", VectorIcons.ZoomGeometry));
            cm.Items.Add(CreateContextMenuItem("Zoom to Fit", () => ZoomToFit(), "Shift + 1", VectorIcons.ZoomGeometry));
            cm.Items.Add(CreateContextMenuItem("Zoom to Selection", () => ZoomToSelection(), "Shift + 2", VectorIcons.ZoomGeometry));
            cm.Items.Add(CreateModernSeparator());

            cm.Items.Add(CreateContextMenuItem("Add Frame", () => SetActiveTool(ToolType.Frame), "F", VectorIcons.FrameGeometry));
            cm.Items.Add(CreateContextMenuItem("Add Rectangle", () => SetActiveTool(ToolType.Rect), "R", VectorIcons.RectangleGeometry));
            cm.Items.Add(CreateContextMenuItem("Add Ellipse", () => SetActiveTool(ToolType.Ellipse), "O", VectorIcons.EllipseGeometry));
            cm.Items.Add(CreateContextMenuItem("Add Text", () => SetActiveTool(ToolType.Text), "T", VectorIcons.TextGeometry));
            cm.Items.Add(CreateContextMenuItem("Add Pen / Path", () => SetActiveTool(ToolType.Pen), "P", VectorIcons.PenGeometry));
            cm.Items.Add(CreateModernSeparator());
            cm.Items.Add(CreateContextMenuItem("Export Document...", () => ShowExportDialog(), "Ctrl + Shift + E", VectorIcons.ExportGeometry));

            return cm;
        }

        #endregion

        #region Right Panel (Property Inspector)

        private FrameworkElement CreateInspectorPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(33, 37, 52)), // #212534
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 58, 80)), // #323A50
                BorderThickness = new Thickness(1, 0, 0, 0)
            };

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _propertyInspectorStack = new StackPanel { Margin = new Thickness(16, 14, 16, 24) };
            scroll.Content = _propertyInspectorStack;
            border.Child = scroll;

            RefreshPropertyInspector();
            return border;
        }

        public void RefreshPropertyInspector()
        {
            if (_propertyInspectorStack == null) return;
            _propertyInspectorStack.Children.Clear();
            if (Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            // If currently in Vector Edit Mode, render dedicated node editor
            if (_isVectorEditing && _editingVectorNode != null && _editingVectorNode.Geometry != null)
            {
                BuildVectorNodeInspector();
                return;
            }

            if (_selectedLayers.Count == 0)
            {
                // Page settings
                _propertyInspectorStack.Children.Add(CreateInspectorSection("Page Settings"));
                _propertyInspectorStack.Children.Add(CreateColorRow("Canvas BG", page.BackgroundColor, hex =>
                {
                    page.BackgroundColor = hex;
                    RefreshCanvas();
                }));
                return;
            }

            // Top Alignment Toolbar
            var alignBar = CreateAlignmentToolbar();
            _propertyInspectorStack.Children.Add(alignBar);

            // If exactly 2 layers selected, show dedicated 2-Layer Arrangement Card!
            if (_selectedLayers.Count == 2)
            {
                _propertyInspectorStack.Children.Add(CreateTwoLayerArrangementCard(_selectedLayers[0], _selectedLayers[1]));
            }

            var primary = _selectedLayers[0];

            // If Master Component or Instance, show dedicated Component Inspector Card
            if (primary.IsMasterComponent)
            {
                _propertyInspectorStack.Children.Add(CreateInspectorSection("Master Component"));
                var compCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 123, 97, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(123, 97, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var compStack = new StackPanel();
                var headerText = new TextBlock
                {
                    Text = "❖ Main Component",
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(162, 89, 255)),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                compStack.Children.Add(headerText);

                var instBtn = CreateSmallButton("❖ Create Instance", "Create linked component instance", () => CreateComponentInstance(primary));
                instBtn.Height = 26;
                compStack.Children.Add(instBtn);
                compCard.Child = compStack;
                _propertyInspectorStack.Children.Add(compCard);
            }
            else if (primary.Type == ElementType.ComponentInstance || !string.IsNullOrEmpty(primary.ComponentId))
            {
                _propertyInspectorStack.Children.Add(CreateInspectorSection("Component Instance"));
                var instCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 162, 89, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(162, 89, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var instStack = new StackPanel();
                var headerText = new TextBlock
                {
                    Text = "◇ Instance",
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(196, 150, 255)),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                instStack.Children.Add(headerText);

                var btnRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var goMasterBtn = CreateSmallButton("Go to Main", "Select master component", () => GoToMasterComponent(primary.ComponentId));
                goMasterBtn.Height = 24;
                Grid.SetColumn(goMasterBtn, 0);
                btnRow.Children.Add(goMasterBtn);

                var resetBtn = CreateSmallButton("Reset Overrides", "Reset all overrides to master", () => ResetComponentOverrides(primary));
                resetBtn.Height = 24;
                Grid.SetColumn(resetBtn, 2);
                btnRow.Children.Add(resetBtn);
                instStack.Children.Add(btnRow);

                var detachBtn = CreateSmallButton("Detach Instance (Ctrl+Alt+B)", "Detach instance into regular layer", () => DetachComponentInstance(primary));
                detachBtn.Height = 24;
                detachBtn.Margin = new Thickness(0, 4, 0, 0);
                instStack.Children.Add(detachBtn);

                instCard.Child = instStack;
                _propertyInspectorStack.Children.Add(instCard);
            }
            if (primary.Type == ElementType.Frame)
            {
                _propertyInspectorStack.Children.Add(CreateInspectorSection("Frame Properties"));
                var frameCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(24, 28, 40)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(40, 48, 70)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var frameDock = new DockPanel();
                var frameLabel = new TextBlock
                {
                    Text = "Frame Clipping",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(frameLabel, Dock.Left);
                frameDock.Children.Add(frameLabel);

                var clipToggle = new CheckBox
                {
                    Content = "Clip content",
                    IsChecked = primary.ClipContent,
                    Foreground = new SolidColorBrush(Color.FromRgb(170, 185, 210)),
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand
                };
                clipToggle.Checked += (s, e) =>
                {
                    primary.ClipContent = true;
                    _sceneGraph.InvalidateNode(primary.Id);
                    RefreshCanvas();
                };
                clipToggle.Unchecked += (s, e) =>
                {
                    primary.ClipContent = false;
                    _sceneGraph.InvalidateNode(primary.Id);
                    RefreshCanvas();
                };
                frameDock.Children.Add(clipToggle);
                frameCard.Child = frameDock;
                _propertyInspectorStack.Children.Add(frameCard);
            }

            // If Path layer selected, offer Vector Node Edit & Curve buttons
            if (primary.Type == ElementType.Path)
            {
                var editVectorBtn = new Button
                {
                    Content = "✎ Edit Vector Nodes (Enter / Double-Click)",
                    Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    Height = 32,
                    Margin = new Thickness(0, 0, 0, 8),
                    Cursor = Cursors.Hand,
                    Template = CreateRoundedButtonTemplate(6)
                };
                editVectorBtn.Click += (s, e) => EnterVectorEditMode(primary);
                _propertyInspectorStack.Children.Add(editVectorBtn);

                if (primary.Geometry != null)
                {
                    var curveRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                    var sBtn = CreateSmallButton("〰 Smooth All", "Curve all corners", () =>
                    {
                        BezierVectorEngine.SmoothAllNodes(primary.Geometry);
                        InvalidateAndRefresh(primary);
                    });
                    sBtn.Width = 90;
                    var shBtn = CreateSmallButton("⌢ Sharpen All", "Sharpen all corners", () =>
                    {
                        BezierVectorEngine.SharpenAllNodes(primary.Geometry);
                        InvalidateAndRefresh(primary);
                    });
                    shBtn.Width = 90;
                    curveRow.Children.Add(sBtn);
                    curveRow.Children.Add(shBtn);
                    _propertyInspectorStack.Children.Add(curveRow);
                }
            }

            // 1. Transform Section (X, Y, W, H, Rotation, CornerRadius with Scrub Controls & Smooth Sliders)
            _propertyInspectorStack.Children.Add(CreateInspectorSection("Transform"));
            var row1 = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var xBox = ScrubControls.CreateScrubField("X", primary.X, val => { primary.X = val; InvalidateAndRefresh(primary); });
            var yBox = ScrubControls.CreateScrubField("Y", primary.Y, val => { primary.Y = val; InvalidateAndRefresh(primary); });
            Grid.SetColumn(xBox, 0); Grid.SetColumn(yBox, 2);
            row1.Children.Add(xBox); row1.Children.Add(yBox);
            _propertyInspectorStack.Children.Add(row1);

            var row2 = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var wBox = ScrubControls.CreateScrubField("W", primary.Width, val =>
            {
                primary.Width = Math.Max(1, val);
                if (primary.AutoLayout != null && primary.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    primary.AutoLayout.SizingH = SizingMode.Fixed;
                    AutoLayoutEngine.Reflow(primary);
                }
                InvalidateAndRefresh(primary);
            }, min: 1);
            var hBox = ScrubControls.CreateScrubField("H", primary.Height, val =>
            {
                primary.Height = Math.Max(1, val);
                if (primary.AutoLayout != null && primary.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    primary.AutoLayout.SizingV = SizingMode.Fixed;
                    AutoLayoutEngine.Reflow(primary);
                }
                InvalidateAndRefresh(primary);
            }, min: 1);
            Grid.SetColumn(wBox, 0); Grid.SetColumn(hBox, 2);
            row2.Children.Add(wBox); row2.Children.Add(hBox);
            _propertyInspectorStack.Children.Add(row2);

            var row3 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rotBox = ScrubControls.CreateScrubField("∠", primary.Rotation, val => { primary.Rotation = val; InvalidateAndRefresh(primary); }, suffix: "°");
            var crBox = ScrubControls.CreateScrubField("Radius", primary.CornerRadius, val => { primary.CornerRadius = Math.Max(0, val); InvalidateAndRefresh(primary); }, min: 0, suffix: "px");
            Grid.SetColumn(rotBox, 0); Grid.SetColumn(crBox, 2);
            row3.Children.Add(rotBox); row3.Children.Add(crBox);
            _propertyInspectorStack.Children.Add(row3);

            // Shape Corner Curving Controls (Smooth Slider & Quick Presets: 0px, 4px, 8px, 16px, 24px, Pill)
            if (primary.Type == ElementType.Rectangle || primary.Type == ElementType.Frame || primary.Type == ElementType.Group || primary.Type == ElementType.Image)
            {
                double maxRadius = Math.Max(10.0, Math.Min(primary.Width, primary.Height) / 2.0);

                var crSlider = ScrubControls.CreateSmoothSlider("Corner Curve", primary.CornerRadius, 0.0, maxRadius, val =>
                {
                    primary.CornerRadius = Math.Round(val);
                    InvalidateAndRefresh(primary);
                }, "px");
                _propertyInspectorStack.Children.Add(crSlider);

                var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                double[] presets = new double[] { 0, 4, 8, 16, 24 };
                foreach (var r in presets)
                {
                    double prVal = r;
                    var pBtn = CreateSmallButton(prVal + "px", "Set radius to " + prVal + "px", () =>
                    {
                        primary.CornerRadius = prVal;
                        InvalidateAndRefresh(primary);
                        RefreshPropertyInspector();
                    });
                    pBtn.Width = 32;
                    presetRow.Children.Add(pBtn);
                }
                var pillBtn = CreateSmallButton("Pill", "Fully rounded capsule", () =>
                {
                    primary.CornerRadius = maxRadius;
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                });
                pillBtn.Width = 36;
                presetRow.Children.Add(pillBtn);
                _propertyInspectorStack.Children.Add(presetRow);
            }

            // Layer Opacity Smooth Slider
            _propertyInspectorStack.Children.Add(ScrubControls.CreateSmoothSlider("Layer Opacity", Math.Round(primary.Opacity * 100), 0, 100, val =>
            {
                primary.Opacity = val / 100.0;
                InvalidateAndRefresh(primary);
            }, "%"));

            // 2. Auto Layout (if Frame or Group)
            if (primary.Type == ElementType.Frame || primary.Type == ElementType.Group)
            {
                _propertyInspectorStack.Children.Add(CreateInspectorSection("Auto Layout"));
                _propertyInspectorStack.Children.Add(CreateAutoLayoutCard(primary));
            }

            var activePage = Document != null ? Document.GetActivePage() : null;
            LayerNode parentNode = null;
            if (activePage != null)
            {
                activePage.FindContainer(primary, out parentNode);
            }
            if (parentNode != null && parentNode.AutoLayout != null && parentNode.AutoLayout.Mode != AutoLayoutMode.None)
            {
                _propertyInspectorStack.Children.Add(CreateInspectorSection("Auto Layout Child"));
                _propertyInspectorStack.Children.Add(CreateAutoLayoutChildCard(primary, parentNode));
            }

            // 3. Multiple Fills Section (Figma style)
            BuildFillsSection(primary);

            // 4. Multiple Strokes Section (Figma style)
            BuildStrokesSection(primary);

            // 5. Effects Section (Drop Shadow & Blur)
            // 5. Effects Section (Drop Shadow, Inner Shadow, Layer Blur, Liquid Glass, Grain, Background Blur)
            BuildEffectsSection(primary);

            // 6. Typography Section (if Text - System Fonts, Alignments, Resizing, Style)
            if (primary.Type == ElementType.Text)
            {
                BuildTypographySection(primary);
            }
        }

        private void BuildVectorNodeInspector()
        {
            var geo = _editingVectorNode.Geometry;

            // Header & Done Button
            _propertyInspectorStack.Children.Add(CreateInspectorSection("Vector Edit Mode"));

            var doneBtn = new Button
            {
                Content = "✓ Done Editing (Esc / Enter)",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Height = 30,
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand,
                Template = CreateRoundedButtonTemplate(6)
            };
            doneBtn.Click += (s, e) => ExitVectorEditMode();
            _propertyInspectorStack.Children.Add(doneBtn);

            if (_selectedNodeIndex >= 0 && _selectedNodeIndex < geo.Nodes.Count)
            {
                var cur = geo.Nodes[_selectedNodeIndex];
                var subHeader = new TextBlock
                {
                    Text = string.Format(CultureInfo.InvariantCulture, "Node {0} of {1}", _selectedNodeIndex + 1, geo.Nodes.Count),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                _propertyInspectorStack.Children.Add(subHeader);

                // Coordinates X and Y
                var coordGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                coordGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                coordGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var xField = CreateNumberField("Anchor X", cur.Anchor.X, val =>
                {
                    BezierVectorEngine.MoveAnchor(geo, _selectedNodeIndex, new Point(val, cur.Anchor.Y));
                    InvalidateAndRefresh(_editingVectorNode);
                });
                var yField = CreateNumberField("Anchor Y", cur.Anchor.Y, val =>
                {
                    BezierVectorEngine.MoveAnchor(geo, _selectedNodeIndex, new Point(cur.Anchor.X, val));
                    InvalidateAndRefresh(_editingVectorNode);
                });
                Grid.SetColumn(xField, 0); Grid.SetColumn(yField, 1);
                coordGrid.Children.Add(xField); coordGrid.Children.Add(yField);
                _propertyInspectorStack.Children.Add(coordGrid);

                // Point Type Toggle: Corner / Smooth / Symmetric
                var typeLabel = new TextBlock
                {
                    Text = "Point Type",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                    Margin = new Thickness(0, 4, 0, 4)
                };
                _propertyInspectorStack.Children.Add(typeLabel);

                var typeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                typeRow.Children.Add(CreateTypeButton("Corner", cur.Type == BezierNodeType.Corner, () =>
                {
                    BezierVectorEngine.SetNodeType(geo, _selectedNodeIndex, BezierNodeType.Corner);
                    InvalidateAndRefresh(_editingVectorNode);
                    RefreshPropertyInspector();
                }));
                typeRow.Children.Add(CreateTypeButton("Smooth", cur.Type == BezierNodeType.Smooth, () =>
                {
                    BezierVectorEngine.SetNodeType(geo, _selectedNodeIndex, BezierNodeType.Smooth);
                    InvalidateAndRefresh(_editingVectorNode);
                    RefreshPropertyInspector();
                }));
                typeRow.Children.Add(CreateTypeButton("Symmetric", cur.Type == BezierNodeType.Symmetric, () =>
                {
                    BezierVectorEngine.SetNodeType(geo, _selectedNodeIndex, BezierNodeType.Symmetric);
                    InvalidateAndRefresh(_editingVectorNode);
                    RefreshPropertyInspector();
                }));
                typeRow.Children.Add(CreateTypeButton("Split", cur.Type == BezierNodeType.Split, () =>
                {
                    BezierVectorEngine.SetNodeType(geo, _selectedNodeIndex, BezierNodeType.Split);
                    InvalidateAndRefresh(_editingVectorNode);
                    RefreshPropertyInspector();
                }));
                _propertyInspectorStack.Children.Add(typeRow);

                var splitActionsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                var splitHandlesBtn = CreateSmallButton("⤹ Split Handles", "Decouple In and Out handles (hold Alt while dragging)", () =>
                {
                    BezierVectorEngine.SetNodeType(geo, _selectedNodeIndex, BezierNodeType.Split);
                    InvalidateAndRefresh(_editingVectorNode);
                    RefreshPropertyInspector();
                });
                splitHandlesBtn.Width = 100;
                var splitPathBtn = CreateSmallButton("✂ Split Path", "Cut open path at this anchor point", () =>
                {
                    BezierVectorEngine.SplitPathAtNode(geo, _selectedNodeIndex);
                    InvalidateAndRefresh(_editingVectorNode);
                    RefreshPropertyInspector();
                });
                splitPathBtn.Width = 85;
                splitActionsRow.Children.Add(splitHandlesBtn);
                splitActionsRow.Children.Add(splitPathBtn);
                _propertyInspectorStack.Children.Add(splitActionsRow);

                // Delete Node button
                var delNodeBtn = new Button
                {
                    Content = "🗑 Delete Node",
                    Background = new SolidColorBrush(Color.FromArgb(50, 239, 68, 68)),
                    Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    BorderThickness = new Thickness(1),
                    Height = 26,
                    Margin = new Thickness(0, 0, 0, 12),
                    Cursor = Cursors.Hand,
                    Template = CreateRoundedButtonTemplate(5)
                };
                delNodeBtn.Click += (s, e) =>
                {
                    BezierVectorEngine.RemoveNode(geo, _selectedNodeIndex);
                    if (geo.Nodes.Count == 0)
                    {
                        DeleteSelectedLayers();
                        ExitVectorEditMode();
                    }
                    else
                    {
                        _selectedNodeIndex = Math.Min(_selectedNodeIndex, geo.Nodes.Count - 1);
                        InvalidateAndRefresh(_editingVectorNode);
                        RefreshPropertyInspector();
                    }
                };
                _propertyInspectorStack.Children.Add(delNodeBtn);
            }

            // Global Vector Actions
            _propertyInspectorStack.Children.Add(CreateInspectorSection("Curve & Path Options"));

            var curveActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var smoothAllBtn = CreateSmallButton("〰 Smooth All", "Convert all corners to smooth curves", () =>
            {
                BezierVectorEngine.SmoothAllNodes(geo);
                InvalidateAndRefresh(_editingVectorNode);
                RefreshPropertyInspector();
            });
            smoothAllBtn.Width = 90;
            var sharpenAllBtn = CreateSmallButton("⌢ Sharpen All", "Convert all nodes to sharp corners", () =>
            {
                BezierVectorEngine.SharpenAllNodes(geo);
                InvalidateAndRefresh(_editingVectorNode);
                RefreshPropertyInspector();
            });
            sharpenAllBtn.Width = 90;
            curveActions.Children.Add(smoothAllBtn);
            curveActions.Children.Add(sharpenAllBtn);
            _propertyInspectorStack.Children.Add(curveActions);

            var closeBtn = new Button
            {
                Content = geo.IsClosed ? "Open Path" : "Close Path (Loop)",
                Background = new SolidColorBrush(Color.FromRgb(30, 35, 52)),
                Foreground = Brushes.White,
                Height = 26,
                Margin = new Thickness(0, 0, 0, 12),
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (s, e) =>
            {
                BezierVectorEngine.ToggleClosed(geo);
                InvalidateAndRefresh(_editingVectorNode);
                RefreshPropertyInspector();
            };
            _propertyInspectorStack.Children.Add(closeBtn);
        }

        private Button CreateTypeButton(string label, bool isActive, Action onClick)
        {
            var btn = new Button
            {
                Content = label,
                FontSize = 10,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 4, 0),
                Background = isActive ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(30, 35, 52)),
                Foreground = isActive ? Brushes.Black : Brushes.White,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Template = CreateRoundedButtonTemplate(5)
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private FrameworkElement CreateAutoLayoutCard(LayerNode frame)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var layout = frame.AutoLayout ?? new AutoLayoutData();
            frame.AutoLayout = layout;

            // Mode dropdown
            var modeCombo = CreateModernComboBox(28);
            modeCombo.Margin = new Thickness(0, 0, 0, 8);
            modeCombo.Items.Add(CreateModernComboBoxItem("None"));
            modeCombo.Items.Add(CreateModernComboBoxItem("Horizontal"));
            modeCombo.Items.Add(CreateModernComboBoxItem("Vertical"));
            modeCombo.Items.Add(CreateModernComboBoxItem("Wrap"));
            modeCombo.Items.Add(CreateModernComboBoxItem("Grid"));
            modeCombo.SelectedIndex = (int)layout.Mode;

            modeCombo.SelectionChanged += (s, e) =>
            {
                layout.Mode = (AutoLayoutMode)modeCombo.SelectedIndex;
                AutoLayoutEngine.Reflow(frame);
                InvalidateAndRefresh(frame);
                RefreshPropertyInspector();
            };
            stack.Children.Add(modeCombo);

            if (layout.Mode != AutoLayoutMode.None)
            {
                // Sizing Modes (Width & Height: Hug contents / Fixed size)
                var sizingGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                sizingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                sizingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                sizingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var sizingHCombo = CreateModernComboBox(26);
                sizingHCombo.Items.Add(CreateModernComboBoxItem("W: Fixed"));
                sizingHCombo.Items.Add(CreateModernComboBoxItem("W: Hug"));
                sizingHCombo.SelectedIndex = (layout.SizingH == SizingMode.Hug) ? 1 : 0;
                sizingHCombo.SelectionChanged += (s, e) =>
                {
                    layout.SizingH = (sizingHCombo.SelectedIndex == 1) ? SizingMode.Hug : SizingMode.Fixed;
                    AutoLayoutEngine.Reflow(frame);
                    InvalidateAndRefresh(frame);
                    RefreshPropertyInspector();
                };
                Grid.SetColumn(sizingHCombo, 0);
                sizingGrid.Children.Add(sizingHCombo);

                var sizingVCombo = CreateModernComboBox(26);
                sizingVCombo.Items.Add(CreateModernComboBoxItem("H: Fixed"));
                sizingVCombo.Items.Add(CreateModernComboBoxItem("H: Hug"));
                sizingVCombo.SelectedIndex = (layout.SizingV == SizingMode.Hug) ? 1 : 0;
                sizingVCombo.SelectionChanged += (s, e) =>
                {
                    layout.SizingV = (sizingVCombo.SelectedIndex == 1) ? SizingMode.Hug : SizingMode.Fixed;
                    AutoLayoutEngine.Reflow(frame);
                    InvalidateAndRefresh(frame);
                    RefreshPropertyInspector();
                };
                Grid.SetColumn(sizingVCombo, 2);
                sizingGrid.Children.Add(sizingVCombo);

                stack.Children.Add(sizingGrid);

                stack.Children.Add(CreateNumberField("Gap", layout.Gap, val =>
                {
                    layout.Gap = Math.Max(-500.0, val);
                    AutoLayoutEngine.Reflow(frame);
                    InvalidateAndRefresh(frame);
                }));

                stack.Children.Add(CreateNumberField("Padding", layout.PaddingLeft, val =>
                {
                    layout.PaddingLeft = Math.Max(0, val);
                    layout.PaddingRight = layout.PaddingLeft;
                    layout.PaddingTop = layout.PaddingLeft;
                    layout.PaddingBottom = layout.PaddingLeft;
                    AutoLayoutEngine.Reflow(frame);
                    InvalidateAndRefresh(frame);
                }));

                var alignCombo = CreateModernComboBox(28);
                alignCombo.Margin = new Thickness(0, 0, 0, 8);
                alignCombo.Items.Add(CreateModernComboBoxItem("Align: Start"));
                alignCombo.Items.Add(CreateModernComboBoxItem("Align: Center"));
                alignCombo.Items.Add(CreateModernComboBoxItem("Align: End"));
                alignCombo.Items.Add(CreateModernComboBoxItem("Align: Baseline"));
                alignCombo.SelectedIndex = (int)layout.AlignItems;
                alignCombo.SelectionChanged += (s, e) =>
                {
                    layout.AlignItems = (AlignItems)alignCombo.SelectedIndex;
                    AutoLayoutEngine.Reflow(frame);
                    InvalidateAndRefresh(frame);
                };
                stack.Children.Add(alignCombo);

                var justifyCombo = CreateModernComboBox(28);
                justifyCombo.Margin = new Thickness(0, 0, 0, 8);
                justifyCombo.Items.Add(CreateModernComboBoxItem("Distribution: Start"));
                justifyCombo.Items.Add(CreateModernComboBoxItem("Distribution: Center"));
                justifyCombo.Items.Add(CreateModernComboBoxItem("Distribution: End"));
                justifyCombo.Items.Add(CreateModernComboBoxItem("Distribution: Space Between"));
                justifyCombo.Items.Add(CreateModernComboBoxItem("Distribution: Space Around"));
                justifyCombo.Items.Add(CreateModernComboBoxItem("Distribution: Space Evenly"));
                justifyCombo.SelectedIndex = (int)layout.JustifyContent;
                justifyCombo.SelectionChanged += (s, e) =>
                {
                    layout.JustifyContent = (JustifyContent)justifyCombo.SelectedIndex;
                    AutoLayoutEngine.Reflow(frame);
                    InvalidateAndRefresh(frame);
                };
                stack.Children.Add(justifyCombo);
            }

            return stack;
        }

        private FrameworkElement CreateAutoLayoutChildCard(LayerNode child, LayerNode parent)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

            var ignoreBox = new CheckBox
            {
                Content = "Ignore Auto Layout (Independent)",
                IsChecked = child.IgnoreAutoLayout,
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            ignoreBox.Click += (s, e) =>
            {
                child.IgnoreAutoLayout = (ignoreBox.IsChecked == true);
                AutoLayoutEngine.Reflow(parent);
                InvalidateAndRefresh(parent);
                RefreshCanvas();
            };
            stack.Children.Add(ignoreBox);

            var sizingPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            var sizingHCombo = CreateModernComboBox(26, 90);
            sizingHCombo.Margin = new Thickness(0, 0, 6, 0);
            sizingHCombo.Items.Add(CreateModernComboBoxItem("Fixed W"));
            sizingHCombo.Items.Add(CreateModernComboBoxItem("Hug W"));
            sizingHCombo.Items.Add(CreateModernComboBoxItem("Fill W"));
            sizingHCombo.SelectedIndex = (int)child.LayoutSizingH;
            sizingHCombo.SelectionChanged += (s, e) =>
            {
                child.LayoutSizingH = (SizingMode)sizingHCombo.SelectedIndex;
                AutoLayoutEngine.Reflow(parent);
                InvalidateAndRefresh(parent);
                RefreshCanvas();
            };
            sizingPanel.Children.Add(sizingHCombo);

            var sizingVCombo = CreateModernComboBox(26, 90);
            sizingVCombo.Items.Add(CreateModernComboBoxItem("Fixed H"));
            sizingVCombo.Items.Add(CreateModernComboBoxItem("Hug H"));
            sizingVCombo.Items.Add(CreateModernComboBoxItem("Fill H"));
            sizingVCombo.SelectedIndex = (int)child.LayoutSizingV;
            sizingVCombo.SelectionChanged += (s, e) =>
            {
                child.LayoutSizingV = (SizingMode)sizingVCombo.SelectedIndex;
                AutoLayoutEngine.Reflow(parent);
                InvalidateAndRefresh(parent);
                RefreshCanvas();
            };
            sizingPanel.Children.Add(sizingVCombo);
            stack.Children.Add(sizingPanel);

            return stack;
        }

        private FrameworkElement CreateTwoLayerArrangementCard(LayerNode a, LayerNode b)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 28, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 70, 105)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel();

            // Header with badge
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var title = new TextBlock
            {
                Text = "2-Layer Arrangement",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(title, Dock.Left);
            headerDock.Children.Add(title);

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(38, 44, 64)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 2, 5, 2),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            badge.Child = new TextBlock
            {
                Text = string.Format("{0} & {1}", a.Name, b.Name),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 205)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 130
            };
            headerDock.Children.Add(badge);
            stack.Children.Add(headerDock);

            // Spacing Gap (scrubbable field, default 12px)
            double gap = 12.0;
            var gapRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            gapRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var gapField = ScrubControls.CreateScrubField("Gap", gap, val => { gap = Math.Max(0, val); }, min: 0, max: 1000, suffix: "px");
            gapRow.Children.Add(gapField);
            stack.Children.Add(gapRow);

            // Action wrappers to guarantee full Undo/Redo capability for positions & bounds
            Action<Action> applyArrangement = (mutateAction) =>
            {
                var beforePos = new Dictionary<LayerNode, Point>();
                var beforeGeos = new Dictionary<LayerNode, VectorGeometry>();
                beforePos[a] = new Point(a.X, a.Y);
                beforePos[b] = new Point(b.X, b.Y);
                if (a.Type == ElementType.Path && a.Geometry != null) beforeGeos[a] = a.Geometry.Clone();
                if (b.Type == ElementType.Path && b.Geometry != null) beforeGeos[b] = b.Geometry.Clone();

                mutateAction();

                var afterPos = new Dictionary<LayerNode, Point>();
                var afterGeos = new Dictionary<LayerNode, VectorGeometry>();
                afterPos[a] = new Point(a.X, a.Y);
                afterPos[b] = new Point(b.X, b.Y);
                if (a.Type == ElementType.Path && a.Geometry != null) afterGeos[a] = a.Geometry.Clone();
                if (b.Type == ElementType.Path && b.Geometry != null) afterGeos[b] = b.Geometry.Clone();

                var cmd = new MoveLayersPositionsCommand(beforePos, afterPos, beforeGeos, afterGeos, () =>
                {
                    _sceneGraph.InvalidateNode(a.Id);
                    _sceneGraph.InvalidateNode(b.Id);
                    RefreshCanvas();
                    RefreshPropertyInspector();
                });
                History.Execute(cmd);
            };

            Action<Action> applyBoundsArrangement = (mutateAction) =>
            {
                var beforeBounds = new Dictionary<LayerNode, Rect>();
                var beforeRots = new Dictionary<LayerNode, double>();
                var beforeGeos = new Dictionary<LayerNode, VectorGeometry>();
                beforeBounds[a] = a.GetBounds();
                beforeBounds[b] = b.GetBounds();
                beforeRots[a] = a.Rotation;
                beforeRots[b] = b.Rotation;
                if (a.Type == ElementType.Path && a.Geometry != null) beforeGeos[a] = a.Geometry.Clone();
                if (b.Type == ElementType.Path && b.Geometry != null) beforeGeos[b] = b.Geometry.Clone();

                mutateAction();

                var afterBounds = new Dictionary<LayerNode, Rect>();
                var afterRots = new Dictionary<LayerNode, double>();
                var afterGeos = new Dictionary<LayerNode, VectorGeometry>();
                afterBounds[a] = a.GetBounds();
                afterBounds[b] = b.GetBounds();
                afterRots[a] = a.Rotation;
                afterRots[b] = b.Rotation;
                if (a.Type == ElementType.Path && a.Geometry != null) afterGeos[a] = a.Geometry.Clone();
                if (b.Type == ElementType.Path && b.Geometry != null) afterGeos[b] = b.Geometry.Clone();

                var cmd = new TransformLayersCommand(beforeBounds, afterBounds, beforeRots, afterRots, beforeGeos, afterGeos, () =>
                {
                    _sceneGraph.InvalidateNode(a.Id);
                    _sceneGraph.InvalidateNode(b.Id);
                    RefreshCanvas();
                    RefreshPropertyInspector();
                });
                History.Execute(cmd);
            };

            // Row 1: Side-by-Side (Stack H) & Stack Vertically (Stack V)
            var row1 = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnSide = CreateSmallButton("↔ Side by Side", "Place layer 2 directly right of layer 1", () =>
            {
                applyArrangement(() =>
                {
                    double oldX = b.X;
                    double oldY = b.Y;
                    b.X = a.X + a.Width + gap;
                    b.Y = a.Y;
                    if (b.Type == ElementType.Path && b.Geometry != null)
                    {
                        BezierVectorEngine.OffsetGeometry(b.Geometry, b.X - oldX, b.Y - oldY);
                    }
                });
            });
            btnSide.Height = 26;
            Grid.SetColumn(btnSide, 0);
            row1.Children.Add(btnSide);

            var btnStack = CreateSmallButton("↕ Stack Below", "Place layer 2 directly below layer 1", () =>
            {
                applyArrangement(() =>
                {
                    double oldX = b.X;
                    double oldY = b.Y;
                    b.X = a.X;
                    b.Y = a.Y + a.Height + gap;
                    if (b.Type == ElementType.Path && b.Geometry != null)
                    {
                        BezierVectorEngine.OffsetGeometry(b.Geometry, b.X - oldX, b.Y - oldY);
                    }
                });
            });
            btnStack.Height = 26;
            Grid.SetColumn(btnStack, 2);
            row1.Children.Add(btnStack);
            stack.Children.Add(row1);

            // Row 2: Center Overlap & Swap Positions
            var row2 = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnCenter = CreateSmallButton("🎯 Center Overlap", "Center layer 2 precisely inside layer 1", () =>
            {
                applyArrangement(() =>
                {
                    double oldX = b.X;
                    double oldY = b.Y;
                    b.X = a.X + (a.Width - b.Width) / 2.0;
                    b.Y = a.Y + (a.Height - b.Height) / 2.0;
                    if (b.Type == ElementType.Path && b.Geometry != null)
                    {
                        BezierVectorEngine.OffsetGeometry(b.Geometry, b.X - oldX, b.Y - oldY);
                    }
                });
            });
            btnCenter.Height = 26;
            Grid.SetColumn(btnCenter, 0);
            row2.Children.Add(btnCenter);

            var btnSwap = CreateSmallButton("⇄ Swap Positions", "Swap coordinate locations of both layers", () =>
            {
                applyArrangement(() =>
                {
                    double oldAx = a.X, oldAy = a.Y;
                    double oldBx = b.X, oldBy = b.Y;
                    a.X = oldBx; a.Y = oldBy;
                    b.X = oldAx; b.Y = oldAy;
                    if (a.Type == ElementType.Path && a.Geometry != null) BezierVectorEngine.OffsetGeometry(a.Geometry, a.X - oldAx, a.Y - oldAy);
                    if (b.Type == ElementType.Path && b.Geometry != null) BezierVectorEngine.OffsetGeometry(b.Geometry, b.X - oldBx, b.Y - oldBy);
                });
            });
            btnSwap.Height = 26;
            Grid.SetColumn(btnSwap, 2);
            row2.Children.Add(btnSwap);
            stack.Children.Add(row2);

            // Row 3: Match Width & Match Height
            var row3 = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var btnMatchW = CreateSmallButton("📐 Match Width", "Match layer 2 width to layer 1", () =>
            {
                applyBoundsArrangement(() =>
                {
                    b.Width = a.Width;
                });
            });
            btnMatchW.Height = 26;
            Grid.SetColumn(btnMatchW, 0);
            row3.Children.Add(btnMatchW);

            var btnMatchH = CreateSmallButton("📐 Match Height", "Match layer 2 height to layer 1", () =>
            {
                applyBoundsArrangement(() =>
                {
                    b.Height = a.Height;
                });
            });
            btnMatchH.Height = 26;
            Grid.SetColumn(btnMatchH, 2);
            row3.Children.Add(btnMatchH);
            stack.Children.Add(row3);

            card.Child = stack;
            return card;
        }

        private FrameworkElement CreateAlignmentToolbar()
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            stack.Children.Add(CreateSmallButton("⇦", "Align Left (Alt+A)", () => AlignSelected(0)));
            stack.Children.Add(CreateSmallButton("↔", "Align Center (Alt+H)", () => AlignSelected(1)));
            stack.Children.Add(CreateSmallButton("⇨", "Align Right (Alt+D)", () => AlignSelected(2)));
            stack.Children.Add(CreateSmallButton("⇧", "Align Top (Alt+W)", () => AlignSelected(3)));
            stack.Children.Add(CreateSmallButton("↕", "Align Middle (Alt+V)", () => AlignSelected(4)));
            stack.Children.Add(CreateSmallButton("⇩", "Align Bottom (Alt+S)", () => AlignSelected(5)));
            stack.Children.Add(CreateSmallButton("⊞", "Auto Layout / Auto Align (Shift+A)", () => AutoAlignSelectedLayers()));
            return stack;
        }

        public void AlignSelected(int alignType)
        {
            if (_selectedLayers.Count == 0) return;
            var union = TransformEngine.UnionBounds(_selectedLayers);

            var beforePos = new Dictionary<LayerNode, Point>();
            var beforeGeos = new Dictionary<LayerNode, VectorGeometry>();
            foreach (var node in _selectedLayers)
            {
                beforePos[node] = new Point(node.X, node.Y);
                if (node.Type == ElementType.Path && node.Geometry != null) beforeGeos[node] = node.Geometry.Clone();
            }

            foreach (var node in _selectedLayers)
            {
                double oldX = node.X;
                double oldY = node.Y;
                switch (alignType)
                {
                    case 0: node.X = union.X; break;
                    case 1: node.X = union.X + (union.Width - node.Width) / 2.0; break;
                    case 2: node.X = union.X + union.Width - node.Width; break;
                    case 3: node.Y = union.Y; break;
                    case 4: node.Y = union.Y + (union.Height - node.Height) / 2.0; break;
                    case 5: node.Y = union.Y + union.Height - node.Height; break;
                }
                double shiftX = node.X - oldX;
                double shiftY = node.Y - oldY;
                if (node.Type == ElementType.Path && node.Geometry != null)
                {
                    BezierVectorEngine.OffsetGeometry(node.Geometry, shiftX, shiftY);
                }
            }

            var afterPos = new Dictionary<LayerNode, Point>();
            var afterGeos = new Dictionary<LayerNode, VectorGeometry>();
            foreach (var node in _selectedLayers)
            {
                afterPos[node] = new Point(node.X, node.Y);
                if (node.Type == ElementType.Path && node.Geometry != null) afterGeos[node] = node.Geometry.Clone();
            }

            var cmd = new MoveLayersPositionsCommand(beforePos, afterPos, beforeGeos, afterGeos, () =>
            {
                foreach (var node in _selectedLayers) _sceneGraph.InvalidateNode(node.Id);
                RefreshCanvas();
                RefreshPropertyInspector();
            });
            History.Execute(cmd);
        }

        private FrameworkElement CreateInspectorSection(string title)
        {
            var tb = new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                Margin = new Thickness(0, 12, 0, 8)
            };
            return tb;
        }

        private FrameworkElement CreateInspectorSectionWithAdd(string title, Action onAdd)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 12, 0, 8) };
            var tb = new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 150, 175)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(tb, Dock.Left);
            dock.Children.Add(tb);

            if (onAdd != null)
            {
                var addBtn = new Button
                {
                    Template = CreateRoundedButtonTemplate(4),
                    Content = "+",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Width = 22,
                    Height = 22,
                    Background = new SolidColorBrush(Color.FromRgb(35, 42, 60)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 90)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    ToolTip = "Add " + title
                };
                addBtn.Click += (s, e) => onAdd();
                DockPanel.SetDock(addBtn, Dock.Right);
                dock.Children.Add(addBtn);
            }
            return dock;
        }

        private void BuildFillsSection(LayerNode primary)
        {
            if (primary.Fills == null) primary.Fills = new List<Paint>();

            _propertyInspectorStack.Children.Add(CreateInspectorSectionWithAdd("Fills", () =>
            {
                primary.Fills.Add(Paint.Solid("#D9D9D9"));
                InvalidateAndRefresh(primary);
                RefreshPropertyInspector();
            }));

            if (primary.Fills.Count == 0)
            {
                var emptyBtn = CreateSmallButton("+ Add Fill", "Add solid or gradient fill", () =>
                {
                    primary.Fills.Add(Paint.Solid("#D9D9D9"));
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                });
                _propertyInspectorStack.Children.Add(emptyBtn);
                return;
            }

            for (int i = 0; i < primary.Fills.Count; i++)
            {
                int fillIdx = i;
                var fill = primary.Fills[fillIdx];

                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(24, 28, 40)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(38, 46, 66)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var cardStack = new StackPanel();

                // Top row: Eye, Type dropdown, Swatch, Delete button
                var topRow = new DockPanel { LastChildFill = true };

                var eyeBtn = new Button
                {
                    Template = CreateRoundedButtonTemplate(4),
                    Content = fill.Enabled ? "👁" : "—",
                    FontSize = 10,
                    Width = 22,
                    Height = 22,
                    Background = fill.Enabled ? new SolidColorBrush(Color.FromRgb(35, 42, 60)) : Brushes.Transparent,
                    Foreground = fill.Enabled ? Brushes.White : new SolidColorBrush(Color.FromRgb(100, 110, 130)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = fill.Enabled ? "Disable fill" : "Enable fill"
                };
                eyeBtn.Click += (s, e) =>
                {
                    fill.Enabled = !fill.Enabled;
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                };
                DockPanel.SetDock(eyeBtn, Dock.Left);
                topRow.Children.Add(eyeBtn);

                var delBtn = new Button
                {
                    Template = CreateRoundedButtonTemplate(4),
                    Content = "✕",
                    FontSize = 10,
                    Width = 20,
                    Height = 22,
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Remove fill"
                };
                delBtn.Click += (s, e) =>
                {
                    if (fillIdx < primary.Fills.Count)
                    {
                        primary.Fills.RemoveAt(fillIdx);
                        InvalidateAndRefresh(primary);
                        RefreshPropertyInspector();
                    }
                };
                DockPanel.SetDock(delBtn, Dock.Right);
                topRow.Children.Add(delBtn);

                // Swatch
                var swatch = new Border
                {
                    Width = 24,
                    Height = 22,
                    CornerRadius = new CornerRadius(3),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 70, 95)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(4, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Choose fill color/gradient"
                };
                if (fill.Type == PaintType.Solid)
                {
                    swatch.Background = new SolidColorBrush(GpuRenderer.ParseHexColor(fill.ColorHex, fill.Opacity));
                }
                else if (fill.Type == PaintType.LinearGradient)
                {
                    var lgb = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                    GpuRenderer.PopulateGradientStops(lgb.GradientStops, fill);
                    swatch.Background = lgb;
                }
                else
                {
                    var rgb = new RadialGradientBrush { Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
                    GpuRenderer.PopulateGradientStops(rgb.GradientStops, fill);
                    swatch.Background = rgb;
                }

                swatch.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    if (fill.Type != PaintType.Solid)
                    {
                        SetEditingGradient(primary, fill);
                    }
                    string initialColor = fill.ColorHex ?? "#D9D9D9";
                    if (fill.Type == PaintType.LinearGradient && fill.GradientStops != null && fill.GradientStops.Count > 0)
                        initialColor = fill.GradientStops[0].ColorHex;

                    ColorPickerPopup.ShowAt(swatch, initialColor, newHex =>
                    {
                        fill.ColorHex = newHex;
                        if (fill.GradientStops != null && fill.GradientStops.Count > 0)
                        {
                            fill.GradientStops[0].ColorHex = newHex;
                        }
                        InvalidateAndRefresh(primary);
                        RefreshPropertyInspector();
                    });
                };
                DockPanel.SetDock(swatch, Dock.Left);
                topRow.Children.Add(swatch);

                // Type combo
                var typeCombo = CreateModernComboBox(22);
                typeCombo.Margin = new Thickness(0, 0, 4, 0);
                typeCombo.Items.Add(CreateModernComboBoxItem("Solid"));
                typeCombo.Items.Add(CreateModernComboBoxItem("Linear Gradient"));
                typeCombo.Items.Add(CreateModernComboBoxItem("Radial Gradient"));
                typeCombo.SelectedIndex = (fill.Type == PaintType.LinearGradient) ? 1 : (fill.Type == PaintType.RadialGradient ? 2 : 0);
                typeCombo.SelectionChanged += (s, e) =>
                {
                    if (typeCombo.SelectedIndex == 1)
                    {
                        fill.Type = PaintType.LinearGradient;
                        if (fill.GradientStops == null || fill.GradientStops.Count == 0)
                        {
                            fill.GradientStops = new List<GradientStopData>
                            {
                                new GradientStopData(0.0, fill.ColorHex ?? "#D9D9D9", 1.0),
                                new GradientStopData(1.0, "#4B5563", 1.0)
                            };
                        }
                        SetEditingGradient(primary, fill);
                    }
                    else if (typeCombo.SelectedIndex == 2)
                    {
                        fill.Type = PaintType.RadialGradient;
                        if (fill.GradientStops == null || fill.GradientStops.Count == 0)
                        {
                            fill.GradientStops = new List<GradientStopData>
                            {
                                new GradientStopData(0.0, fill.ColorHex ?? "#D9D9D9", 1.0),
                                new GradientStopData(1.0, "#1F2937", 1.0)
                            };
                        }
                        SetEditingGradient(primary, fill);
                    }
                    else
                    {
                        fill.Type = PaintType.Solid;
                        if (_editingGradientPaint == fill) ClearEditingGradient();
                    }
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                };
                topRow.Children.Add(typeCombo);

                cardStack.Children.Add(topRow);

                // Opacity slider
                var opRow = ScrubControls.CreateSmoothSlider("Opacity", Math.Round(fill.Opacity * 100), 0, 100, val =>
                {
                    fill.Opacity = val / 100.0;
                    InvalidateAndRefresh(primary);
                }, "%");
                cardStack.Children.Add(opRow);

                // If Linear Gradient or Radial Gradient: Mount Figma-style Gradient Ramp & Controls
                if (fill.Type == PaintType.LinearGradient || fill.Type == PaintType.RadialGradient)
                {
                    bool isEditingThis = (_editingGradientPaint == fill && _editingGradientLayer == primary);

                    // Header row with "On-Canvas Roller" toggle button
                    var toggleRow = new DockPanel { Margin = new Thickness(0, 2, 0, 4) };
                    var rollerBtn = new Button
                    {
                        Template = CreateRoundedButtonTemplate(6),
                        Content = isEditingThis ? "● Canvas Roller Active [Done]" : "✏ Edit on Canvas",
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Height = 22,
                        Background = isEditingThis ? new SolidColorBrush(Color.FromRgb(20, 50, 110)) : new SolidColorBrush(Color.FromRgb(30, 36, 52)),
                        Foreground = isEditingThis ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(150, 165, 195)),
                        BorderBrush = isEditingThis ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(48, 58, 80)),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand,
                        Padding = new Thickness(6, 0, 6, 0),
                        ToolTip = isEditingThis ? "Click to exit on-canvas gradient editing (or press Esc)" : "Activate Figma on-canvas gradient handles & rollers"
                    };

                    GradientRampControl rampCtrl = null;
                    rollerBtn.Click += (s, e) =>
                    {
                        if (_editingGradientPaint == fill)
                            ClearEditingGradient();
                        else
                            SetEditingGradient(primary, fill, rampCtrl);
                        RefreshPropertyInspector();
                    };
                    toggleRow.Children.Add(rollerBtn);
                    cardStack.Children.Add(toggleRow);

                    rampCtrl = new GradientRampControl(fill, () =>
                    {
                        SetEditingGradient(primary, fill, rampCtrl);
                        InvalidateAndRefresh(primary);
                    }, () =>
                    {
                        SetEditingGradient(primary, fill, rampCtrl);
                        InvalidateAndRefresh(primary);
                        RefreshPropertyInspector();
                    });

                    rampCtrl.PreviewMouseDown += (s, e) =>
                    {
                        if (_editingGradientPaint != fill)
                        {
                            SetEditingGradient(primary, fill, rampCtrl);
                            RefreshPropertyInspector();
                        }
                    };

                    cardStack.Children.Add(rampCtrl);
                }

                card.Child = cardStack;
                _propertyInspectorStack.Children.Add(card);
            }
        }

        private void BuildStrokesSection(LayerNode primary)
        {
            if (primary.Strokes == null) primary.Strokes = new List<StrokeData>();

            _propertyInspectorStack.Children.Add(CreateInspectorSectionWithAdd("Strokes", () =>
            {
                primary.Strokes.Add(new StrokeData { Paint = Paint.Solid("#000000"), Width = 1.0, Enabled = true });
                InvalidateAndRefresh(primary);
                RefreshPropertyInspector();
            }));

            if (primary.Strokes.Count == 0)
            {
                var emptyBtn = CreateSmallButton("+ Add Stroke", "Add border stroke", () =>
                {
                    primary.Strokes.Add(new StrokeData { Paint = Paint.Solid("#000000"), Width = 1.0, Enabled = true });
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                });
                _propertyInspectorStack.Children.Add(emptyBtn);
                return;
            }

            for (int i = 0; i < primary.Strokes.Count; i++)
            {
                int strokeIdx = i;
                var stroke = primary.Strokes[strokeIdx];
                if (stroke.Paint == null) stroke.Paint = Paint.Solid("#000000");

                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(24, 28, 40)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(38, 46, 66)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var cardStack = new StackPanel();

                // Top row: Eye, Swatch, Width, Delete
                var topRow = new DockPanel { LastChildFill = true };

                var eyeBtn = new Button
                {
                    Template = CreateRoundedButtonTemplate(4),
                    Content = stroke.Enabled ? "👁" : "—",
                    FontSize = 10,
                    Width = 22,
                    Height = 22,
                    Background = stroke.Enabled ? new SolidColorBrush(Color.FromRgb(35, 42, 60)) : Brushes.Transparent,
                    Foreground = stroke.Enabled ? Brushes.White : new SolidColorBrush(Color.FromRgb(100, 110, 130)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = stroke.Enabled ? "Disable stroke" : "Enable stroke"
                };
                eyeBtn.Click += (s, e) =>
                {
                    stroke.Enabled = !stroke.Enabled;
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                };
                DockPanel.SetDock(eyeBtn, Dock.Left);
                topRow.Children.Add(eyeBtn);

                var delBtn = new Button
                {
                    Template = CreateRoundedButtonTemplate(4),
                    Content = "✕",
                    FontSize = 10,
                    Width = 20,
                    Height = 22,
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Remove stroke"
                };
                delBtn.Click += (s, e) =>
                {
                    if (strokeIdx < primary.Strokes.Count)
                    {
                        primary.Strokes.RemoveAt(strokeIdx);
                        InvalidateAndRefresh(primary);
                        RefreshPropertyInspector();
                    }
                };
                DockPanel.SetDock(delBtn, Dock.Right);
                topRow.Children.Add(delBtn);

                var swatch = new Border
                {
                    Width = 24,
                    Height = 22,
                    CornerRadius = new CornerRadius(3),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 70, 95)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(4, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Choose stroke color/gradient"
                };
                if (stroke.Paint.Type == PaintType.Solid)
                {
                    swatch.Background = new SolidColorBrush(GpuRenderer.ParseHexColor(stroke.Paint.ColorHex, stroke.Paint.Opacity));
                }
                else if (stroke.Paint.Type == PaintType.LinearGradient)
                {
                    var lgb = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                    GpuRenderer.PopulateGradientStops(lgb.GradientStops, stroke.Paint);
                    swatch.Background = lgb;
                }
                else
                {
                    var rgb = new RadialGradientBrush { Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
                    GpuRenderer.PopulateGradientStops(rgb.GradientStops, stroke.Paint);
                    swatch.Background = rgb;
                }

                swatch.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    string initialColor = stroke.Paint.ColorHex ?? "#000000";
                    if (stroke.Paint.GradientStops != null && stroke.Paint.GradientStops.Count > 0)
                        initialColor = stroke.Paint.GradientStops[0].ColorHex;

                    ColorPickerPopup.ShowAt(swatch, initialColor, newHex =>
                    {
                        stroke.Paint.ColorHex = newHex;
                        if (stroke.Paint.GradientStops != null && stroke.Paint.GradientStops.Count > 0)
                        {
                            stroke.Paint.GradientStops[0].ColorHex = newHex;
                        }
                        InvalidateAndRefresh(primary);
                        RefreshPropertyInspector();
                    });
                };
                DockPanel.SetDock(swatch, Dock.Left);
                topRow.Children.Add(swatch);

                // Stroke Type Combo
                var strokeTypeCombo = CreateModernComboBox(22, 65);
                strokeTypeCombo.Margin = new Thickness(0, 0, 4, 0);
                strokeTypeCombo.Items.Add(CreateModernComboBoxItem("Solid"));
                strokeTypeCombo.Items.Add(CreateModernComboBoxItem("Linear"));
                strokeTypeCombo.Items.Add(CreateModernComboBoxItem("Radial"));
                strokeTypeCombo.SelectedIndex = (stroke.Paint.Type == PaintType.LinearGradient) ? 1 : (stroke.Paint.Type == PaintType.RadialGradient ? 2 : 0);
                strokeTypeCombo.SelectionChanged += (s, e) =>
                {
                    if (strokeTypeCombo.SelectedIndex == 1)
                    {
                        stroke.Paint.Type = PaintType.LinearGradient;
                        if (stroke.Paint.GradientStops == null || stroke.Paint.GradientStops.Count == 0)
                        {
                            stroke.Paint.GradientStops = new List<GradientStopData>
                            {
                                new GradientStopData(0.0, stroke.Paint.ColorHex ?? "#000000", 1.0),
                                new GradientStopData(1.0, "#6366F1", 1.0)
                            };
                        }
                        SetEditingGradient(primary, stroke.Paint);
                    }
                    else if (strokeTypeCombo.SelectedIndex == 2)
                    {
                        stroke.Paint.Type = PaintType.RadialGradient;
                        if (stroke.Paint.GradientStops == null || stroke.Paint.GradientStops.Count == 0)
                        {
                            stroke.Paint.GradientStops = new List<GradientStopData>
                            {
                                new GradientStopData(0.0, stroke.Paint.ColorHex ?? "#000000", 1.0),
                                new GradientStopData(1.0, "#38BDF8", 1.0)
                            };
                        }
                        SetEditingGradient(primary, stroke.Paint);
                    }
                    else
                    {
                        stroke.Paint.Type = PaintType.Solid;
                        if (_editingGradientPaint == stroke.Paint) ClearEditingGradient();
                    }
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                };
                DockPanel.SetDock(strokeTypeCombo, Dock.Left);
                topRow.Children.Add(strokeTypeCombo);

                var widthField = ScrubControls.CreateScrubField("Width", stroke.Width, val =>
                {
                    stroke.Width = Math.Max(0, val);
                    stroke.Enabled = (stroke.Width > 0);
                    InvalidateAndRefresh(primary);
                }, min: 0, max: 200, suffix: "px");
                topRow.Children.Add(widthField);

                cardStack.Children.Add(topRow);

                // If Linear or Radial Gradient: Mount Figma-style Gradient Ramp & Controls
                if (stroke.Paint.Type == PaintType.LinearGradient || stroke.Paint.Type == PaintType.RadialGradient)
                {
                    bool isEditingThis = (_editingGradientPaint == stroke.Paint && _editingGradientLayer == primary);

                    var toggleRow = new DockPanel { Margin = new Thickness(0, 2, 0, 4) };
                    var rollerBtn = new Button
                    {
                        Template = CreateRoundedButtonTemplate(6),
                        Content = isEditingThis ? "● Canvas Roller Active [Done]" : "✏ Edit on Canvas",
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Height = 22,
                        Background = isEditingThis ? new SolidColorBrush(Color.FromRgb(20, 50, 110)) : new SolidColorBrush(Color.FromRgb(30, 36, 52)),
                        Foreground = isEditingThis ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(150, 165, 195)),
                        BorderBrush = isEditingThis ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(48, 58, 80)),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand,
                        Padding = new Thickness(6, 0, 6, 0),
                        ToolTip = isEditingThis ? "Click to exit on-canvas gradient editing (or press Esc)" : "Activate Figma on-canvas gradient handles & rollers"
                    };

                    GradientRampControl strokeRamp = null;
                    rollerBtn.Click += (s, e) =>
                    {
                        if (_editingGradientPaint == stroke.Paint)
                            ClearEditingGradient();
                        else
                            SetEditingGradient(primary, stroke.Paint, strokeRamp);
                        RefreshPropertyInspector();
                    };
                    toggleRow.Children.Add(rollerBtn);
                    cardStack.Children.Add(toggleRow);

                    strokeRamp = new GradientRampControl(stroke.Paint, () =>
                    {
                        SetEditingGradient(primary, stroke.Paint, strokeRamp);
                        InvalidateAndRefresh(primary);
                    }, () =>
                    {
                        SetEditingGradient(primary, stroke.Paint, strokeRamp);
                        InvalidateAndRefresh(primary);
                        RefreshPropertyInspector();
                    });

                    strokeRamp.PreviewMouseDown += (s, e) =>
                    {
                        if (_editingGradientPaint != stroke.Paint)
                        {
                            SetEditingGradient(primary, stroke.Paint, strokeRamp);
                            RefreshPropertyInspector();
                        }
                    };

                    cardStack.Children.Add(strokeRamp);
                }

                // Alignment & Cap row
                var alignRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                alignRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                alignRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                alignRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var alignCombo = CreateModernComboBox(22);
                alignCombo.Items.Add(CreateModernComboBoxItem("Center"));
                alignCombo.Items.Add(CreateModernComboBoxItem("Inside"));
                alignCombo.Items.Add(CreateModernComboBoxItem("Outside"));
                alignCombo.SelectedIndex = (stroke.Alignment == StrokeAlignment.Inside) ? 1 : (stroke.Alignment == StrokeAlignment.Outside ? 2 : 0);
                alignCombo.SelectionChanged += (s, e) =>
                {
                    if (alignCombo.SelectedIndex == 1) stroke.Alignment = StrokeAlignment.Inside;
                    else if (alignCombo.SelectedIndex == 2) stroke.Alignment = StrokeAlignment.Outside;
                    else stroke.Alignment = StrokeAlignment.Center;
                    InvalidateAndRefresh(primary);
                };
                Grid.SetColumn(alignCombo, 0);
                alignRow.Children.Add(alignCombo);

                var capCombo = CreateModernComboBox(22);
                capCombo.Items.Add(CreateModernComboBoxItem("Flat Cap"));
                capCombo.Items.Add(CreateModernComboBoxItem("Round Cap"));
                capCombo.Items.Add(CreateModernComboBoxItem("Square Cap"));
                capCombo.SelectedIndex = (stroke.Cap == StrokeCap.Round) ? 1 : (stroke.Cap == StrokeCap.Square ? 2 : 0);
                capCombo.SelectionChanged += (s, e) =>
                {
                    if (capCombo.SelectedIndex == 1) stroke.Cap = StrokeCap.Round;
                    else if (capCombo.SelectedIndex == 2) stroke.Cap = StrokeCap.Square;
                    else stroke.Cap = StrokeCap.Flat;
                    InvalidateAndRefresh(primary);
                };
                Grid.SetColumn(capCombo, 2);
                alignRow.Children.Add(capCombo);

                cardStack.Children.Add(alignRow);

                card.Child = cardStack;
                _propertyInspectorStack.Children.Add(card);
            }
        }

        private void BuildEffectsSection(LayerNode primary)
        {
            if (primary.Effects == null) primary.Effects = new List<EffectData>();

            _propertyInspectorStack.Children.Add(CreateInspectorSectionWithAdd("Effects", () =>
            {
                var newFx = new EffectData
                {
                    Type = EffectType.DropShadow,
                    Enabled = true,
                    IsExpanded = true,
                    ColorHex = "#000000",
                    Opacity = 0.25,
                    Direction = 90.0,
                    Distance = 4.0,
                    Blur = 8,
                    Spread = 0
                };
                newFx.UpdateOffsetsFromPolar();
                primary.Effects.Add(newFx);
                History.MarkDirty();
                InvalidateAndRefresh(primary);
                RefreshPropertyInspector();
            }));

            if (primary.Effects.Count == 0)
            {
                var emptyBtn = CreateSmallButton("+ Add Effect", "Add drop shadow, inner shadow, liquid glass, grain, or blur", () =>
                {
                    var newFx = new EffectData
                    {
                        Type = EffectType.DropShadow,
                        Enabled = true,
                        IsExpanded = true,
                        ColorHex = "#000000",
                        Opacity = 0.25,
                        Direction = 90.0,
                        Distance = 4.0,
                        Blur = 8,
                        Spread = 0
                    };
                    newFx.UpdateOffsetsFromPolar();
                    primary.Effects.Add(newFx);
                    History.MarkDirty();
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                });
                _propertyInspectorStack.Children.Add(emptyBtn);
                return;
            }

            for (int i = 0; i < primary.Effects.Count; i++)
            {
                int fxIndex = i;
                var fx = primary.Effects[fxIndex];

                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(40, 45, 63)), // #282D3F
                    BorderBrush = new SolidColorBrush(Color.FromRgb(57, 65, 90)), // #39415A
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var cardStack = new StackPanel();

                // Top row: Eye toggle, Type Dropdown, Collapse/Expand Chevron, Delete button
                var topRow = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, fx.IsExpanded ? 6 : 0) };

                var eyeBtn = new Button
                {
                    Template = CreateRoundedButtonTemplate(4),
                    Content = fx.Enabled ? "👁" : "—",
                    FontSize = 10,
                    Width = 22,
                    Height = 22,
                    Background = fx.Enabled ? new SolidColorBrush(Color.FromRgb(50, 58, 80)) : Brushes.Transparent,
                    Foreground = fx.Enabled ? Brushes.White : new SolidColorBrush(Color.FromRgb(120, 130, 150)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = fx.Enabled ? "Disable effect" : "Enable effect"
                };
                eyeBtn.Click += (s, e) =>
                {
                    fx.Enabled = !fx.Enabled;
                    History.MarkDirty();
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                };
                DockPanel.SetDock(eyeBtn, Dock.Left);
                topRow.Children.Add(eyeBtn);

                var delBtn = new Button
                {
                    Template = CreateRoundedButtonTemplate(4),
                    Content = "✕",
                    FontSize = 10,
                    Width = 22,
                    Height = 22,
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Remove effect",
                    Margin = new Thickness(4, 0, 0, 0)
                };
                delBtn.Click += (s, e) =>
                {
                    if (fxIndex < primary.Effects.Count)
                    {
                        primary.Effects.RemoveAt(fxIndex);
                        History.MarkDirty();
                        InvalidateAndRefresh(primary);
                        RefreshPropertyInspector();
                    }
                };
                DockPanel.SetDock(delBtn, Dock.Right);
                topRow.Children.Add(delBtn);

                // Option to collapse and expand the effects controls
                var expandBtn = new Button
                {
                    Template = CreateRoundedButtonTemplate(4),
                    Content = fx.IsExpanded ? "▾" : "▸",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Width = 22,
                    Height = 22,
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 205)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = fx.IsExpanded ? "Collapse effect controls" : "Expand effect controls",
                    Margin = new Thickness(2, 0, 0, 0)
                };
                expandBtn.Click += (s, e) =>
                {
                    fx.IsExpanded = !fx.IsExpanded;
                    RefreshPropertyInspector();
                };
                DockPanel.SetDock(expandBtn, Dock.Right);
                topRow.Children.Add(expandBtn);

                // Type ComboBox
                var typeCombo = CreateModernComboBox(22);
                typeCombo.Margin = new Thickness(6, 0, 4, 0);
                typeCombo.Items.Add(CreateModernComboBoxItem("Drop Shadow", EffectType.DropShadow));
                typeCombo.Items.Add(CreateModernComboBoxItem("Inner Shadow", EffectType.InnerShadow));
                typeCombo.Items.Add(CreateModernComboBoxItem("Layer Blur", EffectType.LayerBlur));
                typeCombo.Items.Add(CreateModernComboBoxItem("Liquid Glass", EffectType.LiquidGlass));
                typeCombo.Items.Add(CreateModernComboBoxItem("Film Grain", EffectType.Grain));
                typeCombo.Items.Add(CreateModernComboBoxItem("Background Blur", EffectType.BackgroundBlur));

                // Select current
                for (int c = 0; c < typeCombo.Items.Count; c++)
                {
                    var item = typeCombo.Items[c] as ComboBoxItem;
                    if (item != null && (EffectType)item.Tag == fx.Type)
                    {
                        typeCombo.SelectedIndex = c;
                        break;
                    }
                }
                if (typeCombo.SelectedIndex < 0) typeCombo.SelectedIndex = 0;

                typeCombo.SelectionChanged += (s, e) =>
                {
                    var sel = typeCombo.SelectedItem as ComboBoxItem;
                    if (sel != null && sel.Tag is EffectType)
                    {
                        var newType = (EffectType)sel.Tag;
                        if (fx.Type != newType)
                        {
                            fx.Type = newType;
                            // Set suitable Apple pro defaults for specific types
                            if (newType == EffectType.LiquidGlass)
                            {
                                fx.Intensity = 0.85;
                                fx.Direction = 315;
                                fx.Spread = 2.2;
                                fx.Blur = 16;
                                fx.ColorHex = "#FFFFFF";
                                fx.Opacity = 0.15;
                                fx.Scale = 1.0;
                                fx.Distance = 60.0;
                                fx.UpdateOffsetsFromPolar();
                            }
                            else if (newType == EffectType.Grain)
                            {
                                fx.Intensity = 0.25;
                                fx.Scale = 1.0;
                                fx.Blur = 0.0;
                                fx.ColorHex = "#FFFFFF";
                            }
                            else if (newType == EffectType.LayerBlur || newType == EffectType.BackgroundBlur)
                            {
                                if (fx.Blur <= 0) fx.Blur = 8;
                            }
                            History.MarkDirty();
                            InvalidateAndRefresh(primary);
                            RefreshPropertyInspector();
                        }
                    }
                };
                topRow.Children.Add(typeCombo);
                cardStack.Children.Add(topRow);

                // Controls rendered only when enabled and expanded
                if (fx.Enabled && fx.IsExpanded)
                {
                    switch (fx.Type)
                    {
                        case EffectType.DropShadow:
                        case EffectType.InnerShadow:
                        {
                            // Color Row with icon
                            string sColor = string.IsNullOrEmpty(fx.ColorHex) ? "#000000" : fx.ColorHex;
                            cardStack.Children.Add(CreateColorRow("🎨", sColor, hex =>
                            {
                                fx.ColorHex = hex;
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }));

                            // Direction & Distance (Polar controls with icons)
                            var polarGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                            polarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            polarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                            polarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                            var dirBox = ScrubControls.CreateScrubField("∠", fx.Direction, val =>
                            {
                                double deg = (val % 360.0);
                                if (deg < 0) deg += 360.0;
                                fx.Direction = Math.Round(deg, 1);
                                fx.UpdateOffsetsFromPolar();
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0, max: 360, suffix: "°", tooltip: "Light Direction / Angle (°)");

                            var distBox = ScrubControls.CreateScrubField("⇥", fx.Distance, val =>
                            {
                                fx.Distance = Math.Max(0, val);
                                fx.UpdateOffsetsFromPolar();
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0, max: 500, suffix: "px", tooltip: "Shadow Distance (px)");

                            Grid.SetColumn(dirBox, 0); Grid.SetColumn(distBox, 2);
                            polarGrid.Children.Add(dirBox); polarGrid.Children.Add(distBox);
                            cardStack.Children.Add(polarGrid);

                            // Offset X & Offset Y (Cartesian controls with icons)
                            var cartGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                            cartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            cartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                            cartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                            var offXBox = ScrubControls.CreateScrubField("X", fx.OffsetX, val =>
                            {
                                fx.OffsetX = val;
                                fx.UpdatePolarFromOffsets();
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, suffix: "px", tooltip: "Horizontal Offset X (px)");

                            var offYBox = ScrubControls.CreateScrubField("Y", fx.OffsetY, val =>
                            {
                                fx.OffsetY = val;
                                fx.UpdatePolarFromOffsets();
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, suffix: "px", tooltip: "Vertical Offset Y (px)");

                            Grid.SetColumn(offXBox, 0); Grid.SetColumn(offYBox, 2);
                            cartGrid.Children.Add(offXBox); cartGrid.Children.Add(offYBox);
                            cardStack.Children.Add(cartGrid);

                            // Blur & Spread with icons
                            var blurGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                            blurGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            blurGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                            blurGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                            var blurBox = ScrubControls.CreateScrubField("◌", fx.Blur, val =>
                            {
                                fx.Blur = Math.Max(0, val);
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0, max: 200, suffix: "px", tooltip: "Blur Radius (px)");

                            var spreadBox = ScrubControls.CreateScrubField("⤢", fx.Spread, val =>
                            {
                                fx.Spread = val;
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: -50, max: 100, suffix: "px", tooltip: "Shadow Spread (px)");

                            Grid.SetColumn(blurBox, 0); Grid.SetColumn(spreadBox, 2);
                            blurGrid.Children.Add(blurBox); blurGrid.Children.Add(spreadBox);
                            cardStack.Children.Add(blurGrid);

                            // Opacity Slider with icon
                            cardStack.Children.Add(ScrubControls.CreateSmoothSlider("◐", Math.Round(fx.Opacity * 100), 0, 100, val =>
                            {
                                fx.Opacity = val / 100.0;
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, "%", tooltip: "Shadow Opacity (%)"));
                            break;
                        }

                        case EffectType.LiquidGlass:
                        {
                            // 1. Quick Presets Bar
                            var presetWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
                            Action<string, double, double, double, double, string, double, double, double> applyPreset = 
                                (name, intensity, dir, rim, blur, color, opacity, bulge, dispersion) =>
                            {
                                fx.Intensity = intensity;
                                fx.Direction = dir;
                                fx.Spread = rim;
                                fx.Blur = blur;
                                fx.ColorHex = color;
                                fx.Opacity = opacity;
                                fx.Scale = bulge;
                                fx.Distance = dispersion;
                                fx.UpdateOffsetsFromPolar();
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                                RefreshPropertyInspector();
                            };

                            presetWrap.Children.Add(CreatePresetPill("VisionOS", () => applyPreset("VisionOS", 0.85, 315, 2.2, 16, "#FFFFFF", 0.15, 1.0, 60.0)));
                            presetWrap.Children.Add(CreatePresetPill("Frosted", () => applyPreset("Frosted", 0.65, 300, 2.0, 32, "#F1F5F9", 0.35, 1.2, 40.0)));
                            presetWrap.Children.Add(CreatePresetPill("Smoked", () => applyPreset("Smoked", 0.90, 315, 2.5, 20, "#0F172A", 0.65, 1.0, 35.0)));
                            presetWrap.Children.Add(CreatePresetPill("Crystal", () => applyPreset("Crystal", 1.00, 270, 3.0, 4, "#E0F2FE", 0.08, 1.4, 95.0)));
                            cardStack.Children.Add(presetWrap);

                            // 2. Glass Tint Color Row with icon
                            string glassColor = string.IsNullOrEmpty(fx.ColorHex) ? "#FFFFFF" : fx.ColorHex;
                            cardStack.Children.Add(CreateColorRow("🎨", glassColor, hex =>
                            {
                                fx.ColorHex = hex;
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }));

                            // 3. Glass Body Translucency / Opacity Slider with icon
                            cardStack.Children.Add(ScrubControls.CreateSmoothSlider("◐", Math.Round(fx.Opacity * 100), 0, 100, val =>
                            {
                                fx.Opacity = val / 100.0;
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, "%", tooltip: "Glass Translucency / Opacity (%)"));

                            // 4. Direction & Caustic Rim Width Grid with icons
                            var lightGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                            lightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            lightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                            lightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                            var dirBox = ScrubControls.CreateScrubField("☼", fx.Direction, val =>
                            {
                                double deg = (val % 360.0);
                                if (deg < 0) deg += 360.0;
                                fx.Direction = Math.Round(deg, 1);
                                fx.UpdateOffsetsFromPolar();
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0, max: 360, suffix: "°", tooltip: "Light Direction Angle (°)");

                            var rimBox = ScrubControls.CreateScrubField("◎", fx.Spread > 0 ? fx.Spread : 2.2, val =>
                            {
                                fx.Spread = Math.Max(0.5, Math.Min(15.0, val));
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0.5, max: 15.0, suffix: "px", tooltip: "Caustic Rim Thickness (px)");

                            Grid.SetColumn(dirBox, 0); Grid.SetColumn(rimBox, 2);
                            lightGrid.Children.Add(dirBox); lightGrid.Children.Add(rimBox);
                            cardStack.Children.Add(lightGrid);

                            // 5. Specular Lens Highlight Intensity Slider with icon
                            cardStack.Children.Add(ScrubControls.CreateSmoothSlider("✦", Math.Round(fx.Intensity * 100), 0, 100, val =>
                            {
                                fx.Intensity = val / 100.0;
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, "%", tooltip: "Specular Lens Highlight (%)"));

                            // 6. Curvature & Frost Diffusion Grid with icons
                            var optGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                            optGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            optGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                            optGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                            var curveBox = ScrubControls.CreateScrubField("⌒", fx.Scale > 0 ? fx.Scale : 1.0, val =>
                            {
                                fx.Scale = Math.Max(0.2, Math.Min(3.0, val));
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0.2, max: 3.0, suffix: "x", tooltip: "Lens Bulge / Curvature (x)");

                            var blurBox = ScrubControls.CreateScrubField("❄", fx.Blur > 0 ? fx.Blur : 16, val =>
                            {
                                fx.Blur = Math.Max(0, val);
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0, max: 100, suffix: "px", tooltip: "Frost Diffusion Blur (px)");

                            Grid.SetColumn(curveBox, 0); Grid.SetColumn(blurBox, 2);
                            optGrid.Children.Add(curveBox); optGrid.Children.Add(blurBox);
                            cardStack.Children.Add(optGrid);

                            // 7. Chromatic Aberration Dispersion Slider with icon
                            double currentDisp = fx.Distance > 0 ? fx.Distance : 60.0;
                            cardStack.Children.Add(ScrubControls.CreateSmoothSlider("🌈", currentDisp, 0, 100, val =>
                            {
                                fx.Distance = val;
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, "%", tooltip: "Chromatic Aberration Dispersion (%)"));
                            break;
                        }

                        case EffectType.Grain:
                        {
                            // Color Row with icon
                            string gColor = string.IsNullOrEmpty(fx.ColorHex) ? "#FFFFFF" : fx.ColorHex;
                            cardStack.Children.Add(CreateColorRow("🎨", gColor, hex =>
                            {
                                fx.ColorHex = hex;
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }));

                            // Grain Intensity Slider with icon
                            cardStack.Children.Add(ScrubControls.CreateSmoothSlider("▦", Math.Round(fx.Intensity * 100), 0, 100, val =>
                            {
                                fx.Intensity = val / 100.0;
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, "%", tooltip: "Grain Intensity (%)"));

                            // Grain Scale & Softness Grid with icons
                            var gGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                            gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                            gGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                            var scaleBox = ScrubControls.CreateScrubField("⛶", fx.Scale > 0 ? fx.Scale : 1.0, val =>
                            {
                                fx.Scale = Math.Max(0.2, Math.Min(6.0, val));
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0.2, max: 6.0, suffix: "x", tooltip: "Grain Texture Scale");

                            var softBox = ScrubControls.CreateScrubField("☁", fx.Blur, val =>
                            {
                                fx.Blur = Math.Max(0, Math.Min(10.0, val));
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0, max: 10.0, suffix: "px", tooltip: "Grain Softness (px)");

                            Grid.SetColumn(scaleBox, 0); Grid.SetColumn(softBox, 2);
                            gGrid.Children.Add(scaleBox); gGrid.Children.Add(softBox);
                            cardStack.Children.Add(gGrid);
                            break;
                        }

                        case EffectType.LayerBlur:
                        case EffectType.BackgroundBlur:
                        {
                            if (fx.Type == EffectType.BackgroundBlur)
                            {
                                string bColor = string.IsNullOrEmpty(fx.ColorHex) ? "#FFFFFF" : fx.ColorHex;
                                cardStack.Children.Add(CreateColorRow("🎨", bColor, hex =>
                                {
                                    fx.ColorHex = hex;
                                    History.MarkDirty();
                                    InvalidateAndRefresh(primary);
                                }));

                                cardStack.Children.Add(ScrubControls.CreateSmoothSlider("◐", Math.Round(fx.Opacity * 100), 0, 100, val =>
                                {
                                    fx.Opacity = val / 100.0;
                                    History.MarkDirty();
                                    InvalidateAndRefresh(primary);
                                }, "%", tooltip: "Tint Opacity (%)"));
                            }

                            var blurBox = ScrubControls.CreateScrubField("◌", fx.Blur > 0 ? fx.Blur : 8, val =>
                            {
                                fx.Blur = Math.Max(0, val);
                                History.MarkDirty();
                                InvalidateAndRefresh(primary);
                            }, min: 0, max: 200, suffix: "px", tooltip: "Blur Radius (px)");
                            cardStack.Children.Add(blurBox);
                            break;
                        }
                    }
                }

                card.Child = cardStack;
                _propertyInspectorStack.Children.Add(card);
            }
        }

        private void BuildTypographySection(LayerNode primary)
        {
            _propertyInspectorStack.Children.Add(CreateInspectorSection("Typography"));
            var typo = primary.Typography ?? new TypographyData();
            primary.Typography = typo;

            // Direct text edit prompt button
            var editPromptBtn = CreateSmallButton("✎ Edit Text on Canvas (Double-Click)", "Edit text in-place", () =>
            {
                StartInPlaceTextEditing(primary);
            });
            editPromptBtn.Margin = new Thickness(0, 0, 0, 6);
            _propertyInspectorStack.Children.Add(editPromptBtn);

            // 1. System Font Family dropdown
            var fontLabel = new TextBlock
            {
                Text = "Font Family",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 160)),
                Margin = new Thickness(0, 2, 0, 2)
            };
            _propertyInspectorStack.Children.Add(fontLabel);

            var fontCombo = CreateModernComboBox(26);
            fontCombo.Margin = new Thickness(0, 0, 0, 6);

            // Populate with system font families
            try
            {
                var sysFonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s).ToList();
                foreach (var fn in sysFonts)
                {
                    fontCombo.Items.Add(fn);
                }
                if (!string.IsNullOrEmpty(typo.FontFamily) && fontCombo.Items.Contains(typo.FontFamily))
                {
                    fontCombo.SelectedItem = typo.FontFamily;
                }
                else
                {
                    fontCombo.SelectedItem = "Segoe UI";
                }
            }
            catch
            {
                fontCombo.Items.Add("Segoe UI");
                fontCombo.Items.Add("Arial");
                fontCombo.Items.Add("Consolas");
                fontCombo.SelectedItem = "Segoe UI";
            }

            fontCombo.SelectionChanged += (s, e) =>
            {
                if (fontCombo.SelectedItem != null)
                {
                    typo.FontFamily = fontCombo.SelectedItem.ToString();
                    InvalidateAndRefresh(primary);
                }
            };
            _propertyInspectorStack.Children.Add(fontCombo);

            // 2. Weight and Italic Style
            var styleGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            styleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            styleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            styleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

            var weightCombo = CreateModernComboBox(26);
            var weights = new int[] { 100, 300, 400, 500, 600, 700, 800, 900 };
            var weightNames = new string[] { "Thin (100)", "Light (300)", "Regular (400)", "Medium (500)", "SemiBold (600)", "Bold (700)", "ExtraBold (800)", "Black (900)" };
            for (int wi = 0; wi < weights.Length; wi++) weightCombo.Items.Add(weightNames[wi]);

            int selWeightIdx = Array.IndexOf(weights, typo.FontWeight);
            weightCombo.SelectedIndex = selWeightIdx >= 0 ? selWeightIdx : 2; // Default Regular
            weightCombo.SelectionChanged += (s, e) =>
            {
                if (weightCombo.SelectedIndex >= 0 && weightCombo.SelectedIndex < weights.Length)
                {
                    typo.FontWeight = weights[weightCombo.SelectedIndex];
                    InvalidateAndRefresh(primary);
                }
            };
            Grid.SetColumn(weightCombo, 0);
            styleGrid.Children.Add(weightCombo);

            var italicBtn = new Button
            {
                Template = CreateRoundedButtonTemplate(5),
                Content = "I",
                FontStyle = FontStyles.Italic,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Height = 24,
                Background = typo.IsItalic ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(35, 42, 60)),
                Foreground = typo.IsItalic ? Brushes.Black : Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 78)),
                Cursor = Cursors.Hand,
                ToolTip = "Italic toggle"
            };
            italicBtn.Click += (s, e) =>
            {
                typo.IsItalic = !typo.IsItalic;
                InvalidateAndRefresh(primary);
                RefreshPropertyInspector();
            };
            Grid.SetColumn(italicBtn, 2);
            styleGrid.Children.Add(italicBtn);
            _propertyInspectorStack.Children.Add(styleGrid);

            // 3. Numeric Fields: Font Size, Line Height
            var numGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            numGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            numGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            numGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var sizeField = ScrubControls.CreateScrubField("Size", typo.FontSize, val =>
            {
                typo.FontSize = Math.Max(6, val);
                InvalidateAndRefresh(primary);
            }, min: 6, max: 200, suffix: "px");
            Grid.SetColumn(sizeField, 0);
            numGrid.Children.Add(sizeField);

            var lhField = ScrubControls.CreateScrubField("Line H", typo.LineHeight, val =>
            {
                typo.LineHeight = Math.Max(0.5, val);
                InvalidateAndRefresh(primary);
            }, min: 0.5, max: 5.0, suffix: "x");
            Grid.SetColumn(lhField, 2);
            numGrid.Children.Add(lhField);
            _propertyInspectorStack.Children.Add(numGrid);

            // 4. Horizontal Alignment Segment
            var hAlignLabel = new TextBlock
            {
                Text = "Text Align",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 160)),
                Margin = new Thickness(0, 2, 0, 2)
            };
            _propertyInspectorStack.Children.Add(hAlignLabel);

            var hAlignRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            for (int i = 0; i < 4; i++) hAlignRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string[] hSymbols = new string[] { "⫷", "≡", "⫸", "⬌" };
            string[] hTips = new string[] { "Align Left", "Align Center", "Align Right", "Justify" };
            TextAlign[] hVals = new TextAlign[] { TextAlign.Left, TextAlign.Center, TextAlign.Right, TextAlign.Justify };

            for (int i = 0; i < 4; i++)
            {
                TextAlign tv = hVals[i];
                bool isSel = (typo.Align == tv);
                var btn = new Button
                {
                    Content = hSymbols[i],
                    ToolTip = hTips[i],
                    Height = 24,
                    FontSize = 11,
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(32, 38, 54)),
                    Foreground = isSel ? Brushes.Black : Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 78)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Template = CreateRoundedButtonTemplate(5)
                };
                btn.Click += (s, e) =>
                {
                    typo.Align = tv;
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                };
                Grid.SetColumn(btn, i);
                hAlignRow.Children.Add(btn);
            }
            _propertyInspectorStack.Children.Add(hAlignRow);

            // 5. Vertical Alignment Segment
            var vAlignLabel = new TextBlock
            {
                Text = "Vertical Align",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 160)),
                Margin = new Thickness(0, 2, 0, 2)
            };
            _propertyInspectorStack.Children.Add(vAlignLabel);

            var vAlignRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            for (int i = 0; i < 3; i++) vAlignRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string[] vSymbols = new string[] { "⤒ Top", "⬍ Middle", "⤓ Bottom" };
            VerticalAlign[] vVals = new VerticalAlign[] { VerticalAlign.Top, VerticalAlign.Middle, VerticalAlign.Bottom };

            for (int i = 0; i < 3; i++)
            {
                VerticalAlign vv = vVals[i];
                bool isSel = (typo.VerticalAlign == vv);
                var btn = new Button
                {
                    Content = vSymbols[i],
                    Height = 24,
                    FontSize = 10,
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(32, 38, 54)),
                    Foreground = isSel ? Brushes.Black : Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 78)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Template = CreateRoundedButtonTemplate(5)
                };
                btn.Click += (s, e) =>
                {
                    typo.VerticalAlign = vv;
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                };
                Grid.SetColumn(btn, i);
                vAlignRow.Children.Add(btn);
            }
            _propertyInspectorStack.Children.Add(vAlignRow);

            // 6. Resizing Mode (Figma Text Dynamics: Auto Width, Auto Height, Fixed Size)
            var resizeLabel = new TextBlock
            {
                Text = "Resizing",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 160)),
                Margin = new Thickness(0, 2, 0, 2)
            };
            _propertyInspectorStack.Children.Add(resizeLabel);

            var resizeRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            for (int i = 0; i < 3; i++) resizeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string[] rSymbols = new string[] { "↔ Auto W", "↕ Auto H", "⏹ Fixed" };
            string[] rTips = new string[] { "Auto Width: width hugs text (single line)", "Auto Height: fixed width, height hugs wrapped lines", "Fixed Size: text clips / overflows fixed bounds" };
            TextAutoResize[] rVals = new TextAutoResize[] { TextAutoResize.AutoWidth, TextAutoResize.AutoHeight, TextAutoResize.Fixed };

            for (int i = 0; i < 3; i++)
            {
                TextAutoResize rv = rVals[i];
                bool isSel = (typo.AutoResize == rv);
                var btn = new Button
                {
                    Content = rSymbols[i],
                    ToolTip = rTips[i],
                    Height = 24,
                    FontSize = 10,
                    Background = isSel ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(32, 38, 54)),
                    Foreground = isSel ? Brushes.Black : Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 78)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Template = CreateRoundedButtonTemplate(5)
                };
                btn.Click += (s, e) =>
                {
                    typo.AutoResize = rv;
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                };
                Grid.SetColumn(btn, i);
                resizeRow.Children.Add(btn);
            }
            _propertyInspectorStack.Children.Add(resizeRow);

            // 7. Decorations & Casing
            var decorLabel = new TextBlock
            {
                Text = "Decoration & Case",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 160)),
                Margin = new Thickness(0, 2, 0, 2)
            };
            _propertyInspectorStack.Children.Add(decorLabel);

            var decorRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            decorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            decorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            decorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var decorCombo = CreateModernComboBox(24);
            decorCombo.Items.Add(CreateModernComboBoxItem("None"));
            decorCombo.Items.Add(CreateModernComboBoxItem("Underline"));
            decorCombo.Items.Add(CreateModernComboBoxItem("Strikethrough"));
            decorCombo.SelectedIndex = (typo.Decoration == TextDecorationMode.Underline) ? 1 : (typo.Decoration == TextDecorationMode.Strikethrough ? 2 : 0);
            decorCombo.SelectionChanged += (s, e) =>
            {
                if (decorCombo.SelectedIndex == 1) typo.Decoration = TextDecorationMode.Underline;
                else if (decorCombo.SelectedIndex == 2) typo.Decoration = TextDecorationMode.Strikethrough;
                else typo.Decoration = TextDecorationMode.None;
                InvalidateAndRefresh(primary);
            };
            Grid.SetColumn(decorCombo, 0);
            decorRow.Children.Add(decorCombo);

            var caseCombo = CreateModernComboBox(24);
            caseCombo.Items.Add(CreateModernComboBoxItem("As-Is"));
            caseCombo.Items.Add(CreateModernComboBoxItem("UPPERCASE"));
            caseCombo.Items.Add(CreateModernComboBoxItem("lowercase"));
            caseCombo.Items.Add(CreateModernComboBoxItem("Title Case"));
            caseCombo.SelectedIndex = (typo.Case == TextCase.Upper) ? 1 : (typo.Case == TextCase.Lower ? 2 : (typo.Case == TextCase.Title ? 3 : 0));
            caseCombo.SelectionChanged += (s, e) =>
            {
                if (caseCombo.SelectedIndex == 1) typo.Case = TextCase.Upper;
                else if (caseCombo.SelectedIndex == 2) typo.Case = TextCase.Lower;
                else if (caseCombo.SelectedIndex == 3) typo.Case = TextCase.Title;
                else typo.Case = TextCase.Original;
                InvalidateAndRefresh(primary);
            };
            Grid.SetColumn(caseCombo, 2);
            decorRow.Children.Add(caseCombo);
            _propertyInspectorStack.Children.Add(decorRow);
        }

        public void StartInPlaceTextEditing(LayerNode node)
        {
            if (node == null || node.Type != ElementType.Text) return;
            if (_isEditingText) EndInPlaceTextEditing(commit: true);

            _isEditingText = true;
            _editingTextNode = node;
            _initialTypographyBeforeEdit = node.Typography != null ? node.Typography.Clone() : new TypographyData();
            _initialTextBoundsBeforeEdit = node.GetBounds();

            var typo = node.Typography ?? new TypographyData();
            node.Typography = typo;

            Point screenPt = CanvasToScreen(new Point(node.X, node.Y));
            double w = Math.Max(100.0, node.Width * _zoomScale + 20);
            double h = Math.Max(40.0, node.Height * _zoomScale + 20);

            _inPlaceTextEditor = new TextBox
            {
                Text = typo.Text,
                FontFamily = new FontFamily(string.IsNullOrEmpty(typo.FontFamily) ? "Segoe UI" : typo.FontFamily),
                FontSize = Math.Max(10.0, typo.FontSize * _zoomScale),
                FontWeight = FontWeight.FromOpenTypeWeight(Math.Max(100, Math.Min(900, typo.FontWeight))),
                FontStyle = typo.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                Background = new SolidColorBrush(Color.FromArgb(240, 20, 24, 36)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(6),
                AcceptsReturn = true,
                TextWrapping = (typo.AutoResize == TextAutoResize.AutoWidth) ? TextWrapping.NoWrap : TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = w,
                Height = h,
                Margin = new Thickness(screenPt.X - 4, screenPt.Y - 4, 0, 0)
            };

            switch (typo.Align)
            {
                case TextAlign.Center: _inPlaceTextEditor.TextAlignment = TextAlignment.Center; break;
                case TextAlign.Right: _inPlaceTextEditor.TextAlignment = TextAlignment.Right; break;
                case TextAlign.Justify: _inPlaceTextEditor.TextAlignment = TextAlignment.Justify; break;
                default: _inPlaceTextEditor.TextAlignment = TextAlignment.Left; break;
            }

            _inPlaceTextEditor.TextChanged += (s, e) =>
            {
                typo.Text = _inPlaceTextEditor.Text;
                var typeFace = new Typeface(_inPlaceTextEditor.FontFamily, _inPlaceTextEditor.FontStyle, _inPlaceTextEditor.FontWeight, FontStretches.Normal);
                var ft = new FormattedText(
                    string.IsNullOrEmpty(typo.Text) ? " " : typo.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeFace,
                    typo.FontSize,
                    Brushes.White);

                if (typo.AutoResize == TextAutoResize.AutoWidth)
                {
                    node.Width = Math.Max(20.0, ft.WidthIncludingTrailingWhitespace);
                    node.Height = Math.Max(16.0, ft.Height);
                    _inPlaceTextEditor.Width = Math.Max(100.0, node.Width * _zoomScale + 20);
                    _inPlaceTextEditor.Height = Math.Max(40.0, node.Height * _zoomScale + 20);
                }
                else if (typo.AutoResize == TextAutoResize.AutoHeight)
                {
                    ft.MaxTextWidth = Math.Max(1.0, node.Width);
                    node.Height = Math.Max(16.0, ft.Height);
                    _inPlaceTextEditor.Height = Math.Max(40.0, node.Height * _zoomScale + 20);
                }
                _sceneGraph.InvalidateNode(node.Id);
                RefreshCanvas();
            };

            _inPlaceTextEditor.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    EndInPlaceTextEditing(commit: true);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    EndInPlaceTextEditing(commit: true);
                    e.Handled = true;
                }
            };

            _inPlaceTextEditor.LostFocus += (s, e) =>
            {
                EndInPlaceTextEditing(commit: true);
            };

            if (_canvasContainer != null)
            {
                _canvasContainer.Children.Add(_inPlaceTextEditor);
                _inPlaceTextEditor.Focus();
                _inPlaceTextEditor.SelectAll();
            }
        }

        public void EndInPlaceTextEditing(bool commit)
        {
            if (!_isEditingText) return;
            _isEditingText = false;

            if (_inPlaceTextEditor != null && _canvasContainer != null)
            {
                _canvasContainer.Children.Remove(_inPlaceTextEditor);
                _inPlaceTextEditor = null;
            }

            if (_editingTextNode != null)
            {
                var curBounds = _editingTextNode.GetBounds();
                if (commit && _initialTypographyBeforeEdit != null)
                {
                    var cmd = new ModifyTypographyCommand(
                        _editingTextNode,
                        _initialTypographyBeforeEdit,
                        _editingTextNode.Typography.Clone(),
                        _initialTextBoundsBeforeEdit,
                        curBounds,
                        () =>
                        {
                            _sceneGraph.InvalidateNode(_editingTextNode.Id);
                            RefreshCanvas();
                            RefreshPropertyInspector();
                        });
                    History.Execute(cmd);
                }
                _sceneGraph.InvalidateNode(_editingTextNode.Id);
            }

            _editingTextNode = null;
            _initialTypographyBeforeEdit = null;
            RefreshCanvas();
            RefreshPropertyInspector();
            Focus();
        }

        private FrameworkElement CreateNumberField(string label, double val, Action<double> onChanged)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 160)),
                Width = 48,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(lbl, Dock.Left);
            dock.Children.Add(lbl);

            var border = new Border
            {
                Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(26, 30, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 76)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            var tb = new TextBox
            {
                Text = val.ToString("F0"),
                FontSize = 12,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            tb.LostFocus += (s, e) =>
            {
                double d;
                if (double.TryParse(tb.Text, out d)) onChanged(d);
            };
            tb.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    double d;
                    if (double.TryParse(tb.Text, out d)) onChanged(d);
                }
            };
            border.Child = tb;
            dock.Children.Add(border);
            return dock;
        }



        private FrameworkElement CreateColorRow(string label, string hex, Action<string> onChanged)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            bool isShort = (label != null && label.Length <= 2);
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = isShort ? 12 : 11,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 160)),
                Width = isShort ? 24 : 48,
                TextAlignment = isShort ? TextAlignment.Center : TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = isShort ? "Color Tint" : label
            };
            DockPanel.SetDock(lbl, Dock.Left);
            dock.Children.Add(lbl);

            var swatch = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(GpuRenderer.ParseHexColor(hex, 1.0)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 70, 95)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Click to open interactive Color Picker"
            };
            DockPanel.SetDock(swatch, Dock.Left);
            dock.Children.Add(swatch);

            var tbBorder = new Border
            {
                Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(26, 30, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 54, 76)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            var tb = new TextBox
            {
                Text = hex,
                FontSize = 12,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            tbBorder.Child = tb;

            swatch.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                ColorPickerPopup.ShowAt(swatch, tb.Text, newHex =>
                {
                    tb.Text = newHex;
                    swatch.Background = new SolidColorBrush(GpuRenderer.ParseHexColor(newHex, 1.0));
                    if (onChanged != null) onChanged(newHex);
                });
            };

            tb.TextChanged += (s, e) =>
            {
                if (tb.Text.StartsWith("#") && (tb.Text.Length == 7 || tb.Text.Length == 4 || tb.Text.Length == 9))
                {
                    swatch.Background = new SolidColorBrush(GpuRenderer.ParseHexColor(tb.Text, 1.0));
                    if (onChanged != null) onChanged(tb.Text);
                }
            };

            dock.Children.Add(tbBorder);
            return dock;
        }

        private FrameworkElement CreateSliderRow(string label, double val, Action<double> onChanged)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 160)),
                Width = 48,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(lbl, Dock.Left);
            dock.Children.Add(lbl);

            var slider = new Slider
            {
                Minimum = 0.0,
                Maximum = 1.0,
                Value = val,
                VerticalAlignment = VerticalAlignment.Center
            };
            slider.ValueChanged += (s, e) => onChanged(slider.Value);
            dock.Children.Add(slider);

            return dock;
        }

        #endregion

        #region Canvas Host & Interaction

        private void CollectDragInitialPositions(LayerNode node, Dictionary<LayerNode, Point> dict)
        {
            if (node == null) return;
            if (!dict.ContainsKey(node))
            {
                dict[node] = new Point(node.X, node.Y);
                _dragInitialBounds[node] = new Rect(node.X, node.Y, node.Width, node.Height);
                _dragInitialRotations[node] = node.Rotation;
                if (node.Type == ElementType.Path && node.Geometry != null)
                {
                    _dragInitialGeometries[node] = node.Geometry.Clone();
                }
            }
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollectDragInitialPositions(child, dict);
                }
            }
        }

        private void CheckAutoReparentLayersOnDrop(List<LayerNode> movedLayers)
        {
            if (movedLayers == null || movedLayers.Count == 0 || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            bool anyReparented = false;
            var allFrames = page.GetAllLayersFlat().Where(l => l.Type == ElementType.Frame && !movedLayers.Contains(l)).ToList();

            foreach (var layer in movedLayers)
            {
                Point center = new Point(layer.X + layer.Width / 2.0, layer.Y + layer.Height / 2.0);

                LayerNode currentParent = !string.IsNullOrEmpty(layer.ParentId) ? page.FindLayerById(layer.ParentId) : null;
                bool isInsideAutoLayout = currentParent != null && currentParent.AutoLayout != null && currentParent.AutoLayout.Mode != AutoLayoutMode.None;

                if (isInsideAutoLayout)
                {
                    var pBounds = currentParent.GetBounds();
                    // Figma sticky parenting: requires dragging > 80px long out of the autolayout frame to detach
                    double longDragThreshold = 80.0;
                    Rect stickyBounds = new Rect(
                        pBounds.X - longDragThreshold,
                        pBounds.Y - longDragThreshold,
                        pBounds.Width + longDragThreshold * 2.0,
                        pBounds.Height + longDragThreshold * 2.0
                    );

                    if (stickyBounds.Contains(center))
                    {
                        // Stays inside current auto layout frame!
                        // Live reorder siblings based on drag position
                        if (currentParent.Children != null && currentParent.Children.Count > 1)
                        {
                            if (currentParent.AutoLayout.Mode == AutoLayoutMode.Horizontal)
                            {
                                currentParent.Children = currentParent.Children.OrderBy(c => c.X + c.Width / 2.0).ToList();
                            }
                            else if (currentParent.AutoLayout.Mode == AutoLayoutMode.Vertical)
                            {
                                currentParent.Children = currentParent.Children.OrderBy(c => c.Y + c.Height / 2.0).ToList();
                            }
                        }
                        AutoLayoutEngine.Reflow(currentParent);
                        anyReparented = true;
                        continue;
                    }
                }

                LayerNode targetFrame = null;
                for (int i = allFrames.Count - 1; i >= 0; i--)
                {
                    var frame = allFrames[i];
                    if (Kerf.Model.Page.IsDescendant(layer, frame)) continue;
                    var bounds = frame.GetBounds();
                    if (bounds.Contains(center))
                    {
                        targetFrame = frame;
                        break;
                    }
                }

                if (targetFrame != null)
                {
                    if (layer.ParentId != targetFrame.Id)
                    {
                        var oldParent = !string.IsNullOrEmpty(layer.ParentId) ? page.FindLayerById(layer.ParentId) : null;
                        page.RemoveLayerEverywhere(layer);
                        layer.ParentId = targetFrame.Id;
                        if (targetFrame.Children == null) targetFrame.Children = new List<LayerNode>();
                        targetFrame.Children.Add(layer);

                        if (targetFrame.AutoLayout != null && targetFrame.AutoLayout.Mode != AutoLayoutMode.None)
                        {
                            if (targetFrame.AutoLayout.Mode == AutoLayoutMode.Horizontal)
                            {
                                targetFrame.Children = targetFrame.Children.OrderBy(c => c.X + c.Width / 2.0).ToList();
                            }
                            else if (targetFrame.AutoLayout.Mode == AutoLayoutMode.Vertical)
                            {
                                targetFrame.Children = targetFrame.Children.OrderBy(c => c.Y + c.Height / 2.0).ToList();
                            }
                            AutoLayoutEngine.Reflow(targetFrame);
                        }

                        if (oldParent != null && oldParent.AutoLayout != null && oldParent.AutoLayout.Mode != AutoLayoutMode.None)
                        {
                            AutoLayoutEngine.Reflow(oldParent);
                        }

                        anyReparented = true;
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(layer.ParentId))
                    {
                        var oldParent = page.FindLayerById(layer.ParentId);
                        page.RemoveLayerEverywhere(layer);
                        layer.ParentId = null;
                        page.Layers.Add(layer);

                        if (oldParent != null && oldParent.AutoLayout != null && oldParent.AutoLayout.Mode != AutoLayoutMode.None)
                        {
                            AutoLayoutEngine.Reflow(oldParent);
                        }

                        anyReparented = true;
                    }
                }
            }

            if (anyReparented)
            {
                _sceneGraph.Synchronize(page);
                RefreshLayersList();
            }
        }

        public Point ScreenToCanvas(Point pt)
        {
            return new Point((pt.X - _panOffset.X) / _zoomScale, (pt.Y - _panOffset.Y) / _zoomScale);
        }

        public Point CanvasToScreen(Point pt)
        {
            return new Point(pt.X * _zoomScale + _panOffset.X, pt.Y * _zoomScale + _panOffset.Y);
        }

        public void HandleCanvasMouseDown(Point screenPt, MouseButton button, int clickCount)
        {
            ColorPickerPopup.CloseActive();
            Focus();
            _lastMousePos = screenPt;
            Point canvasPt = ScreenToCanvas(screenPt);

            if (_isEditingText)
            {
                EndInPlaceTextEditing(commit: true);
            }

            // Middle button or Space held: Pan
            if (button == MouseButton.Middle || (button == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)) || _activeTool == ToolType.Hand)
            {
                _isPanning = true;
                if (_canvasHost != null)
                {
                    _canvasHost.CaptureMouse();
                }
                Cursor = Cursors.ScrollAll;
                return;
            }

            // Right click finishes active Pen drawing or exits Vector Edit Mode
            if (button == MouseButton.Right)
            {
                if (_activeTool == ToolType.Pen && _activePenNode != null)
                {
                    FinishPenDrawing();
                    return;
                }
                if (_isVectorEditing)
                {
                    ExitVectorEditMode();
                    return;
                }
            }

            if (button == MouseButton.Left)
            {
                // In Animate mode, route click directly into State Machine
                if (_currentStudioMode == StudioMode.Animate && _interactiveCanvasEnabled && _timelineController != null && _timelineController.ActiveStateMachine != null)
                {
                    var hit = _sceneGraph.HitTest(canvasPt);
                    if (hit != null)
                    {
                        uint hitId = (uint)Math.Abs(hit.Id.GetHashCode());
                        _timelineController.ActiveStateMachine.OnPointerEvent(hitId, PointerEventKind.Down);
                    }
                    _timelineController.ActiveStateMachine.SetBool(1, true); // isPressed
                    TriggerStateMachineStep();
                }

                // 1. Vector Edit Mode interaction (if currently vector editing a path)
                if (_isVectorEditing && _editingVectorNode != null && _editingVectorNode.Geometry != null)
                {
                    double tolerance = 10.0 / _zoomScale;
                    var hit = BezierVectorEngine.HitTest(_editingVectorNode.Geometry, canvasPt, tolerance);
                    if (hit.NodeIndex >= 0)
                    {
                        _selectedNodeIndex = hit.NodeIndex;
                        if (hit.IsAnchor)
                        {
                            if (clickCount >= 2)
                            {
                                // Double-click anchor toggles Corner <-> Smooth
                                var node = _editingVectorNode.Geometry.Nodes[_selectedNodeIndex];
                                var newType = (node.Type == BezierNodeType.Corner) ? BezierNodeType.Smooth : BezierNodeType.Corner;
                                BezierVectorEngine.SetNodeType(_editingVectorNode.Geometry, _selectedNodeIndex, newType);
                                _sceneGraph.InvalidateNode(_editingVectorNode.Id);
                                History.MarkDirty();
                            }
                            else
                            {
                                _isDraggingNodeAnchor = true;
                                _startVectorGeometry = _editingVectorNode.Geometry.Clone();
                            }
                        }
                        else if (hit.IsInHandle)
                        {
                            _isDraggingNodeHandle = true;
                            _draggingOutHandle = false;
                            _startVectorGeometry = _editingVectorNode.Geometry.Clone();
                        }
                        else if (hit.IsOutHandle)
                        {
                            _isDraggingNodeHandle = true;
                            _draggingOutHandle = true;
                            _startVectorGeometry = _editingVectorNode.Geometry.Clone();
                        }
                    }
                    else
                    {
                        // Figma-style Segment Hit Test & Add Point / Bend
                        int hitSeg;
                        Point splitPt;
                        if (BezierVectorEngine.HitTestSegment(_editingVectorNode.Geometry, canvasPt, tolerance, out hitSeg, out splitPt))
                        {
                            if (_activeTool == ToolType.Pen)
                            {
                                // Pen tool clicking on segment inserts a new vertex on the segment
                                BezierVectorEngine.InsertNode(_editingVectorNode.Geometry, hitSeg + 1, splitPt);
                                _selectedNodeIndex = hitSeg + 1;
                                _isDraggingNodeAnchor = true;
                                _startVectorGeometry = _editingVectorNode.Geometry.Clone();
                                InvalidateAndRefresh(_editingVectorNode);
                                History.MarkDirty();
                                RefreshPropertyInspector();
                                return;
                            }
                            else
                            {
                                // Select / Move tool clicking on segment bends the curve
                                _isDraggingSegment = true;
                                _draggingSegmentIndex = hitSeg;
                                _startVectorGeometry = _editingVectorNode.Geometry.Clone();
                                _selectedNodeIndex = -1;
                                return;
                            }
                        }
                        else if (_activeTool == ToolType.Pen && !_editingVectorNode.Geometry.IsClosed)
                        {
                            // Pen tool clicking on empty canvas extends open geometry
                            if (_selectedNodeIndex == 0)
                            {
                                BezierVectorEngine.InsertNode(_editingVectorNode.Geometry, 0, canvasPt);
                                _selectedNodeIndex = 0;
                            }
                            else
                            {
                                BezierVectorEngine.AddNode(_editingVectorNode.Geometry, canvasPt);
                                _selectedNodeIndex = _editingVectorNode.Geometry.Nodes.Count - 1;
                            }
                            _isDraggingNodeAnchor = true;
                            _startVectorGeometry = _editingVectorNode.Geometry.Clone();
                            InvalidateAndRefresh(_editingVectorNode);
                            History.MarkDirty();
                            RefreshPropertyInspector();
                            return;
                        }
                        else
                        {
                            _selectedNodeIndex = -1;
                        }
                    }
                    RefreshCanvas();
                    RefreshPropertyInspector();
                    return;
                }

                // 2. Corner Radius on-canvas widget hit-test (when single rectangle/frame selected)
                if (_selectedLayers.Count == 1 && _activeTool == ToolType.Select)
                {
                    var primary = _selectedLayers[0];
                    if ((primary.Type == ElementType.Rectangle || primary.Type == ElementType.Frame || primary.Type == ElementType.Group || primary.Type == ElementType.Image) && primary.Width >= 20 && primary.Height >= 20)
                    {
                        if (TransformEngine.HitTestCornerRadiusHandle(canvasPt, primary.GetBounds(), primary.Rotation, primary.CornerRadius, _zoomScale))
                        {
                            _activeHandle = HandleType.CornerRadius;
                            _isTransforming = true;
                            _startCornerRadius = primary.CornerRadius;
                            _transformStartMouse = canvasPt;
                            _transformStartBounds = primary.GetBounds();
                            if (_canvasHost != null)
                            {
                                _canvasHost.CaptureMouse();
                            }
                            return;
                        }
                    }
                }

                // 2.5 Figma-style Gradient On-Canvas Rollers & Handles Hit-Test (active ONLY when gradient is being edited!)
                if (_editingGradientPaint != null && _editingGradientLayer != null && _selectedLayers.Contains(_editingGradientLayer) && _activeTool == ToolType.Select)
                {
                    var primary = _editingGradientLayer;
                    var gradPaint = _editingGradientPaint;
                    var gHit = GradientRollerEngine.HitTest(canvasPt, primary, gradPaint, _zoomScale);
                    if (gHit.Type != GradientHandleType.None)
                    {
                        if (gHit.Type == GradientHandleType.Line)
                        {
                            Color sampled = GpuRenderer.SampleGradientColor(gradPaint.GradientStops, gHit.ParameterT);
                            string hex = string.Format("#{0:X2}{1:X2}{2:X2}", sampled.R, sampled.G, sampled.B);
                            double op = sampled.A / 255.0;
                            var newStop = new GradientStopData(Math.Round(gHit.ParameterT, 3), hex, Math.Round(op, 2));
                            gradPaint.GradientStops.Add(newStop);
                            gradPaint.GradientStops.Sort((a, b) => a.Position.CompareTo(b.Position));
                            _activeGradientStopIndex = gradPaint.GradientStops.IndexOf(newStop);
                            _activeGradientHandle = GradientHandleType.StopNode;
                        }
                        else
                        {
                            _activeGradientHandle = gHit.Type;
                            _activeGradientStopIndex = gHit.StopIndex;
                        }

                        _isDraggingGradientHandle = true;
                        _activeGradientPaint = gradPaint;
                        _activeGradientLayer = primary;
                        if (_canvasHost != null)
                        {
                            _canvasHost.CaptureMouse();
                        }
                        InvalidateAndRefresh(primary);
                        if (_activeGradientRamp != null)
                        {
                            _activeGradientRamp.UpdateTrackVisuals();
                        }
                        RefreshPropertyInspector();
                        return;
                    }
                }

                // 3. Transform handles
                if (_selectedLayers.Count > 0 && _activeTool == ToolType.Select)
                {
                    var union = TransformEngine.UnionBounds(_selectedLayers);
                    double rot = (_selectedLayers.Count == 1) ? _selectedLayers[0].Rotation : 0;
                    var handle = TransformEngine.HitTestHandle(canvasPt, union, rot, _zoomScale);
                    if (handle != HandleType.None)
                    {
                        _activeHandle = handle;
                        _isTransforming = true;
                        _transformStartBounds = union;
                        _transformStartRot = rot;
                        _transformStartMouse = canvasPt;
                        _dragInitialPositions.Clear();
                        _dragInitialBounds.Clear();
                        _dragInitialRotations.Clear();
                        _dragInitialGeometries.Clear();
                        foreach (var l in _selectedLayers)
                        {
                            CollectDragInitialPositions(l, _dragInitialPositions);
                        }
                        return;
                    }
                }

                // 4. Pen tool click & curve drag start
                if (_activeTool == ToolType.Pen)
                {
                    HandlePenClick(canvasPt);
                    return;
                }

                // 5. Shape creation tools
                if (IsShapeTool(_activeTool))
                {
                    StartCreatingShape(_activeTool, canvasPt);
                    return;
                }

                // 6. Marquee tool click & drag start
                if (_activeTool == ToolType.Marquee)
                {
                    _isMarqueeSelecting = true;
                    _marqueeStartPoint = canvasPt;
                    _marqueeCurrentPoint = canvasPt;
                    if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                    {
                        _selectedLayers.Clear();
                        OnSelectionChanged();
                    }
                    return;
                }

                // 7. Select tool hit test
                if (_activeTool == ToolType.Select)
                {
                    // Double-click on Path layer enters Vector Edit Mode, on Text enters in-place text edit!
                    if (clickCount >= 2)
                    {
                        var doubleHit = _sceneGraph.HitTest(canvasPt);
                        if (doubleHit != null)
                        {
                            if (doubleHit.Type == ElementType.Path)
                            {
                                EnterVectorEditMode(doubleHit);
                                return;
                            }
                            else if (doubleHit.Type == ElementType.Text)
                            {
                                StartInPlaceTextEditing(doubleHit);
                                return;
                            }
                        }
                    }

                    var hit = _sceneGraph.HitTest(canvasPt);
                    if (hit != null)
                    {
                        bool isMulti = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)
                                    || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                        if (isMulti)
                        {
                            if (_selectedLayers.Contains(hit)) _selectedLayers.Remove(hit);
                            else _selectedLayers.Add(hit);
                        }
                        else
                        {
                            if (!_selectedLayers.Contains(hit))
                            {
                                _selectedLayers.Clear();
                                _selectedLayers.Add(hit);
                            }
                        }

                        _activeHandle = HandleType.None;
                        _isTransforming = true;
                        _transformStartBounds = TransformEngine.UnionBounds(_selectedLayers);
                        _transformStartRot = 0;
                        _transformStartMouse = canvasPt;
                        _dragInitialPositions.Clear();
                        _dragInitialBounds.Clear();
                        _dragInitialRotations.Clear();
                        _dragInitialGeometries.Clear();
                        foreach (var l in _selectedLayers)
                        {
                            CollectDragInitialPositions(l, _dragInitialPositions);
                        }
                        _hasAltDuplicated = false;
                        _altClonedLayers.Clear();
                        OnSelectionChanged();
                    }
                    else
                    {
                        bool isMultiEmpty = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)
                                         || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                        if (!isMultiEmpty)
                        {
                            _selectedLayers.Clear();
                            OnSelectionChanged();
                        }
                        // Drag on empty canvas starts rectangle marquee selection!
                        _isMarqueeSelecting = true;
                        _marqueeStartPoint = canvasPt;
                        _marqueeCurrentPoint = canvasPt;
                    }
                }
            }
        }

        public void HandleCanvasMouseDown(Point screenPt, MouseButton button)
        {
            HandleCanvasMouseDown(screenPt, button, 1);
        }

        public void HandleCanvasMouseMove(Point screenPt)
        {
            Point canvasPt = ScreenToCanvas(screenPt);

            if (_currentStudioMode == StudioMode.Animate && _interactiveCanvasEnabled && _timelineController != null && _timelineController.ActiveStateMachine != null)
            {
                var hit = _sceneGraph.HitTest(canvasPt);
                uint hitId = (hit != null) ? (uint)Math.Abs(hit.Id.GetHashCode()) : 0;
                if (hitId != _lastHoverNodeId)
                {
                    if (_lastHoverNodeId != 0)
                    {
                        _timelineController.ActiveStateMachine.OnPointerEvent(_lastHoverNodeId, PointerEventKind.Exit);
                    }
                    if (hitId != 0)
                    {
                        _timelineController.ActiveStateMachine.OnPointerEvent(hitId, PointerEventKind.Enter);
                    }
                    _lastHoverNodeId = hitId;
                    _timelineController.ActiveStateMachine.SetBool(0, hitId != 0); // isHovered
                    TriggerStateMachineStep();
                }
            }

            // Marquee / Rectangle selection dragging
            if (_isMarqueeSelecting)
            {
                _marqueeCurrentPoint = canvasPt;
                double mx = Math.Min(_marqueeStartPoint.X, _marqueeCurrentPoint.X);
                double my = Math.Min(_marqueeStartPoint.Y, _marqueeCurrentPoint.Y);
                double mw = Math.Abs(_marqueeCurrentPoint.X - _marqueeStartPoint.X);
                double mh = Math.Abs(_marqueeCurrentPoint.Y - _marqueeStartPoint.Y);
                var mRect = new Rect(mx, my, Math.Max(1, mw), Math.Max(1, mh));

                var page = Document.GetActivePage();
                if (page != null)
                {
                    _selectedLayers.Clear();
                    foreach (var layer in page.GetAllLayersFlat())
                    {
                        if (layer.IsVisible && !layer.IsLocked)
                        {
                            var b = layer.GetBounds();
                            if (mRect.IntersectsWith(b))
                            {
                                _selectedLayers.Add(layer);
                            }
                        }
                    }
                    OnSelectionChanged();
                }
                RefreshCanvas();
                return;
            }

            // Track pen hover position for live rubber-band preview line
            if (_activeTool == ToolType.Pen)
            {
                _penHoverCanvasPoint = canvasPt;
                _hasPenHoverPoint = true;
            }

            if (_isPanning)
            {
                double dx = screenPt.X - _lastMousePos.X;
                double dy = screenPt.Y - _lastMousePos.Y;
                _panOffset.X += dx;
                _panOffset.Y += dy;
                _lastMousePos = screenPt;
                RefreshCanvas();
                return;
            }

            // Gradient On-Canvas Roller Dragging (Figma-style)
            if (_isDraggingGradientHandle && _activeGradientLayer != null && _activeGradientPaint != null)
            {
                bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                if (_activeGradientHandle == GradientHandleType.Start)
                {
                    GradientRollerEngine.UpdateStartHandleFromDrag(_activeGradientLayer, _activeGradientPaint, canvasPt, isShift);
                    InvalidateAndRefresh(_activeGradientLayer);
                    if (_activeGradientRamp != null) _activeGradientRamp.UpdateTrackVisuals();
                    return;
                }
                if (_activeGradientHandle == GradientHandleType.End)
                {
                    GradientRollerEngine.UpdateEndHandleFromDrag(_activeGradientLayer, _activeGradientPaint, canvasPt, isShift);
                    InvalidateAndRefresh(_activeGradientLayer);
                    if (_activeGradientRamp != null) _activeGradientRamp.UpdateTrackVisuals();
                    return;
                }
                if (_activeGradientHandle == GradientHandleType.StopNode && _activeGradientStopIndex >= 0 && _activeGradientStopIndex < _activeGradientPaint.GradientStops.Count)
                {
                    Point p1, p2;
                    List<Point> stops;
                    if (GradientRollerEngine.GetGradientPoints(_activeGradientLayer, _activeGradientPaint, out p1, out p2, out stops))
                    {
                        double t = GradientRollerEngine.ProjectOntoSegment(canvasPt, p1, p2);
                        _activeGradientPaint.GradientStops[_activeGradientStopIndex].Position = Math.Round(t, 3);
                        InvalidateAndRefresh(_activeGradientLayer);
                        if (_activeGradientRamp != null) _activeGradientRamp.UpdateTrackVisuals();
                    }
                    return;
                }
            }

            // Vector Edit Mode: Bend tool dragging segment
            if (_isVectorEditing && _isDraggingSegment && _editingVectorNode != null && _editingVectorNode.Geometry != null && _draggingSegmentIndex >= 0)
            {
                BezierVectorEngine.BendSegment(_editingVectorNode.Geometry, _draggingSegmentIndex, canvasPt);
                _sceneGraph.InvalidateNode(_editingVectorNode.Id);
                RefreshCanvas();
                return;
            }

            // Vector Edit Mode: Dragging anchor node or tangent handle
            if (_isVectorEditing && _editingVectorNode != null && _editingVectorNode.Geometry != null && _selectedNodeIndex >= 0)
            {
                if (_isDraggingNodeAnchor)
                {
                    BezierVectorEngine.MoveAnchor(_editingVectorNode.Geometry, _selectedNodeIndex, canvasPt);
                    _sceneGraph.InvalidateNode(_editingVectorNode.Id);
                    RefreshCanvas();
                    return;
                }
                if (_isDraggingNodeHandle)
                {
                    bool isAlt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
                    var node = _editingVectorNode.Geometry.Nodes[_selectedNodeIndex];
                    if (isAlt && node.Type != BezierNodeType.Split)
                    {
                        // Holding Alt splits the handles like in Illustrator!
                        node.Type = BezierNodeType.Split;
                    }
                    BezierVectorEngine.MoveHandle(_editingVectorNode.Geometry, _selectedNodeIndex, _draggingOutHandle, canvasPt);
                    _sceneGraph.InvalidateNode(_editingVectorNode.Id);
                    RefreshCanvas();
                    return;
                }
            }

            // Pen tool: dragging pulls Bézier curve handles in real time!
            if (_isDraggingPenCurve && _activePenGeometry != null && _activePenNodeIndex >= 0 && _activePenNodeIndex < _activePenGeometry.Nodes.Count)
            {
                var node = _activePenGeometry.Nodes[_activePenNodeIndex];
                double dx = canvasPt.X - node.Anchor.X;
                double dy = canvasPt.Y - node.Anchor.Y;
                if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5)
                {
                    node.Type = BezierNodeType.Symmetric;
                    node.OutHandle = canvasPt;
                    node.InHandle = new Point(node.Anchor.X - dx, node.Anchor.Y - dy);
                    var b = BezierVectorEngine.CalculateBounds(_activePenGeometry);
                    _activePenNode.X = b.X;
                    _activePenNode.Y = b.Y;
                    _activePenNode.Width = Math.Max(2, b.Width);
                    _activePenNode.Height = Math.Max(2, b.Height);
                    _sceneGraph.InvalidateNode(_activePenNode.Id);
                    RefreshCanvas();
                }
                return;
            }

            // Pen tool moving between clicks: refresh canvas to update live rubber-band preview line
            if (_activeTool == ToolType.Pen && _activePenGeometry != null && _activePenGeometry.Nodes.Count > 0)
            {
                RefreshCanvas();
            }

            if (_isDrawingNew && _drawingNode != null)
            {
                double x = Math.Min(_dragStartPoint.X, canvasPt.X);
                double y = Math.Min(_dragStartPoint.Y, canvasPt.Y);
                double w = Math.Abs(canvasPt.X - _dragStartPoint.X);
                double h = Math.Abs(canvasPt.Y - _dragStartPoint.Y);

                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    double side = Math.Max(w, h);
                    w = side;
                    h = side;
                }

                _drawingNode.X = x;
                _drawingNode.Y = y;
                _drawingNode.Width = Math.Max(2, w);
                _drawingNode.Height = Math.Max(2, h);

                if (_drawingNode.Type == ElementType.Line || _drawingNode.Type == ElementType.Arrow)
                {
                    _drawingNode.X = _dragStartPoint.X;
                    _drawingNode.Y = _dragStartPoint.Y;
                    _drawingNode.Width = canvasPt.X - _dragStartPoint.X;
                    _drawingNode.Height = canvasPt.Y - _dragStartPoint.Y;
                }

                _sceneGraph.InvalidateNode(_drawingNode.Id);
                RefreshCanvas();
                return;
            }

            if (_isTransforming && _selectedLayers.Count > 0)
            {
                // Interactive Corner Radius Dragging
                if (_activeHandle == HandleType.CornerRadius && _selectedLayers.Count == 1)
                {
                    var primary = _selectedLayers[0];
                    double maxRadius = Math.Min(primary.Width, primary.Height) / 2.0;
                    var center = new Point(_transformStartBounds.X + _transformStartBounds.Width / 2.0, _transformStartBounds.Y + _transformStartBounds.Height / 2.0);
                    double startDist = Math.Sqrt(Math.Pow(_transformStartMouse.X - center.X, 2) + Math.Pow(_transformStartMouse.Y - center.Y, 2));
                    double curDist = Math.Sqrt(Math.Pow(canvasPt.X - center.X, 2) + Math.Pow(canvasPt.Y - center.Y, 2));
                    double delta = startDist - curDist;
                    double newR = Math.Max(0.0, Math.Min(maxRadius, _startCornerRadius + delta));
                    primary.CornerRadius = Math.Round(newR, 1);
                    primary.CornerRadiusTopLeft = primary.CornerRadius;
                    primary.CornerRadiusTopRight = primary.CornerRadius;
                    primary.CornerRadiusBottomRight = primary.CornerRadius;
                    primary.CornerRadiusBottomLeft = primary.CornerRadius;
                    _sceneGraph.InvalidateNode(primary.Id);
                    RefreshCanvas();
                    RefreshPropertyInspector();
                    _lastMousePos = screenPt;
                    return;
                }

                bool keepAspect = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                bool fromCenter = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

                if (_activeHandle != HandleType.None)
                {
                    var res = TransformEngine.ApplyTransform(
                        _activeHandle,
                        canvasPt,
                        _transformStartMouse,
                        _transformStartBounds,
                        _transformStartRot,
                        keepAspect,
                        fromCenter,
                        0);

                    // Apply to single layer or distribute across selection
                    if (_selectedLayers.Count == 1)
                    {
                        var n = _selectedLayers[0];
                        Rect oldBounds = new Rect(n.X, n.Y, n.Width, n.Height);
                        n.X = res.Bounds.X;
                        n.Y = res.Bounds.Y;
                        n.Width = res.Bounds.Width;
                        n.Height = res.Bounds.Height;
                        n.Rotation = res.Rotation;

                        // Figma Auto Layout Flexbox Resizing: switch resized axis to Fixed and reflow live!
                        if (n.AutoLayout != null && n.AutoLayout.Mode != AutoLayoutMode.None)
                        {
                            bool widthResized = (_activeHandle == HandleType.MiddleLeft || _activeHandle == HandleType.MiddleRight ||
                                                 _activeHandle == HandleType.TopLeft || _activeHandle == HandleType.TopRight ||
                                                 _activeHandle == HandleType.BottomLeft || _activeHandle == HandleType.BottomRight);
                            bool heightResized = (_activeHandle == HandleType.TopCenter || _activeHandle == HandleType.BottomCenter ||
                                                  _activeHandle == HandleType.TopLeft || _activeHandle == HandleType.TopRight ||
                                                  _activeHandle == HandleType.BottomLeft || _activeHandle == HandleType.BottomRight);

                            if (widthResized)
                            {
                                n.AutoLayout.SizingH = SizingMode.Fixed;
                            }
                            if (heightResized)
                            {
                                n.AutoLayout.SizingV = SizingMode.Fixed;
                            }

                            AutoLayoutEngine.Reflow(n);
                        }

                        if (n.Type == ElementType.Path && n.Geometry != null)
                        {
                            BezierVectorEngine.ScaleGeometry(n.Geometry, oldBounds, res.Bounds);
                        }
                        _sceneGraph.InvalidateNode(n.Id);
                    }
                    else
                    {
                        double scaleX = res.Bounds.Width / Math.Max(1, _transformStartBounds.Width);
                        double scaleY = res.Bounds.Height / Math.Max(1, _transformStartBounds.Height);
                        foreach (var n in _selectedLayers)
                        {
                            Rect oldBounds = new Rect(n.X, n.Y, n.Width, n.Height);
                            n.X = res.Bounds.X + (n.X - _transformStartBounds.X) * scaleX;
                            n.Y = res.Bounds.Y + (n.Y - _transformStartBounds.Y) * scaleY;
                            n.Width *= scaleX;
                            n.Height *= scaleY;
                            if (n.Type == ElementType.Path && n.Geometry != null)
                            {
                                Rect newBounds = new Rect(n.X, n.Y, n.Width, n.Height);
                                BezierVectorEngine.ScaleGeometry(n.Geometry, oldBounds, newBounds);
                            }
                            _sceneGraph.InvalidateNode(n.Id);
                        }
                    }
                }
                else
                {
                    // Adobe Illustrator reverse-engineered Alt-Drag Duplicate and Axis Constraint
                    double rawDx = canvasPt.X - _transformStartMouse.X;
                    double rawDy = canvasPt.Y - _transformStartMouse.Y;
                    double dx = rawDx;
                    double dy = rawDy;

                    // Shift-constrain axis alignment (Illustrator 0, 45, 90 degree snap)
                    if (keepAspect)
                    {
                        double absX = Math.Abs(rawDx);
                        double absY = Math.Abs(rawDy);
                        if (absX > 2.0 * absY)
                        {
                            dy = 0;
                        }
                        else if (absY > 2.0 * absX)
                        {
                            dx = 0;
                        }
                        else
                        {
                            double diag = (absX + absY) / 2.0;
                            dx = Math.Sign(rawDx) * diag;
                            dy = Math.Sign(rawDy) * diag;
                        }
                    }

                    // Smart Snapping for Elements (Figma style red/magenta alignment guides)
                    _activeSnapGuides.Clear();
                    if (_smartSnappingEnabled && !keepAspect && _dragInitialPositions.Count > 0)
                    {
                        var page = Document.GetActivePage();
                        if (page != null)
                        {
                            double minInitX = double.MaxValue, minInitY = double.MaxValue;
                            double maxInitX = double.MinValue, maxInitY = double.MinValue;
                            foreach (var kvp in _dragInitialPositions)
                            {
                                var n = kvp.Key;
                                minInitX = Math.Min(minInitX, kvp.Value.X);
                                minInitY = Math.Min(minInitY, kvp.Value.Y);
                                maxInitX = Math.Max(maxInitX, kvp.Value.X + n.Width);
                                maxInitY = Math.Max(maxInitY, kvp.Value.Y + n.Height);
                            }

                            double proposedLeft = minInitX + dx;
                            double proposedTop = minInitY + dy;
                            double selW = maxInitX - minInitX;
                            double selH = maxInitY - minInitY;
                            double proposedCenterX = proposedLeft + selW / 2.0;
                            double proposedRight = proposedLeft + selW;
                            double proposedCenterY = proposedTop + selH / 2.0;
                            double proposedBottom = proposedTop + selH;

                            double snapThreshold = 6.0 / _zoomScale;
                            double bestSnapDeltaX = double.MaxValue;
                            double bestSnapX = 0;
                            double snapSpanMinY = proposedTop;
                            double snapSpanMaxY = proposedBottom;

                            double bestSnapDeltaY = double.MaxValue;
                            double bestSnapY = 0;
                            double snapSpanMinX = proposedLeft;
                            double snapSpanMaxX = proposedRight;

                            var others = page.GetAllLayersFlat().Where(l => !_dragInitialPositions.ContainsKey(l) && l.IsVisible).ToList();
                            foreach (var other in others)
                            {
                                double oL = other.X;
                                double oC = other.X + other.Width / 2.0;
                                double oR = other.X + other.Width;
                                double oT = other.Y;
                                double oM = other.Y + other.Height / 2.0;
                                double oB = other.Y + other.Height;

                                double[] edgesX = new double[] { proposedLeft, proposedCenterX, proposedRight };
                                double[] candX = new double[] { oL, oC, oR };
                                for (int ei = 0; ei < edgesX.Length; ei++)
                                {
                                    for (int ci = 0; ci < candX.Length; ci++)
                                    {
                                        double diff = candX[ci] - edgesX[ei];
                                        if (Math.Abs(diff) <= snapThreshold && Math.Abs(diff) < Math.Abs(bestSnapDeltaX))
                                        {
                                            bestSnapDeltaX = diff;
                                            bestSnapX = candX[ci];
                                            snapSpanMinY = Math.Min(proposedTop, oT);
                                            snapSpanMaxY = Math.Max(proposedBottom, oB);
                                        }
                                    }
                                }

                                double[] edgesY = new double[] { proposedTop, proposedCenterY, proposedBottom };
                                double[] candY = new double[] { oT, oM, oB };
                                for (int ei = 0; ei < edgesY.Length; ei++)
                                {
                                    for (int ci = 0; ci < candY.Length; ci++)
                                    {
                                        double diff = candY[ci] - edgesY[ei];
                                        if (Math.Abs(diff) <= snapThreshold && Math.Abs(diff) < Math.Abs(bestSnapDeltaY))
                                        {
                                            bestSnapDeltaY = diff;
                                            bestSnapY = candY[ci];
                                            snapSpanMinX = Math.Min(proposedLeft, oL);
                                            snapSpanMaxX = Math.Max(proposedRight, oR);
                                        }
                                    }
                                }
                            }

                            if (Math.Abs(bestSnapDeltaX) <= snapThreshold)
                            {
                                dx += bestSnapDeltaX;
                                _activeSnapGuides.Add(new SnapGuide { IsVertical = true, Position = bestSnapX, Start = snapSpanMinY, End = snapSpanMaxY });
                            }
                            if (Math.Abs(bestSnapDeltaY) <= snapThreshold)
                            {
                                dy += bestSnapDeltaY;
                                _activeSnapGuides.Add(new SnapGuide { IsVertical = false, Position = bestSnapY, Start = snapSpanMinX, End = snapSpanMaxX });
                            }
                        }
                    }

                    bool isAlt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

                    if (isAlt && !_hasAltDuplicated && (Math.Abs(dx) > 1 || Math.Abs(dy) > 1))
                    {
                        // Restore original layers to starting positions
                        foreach (var kvp in _dragInitialPositions)
                        {
                            double oldX = kvp.Key.X;
                            double oldY = kvp.Key.Y;
                            kvp.Key.X = kvp.Value.X;
                            kvp.Key.Y = kvp.Value.Y;
                            if (kvp.Key.Type == ElementType.Path && kvp.Key.Geometry != null)
                            {
                                BezierVectorEngine.OffsetGeometry(kvp.Key.Geometry, kvp.Value.X - oldX, kvp.Value.Y - oldY);
                            }
                            _sceneGraph.InvalidateNode(kvp.Key.Id);
                        }

                        var page = Document.GetActivePage();
                        _altClonedLayers.Clear();
                        var clonedInitPositions = new Dictionary<LayerNode, Point>();

                        foreach (var orig in _selectedLayers)
                        {
                            var clone = orig.Clone();
                            clone.Name = orig.Name;
                            clone.X = _dragInitialPositions[orig].X + dx;
                            clone.Y = _dragInitialPositions[orig].Y + dy;
                            if (clone.Type == ElementType.Path && clone.Geometry != null)
                            {
                                clone.Geometry = orig.Geometry.Clone();
                                BezierVectorEngine.OffsetGeometry(clone.Geometry, dx, dy);
                            }

                            LayerNode parent;
                            var container = page.FindContainer(orig, out parent);
                            if (container != null)
                            {
                                int idx = container.IndexOf(orig);
                                clone.ParentId = parent != null ? parent.Id : null;
                                if (idx >= 0 && idx < container.Count)
                                    container.Insert(idx + 1, clone);
                                else
                                    container.Add(clone);
                            }
                            else
                            {
                                clone.ParentId = null;
                                page.Layers.Add(clone);
                            }
                            _altClonedLayers.Add(clone);
                            clonedInitPositions[clone] = _dragInitialPositions[orig];
                        }

                        _dragInitialPositions = clonedInitPositions;
                        _selectedLayers.Clear();
                        _selectedLayers.AddRange(_altClonedLayers);
                        _hasAltDuplicated = true;
                        _sceneGraph.Synchronize(page);
                        RefreshLayersList();
                    }

                    // Apply translation offset to active selection AND all nested frame children synchronously
                    foreach (var kvp in _dragInitialPositions)
                    {
                        var n = kvp.Key;
                        double newX = kvp.Value.X + dx;
                        double newY = kvp.Value.Y + dy;
                        double shiftX = newX - n.X;
                        double shiftY = newY - n.Y;
                        n.X = newX;
                        n.Y = newY;
                        if (n.Type == ElementType.Path && n.Geometry != null)
                        {
                            BezierVectorEngine.OffsetGeometry(n.Geometry, shiftX, shiftY);
                        }
                        _sceneGraph.InvalidateNode(n.Id);
                    }
                }

                RefreshCanvas();
                RefreshPropertyInspector();
            }

            // Hover cursor for Figma-style Corner Radius Handles
            if (_selectedLayers.Count == 1 && _activeTool == ToolType.Select && !_isTransforming && !_isPanning && !_isDrawingNew && !_isDraggingGradientHandle)
            {
                var primary = _selectedLayers[0];
                if ((primary.Type == ElementType.Rectangle || primary.Type == ElementType.Frame || primary.Type == ElementType.Group || primary.Type == ElementType.Image) && primary.Width >= 20 && primary.Height >= 20)
                {
                    if (TransformEngine.HitTestCornerRadiusHandle(canvasPt, primary.GetBounds(), primary.Rotation, primary.CornerRadius, _zoomScale))
                    {
                        if (_canvasHost != null) _canvasHost.Cursor = Cursors.Hand;
                        _lastMousePos = screenPt;
                        return;
                    }
                }
            }

            // Hover cursor for Figma-style Gradient Rollers & Handles (ONLY when editing gradient!)
            if (_editingGradientPaint != null && _editingGradientLayer != null && _selectedLayers.Contains(_editingGradientLayer) && _activeTool == ToolType.Select && !_isTransforming && !_isPanning && !_isDrawingNew && !_isDraggingGradientHandle)
            {
                var gHit = GradientRollerEngine.HitTest(canvasPt, _editingGradientLayer, _editingGradientPaint, _zoomScale);
                if (gHit.Type != GradientHandleType.None)
                {
                    _canvasHost.Cursor = Cursors.Hand;
                    _lastMousePos = screenPt;
                    return;
                }
                else if (_canvasHost.Cursor == Cursors.Hand && _activeTool == ToolType.Select)
                {
                    _canvasHost.Cursor = Cursors.Arrow;
                }
            }

            _lastMousePos = screenPt;
        }

        public void HandleCanvasMouseUp(Point screenPt, MouseButton button)
        {
            if (button == MouseButton.Right)
            {
                if (_isPanning) _isPanning = false;
                if (_isDrawingNew) { _isDrawingNew = false; _drawingNode = null; }
                Point canvasPt = ScreenToCanvas(screenPt);
                _lastRightClickCanvasPoint = canvasPt;
                var hit = _sceneGraph.HitTest(canvasPt);
                if (hit != null)
                {
                    if (!_selectedLayers.Contains(hit))
                    {
                        _selectedLayers.Clear();
                        _selectedLayers.Add(hit);
                        OnSelectionChanged();
                        RefreshCanvas();
                    }
                    var cm = CreateLayerContextMenu(hit);
                    cm.PlacementTarget = _canvasHost;
                    cm.Placement = PlacementMode.MousePoint;
                    cm.IsOpen = true;
                }
                else
                {
                    var cm = CreateCanvasContextMenu(canvasPt);
                    cm.PlacementTarget = _canvasHost;
                    cm.Placement = PlacementMode.MousePoint;
                    cm.IsOpen = true;
                }
                return;
            }

            if (_currentStudioMode == StudioMode.Animate && _interactiveCanvasEnabled && _timelineController != null && _timelineController.ActiveStateMachine != null)
            {
                Point canvasPt = ScreenToCanvas(screenPt);
                var hit = _sceneGraph.HitTest(canvasPt);
                if (hit != null)
                {
                    uint hitId = (uint)Math.Abs(hit.Id.GetHashCode());
                    _timelineController.ActiveStateMachine.OnPointerEvent(hitId, PointerEventKind.Up);
                }
                _timelineController.ActiveStateMachine.SetBool(1, false); // isPressed = false
                _timelineController.ActiveStateMachine.FireTrigger(3); // clickTrigger
                TriggerStateMachineStep();
            }

            if (_isDraggingGradientHandle)
            {
                _isDraggingGradientHandle = false;
                _activeGradientHandle = GradientHandleType.None;
                if (_canvasHost != null && _canvasHost.IsMouseCaptured)
                {
                    _canvasHost.ReleaseMouseCapture();
                }
                if (_activeGradientPaint != null && _activeGradientPaint.GradientStops != null)
                {
                    _activeGradientPaint.GradientStops.Sort((a, b) => a.Position.CompareTo(b.Position));
                }
                if (_activeGradientLayer != null)
                {
                    InvalidateAndRefresh(_activeGradientLayer);
                    RefreshPropertyInspector();
                }
                _activeGradientLayer = null;
                _activeGradientPaint = null;
            }

            if (_isPanning)
            {
                _isPanning = false;
                if (_canvasHost != null && _canvasHost.IsMouseCaptured)
                {
                    _canvasHost.ReleaseMouseCapture();
                }
                Cursor = (_activeTool == ToolType.Hand) ? Cursors.Hand : Cursors.Arrow;
            }

            if (_isMarqueeSelecting)
            {
                _isMarqueeSelecting = false;
                RefreshCanvas();
                RefreshPropertyInspector();
            }

            if (_isVectorEditing)
            {
                _isDraggingNodeAnchor = false;
                _isDraggingNodeHandle = false;
                if (_editingVectorNode != null && _editingVectorNode.Geometry != null)
                {
                    var b = BezierVectorEngine.CalculateBounds(_editingVectorNode.Geometry);
                    _editingVectorNode.X = b.X;
                    _editingVectorNode.Y = b.Y;
                    _editingVectorNode.Width = b.Width;
                    _editingVectorNode.Height = b.Height;
                    _sceneGraph.InvalidateNode(_editingVectorNode.Id);

                    if (_startVectorGeometry != null)
                    {
                        var cmd = new ModifyVectorGeometryCommand(
                            _editingVectorNode,
                            _startVectorGeometry,
                            _editingVectorNode.Geometry.Clone(),
                            () =>
                            {
                                _sceneGraph.InvalidateNode(_editingVectorNode.Id);
                                RefreshCanvas();
                                RefreshPropertyInspector();
                            });
                        History.Execute(cmd);
                        _startVectorGeometry = null;
                    }
                    else
                    {
                        History.MarkDirty();
                    }

                    RefreshCanvas();
                    RefreshPropertyInspector();
                }
            }

            if (_isDraggingSegment)
            {
                _isDraggingSegment = false;
                _draggingSegmentIndex = -1;
                if (_editingVectorNode != null)
                {
                    _sceneGraph.InvalidateNode(_editingVectorNode.Id);
                    RefreshCanvas();
                }
            }

            if (_isDraggingPenCurve)
            {
                _isDraggingPenCurve = false;
                _activePenNodeIndex = -1;
                if (_activePenGeometry != null && _activePenNode != null)
                {
                    var b = BezierVectorEngine.CalculateBounds(_activePenGeometry);
                    _activePenNode.X = b.X;
                    _activePenNode.Y = b.Y;
                    _activePenNode.Width = b.Width;
                    _activePenNode.Height = b.Height;
                    _sceneGraph.InvalidateNode(_activePenNode.Id);
                    RefreshCanvas();
                }
            }

            _activeSnapGuides.Clear();

            if (_isDrawingNew)
            {
                _isDrawingNew = false;
                if (_drawingNode != null)
                {
                    var page = Document.GetActivePage();
                    History.Execute(new AddLayerCommand(page, _drawingNode));
                    _selectedLayers.Clear();
                    _selectedLayers.Add(_drawingNode);
                    var created = _drawingNode;
                    _drawingNode = null;
                    SetActiveTool(ToolType.Select);
                    OnSelectionChanged();
                    if (created.Type == ElementType.Text)
                    {
                        StartInPlaceTextEditing(created);
                    }
                }
            }

            if (_isTransforming)
            {
                if (_canvasHost != null && _canvasHost.IsMouseCaptured)
                {
                    _canvasHost.ReleaseMouseCapture();
                }
                _isTransforming = false;
                var handleUsed = _activeHandle;
                _activeHandle = HandleType.None;

                if (handleUsed == HandleType.CornerRadius)
                {
                    if (_selectedLayers.Count == 1 && Math.Abs(_selectedLayers[0].CornerRadius - _startCornerRadius) > 0.01)
                    {
                        var primary = _selectedLayers[0];
                        double afterR = primary.CornerRadius;
                        double beforeR = _startCornerRadius;
                        History.Execute(new ModifyCornerRadiusCommand(primary, beforeR, afterR, () =>
                        {
                            _sceneGraph.InvalidateNode(primary.Id);
                            RefreshCanvas();
                            RefreshPropertyInspector();
                        }));
                    }
                    else
                    {
                        History.MarkDirty();
                        RefreshCanvas();
                        RefreshPropertyInspector();
                    }
                    return;
                }

                // Record the last transformation delta for Adobe Illustrator "Transform Again" (Ctrl+D)
                if (_dragInitialPositions.Count > 0 && _selectedLayers.Count > 0)
                {
                    var first = _selectedLayers[0];
                    if (_dragInitialPositions.ContainsKey(first))
                    {
                        double finalDx = first.X - _dragInitialPositions[first].X;
                        double finalDy = first.Y - _dragInitialPositions[first].Y;
                        if (Math.Abs(finalDx) > 1 || Math.Abs(finalDy) > 1)
                        {
                            _lastTransformDelta = new Point(finalDx, finalDy);
                            CheckAutoReparentLayersOnDrop(_selectedLayers);
                        }
                    }
                }

                if (_hasAltDuplicated)
                {
                    var page = Document.GetActivePage();
                    History.Execute(new AddLayersCommand(page, _altClonedLayers));
                    _sceneGraph.Synchronize(page);
                    OnSelectionChanged();
                    RefreshLayersList();
                    RefreshCanvas();
                    History.MarkDirty();
                }
                else
                {
                    if (handleUsed != HandleType.None)
                    {
                        bool changed = false;
                        foreach (var kvp in _dragInitialBounds)
                        {
                            var node = kvp.Key;
                            var initRect = kvp.Value;
                            if (Math.Abs(node.X - initRect.X) > 0.001 ||
                                Math.Abs(node.Y - initRect.Y) > 0.001 ||
                                Math.Abs(node.Width - initRect.Width) > 0.001 ||
                                Math.Abs(node.Height - initRect.Height) > 0.001 ||
                                (_dragInitialRotations.ContainsKey(node) && Math.Abs(node.Rotation - _dragInitialRotations[node]) > 0.001))
                            {
                                changed = true;
                                break;
                            }
                        }
                        if (changed)
                        {
                            var afterBounds = new Dictionary<LayerNode, Rect>();
                            var afterRots = new Dictionary<LayerNode, double>();
                            var afterGeos = new Dictionary<LayerNode, VectorGeometry>();
                            foreach (var n in _dragInitialBounds.Keys)
                            {
                                afterBounds[n] = new Rect(n.X, n.Y, n.Width, n.Height);
                                afterRots[n] = n.Rotation;
                                if (n.Type == ElementType.Path && n.Geometry != null)
                                {
                                    afterGeos[n] = n.Geometry.Clone();
                                }
                            }
                            var cmd = new TransformLayersCommand(
                                _dragInitialBounds,
                                afterBounds,
                                _dragInitialRotations,
                                afterRots,
                                _dragInitialGeometries,
                                afterGeos,
                                () => {
                                    foreach (var n in afterBounds.Keys) _sceneGraph.InvalidateNode(n.Id);
                                    RefreshCanvas();
                                    RefreshPropertyInspector();
                                });
                            History.Execute(cmd);
                        }
                    }
                    else
                    {
                        bool moved = false;
                        foreach (var kvp in _dragInitialPositions)
                        {
                            var node = kvp.Key;
                            var initPt = kvp.Value;
                            if (Math.Abs(node.X - initPt.X) > 0.001 || Math.Abs(node.Y - initPt.Y) > 0.001)
                            {
                                moved = true;
                                break;
                            }
                        }
                        if (moved)
                        {
                            var afterPositions = new Dictionary<LayerNode, Point>();
                            var afterGeos = new Dictionary<LayerNode, VectorGeometry>();
                            foreach (var n in _dragInitialPositions.Keys)
                            {
                                afterPositions[n] = new Point(n.X, n.Y);
                                if (n.Type == ElementType.Path && n.Geometry != null)
                                {
                                    afterGeos[n] = n.Geometry.Clone();
                                }
                            }
                            var cmd = new MoveLayersPositionsCommand(
                                _dragInitialPositions,
                                afterPositions,
                                _dragInitialGeometries,
                                afterGeos,
                                () => {
                                    foreach (var n in afterPositions.Keys) _sceneGraph.InvalidateNode(n.Id);
                                    RefreshCanvas();
                                    RefreshPropertyInspector();
                                });
                            History.Execute(cmd);
                        }
                    }
                }

                _dragInitialPositions.Clear();
                _dragInitialBounds.Clear();
                _dragInitialRotations.Clear();
                _dragInitialGeometries.Clear();
                _hasAltDuplicated = false;
            }
        }

        public void HandleCanvasMouseWheel(Point screenPt, int delta)
        {
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isCtrl)
            {
                double zoomFactor = (delta > 0) ? 1.15 : 0.85;
                double newZoom = Math.Max(0.05, Math.Min(32.0, _zoomScale * zoomFactor));

                // Centered zoom on mouse cursor
                _panOffset.X = screenPt.X - (screenPt.X - _panOffset.X) * (newZoom / _zoomScale);
                _panOffset.Y = screenPt.Y - (screenPt.Y - _panOffset.Y) * (newZoom / _zoomScale);
                _zoomScale = newZoom;

                if (_zoomPercentBlock != null)
                {
                    _zoomPercentBlock.Text = string.Format("{0:P0}", _zoomScale);
                }
                RefreshCanvas();
            }
            else if (isShift)
            {
                // Shift + Wheel / Slide = Horizontal Pan
                _panOffset.X += delta;
                RefreshCanvas();
            }
            else
            {
                // Double finger slide / standard wheel = Smooth Vertical Pan
                _panOffset.Y += delta;
                RefreshCanvas();
            }
        }

        public void HandleCanvasManipulationDelta(ManipulationDeltaEventArgs e)
        {
            if (e == null) return;
            if (e.DeltaManipulation.Translation.X != 0 || e.DeltaManipulation.Translation.Y != 0)
            {
                _panOffset.X += e.DeltaManipulation.Translation.X;
                _panOffset.Y += e.DeltaManipulation.Translation.Y;
            }
            if (e.DeltaManipulation.Scale.X != 1.0 && e.DeltaManipulation.Scale.X > 0)
            {
                double factor = e.DeltaManipulation.Scale.X;
                double newZoom = Math.Max(0.05, Math.Min(32.0, _zoomScale * factor));
                Point origin = e.ManipulationOrigin;
                _panOffset.X = origin.X - (origin.X - _panOffset.X) * (newZoom / _zoomScale);
                _panOffset.Y = origin.Y - (origin.Y - _panOffset.Y) * (newZoom / _zoomScale);
                _zoomScale = newZoom;
                if (_zoomPercentBlock != null)
                {
                    _zoomPercentBlock.Text = string.Format("{0:P0}", _zoomScale);
                }
            }
            RefreshCanvas();
            e.Handled = true;
        }

        private void StartCreatingShape(ToolType tool, Point canvasPt)
        {
            _isDrawingNew = true;
            _dragStartPoint = canvasPt;

            ElementType elemType = ElementType.Rectangle;
            string defaultColor = "#0D99FF";

            switch (tool)
            {
                case ToolType.Frame: elemType = ElementType.Frame; defaultColor = "#1E2230"; break;
                case ToolType.Rect: elemType = ElementType.Rectangle; defaultColor = "#D9D9D9"; break;
                case ToolType.Ellipse: elemType = ElementType.Ellipse; defaultColor = "#D9D9D9"; break;
                case ToolType.Star: elemType = ElementType.Star; defaultColor = "#D9D9D9"; break;
                case ToolType.Polygon: elemType = ElementType.Polygon; defaultColor = "#D9D9D9"; break;
                case ToolType.Text: elemType = ElementType.Text; defaultColor = "#FFFFFF"; break;
                case ToolType.Line: elemType = ElementType.Line; defaultColor = "#94A3B8"; break;
                case ToolType.Arrow: elemType = ElementType.Arrow; defaultColor = "#A855F7"; break;
            }

            _drawingNode = new LayerNode
            {
                Type = elemType,
                Name = elemType.ToString(),
                X = canvasPt.X,
                Y = canvasPt.Y,
                Width = (elemType == ElementType.Text) ? 140 : 10,
                Height = (elemType == ElementType.Text) ? 30 : 10,
                Fills = new List<Paint> { Paint.Solid(defaultColor) },
                Strokes = new List<StrokeData>() // NO STROKE on default for shapes!
            };

            if (elemType == ElementType.Text)
            {
                _drawingNode.Typography = new TypographyData { Text = "Type something...", FontSize = 16.0 };
            }
            else if (elemType == ElementType.Line || elemType == ElementType.Arrow)
            {
                _drawingNode.Fills = new List<Paint>();
                _drawingNode.Strokes = new List<StrokeData>
                {
                    new StrokeData { Paint = Paint.Solid(defaultColor), Width = 2.0 }
                };
            }

            var page = Document.GetActivePage();
            page.Layers.Add(_drawingNode);
            _sceneGraph.Synchronize(page);
        }

        private void HandlePenClick(Point canvasPt)
        {
            var page = Document.GetActivePage();
            if (_activePenNode == null)
            {
                _activePenGeometry = new VectorGeometry { IsClosed = false };
                _activePenNode = new LayerNode
                {
                    Type = ElementType.Path,
                    Name = "Pen Path",
                    X = canvasPt.X,
                    Y = canvasPt.Y,
                    Width = 10,
                    Height = 10,
                    Geometry = _activePenGeometry,
                    Fills = new List<Paint> { Paint.Solid("#6366F1", 0.25) },
                    Strokes = new List<StrokeData> { new StrokeData { Paint = Paint.Solid("#6366F1"), Width = 2.0 } }
                };
                page.Layers.Add(_activePenNode);
                _sceneGraph.Synchronize(page);
            }

            // If clicking near first anchor node, close path and finish!
            if (_activePenGeometry.Nodes.Count > 1)
            {
                var first = _activePenGeometry.Nodes[0];
                double closeDist = 12.0 / _zoomScale;
                if (Math.Abs(canvasPt.X - first.Anchor.X) <= closeDist && Math.Abs(canvasPt.Y - first.Anchor.Y) <= closeDist)
                {
                    _activePenGeometry.IsClosed = true;
                    FinishPenDrawing();
                    return;
                }
            }

            var newNode = new BezierNode(canvasPt.X, canvasPt.Y);
            _activePenGeometry.Nodes.Add(newNode);
            _activePenNodeIndex = _activePenGeometry.Nodes.Count - 1;
            _isDraggingPenCurve = true;
            _dragStartPoint = canvasPt;

            var b = BezierVectorEngine.CalculateBounds(_activePenGeometry);
            _activePenNode.X = b.X;
            _activePenNode.Y = b.Y;
            _activePenNode.Width = Math.Max(2, b.Width);
            _activePenNode.Height = Math.Max(2, b.Height);

            _sceneGraph.InvalidateNode(_activePenNode.Id);
            RefreshCanvas();
        }

        public void FinishPenDrawing()
        {
            if (_activePenNode != null && _activePenGeometry != null)
            {
                if (_activePenGeometry.Nodes.Count < 2)
                {
                    var p = Document.GetActivePage();
                    if (p != null) p.Layers.Remove(_activePenNode);
                    _sceneGraph.Synchronize(p);
                    _activePenNode = null;
                    _activePenGeometry = null;
                    _isDraggingPenCurve = false;
                    _activePenNodeIndex = -1;
                    RefreshCanvas();
                    return;
                }

                var b = BezierVectorEngine.CalculateBounds(_activePenGeometry);
                _activePenNode.X = b.X;
                _activePenNode.Y = b.Y;
                _activePenNode.Width = Math.Max(2, b.Width);
                _activePenNode.Height = Math.Max(2, b.Height);
                var page = Document.GetActivePage();
                History.Execute(new AddLayerCommand(page, _activePenNode));
                _selectedLayers.Clear();
                _selectedLayers.Add(_activePenNode);
                _activePenNode = null;
                _activePenGeometry = null;
                _isDraggingPenCurve = false;
                _activePenNodeIndex = -1;
                SetActiveTool(ToolType.Select);
                OnSelectionChanged();
                RefreshCanvas();
            }
        }

        public void UndoLastPenNode()
        {
            if (_activePenGeometry != null && _activePenGeometry.Nodes.Count > 0)
            {
                _activePenGeometry.Nodes.RemoveAt(_activePenGeometry.Nodes.Count - 1);
                if (_activePenGeometry.Nodes.Count == 0)
                {
                    var p = Document.GetActivePage();
                    if (p != null && _activePenNode != null) p.Layers.Remove(_activePenNode);
                    _sceneGraph.Synchronize(p);
                    _activePenNode = null;
                    _activePenGeometry = null;
                    _isDraggingPenCurve = false;
                    _activePenNodeIndex = -1;
                }
                else
                {
                    _activePenNodeIndex = _activePenGeometry.Nodes.Count - 1;
                    var b = BezierVectorEngine.CalculateBounds(_activePenGeometry);
                    _activePenNode.X = b.X;
                    _activePenNode.Y = b.Y;
                    _activePenNode.Width = Math.Max(2, b.Width);
                    _activePenNode.Height = Math.Max(2, b.Height);
                    _sceneGraph.InvalidateNode(_activePenNode.Id);
                }
                RefreshCanvas();
            }
        }

        public void EnterVectorEditMode(LayerNode pathNode)
        {
            if (pathNode == null || pathNode.Type != ElementType.Path || pathNode.Geometry == null) return;
            _isVectorEditing = true;
            _editingVectorNode = pathNode;
            _selectedLayers.Clear();
            _selectedLayers.Add(pathNode);
            _selectedNodeIndex = (pathNode.Geometry.Nodes.Count > 0) ? 0 : -1;
            _isDraggingNodeAnchor = false;
            _isDraggingNodeHandle = false;
            SetActiveTool(ToolType.Select);
            RefreshCanvas();
            RefreshPropertyInspector();
        }

        public void ExitVectorEditMode()
        {
            if (!_isVectorEditing) return;
            if (_editingVectorNode != null && _editingVectorNode.Geometry != null)
            {
                var b = BezierVectorEngine.CalculateBounds(_editingVectorNode.Geometry);
                _editingVectorNode.X = b.X;
                _editingVectorNode.Y = b.Y;
                _editingVectorNode.Width = b.Width;
                _editingVectorNode.Height = b.Height;
                _sceneGraph.InvalidateNode(_editingVectorNode.Id);
                History.MarkDirty();
            }
            _isVectorEditing = false;
            _editingVectorNode = null;
            _selectedNodeIndex = -1;
            _isDraggingNodeAnchor = false;
            _isDraggingNodeHandle = false;
            RefreshCanvas();
            RefreshPropertyInspector();
        }

        private bool IsShapeTool(ToolType tool)
        {
            return tool == ToolType.Rect || tool == ToolType.Ellipse || tool == ToolType.Star ||
                   tool == ToolType.Polygon || tool == ToolType.Frame || tool == ToolType.Text ||
                   tool == ToolType.Line || tool == ToolType.Arrow;
        }

        public void RenderOverlays(DrawingContext dc)
        {
            // If actively drawing with Pen Tool, render full pen drawing overlay (all nodes, handles, rubber-band)
            if (_activeTool == ToolType.Pen && _activePenGeometry != null && _activePenGeometry.Nodes.Count > 0)
            {
                RenderPenDrawingOverlay(dc);
                return;
            }

            // If in Vector Edit Mode, render vector node editing overlay
            if (_isVectorEditing && _editingVectorNode != null && _editingVectorNode.Geometry != null)
            {
                RenderVectorEditModeOverlay(dc, _editingVectorNode.Geometry);
                return;
            }

            // Marquee / Rectangle selection preview rectangle
            if (_isMarqueeSelecting)
            {
                double mx = Math.Min(_marqueeStartPoint.X, _marqueeCurrentPoint.X);
                double my = Math.Min(_marqueeStartPoint.Y, _marqueeCurrentPoint.Y);
                double mw = Math.Abs(_marqueeCurrentPoint.X - _marqueeStartPoint.X);
                double mh = Math.Abs(_marqueeCurrentPoint.Y - _marqueeStartPoint.Y);
                var marqueeRect = new Rect(mx, my, Math.Max(1, mw), Math.Max(1, mh));

                var marqueeFill = new SolidColorBrush(Color.FromArgb(35, 56, 189, 248)); // #38BDF8 @ 14%
                marqueeFill.Freeze();
                var marqueeStroke = new Pen(new SolidColorBrush(Color.FromArgb(220, 56, 189, 248)), 1.2 / _zoomScale);
                marqueeStroke.DashStyle = DashStyles.Dash;
                marqueeStroke.Freeze();

                dc.DrawRectangle(marqueeFill, marqueeStroke, marqueeRect);
            }

            // Smart Snapping Alignment Guides
            if (_activeSnapGuides != null && _activeSnapGuides.Count > 0)
            {
                var snapPen = new Pen(new SolidColorBrush(Color.FromRgb(244, 63, 94)), 1.2 / _zoomScale);
                snapPen.DashStyle = new DashStyle(new double[] { 4, 3 }, 0);
                snapPen.Freeze();

                foreach (var g in _activeSnapGuides)
                {
                    if (g.IsVertical)
                    {
                        dc.DrawLine(snapPen, new Point(g.Position, g.Start - 20 / _zoomScale), new Point(g.Position, g.End + 20 / _zoomScale));
                    }
                    else
                    {
                        dc.DrawLine(snapPen, new Point(g.Start - 20 / _zoomScale, g.Position), new Point(g.End + 20 / _zoomScale, g.Position));
                    }
                }
            }

            if (_selectedLayers.Count == 0) return;

            // Draw bounding box & 8 transform handles
            var union = TransformEngine.UnionBounds(_selectedLayers);
            if (union.IsEmpty) return;

            double rot = (_selectedLayers.Count == 1) ? _selectedLayers[0].Rotation : 0;
            var center = new Point(union.X + union.Width / 2.0, union.Y + union.Height / 2.0);

            var mat = Matrix.Identity;
            if (Math.Abs(rot) > 0.001)
            {
                mat.RotateAt(rot, center.X, center.Y);
            }

            dc.PushTransform(new MatrixTransform(mat));

            // Blue selection border
            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(99, 102, 241)), 1.5 / _zoomScale);
            borderPen.Freeze();
            dc.DrawRectangle(null, borderPen, union);

            // 4 Corner radius circular handles inside corners for rectangles & frames
            if (_selectedLayers.Count == 1)
            {
                var primary = _selectedLayers[0];
                if ((primary.Type == ElementType.Rectangle || primary.Type == ElementType.Frame || primary.Type == ElementType.Group || primary.Type == ElementType.Image) && primary.Width >= 20 && primary.Height >= 20)
                {
                    var crPoints = TransformEngine.GetCornerRadiusHandlePositions(union, primary.CornerRadius, _zoomScale);
                    double crRadius = 4.0 / _zoomScale;

                    var crBrush = Brushes.White;
                    var crPen = new Pen(new SolidColorBrush(Color.FromRgb(99, 102, 241)), 1.5 / _zoomScale);
                    crPen.Freeze();

                    foreach (var p in crPoints)
                    {
                        dc.DrawEllipse(crBrush, crPen, p, crRadius, crRadius);
                    }
                }
            }

            // Handle points
            var handles = TransformEngine.GetHandlePositions(union);
            double hSize = TransformEngine.HandleSize / _zoomScale;
            var handleBrush = Brushes.White;
            var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(99, 102, 241)), 1.5 / _zoomScale);
            handlePen.Freeze();

            foreach (var kvp in handles)
            {
                var pt = kvp.Value;
                if (kvp.Key == HandleType.Rotate)
                {
                    // Rotate circle handle
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(99, 102, 241)), handlePen, pt, hSize * 0.7, hSize * 0.7);
                    // Line from top-center to rotate handle
                    var tc = handles[HandleType.TopCenter];
                    dc.DrawLine(handlePen, tc, pt);
                }
                else
                {
                    dc.DrawRectangle(handleBrush, handlePen, new Rect(pt.X - hSize / 2, pt.Y - hSize / 2, hSize, hSize));
                }
            }

            // Draw dimension pill: "120 x 80"
            var typeFace = new Typeface("Segoe UI");
            var ft = new FormattedText(
                string.Format(CultureInfo.InvariantCulture, "{0:F0} × {1:F0}", union.Width, union.Height),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeFace,
                11 / _zoomScale,
                Brushes.White);

            var pillRect = new Rect(center.X - ft.Width / 2.0 - 4 / _zoomScale, union.Y + union.Height + 8 / _zoomScale, ft.Width + 8 / _zoomScale, ft.Height + 4 / _zoomScale);
            var pillBg = new SolidColorBrush(Color.FromArgb(200, 20, 24, 36));
            pillBg.Freeze();
            dc.DrawRoundedRectangle(pillBg, null, pillRect, 4 / _zoomScale, 4 / _zoomScale);
            dc.DrawText(ft, new Point(center.X - ft.Width / 2.0, union.Y + union.Height + 10 / _zoomScale));

            dc.Pop(); // pop transform

            // If Bézier Path selected, draw anchor nodes
            if (_selectedLayers.Count == 1 && _selectedLayers[0].Type == ElementType.Path && _selectedLayers[0].Geometry != null)
            {
                RenderBezierNodesOverlay(dc, _selectedLayers[0].Geometry);
            }

            // Figma-style Gradient On-Canvas Rollers & Handles (active ONLY when editing/selected from effects bar)
            if (_editingGradientPaint != null && _editingGradientLayer != null && _selectedLayers.Contains(_editingGradientLayer))
            {
                GradientRollerEngine.RenderOverlay(dc, _editingGradientLayer, _editingGradientPaint, _zoomScale, _activeGradientStopIndex);
            }
        }

        private void RenderVectorEditModeOverlay(DrawingContext dc, VectorGeometry geo)
        {
            if (geo == null || geo.Nodes == null) return;

            double aSize = 7.0 / _zoomScale;
            var anchorPen = new Pen(Brushes.White, 1.5 / _zoomScale);
            anchorPen.Freeze();
            var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.2 / _zoomScale);
            handlePen.Freeze();
            var selGlowPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 56, 189, 248)), 3.0 / _zoomScale);
            selGlowPen.Freeze();

            var normalAnchorBrush = Brushes.White;
            var normalAnchorPen = new Pen(new SolidColorBrush(Color.FromRgb(99, 102, 241)), 1.5 / _zoomScale);
            normalAnchorPen.Freeze();

            var selAnchorBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            selAnchorBrush.Freeze();

            for (int i = 0; i < geo.Nodes.Count; i++)
            {
                var node = geo.Nodes[i];
                bool isSelected = (i == _selectedNodeIndex);

                // Draw handles and tangent lines (if selected or if handles moved)
                if (isSelected || node.Type != BezierNodeType.Corner)
                {
                    if (node.InHandle.X != node.Anchor.X || node.InHandle.Y != node.Anchor.Y)
                    {
                        dc.DrawLine(handlePen, node.Anchor, node.InHandle);
                        dc.DrawEllipse(Brushes.White, handlePen, node.InHandle, aSize * 0.5, aSize * 0.5);
                    }
                    if (node.OutHandle.X != node.Anchor.X || node.OutHandle.Y != node.Anchor.Y)
                    {
                        dc.DrawLine(handlePen, node.Anchor, node.OutHandle);
                        dc.DrawEllipse(Brushes.White, handlePen, node.OutHandle, aSize * 0.5, aSize * 0.5);
                    }
                }

                // Draw anchor node
                if (isSelected)
                {
                    dc.DrawRectangle(null, selGlowPen, new Rect(node.Anchor.X - aSize * 0.75, node.Anchor.Y - aSize * 0.75, aSize * 1.5, aSize * 1.5));
                    dc.DrawRectangle(selAnchorBrush, anchorPen, new Rect(node.Anchor.X - aSize / 2, node.Anchor.Y - aSize / 2, aSize, aSize));
                }
                else
                {
                    dc.DrawRectangle(normalAnchorBrush, normalAnchorPen, new Rect(node.Anchor.X - aSize / 2, node.Anchor.Y - aSize / 2, aSize, aSize));
                }
            }

            // Draw "Vector Edit Mode (Esc / Enter to exit)" badge
            var b = BezierVectorEngine.CalculateBounds(geo);
            if (!b.IsEmpty)
            {
                var typeFace = new Typeface("Segoe UI");
                var badgeText = new FormattedText(
                    "Vector Edit Mode | V: Move • P: Pen • X: Cut • Enter: Done",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeFace,
                    11 / _zoomScale,
                    new SolidColorBrush(Color.FromRgb(56, 189, 248)));
                var badgeBg = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42));
                badgeBg.Freeze();
                var badgeRect = new Rect(b.X + b.Width / 2.0 - badgeText.Width / 2.0 - 6 / _zoomScale, b.Y - 26 / _zoomScale, badgeText.Width + 12 / _zoomScale, badgeText.Height + 6 / _zoomScale);
                dc.DrawRoundedRectangle(badgeBg, new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.0 / _zoomScale), badgeRect, 4 / _zoomScale, 4 / _zoomScale);
                dc.DrawText(badgeText, new Point(b.X + b.Width / 2.0 - badgeText.Width / 2.0, b.Y - 23 / _zoomScale));
            }
        }

        private void RenderBezierNodesOverlay(DrawingContext dc, VectorGeometry geo)
        {
            if (geo == null || geo.Nodes == null) return;

            var anchorBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233));
            var anchorPen = new Pen(Brushes.White, 1.5 / _zoomScale);
            var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(150, 180, 220)), 1.0 / _zoomScale);
            anchorPen.Freeze();
            handlePen.Freeze();

            double aSize = 6.0 / _zoomScale;

            foreach (var node in geo.Nodes)
            {
                // In Handle line
                if (node.InHandle.X != node.Anchor.X || node.InHandle.Y != node.Anchor.Y)
                {
                    dc.DrawLine(handlePen, node.Anchor, node.InHandle);
                    dc.DrawEllipse(Brushes.White, anchorPen, node.InHandle, aSize * 0.6, aSize * 0.6);
                }
                // Out Handle line
                if (node.OutHandle.X != node.Anchor.X || node.OutHandle.Y != node.Anchor.Y)
                {
                    dc.DrawLine(handlePen, node.Anchor, node.OutHandle);
                    dc.DrawEllipse(Brushes.White, anchorPen, node.OutHandle, aSize * 0.6, aSize * 0.6);
                }
                // Anchor node
                dc.DrawRectangle(anchorBrush, anchorPen, new Rect(node.Anchor.X - aSize / 2, node.Anchor.Y - aSize / 2, aSize, aSize));
            }
        }

        private void RenderPenDrawingOverlay(DrawingContext dc)
        {
            if (_activePenGeometry == null || _activePenGeometry.Nodes.Count == 0) return;

            double aSize = 7.5 / _zoomScale;
            var normalAnchorBrush = Brushes.White;
            var normalAnchorPen = new Pen(new SolidColorBrush(Color.FromRgb(99, 102, 241)), 1.5 / _zoomScale);
            normalAnchorPen.Freeze();

            var activeAnchorBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // sky blue
            activeAnchorBrush.Freeze();
            var activeAnchorPen = new Pen(Brushes.White, 1.5 / _zoomScale);
            activeAnchorPen.Freeze();

            var selGlowPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)), 3.5 / _zoomScale);
            selGlowPen.Freeze();

            var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.2 / _zoomScale);
            handlePen.Freeze();

            double hCircleRadius = 3.5 / _zoomScale;

            // 1. Draw all anchor nodes and bezier tangent arms
            for (int i = 0; i < _activePenGeometry.Nodes.Count; i++)
            {
                var node = _activePenGeometry.Nodes[i];
                bool isLastOrActive = (i == _activePenGeometry.Nodes.Count - 1);

                // Draw Bézier tangent handle lines and handle circles
                if (node.InHandle.X != node.Anchor.X || node.InHandle.Y != node.Anchor.Y)
                {
                    dc.DrawLine(handlePen, node.Anchor, node.InHandle);
                    dc.DrawEllipse(Brushes.White, handlePen, node.InHandle, hCircleRadius, hCircleRadius);
                }
                if (node.OutHandle.X != node.Anchor.X || node.OutHandle.Y != node.Anchor.Y)
                {
                    dc.DrawLine(handlePen, node.Anchor, node.OutHandle);
                    dc.DrawEllipse(Brushes.White, handlePen, node.OutHandle, hCircleRadius, hCircleRadius);
                }

                // Draw Anchor Node square
                if (isLastOrActive)
                {
                    dc.DrawRectangle(null, selGlowPen, new Rect(node.Anchor.X - aSize * 0.85, node.Anchor.Y - aSize * 0.85, aSize * 1.7, aSize * 1.7));
                    dc.DrawRectangle(activeAnchorBrush, activeAnchorPen, new Rect(node.Anchor.X - aSize / 2, node.Anchor.Y - aSize / 2, aSize, aSize));
                }
                else
                {
                    dc.DrawRectangle(normalAnchorBrush, normalAnchorPen, new Rect(node.Anchor.X - aSize / 2, node.Anchor.Y - aSize / 2, aSize, aSize));
                }
            }

            // 2. Dynamic elastic rubber-band preview from last node to cursor
            if (!_isDraggingPenCurve && _hasPenHoverPoint && _activePenGeometry.Nodes.Count > 0)
            {
                var last = _activePenGeometry.Nodes[_activePenGeometry.Nodes.Count - 1];
                var first = _activePenGeometry.Nodes[0];
                double closeDist = 12.0 / _zoomScale;
                bool isNearFirst = (_activePenGeometry.Nodes.Count > 1) &&
                                   Math.Abs(_penHoverCanvasPoint.X - first.Anchor.X) <= closeDist &&
                                   Math.Abs(_penHoverCanvasPoint.Y - first.Anchor.Y) <= closeDist;

                Point targetPt = isNearFirst ? first.Anchor : _penHoverCanvasPoint;

                var previewPen = new Pen(new SolidColorBrush(Color.FromArgb(190, 99, 102, 241)), 1.5 / _zoomScale)
                {
                    DashStyle = DashStyles.Dash
                };
                previewPen.Freeze();

                if (last.OutHandle.X != last.Anchor.X || last.OutHandle.Y != last.Anchor.Y)
                {
                    // Curved preview segment
                    var streamGeo = new StreamGeometry();
                    using (var sgc = streamGeo.Open())
                    {
                        sgc.BeginFigure(last.Anchor, false, false);
                        sgc.BezierTo(last.OutHandle, targetPt, targetPt, true, false);
                    }
                    streamGeo.Freeze();
                    dc.DrawGeometry(null, previewPen, streamGeo);
                }
                else
                {
                    dc.DrawLine(previewPen, last.Anchor, targetPt);
                }

                if (isNearFirst)
                {
                    // Loop closure indicator: glowing rose ring around the first anchor
                    var loopPen = new Pen(new SolidColorBrush(Color.FromRgb(244, 63, 94)), 2.0 / _zoomScale);
                    loopPen.Freeze();
                    dc.DrawEllipse(null, loopPen, first.Anchor, 9.0 / _zoomScale, 9.0 / _zoomScale);

                    // Loop badge
                    var typeFace = new Typeface("Segoe UI");
                    var loopText = new FormattedText("Close Path", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeFace, 10 / _zoomScale, Brushes.White);
                    var textBg = new SolidColorBrush(Color.FromArgb(220, 244, 63, 94));
                    textBg.Freeze();
                    var textRect = new Rect(first.Anchor.X + 12 / _zoomScale, first.Anchor.Y - 8 / _zoomScale, loopText.Width + 8 / _zoomScale, loopText.Height + 4 / _zoomScale);
                    dc.DrawRoundedRectangle(textBg, null, textRect, 3 / _zoomScale, 3 / _zoomScale);
                    dc.DrawText(loopText, new Point(first.Anchor.X + 16 / _zoomScale, first.Anchor.Y - 6 / _zoomScale));
                }
                else
                {
                    // Small crosshair target ring at hover point
                    var targetPen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.2 / _zoomScale);
                    targetPen.Freeze();
                    dc.DrawEllipse(null, targetPen, _penHoverCanvasPoint, 3.5 / _zoomScale, 3.5 / _zoomScale);
                }
            }

            // 3. Floating HUD Badge for state & hotkeys
            var b = BezierVectorEngine.CalculateBounds(_activePenGeometry);
            double hudX = _activePenGeometry.Nodes[0].Anchor.X;
            double hudY = (!b.IsEmpty ? b.Y : _activePenGeometry.Nodes[0].Anchor.Y) - 30 / _zoomScale;

            var tf = new Typeface("Segoe UI");
            string statusStr = string.Format("Pen: {0} nodes | Click to add • Drag for curve • Esc/Enter to finish", _activePenGeometry.Nodes.Count);
            var hudText = new FormattedText(
                statusStr,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                tf,
                11 / _zoomScale,
                new SolidColorBrush(Color.FromRgb(224, 231, 255)));

            var hudBg = new SolidColorBrush(Color.FromArgb(230, 15, 23, 42));
            hudBg.Freeze();
            var hudBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(99, 102, 241)), 1.0 / _zoomScale);
            hudBorderPen.Freeze();

            var hudRect = new Rect(hudX - hudText.Width / 2.0 - 8 / _zoomScale, hudY, hudText.Width + 16 / _zoomScale, hudText.Height + 6 / _zoomScale);
            dc.DrawRoundedRectangle(hudBg, hudBorderPen, hudRect, 5 / _zoomScale, 5 / _zoomScale);
            dc.DrawText(hudText, new Point(hudX - hudText.Width / 2.0, hudY + 3 / _zoomScale));
        }

        public void RefreshCanvas()
        {
            if (_canvasHost == null) return;

            if (_renderer != null)
            {
                _renderer.CurrentPage = Document != null ? Document.GetActivePage() : null;
            }

            // Invalidate viewport
            Rect viewport = new Rect(-_panOffset.X / _zoomScale, -_panOffset.Y / _zoomScale,
                                     _canvasHost.ActualWidth / _zoomScale, _canvasHost.ActualHeight / _zoomScale);

            var diag = _sceneGraph.UpdateAndRender(viewport, _zoomScale);
            _telemetryBlock.Text = diag.ToString();

            _canvasHost.InvalidateVisual();
        }

        private void InvalidateAndRefresh(LayerNode node)
        {
            _sceneGraph.InvalidateNode(node.Id);
            RefreshCanvas();
        }

        private void OnSelectionChanged()
        {
            ColorPickerPopup.CloseActive();
            if (_editingGradientLayer != null && !_selectedLayers.Contains(_editingGradientLayer))
            {
                ClearEditingGradient();
            }
            _booleanToolbar.Visibility = (_selectedLayers.Count >= 2) ? Visibility.Visible : Visibility.Collapsed;
            RefreshCanvas();
            RefreshLayersList();
            RefreshPropertyInspector();
            if (_currentStudioMode == StudioMode.Animate)
            {
                RefreshTimelineTracks();
                RefreshTimelineCanvas();
            }
        }

        #endregion

        #region Commands & Editing Actions

        public void Undo()
        {
            History.Undo();
            var page = Document.GetActivePage();
            _sceneGraph.Synchronize(page);
            RefreshCanvas();
            RefreshLayersList();
            RefreshPropertyInspector();
        }

        public void Redo()
        {
            History.Redo();
            var page = Document.GetActivePage();
            _sceneGraph.Synchronize(page);
            RefreshCanvas();
            RefreshLayersList();
            RefreshPropertyInspector();
        }

        public bool SaveCurrentDocument()
        {
            if (string.IsNullOrEmpty(CurrentFilePath))
            {
                return SaveAsCurrentDocument();
            }

            try
            {
                StorageManager.SaveAtomic(Document, CurrentFilePath);
                StorageManager.AddRecentProject(CurrentFilePath);
                History.MarkClean();
                StorageManager.ClearAutosave(CurrentFilePath);
                if (DocumentSaved != null) DocumentSaved(CurrentFilePath);
                MessageBox.Show("Project saved successfully!", "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save project: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public bool SaveAsCurrentDocument()
        {
            if (Document == null) return false;
            var dlg = new SaveFileDialog
            {
                Filter = "Kerf Design Files (*.kerf)|*.kerf|All Files (*.*)|*.*",
                FileName = (string.IsNullOrEmpty(Document.Name) ? "Untitled Project" : Document.Name) + ".kerf",
                Title = "Save Kerf Design As..."
            };

            if (!string.IsNullOrEmpty(CurrentFilePath))
            {
                string dir = System.IO.Path.GetDirectoryName(CurrentFilePath);
                if (System.IO.Directory.Exists(dir)) dlg.InitialDirectory = dir;
            }
            else
            {
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            if (dlg.ShowDialog() == true)
            {
                CurrentFilePath = dlg.FileName;
                Document.Name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                _titleBlock.Text = Document.Name;
                _titleTextBox.Text = Document.Name;
                try
                {
                    StorageManager.SaveAs(Document, CurrentFilePath);
                    History.MarkClean();
                    StorageManager.ClearAutosave(CurrentFilePath);
                    if (DocumentSaved != null) DocumentSaved(CurrentFilePath);
                    MessageBox.Show("Project saved successfully to:\n" + CurrentFilePath, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to save project: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            return false;
        }

        public void OpenProjectFileDialog()
        {
            if (Document != null && History.IsDirty)
            {
                var res = MessageBox.Show(
                    string.Format("Do you want to save changes to \"{0}\" before opening another project?", Document.Name),
                    "Kerf Studio",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    if (!SaveCurrentDocument()) return;
                }
                else if (res == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            var dlg = new OpenFileDialog
            {
                Filter = "Kerf Design Files (*.kerf)|*.kerf|All Files (*.*)|*.*",
                Title = "Open Kerf Design File"
            };

            if (!string.IsNullOrEmpty(CurrentFilePath))
            {
                string dir = System.IO.Path.GetDirectoryName(CurrentFilePath);
                if (System.IO.Directory.Exists(dir)) dlg.InitialDirectory = dir;
            }
            else
            {
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var doc = StorageManager.LoadDocument(dlg.FileName);
                    LoadDocument(doc, dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to open file: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void DeleteSelectedLayers()
        {
            if (_selectedLayers.Count == 0) return;
            var page = Document.GetActivePage();
            if (page == null) return;
            History.Execute(new DeleteLayersCommand(page, _selectedLayers));
            _selectedLayers.Clear();
            _sceneGraph.Synchronize(page);
            OnSelectionChanged();
        }

        public void MoveSelectedLayerZ(bool up, bool toExtremes = false)
        {
            if (_selectedLayers.Count == 0) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            var layer = _selectedLayers[0];
            LayerNode parent;
            var container = page.FindContainer(layer, out parent);
            if (container == null) return;

            int idx = container.IndexOf(layer);
            if (idx < 0) return;

            int targetIdx;
            if (toExtremes)
            {
                targetIdx = up ? container.Count - 1 : 0;
            }
            else
            {
                targetIdx = up ? idx + 1 : idx - 1;
            }

            if (targetIdx >= 0 && targetIdx < container.Count && targetIdx != idx)
            {
                container.RemoveAt(idx);
                container.Insert(targetIdx, layer);
                _sceneGraph.Synchronize(page);
                RefreshCanvas();
                RefreshLayersList();
                History.MarkDirty();
            }
        }

        public void DuplicateSelectedLayers()
        {
            if (_selectedLayers.Count == 0) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            // Adobe Illustrator "Transform Again" logic:
            // Use the last drag/duplicate delta (dx, dy) if available, or default (24, 24)
            double offX = (_lastTransformDelta.X != 0 || _lastTransformDelta.Y != 0) ? _lastTransformDelta.X : 24.0;
            double offY = (_lastTransformDelta.X != 0 || _lastTransformDelta.Y != 0) ? _lastTransformDelta.Y : 24.0;

            var duplicates = new List<LayerNode>();
            foreach (var orig in _selectedLayers.ToArray())
            {
                var clone = orig.Clone();
                clone.Name = orig.Name;
                clone.X = orig.X + offX;
                clone.Y = orig.Y + offY;
                if (clone.Type == ElementType.Path && clone.Geometry != null)
                {
                    clone.Geometry = orig.Geometry.Clone();
                    BezierVectorEngine.OffsetGeometry(clone.Geometry, offX, offY);
                }

                LayerNode parent;
                var container = page.FindContainer(orig, out parent);
                if (container != null)
                {
                    int idx = container.IndexOf(orig);
                    clone.ParentId = parent != null ? parent.Id : null;
                    if (idx >= 0 && idx < container.Count)
                    {
                        container.Insert(idx + 1, clone);
                    }
                    else
                    {
                        container.Add(clone);
                    }
                }
                else
                {
                    clone.ParentId = null;
                    page.Layers.Add(clone);
                }
                duplicates.Add(clone);
            }

            _selectedLayers.Clear();
            _selectedLayers.AddRange(duplicates);
            History.Execute(new AddLayersCommand(page, duplicates));
            _sceneGraph.Synchronize(page);
            OnSelectionChanged();
            RefreshCanvas();
            RefreshLayersList();
            History.MarkDirty();
        }

        public void GroupSelectedLayers()
        {
            if (_selectedLayers.Count == 0) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            var bounds = TransformEngine.UnionBounds(_selectedLayers);
            var group = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = "Group " + (_layerGroupCounter++),
                Type = ElementType.Group,
                X = bounds.X,
                Y = bounds.Y,
                Width = Math.Max(1, bounds.Width),
                Height = Math.Max(1, bounds.Height),
                Children = new List<LayerNode>()
            };

            var first = _selectedLayers[0];
            LayerNode targetParent;
            var container = page.FindContainer(first, out targetParent);
            if (container == null) container = page.Layers;
            int insertIdx = container.IndexOf(first);
            if (insertIdx < 0) insertIdx = container.Count;

            foreach (var layer in _selectedLayers.ToArray())
            {
                page.RemoveLayerEverywhere(layer);
                layer.ParentId = group.Id;
                layer.X = layer.X - bounds.X;
                layer.Y = layer.Y - bounds.Y;
                group.Children.Add(layer);
            }

            group.ParentId = targetParent != null ? targetParent.Id : null;
            if (insertIdx <= container.Count)
            {
                container.Insert(insertIdx, group);
            }
            else
            {
                container.Add(group);
            }

            _selectedLayers.Clear();
            _selectedLayers.Add(group);
            _sceneGraph.Synchronize(page);
            OnSelectionChanged();
            RefreshCanvas();
            RefreshLayersList();
            History.MarkDirty();
        }

        public void UngroupSelectedLayers()
        {
            if (_selectedLayers.Count == 0) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            var newSelection = new List<LayerNode>();
            foreach (var layer in _selectedLayers.ToArray())
            {
                if ((layer.Type == ElementType.Group || layer.Type == ElementType.Frame) && layer.Children != null && layer.Children.Count > 0)
                {
                    LayerNode parent;
                    var container = page.FindContainer(layer, out parent);
                    if (container == null) container = page.Layers;
                    int idx = container.IndexOf(layer);
                    if (idx < 0) idx = container.Count;

                    container.Remove(layer);

                    var children = layer.Children.ToArray();
                    for (int i = 0; i < children.Length; i++)
                    {
                        var child = children[i];
                        child.ParentId = parent != null ? parent.Id : null;
                        child.X += layer.X;
                        child.Y += layer.Y;
                        container.Insert(Math.Min(idx + i, container.Count), child);
                        newSelection.Add(child);
                    }
                }
                else
                {
                    newSelection.Add(layer);
                }
            }

            if (newSelection.Count > 0)
            {
                _selectedLayers.Clear();
                _selectedLayers.AddRange(newSelection);
                _sceneGraph.Synchronize(page);
                OnSelectionChanged();
                RefreshCanvas();
                RefreshLayersList();
                History.MarkDirty();
            }
        }

        public void MoveLayerHierarchy(LayerNode source, LayerNode target, DropPosition position)
        {
            if (source == null || target == null || source == target || (source.Id != null && source.Id == target.Id)) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            // Prevent cyclic hierarchy
            if (Kerf.Model.Page.IsDescendant(source, target)) return;

            LayerNode oldParent;
            var oldContainer = page.FindContainer(source, out oldParent);
            if (oldContainer == null) oldContainer = page.Layers;
            int oldIdx = oldContainer.IndexOf(source);
            string oldParentId = oldParent != null ? oldParent.Id : null;

            if (position == DropPosition.Inside && (target.Type == ElementType.Frame || target.Type == ElementType.Group))
            {
                string newParentId = target.Id;
                int newIdx = (target.Children != null) ? target.Children.Count : 0;
                var cmd = new ReorderLayerCommand(page, source, oldParentId, oldIdx, newParentId, newIdx, () =>
                {
                    _sceneGraph.Synchronize(page);
                    RefreshCanvas();
                    RefreshLayersList();
                });
                History.Execute(cmd);
                target.IsCollapsed = false;
            }
            else
            {
                LayerNode targetParent;
                var targetContainer = page.FindContainer(target, out targetParent);
                if (targetContainer == null) targetContainer = page.Layers;
                int targetIdx = targetContainer.IndexOf(target);
                string targetParentId = targetParent != null ? targetParent.Id : null;

                int newIdx;
                bool isVisuallyAbove = (position == DropPosition.Before || position == DropPosition.Above);

                if (isVisuallyAbove)
                {
                    // In UI, dropping on top half means visually ABOVE target in list, which is HIGHER z-index (after target in container).
                    if (oldContainer == targetContainer && oldIdx >= 0)
                    {
                        newIdx = (oldIdx < targetIdx) ? targetIdx : targetIdx + 1;
                    }
                    else
                    {
                        newIdx = targetIdx + 1;
                    }
                }
                else
                {
                    // In UI, dropping on bottom half means visually BELOW target in list, which is LOWER z-index (before target in container).
                    if (oldContainer == targetContainer && oldIdx >= 0)
                    {
                        newIdx = (oldIdx < targetIdx) ? Math.Max(0, targetIdx - 1) : targetIdx;
                    }
                    else
                    {
                        newIdx = targetIdx;
                    }
                }

                var cmd = new ReorderLayerCommand(page, source, oldParentId, oldIdx, targetParentId, newIdx, () =>
                {
                    _sceneGraph.Synchronize(page);
                    RefreshCanvas();
                    RefreshLayersList();
                });
                History.Execute(cmd);
            }

            _sceneGraph.Synchronize(page);
            RefreshCanvas();
            RefreshLayersList();
            History.MarkDirty();
        }

        public void FrameSelectedLayers()
        {
            if (_selectedLayers.Count == 0 || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            var bounds = TransformEngine.UnionBounds(_selectedLayers);
            var first = _selectedLayers[0];
            LayerNode parent;
            var container = page.FindContainer(first, out parent);
            if (container == null) container = page.Layers;
            int insertIdx = container.IndexOf(first);
            if (insertIdx < 0) insertIdx = container.Count;
            string parentId = parent != null ? parent.Id : null;

            int nextNum = page.GetAllLayersFlat().Count(l => l.Type == ElementType.Frame) + 1;

            var frame = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = "Frame " + nextNum,
                Type = ElementType.Frame,
                X = bounds.X,
                Y = bounds.Y,
                Width = Math.Max(20, bounds.Width),
                Height = Math.Max(20, bounds.Height),
                ClipContent = false,
                Fills = new List<Paint>(),
                Children = new List<LayerNode>()
            };

            var toFrame = _selectedLayers.ToArray();
            var cmd = new FrameSelectionCommand(page, frame, toFrame, parentId, insertIdx, () =>
            {
                _sceneGraph.Synchronize(page);
                RefreshCanvas();
                RefreshLayersList();
            });
            History.Execute(cmd);

            _selectedLayers.Clear();
            _selectedLayers.Add(frame);
            _sceneGraph.Synchronize(page);
            OnSelectionChanged();
            RefreshCanvas();
            RefreshLayersList();
        }

        public void CreateComponentFromSelection()
        {
            if (_selectedLayers.Count == 0 || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            if (_selectedLayers.Count > 1)
            {
                FrameSelectedLayers();
            }

            if (_selectedLayers.Count == 1)
            {
                var node = _selectedLayers[0];
                node.IsMasterComponent = true;
                node.ComponentId = Guid.NewGuid().ToString().Substring(0, 8);
                if (!node.Name.StartsWith("Component") && node.Name.StartsWith("Frame"))
                {
                    node.Name = "Component " + node.Name.Substring(5).Trim();
                }
                else if (!node.Name.Contains("Component"))
                {
                    node.Name = node.Name + " (Component)";
                }
                History.MarkDirty();
                RefreshLayersList();
                RefreshPropertyInspector();
            }
        }

        public void CreateComponentInstance(LayerNode master = null)
        {
            if (master == null)
            {
                if (_selectedLayers.Count == 0) return;
                master = _selectedLayers[0];
            }

            var page = Document.GetActivePage();
            if (page == null) return;

            if (!master.IsMasterComponent)
            {
                if (!string.IsNullOrEmpty(master.ComponentId))
                {
                    master = page.FindMasterComponent(master.ComponentId) ?? master;
                }
                else
                {
                    CreateComponentFromSelection();
                    master = _selectedLayers[0];
                }
            }

            var instance = master.Clone();
            instance.Id = Guid.NewGuid().ToString().Substring(0, 8);
            instance.Type = ElementType.ComponentInstance;
            instance.IsMasterComponent = false;
            instance.ComponentId = master.ComponentId;
            instance.Name = master.Name;
            instance.X = master.X + 40;
            instance.Y = master.Y + 40;
            if (instance.Type == ElementType.Path && instance.Geometry != null)
            {
                BezierVectorEngine.OffsetGeometry(instance.Geometry, 40, 40);
            }
            instance.ParentId = null;

            page.Layers.Add(instance);
            History.Execute(new AddLayerCommand(page, instance));

            _selectedLayers.Clear();
            _selectedLayers.Add(instance);
            _sceneGraph.Synchronize(page);
            OnSelectionChanged();
            RefreshCanvas();
            RefreshLayersList();
        }

        public void DetachComponentInstance(LayerNode instance)
        {
            if (instance == null) return;
            instance.ComponentId = null;
            instance.IsMasterComponent = false;
            instance.Type = (instance.Children != null && instance.Children.Count > 0) ? ElementType.Frame : ElementType.Rectangle;
            History.MarkDirty();
            RefreshLayersList();
            RefreshPropertyInspector();
        }

        public void ResetComponentOverrides(LayerNode instance)
        {
            if (instance == null || string.IsNullOrEmpty(instance.ComponentId) || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            var master = page.FindMasterComponent(instance.ComponentId);
            if (master == null) return;

            double curX = instance.X;
            double curY = instance.Y;
            var clone = master.Clone();

            instance.Width = clone.Width;
            instance.Height = clone.Height;
            instance.CornerRadius = clone.CornerRadius;
            instance.Opacity = clone.Opacity;
            instance.Fills = clone.Fills;
            instance.Strokes = clone.Strokes;
            instance.Effects = clone.Effects;
            instance.Typography = clone.Typography;
            instance.AutoLayout = clone.AutoLayout;
            instance.Children = clone.Children;
            instance.X = curX;
            instance.Y = curY;

            _sceneGraph.InvalidateNode(instance.Id);
            History.MarkDirty();
            RefreshCanvas();
            RefreshLayersList();
            RefreshPropertyInspector();
        }

        public void GoToMasterComponent(string componentId)
        {
            if (string.IsNullOrEmpty(componentId) || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            var master = page.FindMasterComponent(componentId);
            if (master != null)
            {
                _selectedLayers.Clear();
                _selectedLayers.Add(master);
                OnSelectionChanged();
                ZoomToSelection();
                RefreshCanvas();
                RefreshLayersList();
            }
        }

        public void AutoAlignSelectedLayers()
        {
            if (_selectedLayers.Count == 0 || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            var cmd = AutoLayoutEngine.CreateAutoLayoutCommand(page, _selectedLayers, () =>
            {
                _sceneGraph.Synchronize(page);
                RefreshCanvas();
                RefreshLayersList();
                RefreshPropertyInspector();
            });

            if (cmd != null)
            {
                History.Execute(cmd);
                _sceneGraph.Synchronize(page);
                RefreshCanvas();
                RefreshLayersList();
                RefreshPropertyInspector();
            }
        }

        public void CopySelectedLayers()
        {
            if (_selectedLayers.Count == 0) return;
            _clipboardLayers.Clear();
            foreach (var l in _selectedLayers)
            {
                _clipboardLayers.Add(l.Clone());
            }
        }

        public void CutSelectedLayers()
        {
            CopySelectedLayers();
            DeleteSelectedLayers();
        }

        public void PasteLayers()
        {
            if (_clipboardLayers.Count == 0 || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            var pasted = new List<LayerNode>();
            foreach (var orig in _clipboardLayers)
            {
                var clone = orig.Clone();
                clone.X += 24;
                clone.Y += 24;
                if (clone.Type == ElementType.Path && clone.Geometry != null)
                {
                    BezierVectorEngine.OffsetGeometry(clone.Geometry, 24, 24);
                }
                clone.ParentId = null;
                page.Layers.Add(clone);
                pasted.Add(clone);
            }

            _selectedLayers.Clear();
            _selectedLayers.AddRange(pasted);
            History.Execute(new AddLayersCommand(page, pasted));
            _sceneGraph.Synchronize(page);
            OnSelectionChanged();
            RefreshCanvas();
            RefreshLayersList();
        }

        public void PasteLayersHere(Point canvasPt)
        {
            if (_clipboardLayers.Count == 0 || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;

            var bounds = TransformEngine.UnionBounds(_clipboardLayers);
            double offsetX = canvasPt.X - bounds.X;
            double offsetY = canvasPt.Y - bounds.Y;

            var pasted = new List<LayerNode>();
            foreach (var orig in _clipboardLayers)
            {
                var clone = orig.Clone();
                clone.X += offsetX;
                clone.Y += offsetY;
                if (clone.Type == ElementType.Path && clone.Geometry != null)
                {
                    BezierVectorEngine.OffsetGeometry(clone.Geometry, offsetX, offsetY);
                }
                clone.ParentId = null;
                page.Layers.Add(clone);
                pasted.Add(clone);
            }

            _selectedLayers.Clear();
            _selectedLayers.AddRange(pasted);
            History.Execute(new AddLayersCommand(page, pasted));
            _sceneGraph.Synchronize(page);
            OnSelectionChanged();
            RefreshCanvas();
            RefreshLayersList();
        }

        public void ZoomTo100()
        {
            if (_canvasHost == null) return;
            Point center = new Point(_canvasHost.ActualWidth / 2.0, _canvasHost.ActualHeight / 2.0);
            double oldZoom = _zoomScale;
            _zoomScale = 1.0;
            _panOffset.X = center.X - (center.X - _panOffset.X) * (1.0 / oldZoom);
            _panOffset.Y = center.Y - (center.Y - _panOffset.Y) * (1.0 / oldZoom);
            if (_zoomPercentBlock != null) _zoomPercentBlock.Text = "100%";
            RefreshCanvas();
        }

        public void ZoomToFit()
        {
            if (Document == null || _canvasHost == null) return;
            var page = Document.GetActivePage();
            if (page == null || page.Layers == null || page.Layers.Count == 0) return;

            var all = page.GetAllLayersFlat().ToList();
            if (all.Count == 0) return;
            var bounds = TransformEngine.UnionBounds(all);
            ZoomToBounds(bounds);
        }

        public void ZoomToSelection()
        {
            if (_selectedLayers == null || _selectedLayers.Count == 0)
            {
                ZoomToFit();
                return;
            }
            var bounds = TransformEngine.UnionBounds(_selectedLayers);
            ZoomToBounds(bounds);
        }

        private void ZoomToBounds(Rect bounds)
        {
            if (_canvasHost == null || bounds.Width <= 0 || bounds.Height <= 0) return;
            double availW = Math.Max(100, _canvasHost.ActualWidth - 120);
            double availH = Math.Max(100, _canvasHost.ActualHeight - 120);

            double scaleX = availW / bounds.Width;
            double scaleY = availH / bounds.Height;
            double fitZoom = Math.Max(0.1, Math.Min(4.0, Math.Min(scaleX, scaleY)));

            _zoomScale = fitZoom;
            _panOffset.X = (_canvasHost.ActualWidth / 2.0) - (bounds.X + bounds.Width / 2.0) * fitZoom;
            _panOffset.Y = (_canvasHost.ActualHeight / 2.0) - (bounds.Y + bounds.Height / 2.0) * fitZoom;

            if (_zoomPercentBlock != null) _zoomPercentBlock.Text = string.Format("{0:P0}", _zoomScale);
            RefreshCanvas();
        }

        public void ZoomIn()
        {
            if (_canvasHost == null) return;
            HandleCanvasMouseWheel(new Point(_canvasHost.ActualWidth / 2.0, _canvasHost.ActualHeight / 2.0), 120);
        }

        public void ZoomOut()
        {
            if (_canvasHost == null) return;
            HandleCanvasMouseWheel(new Point(_canvasHost.ActualWidth / 2.0, _canvasHost.ActualHeight / 2.0), -120);
        }

        public void FlipSelectedLayers(bool horizontal)
        {
            if (_selectedLayers.Count == 0) return;
            foreach (var l in _selectedLayers)
            {
                if (horizontal) l.FlipH = !l.FlipH;
                else l.FlipV = !l.FlipV;

                if (l.Type == ElementType.Path && l.Geometry != null)
                {
                    var bounds = BezierVectorEngine.CalculateBounds(l.Geometry);
                    double midX = bounds.X + bounds.Width / 2.0;
                    double midY = bounds.Y + bounds.Height / 2.0;
                    foreach (var node in l.Geometry.Nodes)
                    {
                        if (horizontal)
                        {
                            node.Anchor = new Point(2 * midX - node.Anchor.X, node.Anchor.Y);
                            node.InHandle = new Point(2 * midX - node.InHandle.X, node.InHandle.Y);
                            node.OutHandle = new Point(2 * midX - node.OutHandle.X, node.OutHandle.Y);
                        }
                        else
                        {
                            node.Anchor = new Point(node.Anchor.X, 2 * midY - node.Anchor.Y);
                            node.InHandle = new Point(node.InHandle.X, 2 * midY - node.InHandle.Y);
                            node.OutHandle = new Point(node.OutHandle.X, 2 * midY - node.OutHandle.Y);
                        }
                    }
                }
                _sceneGraph.InvalidateNode(l.Id);
            }
            History.MarkDirty();
            RefreshCanvas();
            RefreshPropertyInspector();
        }

        public void ToggleLockSelectedLayers()
        {
            if (_selectedLayers.Count == 0) return;
            bool anyUnlocked = _selectedLayers.Any(l => !l.IsLocked);
            foreach (var l in _selectedLayers)
            {
                l.IsLocked = anyUnlocked;
            }
            History.MarkDirty();
            RefreshLayersList();
        }

        public void ToggleVisibilitySelectedLayers()
        {
            if (_selectedLayers.Count == 0) return;
            bool anyVisible = _selectedLayers.Any(l => l.IsVisible);
            foreach (var l in _selectedLayers)
            {
                l.IsVisible = !anyVisible;
                InvalidateAndRefresh(l);
            }
            History.MarkDirty();
            RefreshLayersList();
        }

        public void SelectAllLayers()
        {
            if (Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;
            _selectedLayers.Clear();
            _selectedLayers.AddRange(page.GetAllLayersFlat());
            OnSelectionChanged();
            RefreshCanvas();
        }

        public void StartRename(string layerId)
        {
            _renamingLayerId = layerId;
            RefreshLayersList();
        }

        public void FinishRename(string layerId, string newName)
        {
            if (_renamingLayerId != layerId) return;
            _renamingLayerId = null;

            if (!string.IsNullOrWhiteSpace(newName))
            {
                var page = Document.GetActivePage();
                var layer = page != null ? page.FindLayerById(layerId) : null;
                if (layer != null && layer.Name != newName.Trim())
                {
                    layer.Name = newName.Trim();
                    History.MarkDirty();
                }
            }
            RefreshLayersList();
        }

        public void NudgeSelectedLayers(double dx, double dy)
        {
            if (_selectedLayers.Count == 0) return;
            var beforePos = new Dictionary<LayerNode, Point>();
            var beforeGeos = new Dictionary<LayerNode, VectorGeometry>();
            var afterPos = new Dictionary<LayerNode, Point>();
            var afterGeos = new Dictionary<LayerNode, VectorGeometry>();

            foreach (var layer in _selectedLayers)
            {
                beforePos[layer] = new Point(layer.X, layer.Y);
                if (layer.Type == ElementType.Path && layer.Geometry != null)
                {
                    beforeGeos[layer] = layer.Geometry.Clone();
                }

                layer.X += dx;
                layer.Y += dy;

                afterPos[layer] = new Point(layer.X, layer.Y);
                if (layer.Type == ElementType.Path && layer.Geometry != null)
                {
                    BezierVectorEngine.OffsetGeometry(layer.Geometry, dx, dy);
                    afterGeos[layer] = layer.Geometry.Clone();
                }
            }

            var cmd = new MoveLayersPositionsCommand(beforePos, afterPos, beforeGeos, afterGeos, () =>
            {
                foreach (var l in _selectedLayers) _sceneGraph.InvalidateNode(l.Id);
                RefreshPropertyInspector();
                RefreshCanvas();
            });
            History.Execute(cmd);
        }

        public void ApplyBoolean(BooleanOp op)
        {
            if (_selectedLayers.Count < 2) return;
            var combined = BooleanGeometryEngine.Combine(_selectedLayers, op, _renderer);
            if (combined != null)
            {
                var page = Document.GetActivePage();
                var toDelete = new List<LayerNode>(_selectedLayers);
                page.Layers.Add(combined);
                foreach (var del in toDelete) page.Layers.Remove(del);

                _selectedLayers.Clear();
                _selectedLayers.Add(combined);
                _sceneGraph.Synchronize(page);
                OnSelectionChanged();
                History.MarkDirty();
            }
        }

        private void SetZoom(double zoom)
        {
            _zoomScale = Math.Max(0.05, Math.Min(32.0, zoom));
            _zoomPercentBlock.Text = string.Format("{0:P0}", _zoomScale);
            RefreshCanvas();
        }

        private void ResetZoom()
        {
            _zoomScale = 1.0;
            _panOffset = new Point(100, 80);
            _zoomPercentBlock.Text = "100%";
            RefreshCanvas();
        }

        private void ShowExportDialog()
        {
            var page = Document.GetActivePage();
            if (page == null) return;

            string defaultName = Document.Name;
            if (_selectedLayers != null && _selectedLayers.Count == 1 && !string.IsNullOrEmpty(_selectedLayers[0].Name))
            {
                defaultName = _selectedLayers[0].Name;
            }
            else if (_selectedLayers != null && _selectedLayers.Count > 1)
            {
                defaultName = _selectedLayers[0].Name + "_selection";
            }

            var dlg = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|Vector SVG (*.svg)|*.svg|JPEG Image (*.jpg)|*.jpg",
                FileName = defaultName,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dlg.ShowDialog() == true)
            {
                string ext = IOPath.GetExtension(dlg.FileName).ToLowerInvariant();
                try
                {
                    var targets = (_selectedLayers != null && _selectedLayers.Count > 0)
                        ? (IEnumerable<LayerNode>)_selectedLayers
                        : null;

                    if (ext == ".svg")
                    {
                        string svg = SvgExporter.ExportPage(page);
                        File.WriteAllText(dlg.FileName, svg, System.Text.Encoding.UTF8);
                    }
                    else if (ext == ".jpg" || ext == ".jpeg")
                    {
                        HighQualityImageRenderer.ExportToFile(page, targets, dlg.FileName, RasterFormat.Jpeg, 2.0);
                    }
                    else
                    {
                        HighQualityImageRenderer.ExportToFile(page, targets, dlg.FileName, RasterFormat.Png, 2.0);
                    }

                    MessageBox.Show("Exported successfully to: " + dlg.FileName, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export failed: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnStudioKeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox)
            {
                if (e.Key == Key.Escape)
                {
                    Keyboard.ClearFocus();
                    e.Handled = true;
                }
                return;
            }

            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt || Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

            if (e.Key == Key.Space && !(e.OriginalSource is TextBox) && _currentStudioMode == StudioMode.Animate)
            {
                ToggleAnimationPlayback();
                e.Handled = true;
                return;
            }

            // 1. Delete / Backspace instant layer deletion
            if (!isCtrl && !isAlt && (e.Key == Key.Delete || e.Key == Key.Back))
            {
                if (_activeTool == ToolType.Pen && _activePenGeometry != null && _activePenGeometry.Nodes.Count > 0)
                {
                    UndoLastPenNode();
                    e.Handled = true;
                    return;
                }
                if (_isVectorEditing && _editingVectorNode != null && _editingVectorNode.Geometry != null)
                {
                    if (_selectedNodeIndex >= 0 && _selectedNodeIndex < _editingVectorNode.Geometry.Nodes.Count)
                    {
                        BezierVectorEngine.RemoveNode(_editingVectorNode.Geometry, _selectedNodeIndex);
                        if (_editingVectorNode.Geometry.Nodes.Count == 0)
                        {
                            DeleteSelectedLayers();
                            ExitVectorEditMode();
                        }
                        else
                        {
                            _selectedNodeIndex = Math.Min(_selectedNodeIndex, _editingVectorNode.Geometry.Nodes.Count - 1);
                            InvalidateAndRefresh(_editingVectorNode);
                            RefreshPropertyInspector();
                        }
                    }
                    e.Handled = true;
                    return;
                }
                DeleteSelectedLayers();
                e.Handled = true;
                return;
            }

            if (isCtrl && !isShift && !isAlt)
            {
                if (e.Key == Key.Z) { Undo(); e.Handled = true; }
                else if (e.Key == Key.Y) { Redo(); e.Handled = true; }
                else if (e.Key == Key.S) { SaveCurrentDocument(); e.Handled = true; }
                else if (e.Key == Key.O) { OpenProjectFileDialog(); e.Handled = true; }
                else if (e.Key == Key.D) { DuplicateSelectedLayers(); e.Handled = true; }
                else if (e.Key == Key.E) { ShowExportDialog(); e.Handled = true; }
                else if (e.Key == Key.G) { GroupSelectedLayers(); e.Handled = true; }
                else if (e.Key == Key.C) { CopySelectedLayers(); e.Handled = true; }
                else if (e.Key == Key.X) { CutSelectedLayers(); e.Handled = true; }
                else if (e.Key == Key.V) { PasteLayers(); e.Handled = true; }
                else if (e.Key == Key.A) { SelectAllLayers(); e.Handled = true; }
                else if (e.Key == Key.R)
                {
                    if (_selectedLayers.Count == 1) { StartRename(_selectedLayers[0].Id); e.Handled = true; }
                }
                else if (e.Key == Key.OemCloseBrackets) { MoveSelectedLayerZ(true, false); e.Handled = true; }
                else if (e.Key == Key.OemOpenBrackets) { MoveSelectedLayerZ(false, false); e.Handled = true; }
                else if (e.Key == Key.OemPlus || e.Key == Key.Add) { ZoomIn(); e.Handled = true; }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) { ZoomOut(); e.Handled = true; }
            }
            else if (isCtrl && isShift && !isAlt)
            {
                if (e.Key == Key.Z) { Redo(); e.Handled = true; }
                else if (e.Key == Key.S) { SaveAsCurrentDocument(); e.Handled = true; }
                else if (e.Key == Key.E) { ShowExportDialog(); e.Handled = true; }
                else if (e.Key == Key.G) { UngroupSelectedLayers(); e.Handled = true; }
                else if (e.Key == Key.V) { PasteLayersHere(_lastRightClickCanvasPoint); e.Handled = true; }
                else if (e.Key == Key.OemCloseBrackets) { MoveSelectedLayerZ(true, true); e.Handled = true; }
                else if (e.Key == Key.OemOpenBrackets) { MoveSelectedLayerZ(false, true); e.Handled = true; }
                else if (e.Key == Key.H) { ToggleVisibilitySelectedLayers(); e.Handled = true; }
                else if (e.Key == Key.L) { ToggleLockSelectedLayers(); e.Handled = true; }
            }
            else if (isCtrl && isAlt && !isShift)
            {
                if (e.Key == Key.G) { FrameSelectedLayers(); e.Handled = true; } // Figma Frame Selection
                else if (e.Key == Key.K) { CreateComponentFromSelection(); e.Handled = true; } // Figma Create Component
                else if (e.Key == Key.B) { DetachComponentInstance(_selectedLayers.FirstOrDefault()); e.Handled = true; } // Figma Detach Instance
            }
            else if (!isCtrl && isShift && !isAlt)
            {
                if (e.Key == Key.A) { AutoAlignSelectedLayers(); e.Handled = true; } // Shift + A Auto align / Auto layout
                else if (e.Key == Key.D0) { ZoomTo100(); e.Handled = true; } // Shift + 0 = 100%
                else if (e.Key == Key.D1) { ZoomToFit(); e.Handled = true; } // Shift + 1 = Zoom to Fit
                else if (e.Key == Key.D2) { ZoomToSelection(); e.Handled = true; } // Shift + 2 = Zoom to Selection
                else if (e.Key == Key.H) { FlipSelectedLayers(true); e.Handled = true; } // Shift + H = Flip horizontal
                else if (e.Key == Key.V) { FlipSelectedLayers(false); e.Handled = true; } // Shift + V = Flip vertical
                else if (e.Key == Key.L) { SetActiveTool(ToolType.Arrow); e.Handled = true; }
            }
            else if (!isCtrl && isAlt && !isShift)
            {
                Key k = (e.Key == Key.System) ? e.SystemKey : e.Key;
                if (k == Key.A) { AlignSelected(0); e.Handled = true; }
                else if (k == Key.H) { AlignSelected(1); e.Handled = true; }
                else if (k == Key.D) { AlignSelected(2); e.Handled = true; }
                else if (k == Key.W) { AlignSelected(3); e.Handled = true; }
                else if (k == Key.V) { AlignSelected(4); e.Handled = true; }
                else if (k == Key.S) { AlignSelected(5); e.Handled = true; }
                else if (k == Key.Left || k == Key.Right || k == Key.Up || k == Key.Down)
                {
                    // Adobe Illustrator Alt+Arrow instant duplicate & nudge
                    double nudgeDist = 1.0;
                    if (k == Key.Left) { _lastTransformDelta = new Point(-nudgeDist, 0); DuplicateSelectedLayers(); e.Handled = true; }
                    else if (k == Key.Right) { _lastTransformDelta = new Point(nudgeDist, 0); DuplicateSelectedLayers(); e.Handled = true; }
                    else if (k == Key.Up) { _lastTransformDelta = new Point(0, -nudgeDist); DuplicateSelectedLayers(); e.Handled = true; }
                    else if (k == Key.Down) { _lastTransformDelta = new Point(0, nudgeDist); DuplicateSelectedLayers(); e.Handled = true; }
                }
            }
            else if (!isCtrl && !isAlt)
            {
                if (e.Key == Key.Enter)
                {
                    if (_isVectorEditing)
                    {
                        ExitVectorEditMode();
                        e.Handled = true;
                        return;
                    }
                    if (_activePenNode != null)
                    {
                        FinishPenDrawing();
                        e.Handled = true;
                        return;
                    }
                    if (_selectedLayers.Count == 1 && _selectedLayers[0].Type == ElementType.Path)
                    {
                        EnterVectorEditMode(_selectedLayers[0]);
                        e.Handled = true;
                        return;
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    if (_editingGradientPaint != null)
                    {
                        ClearEditingGradient();
                        RefreshPropertyInspector();
                        e.Handled = true;
                        return;
                    }
                    if (_isVectorEditing)
                    {
                        ExitVectorEditMode();
                        e.Handled = true;
                        return;
                    }
                    if (_activePenNode != null)
                    {
                        FinishPenDrawing();
                        e.Handled = true;
                        return;
                    }
                    if (_selectedLayers.Count > 0)
                    {
                        _selectedLayers.Clear();
                        OnSelectionChanged();
                        RefreshCanvas();
                        e.Handled = true;
                        return;
                    }
                }
                else if (e.Key == Key.Delete || e.Key == Key.Back)
                {
                    if (_activeTool == ToolType.Pen && _activePenGeometry != null && _activePenGeometry.Nodes.Count > 0)
                    {
                        UndoLastPenNode();
                        e.Handled = true;
                        return;
                    }
                    if (_isVectorEditing && _editingVectorNode != null && _editingVectorNode.Geometry != null)
                    {
                        if (_selectedNodeIndex >= 0 && _selectedNodeIndex < _editingVectorNode.Geometry.Nodes.Count)
                        {
                            BezierVectorEngine.RemoveNode(_editingVectorNode.Geometry, _selectedNodeIndex);
                            if (_editingVectorNode.Geometry.Nodes.Count == 0)
                            {
                                DeleteSelectedLayers();
                                ExitVectorEditMode();
                            }
                            else
                            {
                                _selectedNodeIndex = Math.Min(_selectedNodeIndex, _editingVectorNode.Geometry.Nodes.Count - 1);
                                InvalidateAndRefresh(_editingVectorNode);
                                RefreshPropertyInspector();
                            }
                        }
                        e.Handled = true;
                        return;
                    }
                    DeleteSelectedLayers();
                    e.Handled = true;
                }
                else
                {
                    double nudgeDist = isShift ? 10.0 : 1.0;
                    if (e.Key == Key.Left) { NudgeSelectedLayers(-nudgeDist, 0); e.Handled = true; }
                    else if (e.Key == Key.Right) { NudgeSelectedLayers(nudgeDist, 0); e.Handled = true; }
                    else if (e.Key == Key.Up) { NudgeSelectedLayers(0, -nudgeDist); e.Handled = true; }
                    else if (e.Key == Key.Down) { NudgeSelectedLayers(0, nudgeDist); e.Handled = true; }
                    else if (e.Key == Key.F2)
                    {
                        if (_selectedLayers.Count == 1)
                        {
                            StartRename(_selectedLayers[0].Id);
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == Key.V) { SetActiveTool(ToolType.Select); }
                    else if (e.Key == Key.M) { SetActiveTool(ToolType.Marquee); }
                    else if (e.Key == Key.H) { SetActiveTool(ToolType.Hand); }
                    else if (e.Key == Key.R) { SetActiveTool(ToolType.Rect); }
                    else if (e.Key == Key.O) { SetActiveTool(ToolType.Ellipse); }
                    else if (e.Key == Key.F) { SetActiveTool(ToolType.Frame); }
                    else if (e.Key == Key.P || e.Key == Key.B) { SetActiveTool(ToolType.Pen); }
                    else if (e.Key == Key.X && _isVectorEditing && _editingVectorNode != null && _editingVectorNode.Geometry != null)
                    {
                        if (_selectedNodeIndex >= 0 && _selectedNodeIndex < _editingVectorNode.Geometry.Nodes.Count)
                        {
                            BezierVectorEngine.SplitPathAtNode(_editingVectorNode.Geometry, _selectedNodeIndex);
                            InvalidateAndRefresh(_editingVectorNode);
                            RefreshPropertyInspector();
                            History.MarkDirty();
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == Key.T) { SetActiveTool(ToolType.Text); }
                    else if (e.Key == Key.L) { SetActiveTool(ToolType.Line); }
                    else if (e.Key == Key.Z) { ZoomIn(); e.Handled = true; }
                    else if (e.Key == Key.OemPlus || e.Key == Key.Add) { ZoomIn(); e.Handled = true; }
                    else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) { ZoomOut(); e.Handled = true; }
                }
            }
        }

        #endregion


        #region Animation Tab & Timeline Engine

        private FrameworkElement CreateModeSwitcher()
        {
            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 32, 44)), // #1C202C
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 58, 80)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(2),
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            _designTabButton = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)), // Active Indigo
                Padding = new Thickness(12, 4, 12, 4),
                Cursor = Cursors.Hand
            };
            var designText = new TextBlock
            {
                Text = "Design",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            _designTabButton.Child = designText;
            _designTabButton.MouseLeftButtonUp += (s, e) => SwitchStudioMode(StudioMode.Design);

            _animateTabButton = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Padding = new Thickness(12, 4, 12, 4),
                Cursor = Cursors.Hand
            };
            var animateText = new TextBlock
            {
                Text = "Animate",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 205))
            };
            _animateTabButton.Child = animateText;
            _animateTabButton.MouseLeftButtonUp += (s, e) => SwitchStudioMode(StudioMode.Animate);

            stack.Children.Add(_designTabButton);
            stack.Children.Add(_animateTabButton);
            pill.Child = stack;
            return pill;
        }

        public void SwitchStudioMode(StudioMode mode)
        {
            _currentStudioMode = mode;
            if (mode == StudioMode.Animate)
            {
                if (_designTabButton != null)
                {
                    _designTabButton.Background = Brushes.Transparent;
                    var dt = _designTabButton.Child as TextBlock;
                    if (dt != null) dt.Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 205));
                }
                if (_animateTabButton != null)
                {
                    _animateTabButton.Background = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                    var at = _animateTabButton.Child as TextBlock;
                    if (at != null) at.Foreground = Brushes.White;
                }

                if (_timelinePanel != null)
                {
                    _timelinePanel.Visibility = Visibility.Visible;
                }

                EnsureTimelineControllerInitialized();
                RefreshTimelineUI();
            }
            else
            {
                PauseAnimation();
                if (_designTabButton != null)
                {
                    _designTabButton.Background = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                    var dt = _designTabButton.Child as TextBlock;
                    if (dt != null) dt.Foreground = Brushes.White;
                }
                if (_animateTabButton != null)
                {
                    _animateTabButton.Background = Brushes.Transparent;
                    var at = _animateTabButton.Child as TextBlock;
                    if (at != null) at.Foreground = new SolidColorBrush(Color.FromRgb(160, 175, 205));
                }

                if (_timelinePanel != null)
                {
                    _timelinePanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private FrameworkElement CreateTimelinePanel()
        {
            var panelBorder = new Border
            {
                Height = 220,
                Background = new SolidColorBrush(Color.FromRgb(22, 25, 34)), // #161922
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 66)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Visibility = Visibility.Collapsed // Default Design mode
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) }); // Transport Bar
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tracks & Timeline

            // 1. Transport Bar
            var transportBar = CreateTimelineTransportBar();
            Grid.SetRow(transportBar, 0);
            grid.Children.Add(transportBar);

            // 2. Timeline Body (Tracks container or State Machine container)
            var bodyGrid = new Grid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Left: Track headers
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right: Ruler & Lanes

            // Left: Track Headers Stack
            var leftTrackBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 30, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 66)),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };
            var trackScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _timelineTracksStack = new StackPanel { Margin = new Thickness(8, 4, 8, 4) };
            trackScroll.Content = _timelineTracksStack;
            leftTrackBorder.Child = trackScroll;
            Grid.SetColumn(leftTrackBorder, 0);
            bodyGrid.Children.Add(leftTrackBorder);

            // Right: Ruler & Lanes Host Canvas
            var rightCanvasBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 21, 29)),
                ClipToBounds = true
            };
            _timelineCanvasHost = new Canvas
            {
                Height = 182,
                Cursor = Cursors.Hand
            };

            _timelineCanvasHost.MouseLeftButtonDown += (s, e) =>
            {
                _isScrubbingTimeline = true;
                _timelineCanvasHost.CaptureMouse();
                ScrubTimelineAtPoint(e.GetPosition(_timelineCanvasHost));
            };
            _timelineCanvasHost.MouseMove += (s, e) =>
            {
                if (_isScrubbingTimeline)
                {
                    ScrubTimelineAtPoint(e.GetPosition(_timelineCanvasHost));
                }
            };
            _timelineCanvasHost.MouseLeftButtonUp += (s, e) =>
            {
                if (_isScrubbingTimeline)
                {
                    _isScrubbingTimeline = false;
                    _timelineCanvasHost.ReleaseMouseCapture();
                }
            };

            rightCanvasBorder.Child = _timelineCanvasHost;
            Grid.SetColumn(rightCanvasBorder, 1);
            bodyGrid.Children.Add(rightCanvasBorder);

            _timelineTracksContainer = bodyGrid;
            Grid.SetRow(_timelineTracksContainer, 1);
            grid.Children.Add(_timelineTracksContainer);

            // State Machine Live Testing View Container (Toggleable)
            _stateMachineContainer = CreateStateMachineControlsPanel();
            _stateMachineContainer.Visibility = Visibility.Collapsed;
            Grid.SetRow(_stateMachineContainer, 1);
            grid.Children.Add(_stateMachineContainer);

            panelBorder.Child = grid;
            return panelBorder;
        }

        private FrameworkElement CreateTimelineTransportBar()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 32, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 66)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 0, 12, 0)
            };

            var dock = new DockPanel { LastChildFill = false };

            // Left: Transport Buttons
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Play / Pause Button
            var playBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)), // Emerald Green
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _timelinePlayBtnText = new TextBlock
            {
                Text = "▶ Play",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            playBtn.Child = _timelinePlayBtnText;
            playBtn.MouseLeftButtonUp += (s, e) => ToggleAnimationPlayback();
            leftStack.Children.Add(playBtn);

            // Stop / Rewind Button
            var stopBtn = CreateSmallTimelineButton("⏹ Stop", () => StopAnimationPlayback());
            leftStack.Children.Add(stopBtn);

            // Loop Toggle Button
            var loopBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(38, 44, 62)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _timelineLoopText = new TextBlock
            {
                Text = "🔁 Loop: All",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 235))
            };
            loopBtn.Child = _timelineLoopText;
            loopBtn.MouseLeftButtonUp += (s, e) => CycleTimelineLoopMode();
            leftStack.Children.Add(loopBtn);

            // Time Readout
            _timelineTimeText = new TextBlock
            {
                Text = "0.00s / 2.00s",
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)), // Cyan
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 12, 0)
            };
            leftStack.Children.Add(_timelineTimeText);

            // Interactive Scrubber Slider
            _timelineScrubber = new Slider
            {
                Minimum = 0.0,
                Maximum = 2.0,
                Value = 0.0,
                Width = 140,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0)
            };
            _timelineScrubber.ValueChanged += (s, e) =>
            {
                if (_timelineController != null && !_timelineController.IsPlaying)
                {
                    _timelineController.Seek(_timelineScrubber.Value);
                    UpdateTimelineTimeReadout();
                    RefreshTimelineCanvas();
                }
            };
            leftStack.Children.Add(_timelineScrubber);

            DockPanel.SetDock(leftStack, Dock.Left);
            dock.Children.Add(leftStack);

            // Right: Clip selector, Keyframe button, Mode switch (Tracks vs State Machine)
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Active Clip Badge
            var clipBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(36, 42, 60)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _timelineClipText = new TextBlock
            {
                Text = "Clip: Default Animation",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 220))
            };
            clipBadge.Child = _timelineClipText;
            rightStack.Children.Add(clipBadge);

            // Add Keyframe Button
            var addKeyBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)), // Indigo Accent
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            var addKeyText = new TextBlock
            {
                Text = "◆ + Keyframe",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            addKeyBtn.Child = addKeyText;
            addKeyBtn.MouseLeftButtonUp += (s, e) => AddKeyframeForSelectedLayer();
            rightStack.Children.Add(addKeyBtn);

            // Speed Selector Button
            var speedBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(38, 44, 62)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            _timelineSpeedText = new TextBlock
            {
                Text = "1.0x",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 235))
            };
            speedBtn.Child = _timelineSpeedText;
            speedBtn.MouseLeftButtonUp += (s, e) => CycleTimelineSpeed();
            rightStack.Children.Add(speedBtn);

            // Mode Toggle: [ Tracks ] vs [ State Machine ]
            var modeToggleBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(48, 56, 78)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand
            };
            var modeToggleText = new TextBlock
            {
                Text = "State Machine ⚙",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            modeToggleBtn.Child = modeToggleText;
            modeToggleBtn.MouseLeftButtonUp += (s, e) => ToggleTimelineMode();
            rightStack.Children.Add(modeToggleBtn);

            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

            border.Child = dock;
            return border;
        }

        private Border CreateSmallTimelineButton(string text, Action onClick)
        {
            var btn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(38, 44, 62)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 235))
            };
            btn.Child = tb;
            btn.MouseLeftButtonUp += (s, e) => onClick();
            return btn;
        }

        private FrameworkElement CreateStateMachineControlsPanel()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 23, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 42, 60)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 10, 16, 12)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header & Status Bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Inputs & Controls
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // States & Transitions

            // 1. Header Toolbar
            var headerDock = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 10) };

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var titleIcon = new TextBlock
            {
                Text = "⚡ ",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11))
            };
            titleStack.Children.Add(titleIcon);

            var title = new TextBlock
            {
                Text = "Interactive Vector State Machine",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(title);

            // Active State Live Badge
            _stateMachineStatusBadge = new TextBlock
            {
                Text = "● Active State: Idle",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)), // Emerald
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(_stateMachineStatusBadge);

            DockPanel.SetDock(titleStack, Dock.Left);
            headerDock.Children.Add(titleStack);

            // Right side: Interactive Canvas Toggle & Step Frame
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _canvasInteractBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 34, 197, 94)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            _canvasInteractText = new TextBlock
            {
                Text = "Canvas Interaction: ON",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128))
            };
            _canvasInteractBtn.Child = _canvasInteractText;
            _canvasInteractBtn.MouseLeftButtonUp += (s, e) =>
            {
                _interactiveCanvasEnabled = !_interactiveCanvasEnabled;
                _canvasInteractText.Text = _interactiveCanvasEnabled ? "Canvas Interaction: ON" : "Canvas Interaction: OFF";
                _canvasInteractBtn.Background = _interactiveCanvasEnabled
                    ? new SolidColorBrush(Color.FromArgb(40, 34, 197, 94))
                    : new SolidColorBrush(Color.FromArgb(40, 100, 116, 139));
                _canvasInteractBtn.BorderBrush = _interactiveCanvasEnabled
                    ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
                    : new SolidColorBrush(Color.FromRgb(100, 116, 139));
                _canvasInteractText.Foreground = _interactiveCanvasEnabled
                    ? new SolidColorBrush(Color.FromRgb(74, 222, 128))
                    : new SolidColorBrush(Color.FromRgb(148, 163, 184));
            };
            rightStack.Children.Add(_canvasInteractBtn);

            var tickBtn = CreateSmallTimelineButton("▶ Step Frame (0.016s)", () => TriggerStateMachineStep());
            rightStack.Children.Add(tickBtn);

            DockPanel.SetDock(rightStack, Dock.Right);
            headerDock.Children.Add(rightStack);

            Grid.SetRow(headerDock, 0);
            mainGrid.Children.Add(headerDock);

            // 2. Inputs & Triggers Row
            var inputsRow = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var inputsHeader = new TextBlock
            {
                Text = "STATE MACHINE INPUTS (CLICK OR TOGGLE TO TRIGGER POSE TRANSITIONS):",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 145, 175)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            inputsRow.Children.Add(inputsHeader);

            _stateMachineInputsStack = new StackPanel { Orientation = Orientation.Horizontal };
            BuildStateMachineInputsControls();
            inputsRow.Children.Add(_stateMachineInputsStack);

            Grid.SetRow(inputsRow, 1);
            mainGrid.Children.Add(inputsRow);

            // 3. States & Transitions Graph Row
            var statesRow = new StackPanel();
            var statesHeader = new TextBlock
            {
                Text = "MACHINE STATES & TRANSITION GRAPH (CLICK STATE TO PREVIEW POSE):",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 145, 175)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            statesRow.Children.Add(statesHeader);

            _stateMachineStatesStack = new StackPanel { Orientation = Orientation.Horizontal };
            BuildStateMachineStatesGraph();
            statesRow.Children.Add(_stateMachineStatesStack);

            Grid.SetRow(statesRow, 2);
            mainGrid.Children.Add(statesRow);

            border.Child = mainGrid;
            return border;
        }

        private void BuildStateMachineInputsControls()
        {
            if (_stateMachineInputsStack == null) return;
            _stateMachineInputsStack.Children.Clear();

            if (_timelineController == null || _timelineController.ActiveStateMachine == null)
            {
                EnsureTimelineControllerInitialized();
            }

            var sm = _timelineController.ActiveStateMachine;
            if (sm == null || sm.Definition == null) return;

            for (int i = 0; i < sm.Definition.Inputs.Count; i++)
            {
                int inIdx = i;
                var input = sm.Definition.Inputs[i];

                if (input.Kind == InputKind.Trigger)
                {
                    var trgBtn = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(139, 92, 246)), // Purple
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(10, 5, 10, 5),
                        Margin = new Thickness(0, 0, 8, 0),
                        Cursor = Cursors.Hand
                    };
                    var tb = new TextBlock
                    {
                        Text = "⚡ " + input.Name,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    };
                    trgBtn.Child = tb;
                    trgBtn.MouseLeftButtonUp += (s, e) =>
                    {
                        sm.FireTrigger(inIdx);
                        TriggerStateMachineStep();
                    };
                    _stateMachineInputsStack.Children.Add(trgBtn);
                }
                else if (input.Kind == InputKind.Bool)
                {
                    bool isVal = input.Value > 0.5;
                    var boolBtn = new Border
                    {
                        Background = isVal ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(10, 5, 10, 5),
                        Margin = new Thickness(0, 0, 8, 0),
                        Cursor = Cursors.Hand
                    };
                    var tb = new TextBlock
                    {
                        Text = string.Format("⏻ {0}: {1}", input.Name, isVal ? "ON" : "OFF"),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    };
                    boolBtn.Child = tb;
                    boolBtn.MouseLeftButtonUp += (s, e) =>
                    {
                        bool newVal = !(sm.GetInputValue(inIdx) > 0.5);
                        sm.SetBool(inIdx, newVal);
                        tb.Text = string.Format("⏻ {0}: {1}", input.Name, newVal ? "ON" : "OFF");
                        boolBtn.Background = newVal ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        TriggerStateMachineStep();
                    };
                    _stateMachineInputsStack.Children.Add(boolBtn);
                }
                else if (input.Kind == InputKind.Number)
                {
                    var numStack = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var label = new TextBlock
                    {
                        Text = "🎚 " + input.Name + ": ",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 235)),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    numStack.Children.Add(label);

                    var slider = new Slider
                    {
                        Minimum = 0.0,
                        Maximum = 2.0,
                        Value = input.Value,
                        Width = 90,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    slider.ValueChanged += (s, e) =>
                    {
                        sm.SetNumber(inIdx, slider.Value);
                        TriggerStateMachineStep();
                    };
                    numStack.Children.Add(slider);
                    _stateMachineInputsStack.Children.Add(numStack);
                }
            }
        }

        private void BuildStateMachineStatesGraph()
        {
            if (_stateMachineStatesStack == null) return;
            _stateMachineStatesStack.Children.Clear();

            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return;
            var sm = _timelineController.ActiveStateMachine;
            if (sm.Definition == null || sm.Definition.Layers.Count == 0) return;

            var layer = sm.Definition.Layers[0];
            int curState = (sm.LayerRuntimes.Count > 0) ? sm.LayerRuntimes[0].CurrentState : 0;

            for (int s = 0; s < layer.States.Count; s++)
            {
                int stateIdx = s;
                var st = layer.States[s];
                bool isActive = (stateIdx == curState);

                var chip = new Border
                {
                    Background = isActive ? new SolidColorBrush(Color.FromArgb(60, 34, 197, 94)) : new SolidColorBrush(Color.FromRgb(33, 39, 54)),
                    BorderBrush = isActive ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(50, 60, 85)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand
                };
                var tb = new TextBlock
                {
                    Text = (isActive ? "● " : "○ ") + st.Name,
                    FontSize = 11,
                    FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isActive ? new SolidColorBrush(Color.FromRgb(74, 222, 128)) : new SolidColorBrush(Color.FromRgb(180, 195, 220))
                };
                chip.Child = tb;
                chip.MouseLeftButtonUp += (s2, e2) =>
                {
                    if (sm.LayerRuntimes.Count > 0)
                    {
                        sm.LayerRuntimes[0].CurrentState = stateIdx;
                        sm.LayerRuntimes[0].ActiveTransition = null;
                        TriggerStateMachineStep();
                    }
                };
                _stateMachineStatesStack.Children.Add(chip);

                if (s < layer.States.Count - 1)
                {
                    var arrow = new TextBlock
                    {
                        Text = "➔",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 115, 145)),
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    _stateMachineStatesStack.Children.Add(arrow);
                }
            }
        }

        private void UpdateStateMachineStatusUI()
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return;
            var sm = _timelineController.ActiveStateMachine;
            if (sm.Definition == null || sm.Definition.Layers.Count == 0 || sm.LayerRuntimes.Count == 0) return;

            var lr = sm.LayerRuntimes[0];
            var layerDef = sm.Definition.Layers[0];
            string curName = (lr.CurrentState >= 0 && lr.CurrentState < layerDef.States.Count)
                ? layerDef.States[lr.CurrentState].Name
                : "Idle";

            if (_stateMachineStatusBadge != null)
            {
                if (lr.ActiveTransition != null)
                {
                    string toName = (lr.ActiveTransition.ToState >= 0 && lr.ActiveTransition.ToState < layerDef.States.Count)
                        ? layerDef.States[lr.ActiveTransition.ToState].Name
                        : "Target";
                    _stateMachineStatusBadge.Text = string.Format("● Transitioning: {0} ➔ {1} ({2:F2}s)", curName, toName, lr.ActiveTransition.Elapsed);
                    _stateMachineStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                }
                else
                {
                    _stateMachineStatusBadge.Text = string.Format("● Active State: {0}", curName);
                    _stateMachineStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Emerald
                }
            }

            BuildStateMachineStatesGraph();
        }

        private void TriggerStateMachineStep()
        {
            if (_timelineController != null)
            {
                _timelineController.Step(0.016);
                UpdateStateMachineStatusUI();
                RefreshTimelineCanvas();
                StartAnimationTimer();
            }
        }

        private void ToggleTimelineMode()
        {
            _isStateMachineMode = !_isStateMachineMode;
            if (_isStateMachineMode)
            {
                _timelineTracksContainer.Visibility = Visibility.Collapsed;
                _stateMachineContainer.Visibility = Visibility.Visible;
            }
            else
            {
                _stateMachineContainer.Visibility = Visibility.Collapsed;
                _timelineTracksContainer.Visibility = Visibility.Visible;
                RefreshTimelineUI();
            }
        }

        private void CycleTimelineLoopMode()
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            if (_timelineController.LoopMode == LoopMode.Loop)
            {
                _timelineController.LoopMode = LoopMode.OneShot;
                _timelineLoopText.Text = "🔁 Loop: Once";
            }
            else if (_timelineController.LoopMode == LoopMode.OneShot)
            {
                _timelineController.LoopMode = LoopMode.PingPong;
                _timelineLoopText.Text = "🔁 Loop: PingPong";
            }
            else
            {
                _timelineController.LoopMode = LoopMode.Loop;
                _timelineLoopText.Text = "🔁 Loop: All";
            }
        }

        private void CycleTimelineSpeed()
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            if (Math.Abs(_timelineController.Speed - 1.0) < 0.1)
            {
                _timelineController.Speed = 2.0;
                _timelineSpeedText.Text = "2.0x";
            }
            else if (Math.Abs(_timelineController.Speed - 2.0) < 0.1)
            {
                _timelineController.Speed = 0.5;
                _timelineSpeedText.Text = "0.5x";
            }
            else
            {
                _timelineController.Speed = 1.0;
                _timelineSpeedText.Text = "1.0x";
            }
        }

        private void ScrubTimelineAtPoint(Point pt)
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            double time = Math.Max(0.0, Math.Min(_timelineController.Duration, pt.X / _timelinePixelsPerSecond));
            _timelineController.Seek(time);
            UpdateTimelineTimeReadout();
            UpdateTimelineScrubberPosition();
            RefreshTimelineCanvas();
        }

        private void UpdateTimelineTimeReadout()
        {
            if (_timelineController == null || _timelineTimeText == null) return;
            _timelineTimeText.Text = string.Format("{0:F2}s / {1:F2}s", _timelineController.CurrentTime, _timelineController.Duration);
        }

        private void UpdateTimelineScrubberPosition()
        {
            if (_timelineScrubber == null || _timelineController == null) return;
            _timelineScrubber.Value = _timelineController.CurrentTime;
        }

        public void ToggleAnimationPlayback()
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            if (_timelineController.IsPlaying)
            {
                PauseAnimation();
            }
            else
            {
                PlayAnimation();
            }
        }

        public void PlayAnimation()
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            _timelineController.Play();
            if (_timelinePlayBtnText != null) _timelinePlayBtnText.Text = "⏸ Pause";
            StartAnimationTimer();
        }

        public void PauseAnimation()
        {
            if (_timelineController != null) _timelineController.Pause();
            if (_timelinePlayBtnText != null) _timelinePlayBtnText.Text = "▶ Play";
            if (_animationTimer != null) _animationTimer.Stop();
        }

        public void StopAnimationPlayback()
        {
            PauseAnimation();
            if (_timelineController != null)
            {
                _timelineController.Stop();
                UpdateTimelineTimeReadout();
                UpdateTimelineScrubberPosition();
                RefreshTimelineCanvas();
            }
        }

        private void StartAnimationTimer()
        {
            if (_animationTimer == null)
            {
                _animationTimer = new DispatcherTimer(DispatcherPriority.Render);
                _animationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
                _animationTimer.Tick += (s, e) =>
                {
                    if (_timelineController != null)
                    {
                        if (_timelineController.IsPlaying)
                        {
                            _timelineController.Step(0.016);
                            UpdateTimelineTimeReadout();
                            UpdateTimelineScrubberPosition();
                            RefreshTimelineCanvas();
                        }
                        else
                        {
                            PauseAnimation();
                        }
                    }
                };
            }
            if (!_animationTimer.IsEnabled)
            {
                _animationTimer.Start();
            }
        }

        private void EnsureTimelineControllerInitialized()
        {
            if (_timelineController != null) return;

            _timelineController = new AnimationTimelineController(2.0, 60.0);
            _timelineController.FrameUpdated += (s, pose) =>
            {
                var page = Document != null ? Document.GetActivePage() : null;
                if (page != null)
                {
                    PropertyBinder.ApplyPoseToLayers(page.GetAllLayersFlat(), pose);
                    _sceneGraph.InvalidateAll();
                    RefreshCanvas();
                    RefreshPropertyInspector();
                }
            };

            var defaultClip = new Clip(1, "Default Animation", 2.0);

            var pageLayers = (Document != null && Document.GetActivePage() != null) ? Document.GetActivePage().Layers : null;
            if (pageLayers != null && pageLayers.Count > 0)
            {
                var target = pageLayers[0];
                uint targetId = (uint)Math.Abs(target.Id.GetHashCode());

                // Default Rotation track (0 -> 180 -> 360 deg)
                var rotTrack = new AnimTrack(targetId, (ushort)PropKey.Rotation);
                rotTrack.AddKeyframe(Keyframe.Cubic(0.0, 0.0, 0.42, 0.0, 0.58, 1.0));
                rotTrack.AddKeyframe(Keyframe.Cubic(1.0, 180.0, 0.42, 0.0, 0.58, 1.0));
                rotTrack.AddKeyframe(Keyframe.Cubic(2.0, 360.0, 0.42, 0.0, 0.58, 1.0));
                defaultClip.Tracks.Add(rotTrack);

                // Default Opacity track (1.0 -> 0.4 -> 1.0)
                var opTrack = new AnimTrack(targetId, (ushort)PropKey.Opacity);
                opTrack.AddKeyframe(Keyframe.Linear(0.0, target.Opacity));
                opTrack.AddKeyframe(Keyframe.Linear(1.0, Math.Max(0.2, target.Opacity * 0.5)));
                opTrack.AddKeyframe(Keyframe.Linear(2.0, target.Opacity));
                defaultClip.Tracks.Add(opTrack);
            }

            _timelineController.ActiveClip = defaultClip;

            // Multi-State Distinct Clips: Idle, Hover, Pressed, Active
            var clipIdle = new Clip(0, "Idle Pose", 1.0);
            var clipHover = new Clip(1, "Hover Pose", 1.0);
            var clipPressed = new Clip(2, "Pressed Pose", 1.0);
            var clipActive = new Clip(3, "Active Pose", 1.0);

            if (pageLayers != null && pageLayers.Count > 0)
            {
                var target = pageLayers[0];
                uint targetId = (uint)Math.Abs(target.Id.GetHashCode());

                // Idle: Rest position and scale
                var trkIdleRot = new AnimTrack(targetId, (ushort)PropKey.Rotation);
                trkIdleRot.AddKeyframe(Keyframe.Linear(0.0, 0.0));
                clipIdle.Tracks.Add(trkIdleRot);

                var trkIdleY = new AnimTrack(targetId, (ushort)PropKey.Y);
                trkIdleY.AddKeyframe(Keyframe.Linear(0.0, target.Y));
                clipIdle.Tracks.Add(trkIdleY);

                // Hover: Elevated -8px
                var trkHovRot = new AnimTrack(targetId, (ushort)PropKey.Rotation);
                trkHovRot.AddKeyframe(Keyframe.Linear(0.0, 0.0));
                clipHover.Tracks.Add(trkHovRot);

                var trkHovY = new AnimTrack(targetId, (ushort)PropKey.Y);
                trkHovY.AddKeyframe(Keyframe.Cubic(0.0, target.Y - 10.0, 0.25, 0.1, 0.25, 1.0));
                clipHover.Tracks.Add(trkHovY);

                // Pressed: Down +4px, opacity 0.85
                var trkPresY = new AnimTrack(targetId, (ushort)PropKey.Y);
                trkPresY.AddKeyframe(Keyframe.Linear(0.0, target.Y + 4.0));
                clipPressed.Tracks.Add(trkPresY);

                var trkPresOp = new AnimTrack(targetId, (ushort)PropKey.Opacity);
                trkPresOp.AddKeyframe(Keyframe.Linear(0.0, Math.Max(0.2, target.Opacity * 0.85)));
                clipPressed.Tracks.Add(trkPresOp);

                // Active: Rotated 15 deg
                var trkActRot = new AnimTrack(targetId, (ushort)PropKey.Rotation);
                trkActRot.AddKeyframe(Keyframe.Cubic(0.0, 15.0, 0.34, 1.56, 0.64, 1.0));
                clipActive.Tracks.Add(trkActRot);

                var trkActY = new AnimTrack(targetId, (ushort)PropKey.Y);
                trkActY.AddKeyframe(Keyframe.Linear(0.0, target.Y));
                clipActive.Tracks.Add(trkActY);
            }

            // Real Vector State Machine Definition Setup
            var smDef = new StateMachineDefinition("Interactive Vector SM");
            int inHover = smDef.AddInput(MachineInput.Bool("isHovered", false));
            int inPress = smDef.AddInput(MachineInput.Bool("isPressed", false));
            int inActive = smDef.AddInput(MachineInput.Bool("isActive", false));
            int inClick = smDef.AddInput(MachineInput.Trigger("clickTrigger"));
            int inReset = smDef.AddInput(MachineInput.Trigger("resetTrigger"));
            int inSpeed = smDef.AddInput(MachineInput.Number("speed", 1.0));

            var smLayer = new MachineLayer("Base Layer");
            int stEntry = smLayer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int stIdle = smLayer.AddState(new MachineState(1, "Idle", StateKind.Animation, 0));
            int stHover = smLayer.AddState(new MachineState(2, "Hover", StateKind.Animation, 1));
            int stPressed = smLayer.AddState(new MachineState(3, "Pressed", StateKind.Animation, 2));
            int stActive = smLayer.AddState(new MachineState(4, "Active", StateKind.Animation, 3));
            int stAny = smLayer.AddState(new MachineState(5, "Any", StateKind.Any));

            // Entry -> Idle (instant)
            smLayer.AddTransition(new StateTransition(stEntry, stIdle, 0.0));

            // Idle -> Hover (isHovered == true)
            var tIdleHov = new StateTransition(stIdle, stHover, 0.25);
            tIdleHov.Conditions.Add(new TransitionCondition(inHover, ConditionOp.Eq, 1.0));
            smLayer.AddTransition(tIdleHov);

            // Hover -> Idle (isHovered == false)
            var tHovIdle = new StateTransition(stHover, stIdle, 0.25);
            tHovIdle.Conditions.Add(new TransitionCondition(inHover, ConditionOp.Eq, 0.0));
            smLayer.AddTransition(tHovIdle);

            // Hover -> Pressed (isPressed == true)
            var tHovPress = new StateTransition(stHover, stPressed, 0.10);
            tHovPress.Conditions.Add(new TransitionCondition(inPress, ConditionOp.Eq, 1.0));
            smLayer.AddTransition(tHovPress);

            // Pressed -> Active (clickTrigger)
            var tPressAct = new StateTransition(stPressed, stActive, 0.25);
            tPressAct.Conditions.Add(new TransitionCondition(inClick, ConditionOp.Eq, 1.0));
            smLayer.AddTransition(tPressAct);

            // Active -> Idle (clickTrigger or resetTrigger)
            var tActIdle = new StateTransition(stActive, stIdle, 0.30);
            tActIdle.Conditions.Add(new TransitionCondition(inClick, ConditionOp.Eq, 1.0));
            smLayer.AddTransition(tActIdle);

            // Any -> Idle (resetTrigger)
            var tAnyReset = new StateTransition(stAny, stIdle, 0.20);
            tAnyReset.Conditions.Add(new TransitionCondition(inReset, ConditionOp.Eq, 1.0));
            smLayer.AddTransition(tAnyReset);

            smDef.AddLayer(smLayer);
            var clips = new List<Clip> { clipIdle, clipHover, clipPressed, clipActive };
            _timelineController.ActiveStateMachine = new StateMachineRuntime(smDef, clips);
        }

        private void AddKeyframeForSelectedLayer()
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            var clip = _timelineController.ActiveClip;
            if (clip == null)
            {
                clip = new Clip(1, "Default Animation", 2.0);
                _timelineController.ActiveClip = clip;
            }

            var sel = _selectedLayers.FirstOrDefault();
            if (sel == null)
            {
                MessageBox.Show("Please select a layer on canvas or in the layers list to record animation keyframes.", "Kerf Studio - Animation", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double time = _timelineController.CurrentTime;
            uint nodeId = (uint)Math.Abs(sel.Id.GetHashCode());

            AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)PropKey.X, time, sel.X);
            AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)PropKey.Y, time, sel.Y);
            AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)PropKey.Width, time, sel.Width);
            AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)PropKey.Height, time, sel.Height);
            AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)PropKey.Rotation, time, sel.Rotation);
            AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)PropKey.Opacity, time, sel.Opacity);
            AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)PropKey.CornerRadius, time, sel.CornerRadius);

            RefreshTimelineUI();
        }

        private static void AddOrUpdateTrackKeyframe(Clip clip, uint nodeId, ushort propertyId, double time, double val)
        {
            AnimTrack track = null;
            for (int i = 0; i < clip.Tracks.Count; i++)
            {
                if (clip.Tracks[i].NodeId == nodeId && clip.Tracks[i].Prop == propertyId)
                {
                    track = clip.Tracks[i];
                    break;
                }
            }

            if (track == null)
            {
                track = new AnimTrack(nodeId, propertyId);
                clip.Tracks.Add(track);
            }

            Keyframe existing = null;
            for (int i = 0; i < track.Keys.Count; i++)
            {
                if (Math.Abs(track.Keys[i].Time - time) < 0.02)
                {
                    existing = track.Keys[i];
                    break;
                }
            }

            if (existing != null)
            {
                existing.Value = val;
            }
            else
            {
                track.AddKeyframe(Keyframe.Cubic(time, val, 0.42, 0.0, 0.58, 1.0));
            }
        }

        public void RefreshTimelineUI()
        {
            RefreshTimelineTracks();
            RefreshTimelineCanvas();
            UpdateTimelineTimeReadout();
            UpdateTimelineScrubberPosition();
        }

        private void RefreshTimelineTracks()
        {
            if (_timelineTracksStack == null) return;
            _timelineTracksStack.Children.Clear();

            var sel = _selectedLayers.FirstOrDefault();
            var header = new TextBlock
            {
                Text = (sel != null) ? string.Format("Layer: {0}", sel.Name) : "No Layer Selected",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _timelineTracksStack.Children.Add(header);

            if (sel != null)
            {
                _timelineTracksStack.Children.Add(CreateTrackRow("Position X", string.Format("{0:F1} px", sel.X)));
                _timelineTracksStack.Children.Add(CreateTrackRow("Position Y", string.Format("{0:F1} px", sel.Y)));
                _timelineTracksStack.Children.Add(CreateTrackRow("Width", string.Format("{0:F1} px", sel.Width)));
                _timelineTracksStack.Children.Add(CreateTrackRow("Height", string.Format("{0:F1} px", sel.Height)));
                _timelineTracksStack.Children.Add(CreateTrackRow("Rotation", string.Format("{0:F1} °", sel.Rotation)));
                _timelineTracksStack.Children.Add(CreateTrackRow("Opacity", string.Format("{0:F0} %", sel.Opacity * 100)));
                _timelineTracksStack.Children.Add(CreateTrackRow("Corner Radius", string.Format("{0:F1} px", sel.CornerRadius)));
            }
            else
            {
                var hint = new TextBlock
                {
                    Text = "Select a layer on canvas to view tracks & record keyframes.",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 135, 160)),
                    TextWrapping = TextWrapping.Wrap
                };
                _timelineTracksStack.Children.Add(hint);
            }
        }

        private FrameworkElement CreateTrackRow(string name, string val)
        {
            var grid = new Grid { Height = 20, Margin = new Thickness(0, 1, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBlock = new TextBlock
            {
                Text = name,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 220)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameBlock, 0);
            grid.Children.Add(nameBlock);

            var valBlock = new TextBlock
            {
                Text = val,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 145, 175)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            return grid;
        }

        private void RefreshTimelineCanvas()
        {
            if (_timelineCanvasHost == null) return;
            _timelineCanvasHost.Children.Clear();

            double duration = (_timelineController != null) ? _timelineController.Duration : 2.0;
            double pps = _timelinePixelsPerSecond;
            double totalWidth = duration * pps;

            // 1. Time Ruler (Top 22px)
            var rulerLine = new Line
            {
                X1 = 0,
                Y1 = 22,
                X2 = Math.Max(totalWidth + 100, 1000),
                Y2 = 22,
                Stroke = new SolidColorBrush(Color.FromRgb(42, 48, 66)),
                StrokeThickness = 1
            };
            _timelineCanvasHost.Children.Add(rulerLine);

            // Tick marks & time labels every 0.25s / 0.5s
            for (double t = 0.0; t <= duration + 0.001; t += 0.25)
            {
                double x = t * pps;
                bool isMajor = (Math.Abs(t % 0.5) < 0.01);
                var tick = new Line
                {
                    X1 = x,
                    Y1 = isMajor ? 6 : 14,
                    X2 = x,
                    Y2 = 22,
                    Stroke = new SolidColorBrush(isMajor ? Color.FromRgb(140, 155, 185) : Color.FromRgb(60, 70, 95)),
                    StrokeThickness = 1
                };
                _timelineCanvasHost.Children.Add(tick);

                if (isMajor)
                {
                    var label = new TextBlock
                    {
                        Text = string.Format("{0:0.0}s", t),
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(140, 155, 185)),
                        FontFamily = new FontFamily("Consolas, Courier New")
                    };
                    Canvas.SetLeft(label, x + 3);
                    Canvas.SetTop(label, 4);
                    _timelineCanvasHost.Children.Add(label);
                }
            }

            // 2. Track Lanes (Horizontal separator lines)
            double rowY = 24.0;
            for (int r = 0; r < 7; r++)
            {
                rowY += 21.0;
                var laneLine = new Line
                {
                    X1 = 0,
                    Y1 = rowY,
                    X2 = Math.Max(totalWidth + 100, 1000),
                    Y2 = rowY,
                    Stroke = new SolidColorBrush(Color.FromRgb(30, 35, 48)),
                    StrokeThickness = 1
                };
                _timelineCanvasHost.Children.Add(laneLine);
            }

            // 3. Keyframe Diamonds ◆
            if (_timelineController != null && _timelineController.ActiveClip != null)
            {
                var sel = _selectedLayers.FirstOrDefault();
                uint selId = (sel != null) ? (uint)Math.Abs(sel.Id.GetHashCode()) : 0;

                foreach (var track in _timelineController.ActiveClip.Tracks)
                {
                    if (sel != null && track.NodeId != selId) continue;

                    int propRow = GetPropRowIndex(track.Prop);
                    double ky = 24.0 + propRow * 21.0 + 10.5;

                    foreach (var kf in track.Keys)
                    {
                        double kx = kf.Time * pps;
                        var diamond = new Polygon
                        {
                            Points = new PointCollection
                            {
                                new Point(kx, ky - 5),
                                new Point(kx + 5, ky),
                                new Point(kx, ky + 5),
                                new Point(kx - 5, ky)
                            },
                            Fill = new SolidColorBrush(Color.FromRgb(251, 191, 36)), // Gold/Amber
                            Stroke = new SolidColorBrush(Color.FromRgb(30, 35, 48)),
                            StrokeThickness = 1,
                            ToolTip = string.Format("Keyframe: {0:F2}s, Value: {1:F1}", kf.Time, kf.Value)
                        };
                        _timelineCanvasHost.Children.Add(diamond);
                    }
                }
            }

            // 4. Playhead Line & Scrubber Needle
            if (_timelineController != null)
            {
                double px = _timelineController.CurrentTime * pps;

                // Vertical Playhead Line
                var playheadLine = new Line
                {
                    X1 = px,
                    Y1 = 12,
                    X2 = px,
                    Y2 = 180,
                    Stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red Playhead
                    StrokeThickness = 1.5
                };
                _timelineCanvasHost.Children.Add(playheadLine);

                // Playhead Head Indicator (Downward triangle)
                var playheadHead = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(px - 5, 2),
                        new Point(px + 5, 2),
                        new Point(px + 5, 8),
                        new Point(px, 14),
                        new Point(px - 5, 8)
                    },
                    Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68))
                };
                _timelineCanvasHost.Children.Add(playheadHead);
            }
        }

        private static int GetPropRowIndex(ushort propKey)
        {
            switch ((PropKey)propKey)
            {
                case PropKey.X: return 0;
                case PropKey.Y: return 1;
                case PropKey.Width: return 2;
                case PropKey.Height: return 3;
                case PropKey.Rotation: return 4;
                case PropKey.Opacity: return 5;
                case PropKey.CornerRadius: return 6;
                default: return 0;
            }
        }

        #endregion

        #region UI Helpers

        private FrameworkElement CreateSeparator()
        {
            return new Border
            {
                Width = 1,
                Height = 20,
                Background = new SolidColorBrush(Color.FromRgb(40, 48, 70)),
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
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

        private Button CreateIconButton(Geometry iconGeo, string tooltip, Action onClick)
        {
            var btn = new Button
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(2, 0, 2, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Template = CreateRoundedButtonTemplate(6)
            };
            btn.Content = VectorIcons.CreateIcon(iconGeo, new SolidColorBrush(Color.FromRgb(170, 185, 210)), null, 16);
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private Button CreateTextButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Width = 24,
                Height = 24,
                Content = text,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(28, 33, 48)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 2, 0),
                Template = CreateRoundedButtonTemplate(5)
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private Button CreateSmallButton(string text, string tooltip, Action onClick)
        {
            var btn = new Button
            {
                Width = 24,
                Height = 24,
                Content = text,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(28, 33, 48)),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 220)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Margin = new Thickness(2, 0, 2, 0),
                Template = CreateRoundedButtonTemplate(5)
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        #endregion
    }

    public class RetainedCanvasHost : FrameworkElement
    {
        private readonly RetainedSceneGraph _sceneGraph;
        private readonly StudioView _studio;
        private readonly VisualCollection _visuals;
        private readonly DrawingVisual _overlayVisual;

        public RetainedCanvasHost(RetainedSceneGraph sceneGraph, StudioView studio)
        {
            _sceneGraph = sceneGraph;
            _studio = studio;
            _visuals = new VisualCollection(this);

            _visuals.Add(_sceneGraph.RootVisual);

            _overlayVisual = new DrawingVisual();
            _visuals.Add(_overlayVisual);

            ClipToBounds = true;

            MouseDown += (s, e) => _studio.HandleCanvasMouseDown(e.GetPosition(this), e.ChangedButton, e.ClickCount);
            MouseMove += (s, e) => _studio.HandleCanvasMouseMove(e.GetPosition(this));
            MouseUp += (s, e) => _studio.HandleCanvasMouseUp(e.GetPosition(this), e.ChangedButton);
            MouseWheel += (s, e) => _studio.HandleCanvasMouseWheel(e.GetPosition(this), e.Delta);
            IsManipulationEnabled = true;
            ManipulationDelta += (s, e) => _studio.HandleCanvasManipulationDelta(e);
            SizeChanged += (s, e) => _studio.RefreshCanvas();
        }

        protected override int VisualChildrenCount
        {
            get { return _visuals.Count; }
        }

        protected override Visual GetVisualChild(int index)
        {
            return _visuals[index];
        }

        protected override void OnRender(DrawingContext dc)
        {
            // Canvas background dots / grid
            var page = _studio.Document != null ? _studio.Document.GetActivePage() : null;
            string bgHex = page != null ? page.BackgroundColor : "#141722";
            var bgBrush = new SolidColorBrush(GpuRenderer.ParseHexColor(bgHex, 1.0));
            bgBrush.Freeze();
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            // Grid dots
            if (page != null && page.ShowGrid)
            {
                RenderGridDots(dc);
            }

            // Update RootVisual transform (Pan & Zoom)
            Point pan = _studio.PanOffset;
            double zoom = _studio.ZoomScale;

            var mat = new Matrix();
            mat.Scale(zoom, zoom);
            mat.Translate(pan.X, pan.Y);
            _sceneGraph.RootVisual.Transform = new MatrixTransform(mat);

            // Render Overlays into overlay visual
            using (var odc = _overlayVisual.RenderOpen())
            {
                odc.PushTransform(new MatrixTransform(mat));
                _studio.RenderOverlays(odc);
                odc.Pop();
            }
        }

        private void RenderGridDots(DrawingContext dc)
        {
            Point pan = _studio.PanOffset;
            double zoom = _studio.ZoomScale;

            double step = 32.0 * zoom;
            if (step < 12.0) return; // Too dense

            double startX = pan.X % step;
            double startY = pan.Y % step;

            var dotBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            dotBrush.Freeze();

            for (double x = startX; x < ActualWidth; x += step)
            {
                for (double y = startY; y < ActualHeight; y += step)
                {
                    dc.DrawRectangle(dotBrush, null, new Rect(x, y, 1.5, 1.5));
                }
            }
        }
    }
}
