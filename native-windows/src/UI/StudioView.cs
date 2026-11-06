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
        private StackPanel _timelineStateTabsStack;
        private Canvas _timelineCanvasHost;
        private FrameworkElement _timelineTracksContainer;
        private FrameworkElement _stateMachineContainer;
        private FrameworkElement _stateMachineScrollContainer;
        private bool _isStateMachineMode = true;
        private Border _tabStateMachineBtn;
        private Border _tabTimelineClipsBtn;
        private double _timelinePixelsPerSecond = 260.0;
        private bool _isScrubbingTimeline = false;
        private bool _timelinePointerDown;
        private bool _timelineCandidateMarquee;
        private bool _isTimelineMarqueeSelecting;
        private bool _timelineMarqueeAdditive;
        private Point _timelineMarqueeStartPoint;
        private HashSet<Keyframe> _timelineMarqueeInitialSelection = new HashSet<Keyframe>();
        private Rectangle _timelineMarqueeVisual;
        private bool _isDraggingTimelineKeyframes;
        private bool _timelineKeyframeDragMoved;
        private Point _timelineKeyframeDragStart;
        private Dictionary<Keyframe, double> _timelineKeyframeDragStartTimes = new Dictionary<Keyframe, double>();
        private readonly HashSet<Keyframe> _selectedTimelineKeyframes = new HashSet<Keyframe>();
        private readonly Dictionary<Keyframe, AnimTrack> _timelineKeyframeOwners = new Dictionary<Keyframe, AnimTrack>();
        private readonly HashSet<uint> _collapsedTimelineObjects = new HashSet<uint>();

        private struct TimelineLaneInfo
        {
            public bool IsMaster;
            public uint NodeId;
            public AnimTrack Track;
            public double Y;
            public double Height;
        }

        private DateTime _lastAnimationTickUtc = DateTime.MinValue;

        // Left Sidebar Tabs (Layers vs Assets)
        private bool _isAssetsTabActive = false;
        private Border _layersTabBtn;
        private Border _assetsTabBtn;
        private FrameworkElement _layersViewContainer;
        private FrameworkElement _assetsViewContainer;
        private FrameworkElement _defaultInterpolationPanel;
        private Canvas _defaultInterpolationCanvas;
        private TextBlock _defaultInterpolationValues;
        private Button _applyDefaultInterpolationButton;
        private readonly Border[] _defaultInterpolationPresetButtons = new Border[5];
        private InterpMode _defaultInterpolationMode = InterpMode.Linear;
        private double _defaultInterpolationX1 = 0.0;
        private double _defaultInterpolationY1 = 0.0;
        private double _defaultInterpolationX2 = 1.0;
        private double _defaultInterpolationY2 = 1.0;
        private int _defaultInterpolationPreset = 1;
        private int _defaultInterpolationDragHandle = -1;
        private StackPanel _assetsListStack;
        private TextBox _assetsSearchBox;
        private TextBlock _assetsCountBlock;

        // Interactive Vector State Machine Canvas Binding
        private uint _lastHoverNodeId = 0;
        private uint _stateMachinePointerDownNodeId = 0;
        private bool _interactiveCanvasEnabled = true;
        private TextBlock _stateMachineStatusBadge;
        private Panel _stateMachineInputsStack;
        private Panel _stateMachineListenersStack;
        private Canvas _stateMachineGraphCanvas;
        private ScrollViewer _stateMachineGraphScrollViewer;
        private readonly ScaleTransform _stateMachineGraphScale = new ScaleTransform(1.0, 1.0);
        private TextBlock _stateMachineGraphZoomText;
        private double _stateMachineGraphZoom = 1.0;
        private bool _isPanningStateGraph;
        private Point _stateGraphPanStart;
        private double _stateGraphPanStartHorizontalOffset;
        private double _stateGraphPanStartVerticalOffset;
        private TextBox _stateMachineInputSearch;
        private TextBox _stateMachineListenerSearch;
        private StateMachineRuntime _lastGraphStateMachine;
        private int _lastGraphCurrentState = int.MinValue;
        private int _lastGraphTransitionTarget = int.MinValue;
        private StateTransition _selectedGraphTransition;
        private MachineLayer _selectedGraphTransitionLayer;
        private int _selectedGraphStateIndex = -1;
        private readonly Dictionary<int, FrameworkElement> _stateGraphNodes = new Dictionary<int, FrameworkElement>();
        private int _stateGraphPointerMode; // 0 idle, 1 move node, 2 connect nodes
        private int _stateGraphPointerStateIndex = -1;
        private Point _stateGraphPointerOrigin;
        private Point _stateGraphNodeOrigin;
        private bool _stateGraphMoved;
        private Canvas _timelineInterpolationGraphCanvas;
        private Canvas _stateMachineInterpolationGraphCanvas;
        private TextBlock _timelineInterpolationGraphCaption;
        private TextBlock _stateMachineInterpolationGraphCaption;
        private ComboBox _timelineInterpolationModeCombo;
        private ComboBox _stateMachineInterpolationModeCombo;
        private Keyframe _editingInterpolationKeyframe;
        private AnimTrack _editingInterpolationTrack;
        private int _interpolationControlHandle = -1;
        private bool _isRefreshingInterpolationPanels;
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
        private LayerNode _autoLayoutDropTarget;
        private LayerNode _autoLayoutDropSource;
        private int _autoLayoutDropIndex = -1;
        private Rect _autoLayoutDropPreviewBounds;
        private DateTime _autoLayoutHoverStartUtc = DateTime.MinValue;
        private bool _autoLayoutDropPreviewVisible;
        private bool _autoLayoutDetachPreview;
        private Point _autoLayoutDropPoint;
        private List<LayerNode> _autoLayoutDraggedLayers = new List<LayerNode>();
        private DispatcherTimer _autoLayoutPreviewTimer;
        private DispatcherTimer _autoLayoutReflowTimer;
        private DateTime _autoLayoutReflowStartUtc;
        private Dictionary<LayerNode, Point> _autoLayoutReflowStart = new Dictionary<LayerNode, Point>();
        private Dictionary<LayerNode, Point> _autoLayoutReflowEnd = new Dictionary<LayerNode, Point>();
        private LayerNode _autoLayoutLivePreviewFrame;
        private List<LayerNode> _autoLayoutLivePreviewMovingLayers = new List<LayerNode>();
        private GuideLine _activeGuideDrag;

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
        private TextBlock _saveStatusBlock;
        private DispatcherTimer _saveStatusTimer;
        private TextBlock _zoomPercentBlock;
        private StackPanel _layerListStack;
        private StackPanel _propertyInspectorStack;
        private Dictionary<ToolType, Button> _toolButtons;
        private StackPanel _booleanToolbar;
        private DispatcherTimer _autosaveTimer;

        private sealed class AnimationRestPose
        {
            public double X, Y, Width, Height, Rotation, Opacity;
            public double CornerRadius, CornerRadiusTopLeft, CornerRadiusTopRight;
            public double CornerRadiusBottomRight, CornerRadiusBottomLeft;
            public VectorGeometry Geometry;
        }

        private readonly Dictionary<LayerNode, AnimationRestPose> _animationRestPose = new Dictionary<LayerNode, AnimationRestPose>();
        private readonly Dictionary<uint, LayerNode> _animationNodesById = new Dictionary<uint, LayerNode>();
        private DateTime _savedStatusExpires = DateTime.MinValue;

        // Layer Panel State
        private TextBlock _layerCountBlock;
        private TextBox _layerSearchBox;
        private string _layerSearchText = "";
        private string _layerFilterType = "all";
        private string _renamingLayerId = null;
        private string _renamingDraftName;
        private TextBox _renameTextBox;
        private bool _renameFocusReady;
        private bool _isFinishingRename;
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
        private List<LayerNode> _altOriginalLayers = new List<LayerNode>();
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
            History.HistoryChanged += (s, e) => UpdateSaveStatus();
            _toolButtons = new Dictionary<ToolType, Button>();
            InitializeStudioUI();
            InitializeAutosave();
        }

        public void LoadDocument(KerfDocument doc, string filePath)
        {
            RestoreAnimationRestPose();
            if (_animationTimer != null) _animationTimer.Stop();
            _timelineController = null;
            _animationRestPose.Clear();
            _animationNodesById.Clear();
            _lastHoverNodeId = 0;
            _stateMachinePointerDownNodeId = 0;
            _isStateMachineMode = true;
            if (_timelineTracksContainer != null) _timelineTracksContainer.Visibility = Visibility.Collapsed;
            if (_stateMachineScrollContainer != null) _stateMachineScrollContainer.Visibility = Visibility.Visible;
            Document = doc ?? new KerfDocument();
            CurrentFilePath = filePath;
            SanitizeAnimationClips(Document);
            History.Clear();
            _selectedLayers.Clear();

            _titleBlock.Text = Document.Name;
            _titleTextBox.Text = Document.Name;
            UpdateSaveStatus();

            var page = Document.GetActivePage();
            _sceneGraph.Synchronize(page);
            RefreshCanvas();
            RefreshLayersList();
            RefreshPropertyInspector();
            if (_currentStudioMode == StudioMode.Animate)
            {
                EnsureTimelineControllerInitialized();
                CaptureAnimationRestPose();
                BuildStateMachineInputsControls();
                BuildStateMachineStatesGraph();
                RefreshTimelineUI();
            }
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
            Background = KerfBrand.ObsidianBrush; // #1E082E

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

        #region Top Bar & Desktop Menu

        private Menu CreateDesktopMenuBar()
        {
            var menu = new Menu
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            menu.Resources[SystemColors.MenuBrushKey] = Brushes.White;
            menu.Resources[SystemColors.MenuTextBrushKey] = Brushes.Black;
            menu.Resources[SystemColors.HighlightBrushKey] = KerfBrand.MintBrush;
            menu.Resources[SystemColors.HighlightTextBrushKey] = Brushes.Black;

            // 1. File Menu
            var fileMenu = CreateTopMenuItem("_File");
            fileMenu.Items.Add(CreateSubMenuItem("_New Project", "Ctrl+N", () => CreateNewDocumentPrompt()));
            fileMenu.Items.Add(CreateSubMenuItem("_Open Project...", "Ctrl+O", () => OpenProjectFileDialog()));
            fileMenu.Items.Add(CreateSubMenuItem("_Save", "Ctrl+S", () => SaveCurrentDocument()));
            fileMenu.Items.Add(CreateSubMenuItem("Save _As Anywhere...", "Ctrl+Shift+S", () => SaveAsCurrentDocument()));
            fileMenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(4, 2, 4, 2) });
            fileMenu.Items.Add(CreateSubMenuItem("_Export Document...", "Ctrl+E", () => ShowExportDialog()));
            fileMenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(4, 2, 4, 2) });

            var recentMenu = new MenuItem
            {
                Header = "_Recent Projects",
                Foreground = Brushes.Black,
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 6, 16, 6),
                Cursor = Cursors.Hand,
                FontSize = 12.0
            };
            recentMenu.Resources[SystemColors.MenuBrushKey] = Brushes.White;
            recentMenu.Resources[SystemColors.MenuTextBrushKey] = Brushes.Black;
            recentMenu.Resources[SystemColors.HighlightBrushKey] = KerfBrand.MintBrush;
            recentMenu.Resources[SystemColors.HighlightTextBrushKey] = Brushes.Black;
            recentMenu.MouseEnter += (s, e) => { recentMenu.Background = KerfBrand.MintBrush; recentMenu.Foreground = Brushes.Black; };
            recentMenu.MouseLeave += (s, e) => { recentMenu.Background = Brushes.White; recentMenu.Foreground = Brushes.Black; };
            recentMenu.SubmenuOpened += (s, e) => PopulateRecentProjectsMenu(recentMenu);
            fileMenu.Items.Add(recentMenu);

            fileMenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(4, 2, 4, 2) });
            fileMenu.Items.Add(CreateSubMenuItem("_Home (Back to Projects)", null, () => { if (RequestHomeNavigation != null) RequestHomeNavigation(); }));
            fileMenu.Items.Add(CreateSubMenuItem("E_xit", "Alt+F4", () =>
            {
                var win = Window.GetWindow(this);
                if (win != null) win.Close();
            }));
            menu.Items.Add(fileMenu);

            // 2. Edit Menu
            var editMenu = CreateTopMenuItem("_Edit");
            editMenu.Items.Add(CreateSubMenuItem("_Undo", "Ctrl+Z", () => Undo()));
            editMenu.Items.Add(CreateSubMenuItem("_Redo", "Ctrl+Y", () => Redo()));
            editMenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(4, 2, 4, 2) });
            editMenu.Items.Add(CreateSubMenuItem("Cu_t", "Ctrl+X", () => CutSelectedLayers()));
            editMenu.Items.Add(CreateSubMenuItem("_Copy", "Ctrl+C", () => CopySelectedLayers()));
            editMenu.Items.Add(CreateSubMenuItem("_Paste", "Ctrl+V", () => PasteLayers()));
            editMenu.Items.Add(CreateSubMenuItem("_Duplicate", "Ctrl+D", () => DuplicateSelectedLayers()));
            editMenu.Items.Add(CreateSubMenuItem("_Delete", "Del", () => DeleteSelectedLayers()));
            editMenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(4, 2, 4, 2) });
            editMenu.Items.Add(CreateSubMenuItem("Select _All", "Ctrl+A", () => SelectAllLayers()));
            editMenu.Items.Add(CreateSubMenuItem("Desel_ect All", "Esc", () => DeselectAllLayers()));
            menu.Items.Add(editMenu);

            // 3. View Menu
            var viewMenu = CreateTopMenuItem("_View");
            viewMenu.Items.Add(CreateSubMenuItem("Zoom _In", "Ctrl++", () => SetZoom(_zoomScale * 1.25)));
            viewMenu.Items.Add(CreateSubMenuItem("Zoom _Out", "Ctrl+-", () => SetZoom(_zoomScale * 0.8)));
            viewMenu.Items.Add(CreateSubMenuItem("_100% Zoom", "Ctrl+0", () => ResetZoom()));
            viewMenu.Items.Add(CreateSubMenuItem("Zoom to _Fit", "Shift+1", () => ZoomToFit()));
            viewMenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(4, 2, 4, 2) });
            viewMenu.Items.Add(CreateSubMenuItem("Toggle _Grid", "Ctrl+'", () =>
            {
                var page = Document != null ? Document.GetActivePage() : null;
                if (page != null)
                {
                    page.ShowGrid = !page.ShowGrid;
                    RefreshCanvas();
                }
            }));
            menu.Items.Add(viewMenu);

            // 4. Window Menu
            var windowMenu = CreateTopMenuItem("_Window");
            windowMenu.Items.Add(CreateSubMenuItem("_Design Workspace", "Alt+1", () => SwitchStudioMode(StudioMode.Design)));
            windowMenu.Items.Add(CreateSubMenuItem("_Animate Workspace", "Alt+2", () => SwitchStudioMode(StudioMode.Animate)));
            windowMenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(4, 2, 4, 2) });
            windowMenu.Items.Add(CreateSubMenuItem("_Layers Panel", null, () => SwitchLeftSidebarTab(false)));
            windowMenu.Items.Add(CreateSubMenuItem("_Assets Panel", null, () => SwitchLeftSidebarTab(true)));
            menu.Items.Add(windowMenu);

            // 5. Help Menu
            var helpMenu = CreateTopMenuItem("_Help");
            helpMenu.Items.Add(CreateSubMenuItem("_Keyboard Shortcuts", "F1", () => ShowShortcutsDialog()));
            helpMenu.Items.Add(CreateSubMenuItem("_About Kerf Studio...", null, () => ShowAboutDialog()));
            menu.Items.Add(helpMenu);

            return menu;
        }

        private MenuItem CreateTopMenuItem(string header)
        {
            var item = new MenuItem
            {
                Header = header,
                Foreground = Brushes.White,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 5, 8, 5),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.Medium,
                FontSize = 12.0
            };
            item.Resources[SystemColors.MenuBrushKey] = Brushes.White;
            item.Resources[SystemColors.MenuTextBrushKey] = Brushes.Black;
            item.Resources[SystemColors.HighlightBrushKey] = KerfBrand.MintBrush;
            item.Resources[SystemColors.HighlightTextBrushKey] = Brushes.Black;
            return item;
        }

        private MenuItem CreateSubMenuItem(string header, string gesture, Action onClick)
        {
            var item = new MenuItem
            {
                Header = header,
                InputGestureText = gesture,
                Foreground = Brushes.Black, // Crisp black text for clear readability across all options
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 6, 16, 6),
                Cursor = Cursors.Hand,
                FontSize = 12.0
            };
            item.Resources[SystemColors.MenuTextBrushKey] = Brushes.Black;
            item.MouseEnter += (s, e) =>
            {
                item.Background = KerfBrand.MintBrush;
                item.Foreground = Brushes.Black;
            };
            item.MouseLeave += (s, e) =>
            {
                item.Background = Brushes.White;
                item.Foreground = Brushes.Black;
            };
            if (onClick != null)
            {
                item.Click += (s, e) => onClick();
            }
            return item;
        }

        private void PopulateRecentProjectsMenu(MenuItem recentMenu)
        {
            recentMenu.Items.Clear();
            var recents = StorageManager.GetRecentProjects();
            if (recents == null || recents.Count == 0)
            {
                var emptyItem = new MenuItem
                {
                    Header = "No Recent Projects",
                    IsEnabled = false,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                    Background = Brushes.White
                };
                recentMenu.Items.Add(emptyItem);
                return;
            }

            int count = 0;
            foreach (var p in recents)
            {
                if (count >= 10) break;
                string path = p.FilePath;
                string name = p.Name;
                var item = CreateSubMenuItem(name, null, () => OpenRecentFile(path));
                item.ToolTip = path;
                recentMenu.Items.Add(item);
                count++;
            }
        }

        private void OpenRecentFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show("The specified file could not be found: " + path, "Open Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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
            try
            {
                var doc = StorageManager.LoadDocument(path);
                LoadDocument(doc, path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open project: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateNewDocumentPrompt()
        {
            if (Document != null && History.IsDirty)
            {
                var res = MessageBox.Show(
                    string.Format("Do you want to save changes to \"{0}\" before creating a new project?", Document.Name),
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
            LoadDocument(new KerfDocument { Name = "Untitled Project" }, null);
        }

        public void DeselectAllLayers()
        {
            _selectedLayers.Clear();
            OnSelectionChanged();
            RefreshCanvas();
        }

        private void ShowAboutDialog()
        {
            MessageBox.Show(
                "Kerf Studio v3.0.0\nOfficial Brand Edition\n\nHigh-Performance Windows Desktop Vector & Animation Studio.\n\n• Direct3D 11 Retained Scene Graph\n• SIMD Rust Native Acceleration\n• Real-Time Vector State Machine Engine\n• Figma Auto Layout Flexbox Engine\n\n© Kerf Studio. All rights reserved.",
                "About Kerf Studio",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ShowShortcutsDialog()
        {
            MessageBox.Show(
                "Kerf Studio Keyboard Shortcuts:\n\n" +
                "• V: Select Tool\n" +
                "• H: Hand Pan Tool\n" +
                "• F: Frame Tool\n" +
                "• R: Rectangle Tool\n" +
                "• O: Ellipse Tool\n" +
                "• B: Bézier Pen Tool\n" +
                "• T: Text Tool\n" +
                "• Shift + A: Auto Layout / Auto Align\n" +
                "• Ctrl + G: Group Selection\n" +
                "• Ctrl + Alt + G: Frame Selection\n" +
                "• Ctrl + Z: Undo\n" +
                "• Ctrl + Y: Redo\n" +
                "• Ctrl + S: Save Project\n" +
                "• Ctrl + Shift + S: Save As Anywhere\n" +
                "• Ctrl + O: Open Project\n" +
                "• Ctrl + E: Export Document (PNG/SVG)\n" +
                "• Alt + 1: Design Workspace\n" +
                "• Alt + 2: Animate Workspace\n" +
                "• Del: Delete Selected\n" +
                "• Ctrl + D: Duplicate Selected\n" +
                "• Space + Drag / Middle Mouse: Pan Canvas\n" +
                "• Ctrl + MouseWheel: Centered Zoom",
                "Kerf Studio Shortcuts",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private FrameworkElement CreateTopBar()
        {
            var border = new Border
            {
                Background = KerfBrand.DarkSlateBrush, // #0C4247
                BorderBrush = KerfBrand.TealBrush,     // #0A717B
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 0, 16, 0)
            };

            var dock = new DockPanel { LastChildFill = false };

            // Left: Home/Brand button + Desktop Menu Bar + Project Name + Mode Switcher
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var homeBtn = new Button
            {
                Width = 30,
                Height = 30,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Home (Back to Projects)",
                Template = KerfBrand.CreateRoundedButtonTemplate(6),
                Margin = new Thickness(0, 0, 4, 0)
            };
            homeBtn.Content = KerfLogo.CreateIconElement(18, 22, fillBrush: KerfBrand.MintBrush, withGlow: true);
            homeBtn.Click += (s, e) =>
            {
                if (RequestHomeNavigation != null) RequestHomeNavigation();
            };
            leftStack.Children.Add(homeBtn);

            // Desktop Menu Bar: File, Edit, View, Window, Help
            var menuBar = CreateDesktopMenuBar();
            leftStack.Children.Add(menuBar);

            var sep1 = CreateSeparator();
            sep1.Margin = new Thickness(6, 0, 6, 0);
            leftStack.Children.Add(sep1);

            _titleBlock = new TextBlock
            {
                Text = "Untitled Project",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 4, 0)
            };
            _titleTextBox = new TextBox
            {
                Text = "Untitled Project",
                FontSize = 13,
                Width = 150,
                Height = 26,
                Background = KerfBrand.DarkSlateBrush,
                Foreground = KerfBrand.TextWhiteBrush,
                BorderBrush = KerfBrand.MintBrush,
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

            var sepMode = CreateSeparator();
            sepMode.Margin = new Thickness(6, 0, 6, 0);
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

            // Right: Zoom controls, Save status, Telemetry badge, Export button
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Zoom percent & reset
            var zoomMinus = CreateTextButton("-", () => SetZoom(_zoomScale * 0.8));
            _zoomPercentBlock = new TextBlock
            {
                Text = "100%",
                FontSize = 12,
                Foreground = KerfBrand.TextMutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _zoomPercentBlock.MouseLeftButtonUp += (s, e) => ResetZoom();
            var zoomPlus = CreateTextButton("+", () => SetZoom(_zoomScale * 1.25));

            rightStack.Children.Add(zoomMinus);
            rightStack.Children.Add(_zoomPercentBlock);
            rightStack.Children.Add(zoomPlus);

            _saveStatusBlock = new TextBlock
            {
                Text = "All changes saved",
                FontSize = 10,
                Foreground = KerfBrand.TextMutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0),
                ToolTip = "Project save status"
            };
            rightStack.Children.Add(_saveStatusBlock);

            var sep3 = CreateSeparator();
            rightStack.Children.Add(sep3);

            // Real-time truthful telemetry
            _telemetryBlock = new TextBlock
            {
                Text = "GPU Tier 2 (Direct3D 11) | 0 µs",
                FontSize = 11,
                Foreground = KerfBrand.MintBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 12, 0)
            };
            rightStack.Children.Add(_telemetryBlock);

            var exportBtn = KerfBrand.CreatePrimaryButton("Export", () => ShowExportDialog(), 6);
            exportBtn.Height = 28;
            exportBtn.Padding = new Thickness(14, 0, 14, 0);
            exportBtn.FontSize = 12;
            exportBtn.ToolTip = "Export Document (PNG/SVG) [Ctrl+E]";
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

        private void UpdateSaveStatus()
        {
            if (_saveStatusBlock == null) return;
            if (History != null && History.IsDirty)
            {
                _savedStatusExpires = DateTime.MinValue;
                _saveStatusBlock.Text = "Unsaved changes";
                _saveStatusBlock.Foreground = KerfBrand.AmberBrush;
            }
            else if (DateTime.Now < _savedStatusExpires)
            {
                _saveStatusBlock.Text = "Saved just now";
                _saveStatusBlock.Foreground = KerfBrand.MintBrush;
            }
            else
            {
                _saveStatusBlock.Text = "All changes saved";
                _saveStatusBlock.Foreground = KerfBrand.TextMutedBrush;
                if (_saveStatusTimer != null) _saveStatusTimer.Stop();
            }
        }

        private void ShowSavedStatus()
        {
            _savedStatusExpires = DateTime.Now.AddSeconds(3.0);
            UpdateSaveStatus();
            if (_saveStatusTimer == null)
            {
                _saveStatusTimer = new DispatcherTimer();
                _saveStatusTimer.Interval = TimeSpan.FromSeconds(1);
                _saveStatusTimer.Tick += (s, e) => UpdateSaveStatus();
            }
            if (!_saveStatusTimer.IsEnabled) _saveStatusTimer.Start();
        }

        #endregion

        #region Tool Island

        private FrameworkElement CreateToolIsland()
        {
            var border = new Border
            {
                Background = KerfBrand.FigmaPanelBrush,                                  // Authentic Figma #2C2C2C floating island
                BorderBrush = KerfBrand.FigmaBorderBrush,                                // Authentic Figma #383838 subtle divider
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6, 4, 6, 4),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 16,
                    ShadowDepth = 4,
                    Opacity = 0.55,
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
            var icon = VectorIcons.CreateIcon(iconGeo, KerfBrand.TextWhiteBrush, isFilled ? KerfBrand.TextWhiteBrush : null, 18);
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
                    ? KerfBrand.MintBrush
                    : Brushes.Transparent;

                var path = btn.Content as System.Windows.Shapes.Path;
                if (path != null)
                {
                    var brush = isActive ? (Brush)KerfBrand.TextBlackBrush : (Brush)KerfBrand.TextWhiteBrush;
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
                Background = KerfBrand.DarkSlateBrush, // #0C4247
                BorderBrush = KerfBrand.TealBrush,     // #0A717B
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
                Background = KerfBrand.MintBrush,
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            var layersTabTextStack = new StackPanel { Orientation = Orientation.Horizontal };
            var layersTitle = new TextBlock { Text = "Layers", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = KerfBrand.DarkSlateBrush };
            _layerCountBlock = new TextBlock { Text = " (0)", FontSize = 11, Foreground = KerfBrand.DarkSlateBrush, Margin = new Thickness(2, 0, 0, 0) };
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
            var assetsTitle = new TextBlock { Text = "Assets", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = KerfBrand.TextWhiteBrush };
            _assetsCountBlock = new TextBlock { Text = " (0)", FontSize = 11, Foreground = KerfBrand.TextMutedBrush, Margin = new Thickness(2, 0, 0, 0) };
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
            layersGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Animate interpolation

            var filterStack = new StackPanel { Margin = new Thickness(10, 0, 10, 8) };
            var searchBorder = new Border
            {
                Background = KerfBrand.DarkSlateBrush,
                BorderBrush = KerfBrand.TealBrush,
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

            _defaultInterpolationPanel = CreateDefaultInterpolationSidebarPanel();
            _defaultInterpolationPanel.Visibility = Visibility.Collapsed;
            Grid.SetRow(_defaultInterpolationPanel, 2);
            layersGrid.Children.Add(_defaultInterpolationPanel);

            _layersViewContainer = layersGrid;
            contentHost.Children.Add(_layersViewContainer);

            // B. Assets / Components View Container
            var assetsGrid = new Grid();
            assetsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search / Create bar
            assetsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List

            var assetsFilterStack = new StackPanel { Margin = new Thickness(10, 0, 10, 8) };
            var assetsSearchBorder = new Border
            {
                Background = KerfBrand.DarkSlateBrush,
                BorderBrush = KerfBrand.TealBrush,
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
                Background = new SolidColorBrush(Color.FromArgb(40, 0x0A, 0x71, 0x7B)),
                BorderBrush = KerfBrand.TealBrush,
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
                Foreground = KerfBrand.MintBrush,
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

        private FrameworkElement CreateDefaultInterpolationSidebarPanel()
        {
            var border = new Border
            {
                Height = 272,
                Margin = new Thickness(8, 4, 8, 8),
                Padding = new Thickness(8, 7, 8, 6),
                Background = KerfBrand.DarkSlateBrush,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });

            var title = new TextBlock
            {
                Text = "Default Interpolation",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.TextWhiteBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(title, 0);
            content.Children.Add(title);

            var presets = new Grid { Margin = new Thickness(0, 2, 0, 5), Background = KerfBrand.ObsidianBrush };
            for (int i = 0; i < _defaultInterpolationPresetButtons.Length; i++)
            {
                presets.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                int presetIndex = i;
                var button = new Border
                {
                    Margin = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Cursor = Cursors.Hand,
                    Child = CreateInterpolationPresetIcon(i)
                };
                button.MouseLeftButtonUp += (s, e) =>
                {
                    ApplyDefaultInterpolationPreset(presetIndex);
                    e.Handled = true;
                };
                _defaultInterpolationPresetButtons[i] = button;
                Grid.SetColumn(button, i);
                presets.Children.Add(button);
            }
            Grid.SetRow(presets, 1);
            content.Children.Add(presets);

            _defaultInterpolationCanvas = new Canvas
            {
                Height = 166,
                MinHeight = 120,
                Background = KerfBrand.ObsidianBrush,
                ClipToBounds = true
            };
            _defaultInterpolationCanvas.MouseLeftButtonDown += HandleDefaultInterpolationMouseDown;
            _defaultInterpolationCanvas.MouseMove += HandleDefaultInterpolationMouseMove;
            _defaultInterpolationCanvas.MouseLeftButtonUp += HandleDefaultInterpolationMouseUp;
            _defaultInterpolationCanvas.SizeChanged += (s, e) => DrawDefaultInterpolationGraph();
            var graphBorder = new Border
            {
                Background = KerfBrand.ObsidianBrush,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = _defaultInterpolationCanvas
            };
            Grid.SetRow(graphBorder, 2);
            content.Children.Add(graphBorder);

            _defaultInterpolationValues = new TextBlock
            {
                Text = "0, 0, 1, 1",
                FontSize = 10,
                Foreground = KerfBrand.MintBrush,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4, 4, 0, 0)
            };
            Grid.SetRow(_defaultInterpolationValues, 3);
            content.Children.Add(_defaultInterpolationValues);

            _applyDefaultInterpolationButton = new Button
            {
                Content = "Apply to selected transitions",
                Height = 22,
                Margin = new Thickness(0, 3, 0, 0),
                Padding = new Thickness(6, 1, 6, 1),
                Background = KerfBrand.TealBrush,
                Foreground = KerfBrand.TextWhiteBrush,
                BorderBrush = KerfBrand.MintBrush,
                FontSize = 9.5,
                Cursor = Cursors.Hand
            };
            _applyDefaultInterpolationButton.Click += (s, e) => ApplyDefaultInterpolationToSelection();
            Grid.SetRow(_applyDefaultInterpolationButton, 4);
            content.Children.Add(_applyDefaultInterpolationButton);
            border.Child = content;
            RefreshDefaultInterpolationPresetButtons();
            return border;
        }

        private FrameworkElement CreateInterpolationPresetIcon(int preset)
        {
            var icon = new Canvas { Width = 24, Height = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            var figure = new PathFigure { StartPoint = new Point(2, 17), IsClosed = false };
            if (preset == 0)
            {
                figure.Segments.Add(new LineSegment(new Point(12, 17), true));
                figure.Segments.Add(new LineSegment(new Point(12, 3), true));
                figure.Segments.Add(new LineSegment(new Point(22, 3), true));
            }
            else if (preset == 1)
            {
                figure.Segments.Add(new LineSegment(new Point(22, 3), true));
            }
            else
            {
                Point c1 = preset == 2 ? new Point(10, 17) : preset == 3 ? new Point(5, 3) : new Point(3, 17);
                Point c2 = preset == 2 ? new Point(14, 3) : preset == 3 ? new Point(13, 3) : new Point(21, 3);
                figure.Segments.Add(new BezierSegment(c1, c2, new Point(22, 3), true));
            }
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            icon.Children.Add(new System.Windows.Shapes.Path { Data = geometry, Stroke = new SolidColorBrush(Color.FromRgb(190, 197, 208)), StrokeThickness = 1.2, Fill = Brushes.Transparent });
            return icon;
        }

        private void ApplyDefaultInterpolationPreset(int preset)
        {
            _defaultInterpolationPreset = Math.Max(0, Math.Min(4, preset));
            switch (_defaultInterpolationPreset)
            {
                case 0:
                    _defaultInterpolationMode = InterpMode.Hold;
                    _defaultInterpolationX1 = 0.0; _defaultInterpolationY1 = 0.0; _defaultInterpolationX2 = 1.0; _defaultInterpolationY2 = 1.0;
                    break;
                case 1:
                    _defaultInterpolationMode = InterpMode.Linear;
                    _defaultInterpolationX1 = 0.0; _defaultInterpolationY1 = 0.0; _defaultInterpolationX2 = 1.0; _defaultInterpolationY2 = 1.0;
                    break;
                case 2:
                    _defaultInterpolationMode = InterpMode.Cubic;
                    _defaultInterpolationX1 = 0.42; _defaultInterpolationY1 = 0.0; _defaultInterpolationX2 = 0.58; _defaultInterpolationY2 = 1.0;
                    break;
                case 3:
                    _defaultInterpolationMode = InterpMode.Cubic;
                    _defaultInterpolationX1 = 0.0; _defaultInterpolationY1 = 0.0; _defaultInterpolationX2 = 0.58; _defaultInterpolationY2 = 1.0;
                    break;
                default:
                    _defaultInterpolationMode = InterpMode.Cubic;
                    _defaultInterpolationX1 = 0.25; _defaultInterpolationY1 = 0.1; _defaultInterpolationX2 = 0.25; _defaultInterpolationY2 = 1.0;
                    break;
            }
            RefreshDefaultInterpolationPresetButtons();
            DrawDefaultInterpolationGraph();
        }

        private void ApplyDefaultInterpolationToSelection()
        {
            bool applied = false;
            var keyframes = new HashSet<Keyframe>(_selectedTimelineKeyframes);
            if (_editingInterpolationKeyframe != null) keyframes.Add(_editingInterpolationKeyframe);
            foreach (var keyframe in keyframes)
            {
                keyframe.Interp = _defaultInterpolationMode;
                keyframe.X1 = _defaultInterpolationX1;
                keyframe.Y1 = _defaultInterpolationY1;
                keyframe.X2 = _defaultInterpolationX2;
                keyframe.Y2 = _defaultInterpolationY2;
                applied = true;
            }
            if (_selectedGraphTransition != null)
            {
                _selectedGraphTransition.Interpolation = _defaultInterpolationMode;
                _selectedGraphTransition.X1 = _defaultInterpolationX1;
                _selectedGraphTransition.Y1 = _defaultInterpolationY1;
                _selectedGraphTransition.X2 = _defaultInterpolationX2;
                _selectedGraphTransition.Y2 = _defaultInterpolationY2;
                applied = true;
            }
            if (!applied) return;
            if (History != null) History.MarkDirty();
            RefreshInterpolationGraphPanels();
            RefreshTimelineCanvas();
            if (_selectedGraphTransition != null) BuildStateMachineStatesGraph();
            TriggerStateMachineStep();
        }

        private void RefreshDefaultInterpolationPresetButtons()
        {
            if (_applyDefaultInterpolationButton != null)
                _applyDefaultInterpolationButton.IsEnabled = _editingInterpolationKeyframe != null || _selectedTimelineKeyframes.Count > 0 || _selectedGraphTransition != null;
            for (int i = 0; i < _defaultInterpolationPresetButtons.Length; i++)
            {
                var button = _defaultInterpolationPresetButtons[i];
                if (button == null) continue;
                bool selected = i == _defaultInterpolationPreset;
                button.Background = selected ? KerfBrand.TealBrush : Brushes.Transparent;
                button.BorderBrush = selected ? KerfBrand.MintBrush : Brushes.Transparent;
                button.BorderThickness = new Thickness(selected ? 1 : 0);
            }
        }

        private void DrawDefaultInterpolationGraph()
        {
            var canvas = _defaultInterpolationCanvas;
            if (canvas == null) return;
            canvas.Children.Clear();
            double width = Math.Max(130.0, canvas.ActualWidth > 20.0 ? canvas.ActualWidth : 210.0);
            double height = Math.Max(100.0, canvas.ActualHeight > 20.0 ? canvas.ActualHeight : 166.0);
            double left = 8.0, right = width - 8.0, top = 8.0, bottom = height - 8.0;
            var gridBrush = KerfBrand.DarkSlateBrush;
            for (int i = 1; i < 4; i++)
            {
                double gx = left + (right - left) * i / 4.0;
                double gy = top + (bottom - top) * i / 4.0;
                canvas.Children.Add(new Line { X1 = gx, Y1 = top, X2 = gx, Y2 = bottom, Stroke = gridBrush, StrokeThickness = 0.7 });
                canvas.Children.Add(new Line { X1 = left, Y1 = gy, X2 = right, Y2 = gy, Stroke = gridBrush, StrokeThickness = 0.7 });
            }
            canvas.Children.Add(new Line { X1 = left, Y1 = bottom, X2 = right, Y2 = bottom, Stroke = KerfBrand.TealBrush, StrokeThickness = 1 });
            canvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = bottom, Stroke = KerfBrand.TealBrush, StrokeThickness = 1 });

            Point start = new Point(left, bottom);
            Point end = new Point(right, top);
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            if (_defaultInterpolationMode == InterpMode.Hold)
            {
                figure.Segments.Add(new LineSegment(new Point(right, bottom), true));
                figure.Segments.Add(new LineSegment(end, true));
            }
            else if (_defaultInterpolationMode == InterpMode.Linear)
            {
                figure.Segments.Add(new LineSegment(end, true));
            }
            else
            {
                Point c1 = new Point(left + _defaultInterpolationX1 * (right - left), bottom - _defaultInterpolationY1 * (bottom - top));
                Point c2 = new Point(left + _defaultInterpolationX2 * (right - left), bottom - _defaultInterpolationY2 * (bottom - top));
                figure.Segments.Add(new BezierSegment(c1, c2, end, true));
                canvas.Children.Add(new Line { X1 = start.X, Y1 = start.Y, X2 = c1.X, Y2 = c1.Y, Stroke = KerfBrand.TealBrush, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 2, 2 }, IsHitTestVisible = false });
                canvas.Children.Add(new Line { X1 = end.X, Y1 = end.Y, X2 = c2.X, Y2 = c2.Y, Stroke = KerfBrand.TealBrush, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 2, 2 }, IsHitTestVisible = false });
                AddDefaultInterpolationHandle(canvas, c1, 1);
                AddDefaultInterpolationHandle(canvas, c2, 2);
            }
            var curve = new PathGeometry();
            curve.Figures.Add(figure);
            var gradient = new LinearGradientBrush(KerfBrand.Mint, KerfBrand.Teal, 0.0);
            canvas.Children.Add(new System.Windows.Shapes.Path { Data = curve, Stroke = gradient, StrokeThickness = 1.5, Fill = Brushes.Transparent, IsHitTestVisible = false });
            if (_defaultInterpolationValues != null)
                _defaultInterpolationValues.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.##}, {1:0.##}, {2:0.##}, {3:0.##}", _defaultInterpolationX1, _defaultInterpolationY1, _defaultInterpolationX2, _defaultInterpolationY2);
        }

        private static void AddDefaultInterpolationHandle(Canvas canvas, Point point, int id)
        {
            var handle = new Ellipse { Width = 7, Height = 7, Fill = KerfBrand.MintBrush, Stroke = Brushes.White, StrokeThickness = 1, Tag = id, IsHitTestVisible = false };
            Canvas.SetLeft(handle, point.X - 3.5);
            Canvas.SetTop(handle, point.Y - 3.5);
            canvas.Children.Add(handle);
        }

        private void HandleDefaultInterpolationMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_defaultInterpolationMode != InterpMode.Cubic || _defaultInterpolationCanvas == null) return;
            Point point = e.GetPosition(_defaultInterpolationCanvas);
            double width = Math.Max(130.0, _defaultInterpolationCanvas.ActualWidth);
            double height = Math.Max(100.0, _defaultInterpolationCanvas.ActualHeight);
            double left = 8.0, right = width - 8.0, top = 8.0, bottom = height - 8.0;
            Point c1 = new Point(left + _defaultInterpolationX1 * (right - left), bottom - _defaultInterpolationY1 * (bottom - top));
            Point c2 = new Point(left + _defaultInterpolationX2 * (right - left), bottom - _defaultInterpolationY2 * (bottom - top));
            if ((point - c1).Length > 15.0 && (point - c2).Length > 15.0) return;
            _defaultInterpolationDragHandle = (point - c1).Length < (point - c2).Length ? 1 : 2;
            _defaultInterpolationCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void HandleDefaultInterpolationMouseMove(object sender, MouseEventArgs e)
        {
            if (_defaultInterpolationDragHandle < 0 || _defaultInterpolationCanvas == null || !_defaultInterpolationCanvas.IsMouseCaptured) return;
            Point point = e.GetPosition(_defaultInterpolationCanvas);
            double width = Math.Max(130.0, _defaultInterpolationCanvas.ActualWidth);
            double height = Math.Max(100.0, _defaultInterpolationCanvas.ActualHeight);
            double left = 8.0, right = width - 8.0, top = 8.0, bottom = height - 8.0;
            double x = Math.Max(0.0, Math.Min(1.0, (point.X - left) / Math.Max(1.0, right - left)));
            double y = Math.Max(0.0, Math.Min(1.0, (bottom - point.Y) / Math.Max(1.0, bottom - top)));
            if (_defaultInterpolationDragHandle == 1) { _defaultInterpolationX1 = x; _defaultInterpolationY1 = y; }
            else { _defaultInterpolationX2 = x; _defaultInterpolationY2 = y; }
            _defaultInterpolationMode = InterpMode.Cubic;
            _defaultInterpolationPreset = 4;
            RefreshDefaultInterpolationPresetButtons();
            DrawDefaultInterpolationGraph();
            e.Handled = true;
        }

        private void HandleDefaultInterpolationMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_defaultInterpolationDragHandle < 0) return;
            _defaultInterpolationDragHandle = -1;
            if (_defaultInterpolationCanvas != null && _defaultInterpolationCanvas.IsMouseCaptured) _defaultInterpolationCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void SwitchLeftSidebarTab(bool showAssets)
        {
            _isAssetsTabActive = showAssets;
            if (_layersTabBtn != null)
            {
                _layersTabBtn.Background = !showAssets ? KerfBrand.MintBrush : Brushes.Transparent;
                var sp = _layersTabBtn.Child as StackPanel;
                if (sp != null && sp.Children.Count > 0)
                {
                    var tb = sp.Children[0] as TextBlock;
                    if (tb != null) tb.Foreground = !showAssets ? (Brush)KerfBrand.DarkSlateBrush : (Brush)KerfBrand.TextWhiteBrush;
                }
            }
            if (_assetsTabBtn != null)
            {
                _assetsTabBtn.Background = showAssets ? KerfBrand.MintBrush : Brushes.Transparent;
                var sp = _assetsTabBtn.Child as StackPanel;
                if (sp != null && sp.Children.Count > 0)
                {
                    var tb = sp.Children[0] as TextBlock;
                    if (tb != null) tb.Foreground = showAssets ? (Brush)KerfBrand.DarkSlateBrush : (Brush)KerfBrand.TextWhiteBrush;
                }
            }
            if (_layersViewContainer != null)
            {
                _layersViewContainer.Visibility = !showAssets ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_assetsViewContainer != null)
            {
                _assetsViewContainer.Visibility = showAssets ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_defaultInterpolationPanel != null)
                _defaultInterpolationPanel.Visibility = !showAssets && _currentStudioMode == StudioMode.Animate ? Visibility.Visible : Visibility.Collapsed;
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
                btn.Background = active ? KerfBrand.MintBrush : Brushes.Transparent;
                btn.Foreground = active ? (Brush)KerfBrand.DarkSlateBrush : (Brush)KerfBrand.TextMutedBrush;
                btn.BorderBrush = active ? KerfBrand.MintBrush : KerfBrand.TealBrush;
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
                if (layer.IsMasterComponent) return KerfBrand.MintBrush;
                if (layer.Type == ElementType.ComponentInstance || !string.IsNullOrEmpty(layer.ComponentId)) return KerfBrand.TealBrush;
                return GetBadgeBrushForLayerType(layer.Type);
            }
            return KerfBrand.DarkSlateBrush;
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
                case ElementType.Frame: return KerfBrand.MintBrush;
                case ElementType.Group: return KerfBrand.AmberBrush;
                case ElementType.Text: return KerfBrand.MintBrush;
                case ElementType.Path: return KerfBrand.TealBrush;
                case ElementType.Star:
                case ElementType.Polygon: return KerfBrand.AmberBrush;
                default: return KerfBrand.DarkSlateBrush;
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
                    Foreground = KerfBrand.MintBrush,
                    Margin = new Thickness(0, 0, 0, 6)
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(search)
                        ? "Select any layer or frame and press Ctrl+Alt+K (or right-click → Create Component) to create reusable master components."
                        : "No components match your search filter.",
                    FontSize = 11,
                    Foreground = KerfBrand.TextMutedBrush,
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
                Background = KerfBrand.DarkSlateBrush,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel { LastChildFill = true };

            // Left: Component Glyph (❖)
            var icon = VectorIcons.CreateIcon(VectorIcons.ComponentGeometry, KerfBrand.MintBrush, null, 14, 1.4);
            icon.Margin = new Thickness(0, 2, 8, 0);
            DockPanel.SetDock(icon, Dock.Left);
            dock.Children.Add(icon);

            // Right: Insert Button
            var insertBtn = new Border
            {
                Background = KerfBrand.MintBrush,
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
                Foreground = KerfBrand.TextBlackBrush
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
                card.Background = KerfBrand.FigmaBorderBrush;
                card.BorderBrush = KerfBrand.MintBrush;
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = KerfBrand.FigmaPanelBrush;
                card.BorderBrush = KerfBrand.FigmaBorderBrush;
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
            // Keep the active editor instance alive while the layer list is refreshed
            // by selection/property updates during a rename.
            if (_renamingLayerId != null && _renameTextBox != null && !_isFinishingRename)
            {
                _renamingDraftName = _renameTextBox.Text;
                return;
            }
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
                    ? KerfBrand.TealBrush
                    : KerfBrand.DarkSlateBrush,
                BorderBrush = isSelected
                    ? KerfBrand.MintBrush
                    : KerfBrand.TealBrush,
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
                    Text = string.IsNullOrEmpty(_renamingDraftName) ? layer.Name : _renamingDraftName,
                    FontSize = 11,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromRgb(28, 34, 52)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(2, 1, 2, 1),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                _renameTextBox = editBox;
                editBox.Loaded += (s, e) =>
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        if (_renamingLayerId != layer.Id || !ReferenceEquals(_renameTextBox, editBox)) return;
                        editBox.Focus();
                        Keyboard.Focus(editBox);
                        editBox.SelectAll();
                        _renameFocusReady = true;
                    }));
                };
                editBox.TextChanged += (s, e) =>
                {
                    if (ReferenceEquals(_renameTextBox, editBox)) _renamingDraftName = editBox.Text;
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
                        _renamingDraftName = null;
                        _renameTextBox = null;
                        _renameFocusReady = false;
                        RefreshLayersList();
                        e.Handled = true;
                    }
                };
                editBox.LostKeyboardFocus += (s, e) =>
                {
                    if (!_renameFocusReady || _isFinishingRename || !ReferenceEquals(_renameTextBox, editBox)) return;
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        if (_renamingLayerId == layer.Id && ReferenceEquals(_renameTextBox, editBox) && !editBox.IsKeyboardFocusWithin)
                            FinishRename(layer.Id, editBox.Text);
                    }));
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
                Background = isComp ? new SolidColorBrush(Color.FromArgb(60, 0x46, 0xEF, 0xB1)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
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
                Foreground = isComp ? KerfBrand.MintBrush : KerfBrand.TextMutedBrush
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
                if (_renamingLayerId == layer.Id) { e.Handled = true; return; }
                Focus();
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
                if (_renamingLayerId == layer.Id) return;
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

            cm.Items.Add(CreateContextMenuItem("Add vertical guide here", () => AddLayoutGuide(true, canvasPt)));
            cm.Items.Add(CreateContextMenuItem("Add horizontal guide here", () => AddLayoutGuide(false, canvasPt)));
            var showGuides = new MenuItem { Header = "Show layout guides", IsCheckable = true, IsChecked = Document != null && Document.GetActivePage() != null && Document.GetActivePage().ShowGuides };
            showGuides.Click += (s, e) =>
            {
                var page = Document != null ? Document.GetActivePage() : null;
                if (page == null) return;
                page.ShowGuides = showGuides.IsChecked;
                History.MarkDirty();
                RefreshCanvas();
            };
            cm.Items.Add(showGuides);
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
                Background = KerfBrand.DarkSlateBrush, // #0C4247
                BorderBrush = KerfBrand.TealBrush,     // #0A717B
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
                _propertyInspectorStack.Children.Add(CreateInspectorSection("Layout guides"));
                _propertyInspectorStack.Children.Add(BuildLayoutGuidesSection(page));
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
                    Background = KerfBrand.FigmaCardBrush,
                    BorderBrush = KerfBrand.FigmaBorderBrush,
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
                    Foreground = KerfBrand.MintBrush,
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
                    Background = KerfBrand.FigmaCardBrush,
                    BorderBrush = KerfBrand.FigmaBorderBrush,
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
                    Foreground = KerfBrand.MintBrush,
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

            var xBox = CreateAnimatedPropertyField(ScrubControls.CreateScrubField("X", primary.X, val => { primary.X = val; InvalidateAndRefresh(primary); AutoKeyProperty(primary, PropKey.X, primary.X); }), primary, PropKey.X);
            var yBox = CreateAnimatedPropertyField(ScrubControls.CreateScrubField("Y", primary.Y, val => { primary.Y = val; InvalidateAndRefresh(primary); AutoKeyProperty(primary, PropKey.Y, primary.Y); }), primary, PropKey.Y);
            Grid.SetColumn(xBox, 0); Grid.SetColumn(yBox, 2);
            row1.Children.Add(xBox); row1.Children.Add(yBox);
            _propertyInspectorStack.Children.Add(row1);

            var row2 = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var wBox = ScrubControls.CreateScrubField("W", primary.Width, val =>
            {
                double oldW = Math.Max(0.001, primary.Width);
                double oldH = Math.Max(0.001, primary.Height);
                primary.Width = Math.Max(1, val);
                if (primary.LockAspectRatio)
                {
                    primary.Height = Math.Max(1, Math.Round(primary.Width * (oldH / oldW), 1));
                    AutoKeyProperty(primary, PropKey.Height, primary.Height);
                }
                SetParentAutoLayoutSizingFixed(primary, true);
                if (primary.AutoLayout != null && primary.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    primary.AutoLayout.SizingH = SizingMode.Fixed;
                    if (primary.LockAspectRatio) primary.AutoLayout.SizingV = SizingMode.Fixed;
                    AutoLayoutEngine.ReflowRecursive(primary);
                }
                AutoKeyProperty(primary, PropKey.Width, primary.Width);
                InvalidateAndRefresh(primary);
                if (primary.LockAspectRatio) RefreshPropertyInspector();
            }, min: 1);

            var lockBtn = new Border
            {
                Width = 20,
                Height = 24,
                Margin = new Thickness(2, 0, 2, 0),
                Background = primary.LockAspectRatio ? new SolidColorBrush(Color.FromArgb(45, 0x46, 0xEF, 0xB1)) : Brushes.Transparent,
                BorderBrush = primary.LockAspectRatio ? KerfBrand.MintBrush : KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                ToolTip = primary.LockAspectRatio ? "Uniform scale: Locked (Click to unlock aspect ratio)" : "Uniform scale: Unlocked (Click to lock aspect ratio)",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var lockText = new TextBlock
            {
                Text = primary.LockAspectRatio ? "🔗" : "🔓",
                FontSize = 10,
                Foreground = primary.LockAspectRatio ? KerfBrand.MintBrush : KerfBrand.TextMutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            lockBtn.Child = lockText;
            lockBtn.MouseLeftButtonUp += (s, e) =>
            {
                primary.LockAspectRatio = !primary.LockAspectRatio;
                InvalidateAndRefresh(primary);
                RefreshPropertyInspector();
                History.MarkDirty();
            };

            var hBox = ScrubControls.CreateScrubField("H", primary.Height, val =>
            {
                double oldW = Math.Max(0.001, primary.Width);
                double oldH = Math.Max(0.001, primary.Height);
                primary.Height = Math.Max(1, val);
                if (primary.LockAspectRatio)
                {
                    primary.Width = Math.Max(1, Math.Round(primary.Height * (oldW / oldH), 1));
                    AutoKeyProperty(primary, PropKey.Width, primary.Width);
                }
                SetParentAutoLayoutSizingFixed(primary, false);
                if (primary.AutoLayout != null && primary.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    primary.AutoLayout.SizingV = SizingMode.Fixed;
                    if (primary.LockAspectRatio) primary.AutoLayout.SizingH = SizingMode.Fixed;
                    AutoLayoutEngine.ReflowRecursive(primary);
                }
                AutoKeyProperty(primary, PropKey.Height, primary.Height);
                InvalidateAndRefresh(primary);
                if (primary.LockAspectRatio) RefreshPropertyInspector();
            }, min: 1);
            wBox = CreateAnimatedPropertyField(wBox, primary, PropKey.Width);
            hBox = CreateAnimatedPropertyField(hBox, primary, PropKey.Height);
            Grid.SetColumn(wBox, 0); Grid.SetColumn(lockBtn, 1); Grid.SetColumn(hBox, 2);
            row2.Children.Add(wBox); row2.Children.Add(lockBtn); row2.Children.Add(hBox);
            _propertyInspectorStack.Children.Add(row2);

            var row3 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rotBox = CreateAnimatedPropertyField(ScrubControls.CreateScrubField("∠", primary.Rotation, val => { primary.Rotation = val; InvalidateAndRefresh(primary); AutoKeyProperty(primary, PropKey.Rotation, primary.Rotation); }, suffix: "°"), primary, PropKey.Rotation);
            var crBox = CreateAnimatedPropertyField(ScrubControls.CreateScrubField("Radius", primary.CornerRadius, val => { primary.CornerRadius = Math.Max(0, val); InvalidateAndRefresh(primary); AutoKeyProperty(primary, PropKey.CornerRadius, primary.CornerRadius); }, min: 0, suffix: "px"), primary, PropKey.CornerRadius);
            Grid.SetColumn(rotBox, 0); Grid.SetColumn(crBox, 2);
            row3.Children.Add(rotBox); row3.Children.Add(crBox);
            _propertyInspectorStack.Children.Add(row3);

            if (_currentStudioMode == StudioMode.Animate)
            {
                var scaleRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                scaleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                scaleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                scaleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                double currentScaleX = primary.RenderTransform != null ? primary.RenderTransform.ScaleX : 1.0;
                double currentScaleY = primary.RenderTransform != null ? primary.RenderTransform.ScaleY : 1.0;

                var sxBox = CreateAnimatedPropertyField(ScrubControls.CreateScrubField("ScaleX", currentScaleX, val =>
                {
                    if (primary.RenderTransform == null) primary.RenderTransform = new RenderTransform();
                    double oldSx = Math.Max(0.001, primary.RenderTransform.ScaleX);
                    double oldSy = Math.Max(0.001, primary.RenderTransform.ScaleY);
                    primary.RenderTransform.ScaleX = val;
                    AutoKeyProperty(primary, PropKey.ScaleX, val);
                    if (primary.LockAspectRatio)
                    {
                        double ratio = oldSy / oldSx;
                        primary.RenderTransform.ScaleY = Math.Max(0.01, Math.Round(val * ratio, 3));
                        AutoKeyProperty(primary, PropKey.ScaleY, primary.RenderTransform.ScaleY);
                    }
                    InvalidateAndRefresh(primary);
                    if (primary.LockAspectRatio) RefreshPropertyInspector();
                }, min: 0.01, max: 100.0, step: 0.05), primary, PropKey.ScaleX);

                var scaleLockBtn = new Border
                {
                    Width = 20,
                    Height = 24,
                    Margin = new Thickness(2, 0, 2, 0),
                    Background = primary.LockAspectRatio ? new SolidColorBrush(Color.FromArgb(45, 0x46, 0xEF, 0xB1)) : Brushes.Transparent,
                    BorderBrush = primary.LockAspectRatio ? KerfBrand.MintBrush : KerfBrand.TealBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursors.Hand,
                    ToolTip = primary.LockAspectRatio ? "Uniform scale: Locked (Click to unlock aspect ratio)" : "Uniform scale: Unlocked (Click to lock aspect ratio)",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var scaleLockText = new TextBlock
                {
                    Text = primary.LockAspectRatio ? "🔗" : "🔓",
                    FontSize = 10,
                    Foreground = primary.LockAspectRatio ? KerfBrand.MintBrush : KerfBrand.TextMutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                scaleLockBtn.Child = scaleLockText;
                scaleLockBtn.MouseLeftButtonUp += (s, e) =>
                {
                    primary.LockAspectRatio = !primary.LockAspectRatio;
                    InvalidateAndRefresh(primary);
                    RefreshPropertyInspector();
                    History.MarkDirty();
                };

                var syBox = CreateAnimatedPropertyField(ScrubControls.CreateScrubField("ScaleY", currentScaleY, val =>
                {
                    if (primary.RenderTransform == null) primary.RenderTransform = new RenderTransform();
                    double oldSx = Math.Max(0.001, primary.RenderTransform.ScaleX);
                    double oldSy = Math.Max(0.001, primary.RenderTransform.ScaleY);
                    primary.RenderTransform.ScaleY = val;
                    AutoKeyProperty(primary, PropKey.ScaleY, val);
                    if (primary.LockAspectRatio)
                    {
                        double ratio = oldSx / oldSy;
                        primary.RenderTransform.ScaleX = Math.Max(0.01, Math.Round(val * ratio, 3));
                        AutoKeyProperty(primary, PropKey.ScaleX, primary.RenderTransform.ScaleX);
                    }
                    InvalidateAndRefresh(primary);
                    if (primary.LockAspectRatio) RefreshPropertyInspector();
                }, min: 0.01, max: 100.0, step: 0.05), primary, PropKey.ScaleY);

                Grid.SetColumn(sxBox, 0); Grid.SetColumn(scaleLockBtn, 1); Grid.SetColumn(syBox, 2);
                scaleRow.Children.Add(sxBox); scaleRow.Children.Add(scaleLockBtn); scaleRow.Children.Add(syBox);
                _propertyInspectorStack.Children.Add(scaleRow);

                var anchorRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                anchorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                anchorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                anchorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                double currentAnchorX = primary.RenderTransform != null ? primary.RenderTransform.AnchorX : 0.0;
                double currentAnchorY = primary.RenderTransform != null ? primary.RenderTransform.AnchorY : 0.0;

                var axBox = CreateAnimatedPropertyField(ScrubControls.CreateScrubField("Anc X", currentAnchorX, val =>
                {
                    if (primary.RenderTransform == null) primary.RenderTransform = new RenderTransform();
                    primary.RenderTransform.AnchorX = val;
                    InvalidateAndRefresh(primary);
                    AutoKeyProperty(primary, PropKey.AnchorX, val);
                }, min: 0.0, max: 1.0, step: 0.05), primary, PropKey.AnchorX);

                var ayBox = CreateAnimatedPropertyField(ScrubControls.CreateScrubField("Anc Y", currentAnchorY, val =>
                {
                    if (primary.RenderTransform == null) primary.RenderTransform = new RenderTransform();
                    primary.RenderTransform.AnchorY = val;
                    InvalidateAndRefresh(primary);
                    AutoKeyProperty(primary, PropKey.AnchorY, val);
                }, min: 0.0, max: 1.0, step: 0.05), primary, PropKey.AnchorY);

                Grid.SetColumn(axBox, 0); Grid.SetColumn(ayBox, 2);
                anchorRow.Children.Add(axBox); anchorRow.Children.Add(ayBox);
                _propertyInspectorStack.Children.Add(anchorRow);
            }

            // Shape Corner Curving Controls (Smooth Slider & Quick Presets: 0px, 4px, 8px, 16px, 24px, Pill)
            if (primary.Type == ElementType.Rectangle || primary.Type == ElementType.Frame || primary.Type == ElementType.Group || primary.Type == ElementType.Image)
            {
                double maxRadius = Math.Max(10.0, Math.Min(primary.Width, primary.Height) / 2.0);

                var crSlider = ScrubControls.CreateSmoothSlider("Corner Curve", primary.CornerRadius, 0.0, maxRadius, val =>
                {
                    primary.CornerRadius = Math.Round(val);
                    InvalidateAndRefresh(primary);
                    AutoKeyProperty(primary, PropKey.CornerRadius, primary.CornerRadius);
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
                        AutoKeyProperty(primary, PropKey.CornerRadius, primary.CornerRadius);
                        RefreshPropertyInspector();
                    });
                    pBtn.Width = 32;
                    presetRow.Children.Add(pBtn);
                }
                var pillBtn = CreateSmallButton("Pill", "Fully rounded capsule", () =>
                {
                    primary.CornerRadius = maxRadius;
                    InvalidateAndRefresh(primary);
                    AutoKeyProperty(primary, PropKey.CornerRadius, primary.CornerRadius);
                    RefreshPropertyInspector();
                });
                pillBtn.Width = 36;
                presetRow.Children.Add(pillBtn);
                _propertyInspectorStack.Children.Add(presetRow);
            }

            // Layer Opacity Smooth Slider
            var opacitySlider = ScrubControls.CreateSmoothSlider("Layer Opacity", Math.Round(primary.Opacity * 100), 0, 100, val =>
            {
                primary.Opacity = val / 100.0;
                InvalidateAndRefresh(primary);
                AutoKeyProperty(primary, PropKey.Opacity, primary.Opacity);
            }, "%");
            _propertyInspectorStack.Children.Add(CreateAnimatedPropertyField(opacitySlider, primary, PropKey.Opacity));

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

            // Keep selection-level styles at the end of the inspector, after object properties.
            if (_selectedLayers.Count > 1 || primary.Type == ElementType.Frame || primary.Type == ElementType.Group)
                BuildSelectionColorsSection(_selectedLayers);
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

        private FrameworkElement BuildLayoutGuidesSection(Kerf.Model.Page page)
        {
            if (page.Guides == null) page.Guides = new List<GuideLine>();
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            var showGuides = new CheckBox
            {
                Content = "Show guides",
                IsChecked = page.ShowGuides,
                Foreground = new SolidColorBrush(Color.FromRgb(215, 222, 235)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 7),
                Cursor = Cursors.Hand
            };
            showGuides.Click += (s, e) =>
            {
                page.ShowGuides = showGuides.IsChecked == true;
                History.MarkDirty();
                RefreshCanvas();
            };
            stack.Children.Add(showGuides);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 7) };
            var vertical = CreateSmallButton("＋ Vertical", "Add a draggable vertical layout guide", () => AddLayoutGuide(true, GetVisibleCanvasCenter()));
            vertical.Width = 100;
            vertical.Height = 25;
            vertical.Margin = new Thickness(0, 0, 6, 0);
            actions.Children.Add(vertical);
            var horizontal = CreateSmallButton("＋ Horizontal", "Add a draggable horizontal layout guide", () => AddLayoutGuide(false, GetVisibleCanvasCenter()));
            horizontal.Width = 108;
            horizontal.Height = 25;
            actions.Children.Add(horizontal);
            stack.Children.Add(actions);

            foreach (var guide in page.Guides.ToList())
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                var positionField = CreateNumberField(guide.IsVertical ? "Vertical X" : "Horizontal Y", guide.Position, val =>
                {
                    guide.Position = val;
                    History.MarkDirty();
                    RefreshCanvas();
                });
                Grid.SetColumn(positionField, 0);
                row.Children.Add(positionField);
                var jumpButton = CreateSmallButton("↗", "Focus this guide", () =>
                {
                    if (guide.IsVertical) _panOffset.X = _canvasHost.ActualWidth / 2.0 - guide.Position * _zoomScale;
                    else _panOffset.Y = _canvasHost.ActualHeight / 2.0 - guide.Position * _zoomScale;
                    RefreshCanvas();
                });
                jumpButton.Width = 22;
                jumpButton.Height = 24;
                Grid.SetColumn(jumpButton, 1);
                row.Children.Add(jumpButton);
                var removeButton = CreateSmallButton("×", "Remove guide", () =>
                {
                    page.Guides.Remove(guide);
                    History.MarkDirty();
                    RefreshCanvas();
                    RefreshPropertyInspector();
                });
                removeButton.Width = 20;
                removeButton.Height = 24;
                Grid.SetColumn(removeButton, 2);
                row.Children.Add(removeButton);
                stack.Children.Add(row);
            }

            if (page.Guides.Count > 0)
            {
                var clear = CreateSmallButton("Clear all guides", "Remove all layout guides", () =>
                {
                    page.Guides.Clear();
                    History.MarkDirty();
                    RefreshCanvas();
                    RefreshPropertyInspector();
                });
                clear.Height = 25;
                stack.Children.Add(clear);
            }
            return stack;
        }

        private Point GetVisibleCanvasCenter()
        {
            if (_canvasHost == null) return new Point(0, 0);
            return ScreenToCanvas(new Point(_canvasHost.ActualWidth / 2.0, _canvasHost.ActualHeight / 2.0));
        }

        private void AddLayoutGuide(bool vertical, Point canvasPoint)
        {
            var page = Document != null ? Document.GetActivePage() : null;
            if (page == null) return;
            if (page.Guides == null) page.Guides = new List<GuideLine>();
            var guide = new GuideLine(vertical, vertical ? canvasPoint.X : canvasPoint.Y);
            page.Guides.Add(guide);
            page.ShowGuides = true;
            History.MarkDirty();
            RefreshCanvas();
            RefreshPropertyInspector();
        }

        private GuideLine FindGuideNear(Point canvasPoint)
        {
            var page = Document != null ? Document.GetActivePage() : null;
            if (page == null || !page.ShowGuides || page.Guides == null) return null;
            double maxDistance = 8.0 / Math.Max(0.05, _zoomScale);
            return page.Guides
                .Select(g => new { Guide = g, Distance = Math.Abs((g.IsVertical ? canvasPoint.X : canvasPoint.Y) - g.Position) })
                .Where(item => item.Distance <= maxDistance)
                .OrderBy(item => item.Distance)
                .Select(item => item.Guide)
                .FirstOrDefault();
        }

        private FrameworkElement CreateAutoLayoutCard(LayerNode frame)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            var layout = frame.AutoLayout ?? new AutoLayoutData();
            frame.AutoLayout = layout;
            if (layout.Mode == AutoLayoutMode.None)
            {
                var addButton = CreateSmallButton("＋ Add auto layout", "Wrap and evenly distribute this frame's children", () =>
                {
                    layout.Mode = AutoLayoutMode.Wrap;
                    layout.SizingH = SizingMode.Fixed;
                    layout.JustifyContent = JustifyContent.SpaceEvenly;
                    ApplyAutoLayoutChange(frame, true);
                });
                addButton.Height = 30;
                stack.Children.Add(addButton);
                return stack;
            }

            Action<bool> applyLayoutChange = refreshInspector =>
            {
                ApplyAutoLayoutChange(frame, refreshInspector);
            };

            // Compact flow selector, with Wrap as the normal starting point.
            stack.Children.Add(new TextBlock { Text = "Flow", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(145, 160, 184)), Margin = new Thickness(0, 0, 0, 3) });
            var flowRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var flowModes = new[]
            {
                new { Mode = AutoLayoutMode.Wrap, Label = "↪", Tip = "Wrap" },
                new { Mode = AutoLayoutMode.Vertical, Label = "↕", Tip = "Vertical flow" },
                new { Mode = AutoLayoutMode.Horizontal, Label = "↔", Tip = "Horizontal flow" },
                new { Mode = AutoLayoutMode.Grid, Label = "▦", Tip = "Grid" }
            };
            foreach (var flow in flowModes)
            {
                bool active = layout.Mode == flow.Mode;
                var flowButton = new Button
                {
                    Content = flow.Label,
                    Width = 38,
                    Height = 28,
                    Margin = new Thickness(0, 0, 4, 0),
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Background = active ? new SolidColorBrush(Color.FromRgb(56, 67, 92)) : new SolidColorBrush(Color.FromRgb(35, 40, 55)),
                    Foreground = active ? new SolidColorBrush(Color.FromRgb(220, 230, 245)) : new SolidColorBrush(Color.FromRgb(155, 170, 193)),
                    BorderBrush = active ? new SolidColorBrush(Color.FromRgb(95, 112, 144)) : new SolidColorBrush(Color.FromRgb(48, 56, 75)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    ToolTip = flow.Tip,
                    Template = CreateRoundedButtonTemplate(5)
                };
                flowButton.Click += (s, e) =>
                {
                    layout.Mode = flow.Mode;
                    applyLayoutChange(true);
                };
                flowRow.Children.Add(flowButton);
            }
            var removeButton = CreateSmallButton("×", "Remove auto layout", () =>
            {
                layout.Mode = AutoLayoutMode.None;
                RefreshPropertyInspector();
                RefreshCanvas();
            });
            removeButton.Width = 28;
            removeButton.Height = 28;
            flowRow.Children.Add(removeButton);
            stack.Children.Add(flowRow);

            stack.Children.Add(new TextBlock { Text = "Resizing", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(145, 160, 184)), Margin = new Thickness(0, 0, 0, 3) });
            var sizingGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            sizingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sizingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            sizingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var sizingHCombo = CreateModernComboBox(26);
            sizingHCombo.Items.Add(CreateModernComboBoxItem("W: Fixed"));
            sizingHCombo.Items.Add(CreateModernComboBoxItem("W: Hug"));
            sizingHCombo.SelectedIndex = layout.SizingH == SizingMode.Hug ? 1 : 0;
            sizingHCombo.SelectionChanged += (s, e) => { layout.SizingH = sizingHCombo.SelectedIndex == 1 ? SizingMode.Hug : SizingMode.Fixed; applyLayoutChange(false); };
            Grid.SetColumn(sizingHCombo, 0);
            sizingGrid.Children.Add(sizingHCombo);
            var sizingVCombo = CreateModernComboBox(26);
            sizingVCombo.Items.Add(CreateModernComboBoxItem("H: Fixed"));
            sizingVCombo.Items.Add(CreateModernComboBoxItem("H: Hug"));
            sizingVCombo.SelectedIndex = layout.SizingV == SizingMode.Hug ? 1 : 0;
            sizingVCombo.SelectionChanged += (s, e) => { layout.SizingV = sizingVCombo.SelectedIndex == 1 ? SizingMode.Hug : SizingMode.Fixed; applyLayoutChange(false); };
            Grid.SetColumn(sizingVCombo, 2);
            sizingGrid.Children.Add(sizingVCombo);
            stack.Children.Add(sizingGrid);

            stack.Children.Add(new TextBlock { Text = "Alignment", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(145, 160, 184)), Margin = new Thickness(0, 0, 0, 3) });
            var alignmentAndGap = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            alignmentAndGap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            alignmentAndGap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            alignmentAndGap.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var alignPanel = CreateAutoLayoutAlignmentGrid(frame, layout, applyLayoutChange);
            Grid.SetColumn(alignPanel, 0);
            alignmentAndGap.Children.Add(alignPanel);

            var gapPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
            gapPanel.Children.Add(new TextBlock { Text = "Gap", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(145, 160, 184)), Margin = new Thickness(0, 0, 0, 3) });
            var gapGrid = new Grid();
            gapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            var gapField = CreateNumberField("↔", layout.Gap, val => { layout.Gap = Math.Max(0.0, val); applyLayoutChange(false); });
            Grid.SetColumn(gapField, 0);
            gapGrid.Children.Add(gapField);
            var distCombo = CreateModernComboBox(26);
            distCombo.Items.Add(CreateModernComboBoxItem("Packed"));
            distCombo.Items.Add(CreateModernComboBoxItem("Space between"));
            distCombo.SelectedIndex = layout.JustifyContent == JustifyContent.SpaceBetween ? 1 : 0;
            distCombo.ToolTip = "Distribution of items: Packed or Space between";
            distCombo.SelectionChanged += (s, e) =>
            {
                if (distCombo.SelectedIndex == 1)
                {
                    layout.JustifyContent = JustifyContent.SpaceBetween;
                }
                else
                {
                    if (layout.JustifyContent == JustifyContent.SpaceBetween)
                    {
                        layout.JustifyContent = JustifyContent.Start;
                    }
                }
                applyLayoutChange(true);
            };
            Grid.SetColumn(distCombo, 1);
            gapGrid.Children.Add(distCombo);
            gapPanel.Children.Add(gapGrid);
            Grid.SetColumn(gapPanel, 2);
            alignmentAndGap.Children.Add(gapPanel);
            stack.Children.Add(alignmentAndGap);

            if (layout.Mode == AutoLayoutMode.Grid)
            {
                stack.Children.Add(CreateNumberField("Columns", layout.GridColumns, val =>
                {
                    layout.GridColumns = Math.Max(1, Math.Min(12, (int)Math.Round(val)));
                    applyLayoutChange(false);
                }));
            }

            stack.Children.Add(new TextBlock { Text = "Padding", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(145, 160, 184)), Margin = new Thickness(0, 0, 0, 3) });
            var paddingGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            paddingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paddingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            paddingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paddingGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            paddingGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Action<FrameworkElement, int, int> addPaddingField = (field, col, row) =>
            {
                Grid.SetColumn(field, col);
                Grid.SetRow(field, row);
                paddingGrid.Children.Add(field);
            };
            addPaddingField(CreateNumberField("Left", layout.PaddingLeft, val => { layout.PaddingLeft = Math.Max(0, val); applyLayoutChange(false); }), 0, 0);
            addPaddingField(CreateNumberField("Right", layout.PaddingRight, val => { layout.PaddingRight = Math.Max(0, val); applyLayoutChange(false); }), 2, 0);
            addPaddingField(CreateNumberField("Top", layout.PaddingTop, val => { layout.PaddingTop = Math.Max(0, val); applyLayoutChange(false); }), 0, 1);
            addPaddingField(CreateNumberField("Bottom", layout.PaddingBottom, val => { layout.PaddingBottom = Math.Max(0, val); applyLayoutChange(false); }), 2, 1);
            stack.Children.Add(paddingGrid);

            return stack;
        }

        private void ApplyAutoLayoutChange(LayerNode frame, bool refreshInspector)
        {
            if (frame == null || frame.AutoLayout == null) return;
            var page = Document != null ? Document.GetActivePage() : null;
            if (page == null) return;
            var before = page.GetAllLayersFlat().Where(node => node != null)
                .ToDictionary(node => node, node => new Point(node.X, node.Y));
            AutoLayoutEngine.ReflowHierarchy(page, frame);
            _sceneGraph.Synchronize(page);
            InvalidateLayoutFrameVisuals(frame);
            History.MarkDirty();
            RefreshCanvas();
            var after = page.GetAllLayersFlat().Where(node => node != null && before.ContainsKey(node))
                .ToDictionary(node => node, node => new Point(node.X, node.Y));
            AnimateAutoLayoutPositions(before, after, 170.0);
            if (refreshInspector) RefreshPropertyInspector();
        }

        private FrameworkElement CreateAutoLayoutAlignmentGrid(LayerNode frame, AutoLayoutData layout, Action<bool> applyChange)
        {
            var container = new Border
            {
                Width = 84,
                Height = 72,
                Background = new SolidColorBrush(Color.FromRgb(24, 28, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 54, 74)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4)
            };

            var grid = new Grid();
            for (int i = 0; i < 3; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            var horizontal = layout.Mode == AutoLayoutMode.Horizontal || layout.Mode == AutoLayoutMode.Wrap || layout.Mode == AutoLayoutMode.Grid;
            bool isSpaceBetween = layout.JustifyContent == JustifyContent.SpaceBetween;

            // In horizontal mode: col represents Main axis (Left=0, Center=1, Right=2), row represents Cross axis (Top=0, Center=1, Bottom=2)
            // In vertical mode: row represents Main axis (Top=0, Center=1, Bottom=2), col represents Cross axis (Left=0, Center=1, Right=2)
            int activeMain = (layout.JustifyContent == JustifyContent.Center || layout.JustifyContent == JustifyContent.SpaceAround || layout.JustifyContent == JustifyContent.SpaceEvenly) ? 1
                : (layout.JustifyContent == JustifyContent.End ? 2 : 0);
            int activeCross = layout.AlignItems == AlignItems.Center ? 1 : (layout.AlignItems == AlignItems.End ? 2 : 0);

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int localRow = r;
                    int localCol = c;

                    bool isActive;
                    if (isSpaceBetween)
                    {
                        isActive = horizontal ? (r == activeCross) : (c == activeCross);
                    }
                    else
                    {
                        isActive = horizontal ? (c == activeMain && r == activeCross) : (r == activeMain && c == activeCross);
                    }

                    var cell = new Border
                    {
                        Background = isActive ? new SolidColorBrush(Color.FromArgb(40, 0x46, 0xEF, 0xB1)) : Brushes.Transparent,
                        CornerRadius = new CornerRadius(4),
                        Cursor = Cursors.Hand,
                        Margin = new Thickness(1)
                    };

                    string colName = (c == 0) ? "Left" : (c == 1) ? "Center" : "Right";
                    string rowName = (r == 0) ? "Top" : (r == 1) ? "Center" : "Bottom";
                    cell.ToolTip = string.Format("{0} / {1}", rowName, colName);

                    FrameworkElement indicator;
                    if (isActive)
                    {
                        var activePill = new Border
                        {
                            Width = 10,
                            Height = 7,
                            CornerRadius = new CornerRadius(2),
                            Background = KerfBrand.MintBrush,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        indicator = activePill;
                    }
                    else
                    {
                        var dot = new Ellipse
                        {
                            Width = 4,
                            Height = 4,
                            Fill = KerfBrand.TealBrush,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        indicator = dot;
                    }
                    cell.Child = indicator;

                    var cellRef = cell;
                    var indRef = indicator;
                    bool activeRef = isActive;

                    cell.MouseEnter += (s, e) =>
                    {
                        if (!activeRef)
                        {
                            cellRef.Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
                            var d = indRef as Ellipse;
                            if (d != null) d.Fill = KerfBrand.MintBrush;
                        }
                    };
                    cell.MouseLeave += (s, e) =>
                    {
                        if (!activeRef)
                        {
                            cellRef.Background = Brushes.Transparent;
                            var d = indRef as Ellipse;
                            if (d != null) d.Fill = KerfBrand.TealBrush;
                        }
                    };

                    cell.MouseLeftButtonDown += (s, e) =>
                    {
                        e.Handled = true;
                        if (horizontal)
                        {
                            layout.JustifyContent = (localCol == 0) ? JustifyContent.Start : (localCol == 1 ? JustifyContent.Center : JustifyContent.End);
                            layout.AlignItems = (localRow == 0) ? AlignItems.Start : (localRow == 1 ? AlignItems.Center : AlignItems.End);
                        }
                        else
                        {
                            layout.JustifyContent = (localRow == 0) ? JustifyContent.Start : (localRow == 1 ? JustifyContent.Center : JustifyContent.End);
                            layout.AlignItems = (localCol == 0) ? AlignItems.Start : (localCol == 1 ? AlignItems.Center : AlignItems.End);
                        }
                        applyChange(true);
                    };

                    Grid.SetRow(cell, r);
                    Grid.SetColumn(cell, c);
                    grid.Children.Add(cell);
                }
            }

            container.Child = grid;
            return container;
        }

        private FrameworkElement CreateAutoLayoutChildCard(LayerNode child, LayerNode parent)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

            var ignoreBox = new CheckBox
            {
                Content = "Free position",
                IsChecked = child.IgnoreAutoLayout,
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand,
                ToolTip = "Keep this layer positioned independently of the parent layout flow."
            };
            ignoreBox.Click += (s, e) =>
            {
                child.IgnoreAutoLayout = (ignoreBox.IsChecked == true);
                AutoLayoutEngine.ReflowRecursive(parent);
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
                SetChildLayoutSizingMode(child, true, (SizingMode)sizingHCombo.SelectedIndex);
                AutoLayoutEngine.ReflowRecursive(parent);
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
                SetChildLayoutSizingMode(child, false, (SizingMode)sizingVCombo.SelectedIndex);
                AutoLayoutEngine.ReflowRecursive(parent);
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

        private void BuildSelectionColorsSection(IEnumerable<LayerNode> roots)
        {
            var colorTargets = new Dictionary<string, List<Action<string>>>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<LayerNode>();
            Action<LayerNode> collect = null;
            collect = layer =>
            {
                if (layer == null || !visited.Add(layer)) return;
                if (layer.Fills != null)
                {
                    foreach (var paint in layer.Fills)
                    {
                        if (paint == null) continue;
                        if (paint.Type == PaintType.Solid && !string.IsNullOrWhiteSpace(paint.ColorHex))
                        {
                            string key = NormalizeStyleColor(paint.ColorHex);
                            if (!colorTargets.ContainsKey(key)) colorTargets[key] = new List<Action<string>>();
                            var targetPaint = paint;
                            colorTargets[key].Add(hex => targetPaint.ColorHex = hex);
                        }
                        if (paint.GradientStops != null)
                        {
                            foreach (var stop in paint.GradientStops)
                            {
                                if (stop == null || string.IsNullOrWhiteSpace(stop.ColorHex)) continue;
                                string key = NormalizeStyleColor(stop.ColorHex);
                                if (!colorTargets.ContainsKey(key)) colorTargets[key] = new List<Action<string>>();
                                var targetStop = stop;
                                colorTargets[key].Add(hex => targetStop.ColorHex = hex);
                            }
                        }
                    }
                }
                if (layer.Strokes != null)
                {
                    foreach (var stroke in layer.Strokes)
                    {
                        var paint = stroke != null ? stroke.Paint : null;
                        if (paint == null || string.IsNullOrWhiteSpace(paint.ColorHex)) continue;
                        string key = NormalizeStyleColor(paint.ColorHex);
                        if (!colorTargets.ContainsKey(key)) colorTargets[key] = new List<Action<string>>();
                        var targetPaint = paint;
                        colorTargets[key].Add(hex => targetPaint.ColorHex = hex);
                    }
                }
                if (layer.Children != null)
                {
                    foreach (var child in layer.Children) collect(child);
                }
            };
            foreach (var root in roots ?? Enumerable.Empty<LayerNode>()) collect(root);
            if (colorTargets.Count == 0) return;

            _propertyInspectorStack.Children.Add(CreateInspectorSection("Selection colors"));
            foreach (var entry in colorTargets.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                string originalHex = entry.Key;
                var targets = entry.Value;
                var row = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 40, 55)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(53, 61, 80)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6, 5, 8, 5),
                    Margin = new Thickness(0, 0, 0, 5),
                    Cursor = Cursors.Hand,
                    ToolTip = "Change every matching color in this selection"
                };
                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var swatch = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(GpuRenderer.ParseHexColor(originalHex, 1.0)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(85, 95, 115)),
                    BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(swatch, 0);
                rowGrid.Children.Add(swatch);
                var colorName = new TextBlock
                {
                    Text = GetStyleColorName(originalHex),
                    Foreground = new SolidColorBrush(Color.FromRgb(235, 238, 244)),
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(3, 0, 6, 0)
                };
                Grid.SetColumn(colorName, 1);
                rowGrid.Children.Add(colorName);
                var hexLabel = new TextBlock
                {
                    Text = originalHex.ToUpperInvariant(),
                    Foreground = new SolidColorBrush(Color.FromRgb(145, 159, 181)),
                    FontSize = 9.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(hexLabel, 2);
                rowGrid.Children.Add(hexLabel);
                row.Child = rowGrid;

                Action openPicker = () => ColorPickerPopup.ShowAt(row, originalHex, newHex =>
                {
                    foreach (var update in targets) update(newHex);
                    _sceneGraph.Synchronize(Document.GetActivePage());
                    History.MarkDirty();
                    RefreshCanvas();
                    RefreshPropertyInspector();
                });
                row.MouseLeftButtonUp += (s, e) => { e.Handled = true; openPicker(); };
                _propertyInspectorStack.Children.Add(row);
            }
        }

        private static string NormalizeStyleColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return "#D9D9D9";
            var value = hex.Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal)) value = "#" + value;
            return value.ToUpperInvariant();
        }

        private static string GetStyleColorName(string hex)
        {
            switch (NormalizeStyleColor(hex))
            {
                case "#D9D9D9": case "#808080": case "#888888": return "Gray";
                case "#FF7A00": case "#FF8A00": case "#F97316": case "#EA580C": return "Orange";
                case "#FF0000": case "#EF4444": case "#DC2626": return "Red";
                case "#00FF00": case "#22C55E": case "#16A34A": return "Green";
                case "#0000FF": case "#3B82F6": case "#2563EB": return "Blue";
                case "#FFFF00": case "#EAB308": case "#FACC15": return "Yellow";
                case "#800080": case "#A855F7": case "#8B5CF6": return "Purple";
                case "#FFFFFF": case "#FBFAFA": return "White";
                case "#000000": return "Black";
                default: return "Color";
            }
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

        private void ApplyParentTransformToDescendants(LayerNode parent, Rect transformedParentBounds, double transformedParentRotation)
        {
            if (parent == null || parent.Children == null || _dragInitialBounds == null) return;
            double scaleX = transformedParentBounds.Width / Math.Max(0.001, _transformStartBounds.Width);
            double scaleY = transformedParentBounds.Height / Math.Max(0.001, _transformStartBounds.Height);
            double initialParentRotation;
            if (!_dragInitialRotations.TryGetValue(parent, out initialParentRotation)) initialParentRotation = _transformStartRot;
            double rotationDelta = transformedParentRotation - initialParentRotation;
            double radians = rotationDelta * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            var selected = new HashSet<LayerNode>(_selectedLayers);
            foreach (var child in parent.Children)
                ApplyParentTransformToDescendant(child, transformedParentBounds, scaleX, scaleY, cos, sin, rotationDelta, selected);
        }

        private void ApplyParentTransformToDescendant(LayerNode child, Rect transformedParentBounds, double scaleX, double scaleY, double cos, double sin, double rotationDelta, HashSet<LayerNode> selected)
        {
            if (child == null || (selected != null && selected.Contains(child))) return;
            Rect initialBounds;
            if (_dragInitialBounds.TryGetValue(child, out initialBounds))
            {
                double initialCenterX = initialBounds.X + initialBounds.Width / 2.0;
                double initialCenterY = initialBounds.Y + initialBounds.Height / 2.0;
                double centerX = transformedParentBounds.X + (initialCenterX - _transformStartBounds.X) * scaleX;
                double centerY = transformedParentBounds.Y + (initialCenterY - _transformStartBounds.Y) * scaleY;
                double pivotX = transformedParentBounds.X + transformedParentBounds.Width / 2.0;
                double pivotY = transformedParentBounds.Y + transformedParentBounds.Height / 2.0;
                double relX = centerX - pivotX;
                double relY = centerY - pivotY;
                centerX = pivotX + relX * cos - relY * sin;
                centerY = pivotY + relX * sin + relY * cos;
                double width = Math.Max(1.0, initialBounds.Width * Math.Abs(scaleX));
                double height = Math.Max(1.0, initialBounds.Height * Math.Abs(scaleY));
                var transformedBounds = new Rect(centerX - width / 2.0, centerY - height / 2.0, width, height);
                child.X = transformedBounds.X;
                child.Y = transformedBounds.Y;
                child.Width = transformedBounds.Width;
                child.Height = transformedBounds.Height;
                double initialRotation;
                if (_dragInitialRotations.TryGetValue(child, out initialRotation)) child.Rotation = initialRotation + rotationDelta;
                VectorGeometry initialGeometry;
                if (child.Type == ElementType.Path && _dragInitialGeometries.TryGetValue(child, out initialGeometry) && initialGeometry != null)
                {
                    child.Geometry = initialGeometry.Clone();
                    BezierVectorEngine.ScaleGeometry(child.Geometry, initialBounds, transformedBounds);
                }
                _sceneGraph.InvalidateNode(child.Id);
            }
            if (child.Children != null)
                foreach (var grandchild in child.Children)
                    ApplyParentTransformToDescendant(grandchild, transformedParentBounds, scaleX, scaleY, cos, sin, rotationDelta, selected);
        }

        private void CheckAutoReparentLayersOnDrop(List<LayerNode> movedLayers)
        {
            if (movedLayers == null || movedLayers.Count == 0 || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;
            var positionsBefore = page.GetAllLayersFlat().ToDictionary(l => l, l => new Point(l.X, l.Y));

            var moving = GetTopLevelLayers(movedLayers);
            LayerNode target = _autoLayoutDropTarget;
            LayerNode source = _autoLayoutDropSource;
            bool detach = _autoLayoutDetachPreview;
            // Ordinary canvas moves and root-level Alt-drag duplicates keep their existing
            // layer order. Reparent only when a frame was targeted or an auto-layout item
            // explicitly crossed its sticky boundary.
            if (target == null && source == null && !detach) return;
            bool changed = false;
            foreach (var layer in moving)
            {
                if (layer == null || (target != null && (target == layer || Kerf.Model.Page.IsDescendant(layer, target)))) continue;
                LayerNode oldParent;
                var oldContainer = page.FindContainer(layer, out oldParent);
                int oldIndex = oldContainer != null ? oldContainer.IndexOf(layer) : -1;
                string oldParentId = oldParent != null ? oldParent.Id : null;
                string newParentId = detach || target == null ? null : target.Id;
                bool newIgnoreAutoLayout = target != null && target.AutoLayout != null && target.AutoLayout.Mode != AutoLayoutMode.None
                    ? false
                    : layer.IgnoreAutoLayout;
                List<LayerNode> newContainer = null;
                if (string.IsNullOrEmpty(newParentId)) newContainer = page.Layers;
                else
                {
                    if (target.Children == null) target.Children = new List<LayerNode>();
                    newContainer = target.Children;
                }

                int newIndex;
                if (target != null && target.AutoLayout != null && target.AutoLayout.Mode != AutoLayoutMode.None)
                    newIndex = Math.Max(0, Math.Min(_autoLayoutDropIndex, newContainer.Count));
                else if (target != null)
                    newIndex = newContainer.Count;
                else
                    newIndex = page.Layers.Count;

                if (target != null && (target.AutoLayout == null || target.AutoLayout.Mode == AutoLayoutMode.None) && oldParentId == newParentId)
                    continue;
                if (oldParentId == newParentId && oldIndex == newIndex && layer.IgnoreAutoLayout == newIgnoreAutoLayout) continue;
                Action refresh = () =>
                {
                    if (oldParent != null && oldParent.AutoLayout != null && oldParent.AutoLayout.Mode != AutoLayoutMode.None)
                        AutoLayoutEngine.ReflowRecursive(oldParent);
                    if (target != null && target.AutoLayout != null && target.AutoLayout.Mode != AutoLayoutMode.None)
                        AutoLayoutEngine.ReflowRecursive(target);
                    _sceneGraph.Synchronize(page);
                    if (oldParent != null) InvalidateLayoutFrameVisuals(oldParent);
                    if (target != null) InvalidateLayoutFrameVisuals(target);
                    InvalidateLayoutFrameVisuals(layer);
                    RefreshLayersList();
                    RefreshCanvas();
                };
                History.Execute(new ReorderLayerCommand(page, layer, oldParentId, oldIndex, newParentId, newIndex, refresh, newIgnoreAutoLayout));
                changed = true;
            }

            // Every auto-layout drop resolves to a slot, so child bounds always return to layout control.
            if (!detach && target != null && target.AutoLayout != null && target.AutoLayout.Mode != AutoLayoutMode.None)
            {
                AutoLayoutEngine.ReflowRecursive(target);
                InvalidateLayoutFrameVisuals(target);
            }
            if (changed)
            {
                _sceneGraph.Synchronize(page);
                foreach (var layer in moving) InvalidateLayoutFrameVisuals(layer);
                RefreshLayersList();
            }
            else if (target != null && target.AutoLayout != null && target.AutoLayout.Mode != AutoLayoutMode.None)
            {
                RefreshCanvas();
            }
            AnimateAutoLayoutReflow(positionsBefore, page.GetAllLayersFlat());
        }

        private List<LayerNode> CommitDuplicateDrop(Kerf.Model.Page page, List<LayerNode> duplicates)
        {
            var affectedLayouts = new HashSet<LayerNode>();
            if (page == null || duplicates == null || duplicates.Count == 0) return affectedLayouts.ToList();

            var moving = GetTopLevelLayers(duplicates);
            var sourceParents = new List<LayerNode>();
            foreach (var layer in moving)
            {
                LayerNode oldParent;
                page.FindContainer(layer, out oldParent);
                if (oldParent != null && oldParent.AutoLayout != null && oldParent.AutoLayout.Mode != AutoLayoutMode.None)
                    sourceParents.Add(oldParent);
            }

            LayerNode target = _autoLayoutDropTarget;
            if (_hasAltDuplicated && _altOriginalLayers.Count > 0)
            {
                if (_altOriginalLayers.All(orig => string.IsNullOrEmpty(orig.ParentId)))
                {
                    target = null;
                }
                else if (target != null && _altOriginalLayers.Any(orig => orig == target || Kerf.Model.Page.IsDescendant(orig, target)))
                {
                    target = null;
                }
            }
            bool shouldCommitDrop = target != null || _autoLayoutDetachPreview || _autoLayoutDropSource != null;
            if (!shouldCommitDrop) return affectedLayouts.ToList();
            if (target != null && moving.Any(layer => layer == target || Kerf.Model.Page.IsDescendant(layer, target)))
                target = null;

            // Remove the preview clones as a group before inserting them. The drop
            // index was computed with all dragged clones excluded, so this keeps
            // that index stable for multi-selection and nested-frame drops.
            foreach (var layer in moving)
                page.RemoveLayerEverywhere(layer);

            List<LayerNode> targetList;
            string parentId = null;
            if (!_autoLayoutDetachPreview && target != null)
            {
                if (target.Children == null) target.Children = new List<LayerNode>();
                targetList = target.Children;
                parentId = target.Id;
            }
            else targetList = page.Layers;

            int insertionIndex;
            if (target != null && target.AutoLayout != null && target.AutoLayout.Mode != AutoLayoutMode.None)
                insertionIndex = Math.Max(0, Math.Min(_autoLayoutDropIndex, targetList.Count));
            else if (target != null)
                insertionIndex = targetList.Count;
            else
                insertionIndex = targetList.Count;

            foreach (var layer in moving)
            {
                layer.ParentId = parentId;
                if (target != null && target.AutoLayout != null && target.AutoLayout.Mode != AutoLayoutMode.None)
                    layer.IgnoreAutoLayout = false;
                targetList.Insert(Math.Max(0, Math.Min(insertionIndex++, targetList.Count)), layer);
            }

            foreach (var parent in sourceParents)
                if (parent != target) affectedLayouts.Add(parent);
            if (target != null && target.AutoLayout != null && target.AutoLayout.Mode != AutoLayoutMode.None)
                affectedLayouts.Add(target);

            foreach (var frame in affectedLayouts)
            {
                AutoLayoutEngine.ReflowRecursive(frame);
                InvalidateLayoutFrameVisuals(frame);
            }
            _sceneGraph.Synchronize(page);
            foreach (var layer in moving) InvalidateLayoutFrameVisuals(layer);
            RefreshLayersList();
            RefreshCanvas();
            return affectedLayouts.ToList();
        }

        private void AnimateAutoLayoutReflow(Dictionary<LayerNode, Point> positionsBefore, IEnumerable<LayerNode> currentLayers)
        {
            if (positionsBefore == null || currentLayers == null) return;
            var positionsAfter = currentLayers
                .Where(l => l != null && positionsBefore.ContainsKey(l))
                .ToDictionary(l => l, l => new Point(l.X, l.Y));
            AnimateAutoLayoutPositions(positionsBefore, positionsAfter, 190.0);
        }

        private void AnimateAutoLayoutPositions(Dictionary<LayerNode, Point> startPositions, Dictionary<LayerNode, Point> endPositions, double durationMs)
        {
            if (startPositions == null || endPositions == null) return;
            var changedLayers = endPositions
                .Where(kv => startPositions.ContainsKey(kv.Key) &&
                    (Math.Abs(kv.Value.X - startPositions[kv.Key].X) > 0.25 || Math.Abs(kv.Value.Y - startPositions[kv.Key].Y) > 0.25))
                .ToList();
            if (changedLayers.Count == 0) return;

            if (_autoLayoutReflowTimer != null) _autoLayoutReflowTimer.Stop();
            _autoLayoutReflowStart = new Dictionary<LayerNode, Point>();
            _autoLayoutReflowEnd = new Dictionary<LayerNode, Point>();
            var pathStartGeometries = new Dictionary<LayerNode, VectorGeometry>();
            foreach (var item in changedLayers)
            {
                _autoLayoutReflowStart[item.Key] = startPositions[item.Key];
                _autoLayoutReflowEnd[item.Key] = item.Value;
                if (item.Key.Type == ElementType.Path && item.Key.Geometry != null)
                {
                    var geometry = item.Key.Geometry.Clone();
                    BezierVectorEngine.OffsetGeometry(
                        geometry,
                        startPositions[item.Key].X - item.Key.X,
                        startPositions[item.Key].Y - item.Key.Y);
                    pathStartGeometries[item.Key] = geometry;
                }
                item.Key.X = startPositions[item.Key].X;
                item.Key.Y = startPositions[item.Key].Y;
                _sceneGraph.InvalidateNode(item.Key.Id);
            }

            _autoLayoutReflowStartUtc = DateTime.UtcNow;
            _autoLayoutReflowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _autoLayoutReflowTimer.Tick += (s, e) =>
            {
                double progress = Math.Min(1.0, (DateTime.UtcNow - _autoLayoutReflowStartUtc).TotalMilliseconds / Math.Max(60.0, durationMs));
                double eased = 1.0 - Math.Pow(1.0 - progress, 3.0);
                foreach (var pair in _autoLayoutReflowEnd)
                {
                    var start = _autoLayoutReflowStart[pair.Key];
                    pair.Key.X = start.X + (pair.Value.X - start.X) * eased;
                    pair.Key.Y = start.Y + (pair.Value.Y - start.Y) * eased;
                    VectorGeometry startGeometry;
                    if (pathStartGeometries.TryGetValue(pair.Key, out startGeometry))
                    {
                        pair.Key.Geometry = startGeometry.Clone();
                        BezierVectorEngine.OffsetGeometry(
                            pair.Key.Geometry,
                            (pair.Value.X - start.X) * eased,
                            (pair.Value.Y - start.Y) * eased);
                    }
                    _sceneGraph.InvalidateNode(pair.Key.Id);
                }
                RefreshCanvas();
                if (progress >= 1.0)
                {
                    _autoLayoutReflowTimer.Stop();
                    _autoLayoutReflowStart.Clear();
                    _autoLayoutReflowEnd.Clear();
                }
            };
            _autoLayoutReflowTimer.Start();
        }

        private Dictionary<LayerNode, Point> ComputeAutoLayoutPreviewPositions(LayerNode frame, IEnumerable<LayerNode> movingLayers, int insertionIndex)
        {
            var result = new Dictionary<LayerNode, Point>();
            if (frame == null || frame.AutoLayout == null || frame.AutoLayout.Mode == AutoLayoutMode.None) return result;

            var originalList = frame.Children ?? new List<LayerNode>();
            var moving = GetTopLevelLayers(movingLayers);
            var movingSet = new HashSet<LayerNode>(moving);
            var working = originalList.Where(n => n != null && !movingSet.Contains(n)).ToList();
            if (insertionIndex >= 0 && moving.Count > 0)
            {
                int listIndex = Math.Max(0, Math.Min(insertionIndex, working.Count));
                working.InsertRange(listIndex, moving);
            }

            var previewFrame = new LayerNode
            {
                Type = frame.Type,
                X = frame.X,
                Y = frame.Y,
                Width = frame.Width,
                Height = frame.Height,
                MinWidth = frame.MinWidth,
                MaxWidth = frame.MaxWidth,
                MinHeight = frame.MinHeight,
                MaxHeight = frame.MaxHeight,
                AutoLayout = frame.AutoLayout.Clone(),
                Children = new List<LayerNode>()
            };
            var previewByOriginal = new Dictionary<LayerNode, LayerNode>();
            foreach (var node in working)
            {
                var previewNode = node.Clone();
                previewNode.ParentId = previewFrame.Id;
                if (movingSet.Contains(node)) previewNode.IgnoreAutoLayout = false;
                MapAutoLayoutPreviewTree(node, previewNode, previewByOriginal);
                previewFrame.Children.Add(previewNode);
            }

            AutoLayoutEngine.ReflowRecursive(previewFrame);
            foreach (var node in working)
            {
                if (node != null && !movingSet.Contains(node) && node.IsVisible && !node.IgnoreAutoLayout)
                    CaptureAutoLayoutPreviewPositions(node, previewByOriginal, result);
            }
            return result;
        }

        private static void MapAutoLayoutPreviewTree(LayerNode original, LayerNode preview, Dictionary<LayerNode, LayerNode> map)
        {
            if (original == null || preview == null) return;
            map[original] = preview;
            int count = Math.Min(original.Children != null ? original.Children.Count : 0, preview.Children != null ? preview.Children.Count : 0);
            for (int i = 0; i < count; i++)
                MapAutoLayoutPreviewTree(original.Children[i], preview.Children[i], map);
        }

        private static void CaptureAutoLayoutPreviewPositions(LayerNode original, Dictionary<LayerNode, LayerNode> previewMap, Dictionary<LayerNode, Point> positions)
        {
            if (original == null) return;
            LayerNode preview;
            if (previewMap.TryGetValue(original, out preview) && preview != null)
                positions[original] = new Point(preview.X, preview.Y);
            if (original.Children == null) return;
            foreach (var child in original.Children)
                CaptureAutoLayoutPreviewPositions(child, previewMap, positions);
        }

        private static void CapturePreviewSubtreeState(
            LayerNode node,
            Dictionary<LayerNode, Rect> bounds,
            Dictionary<LayerNode, bool> ignoreStates,
            Dictionary<LayerNode, VectorGeometry> geometries)
        {
            if (node == null || bounds.ContainsKey(node)) return;
            bounds[node] = new Rect(node.X, node.Y, node.Width, node.Height);
            ignoreStates[node] = node.IgnoreAutoLayout;
            if (node.Type == ElementType.Path && node.Geometry != null)
                geometries[node] = node.Geometry.Clone();
            if (node.Children == null) return;
            foreach (var child in node.Children)
                CapturePreviewSubtreeState(child, bounds, ignoreStates, geometries);
        }

        private static void CapturePreviewSubtreePositions(LayerNode node, Dictionary<LayerNode, Point> positions)
        {
            if (node == null) return;
            positions[node] = new Point(node.X, node.Y);
            if (node.Children == null) return;
            foreach (var child in node.Children)
                CapturePreviewSubtreePositions(child, positions);
        }

        private void AnimateAutoLayoutOrderPreview(LayerNode sourceFrame, LayerNode targetFrame, List<LayerNode> moving, int insertionIndex)
        {
            LayerNode oldFrame = _autoLayoutLivePreviewFrame ?? sourceFrame;
            List<LayerNode> oldMoving = _autoLayoutLivePreviewFrame != null
                ? _autoLayoutLivePreviewMovingLayers
                : moving;
            bool targetIsAutoLayout = targetFrame != null && targetFrame.AutoLayout != null && targetFrame.AutoLayout.Mode != AutoLayoutMode.None;

            var endPositions = new Dictionary<LayerNode, Point>();
            if (oldFrame != null && oldFrame != targetFrame)
            {
                foreach (var item in ComputeAutoLayoutPreviewPositions(oldFrame, oldMoving, -1))
                    endPositions[item.Key] = item.Value;
            }
            if (targetIsAutoLayout)
            {
                foreach (var item in ComputeAutoLayoutPreviewPositions(targetFrame, moving, insertionIndex))
                    endPositions[item.Key] = item.Value;
            }

            var startPositions = endPositions.Keys.ToDictionary(node => node, node => new Point(node.X, node.Y));
            AnimateAutoLayoutPositions(startPositions, endPositions, 155.0);
            _autoLayoutLivePreviewFrame = targetIsAutoLayout ? targetFrame : null;
            _autoLayoutLivePreviewMovingLayers = targetIsAutoLayout ? moving.ToList() : new List<LayerNode>();
        }

        private void RestoreAutoLayoutOrderPreview()
        {
            var frame = _autoLayoutLivePreviewFrame;
            if (frame != null && frame.AutoLayout != null && frame.AutoLayout.Mode != AutoLayoutMode.None)
            {
                var children = frame.Children ?? new List<LayerNode>();
                var starts = new Dictionary<LayerNode, Point>();
                var layerBounds = new Dictionary<LayerNode, Rect>();
                var ignoreStates = new Dictionary<LayerNode, bool>();
                var geometries = new Dictionary<LayerNode, VectorGeometry>();
                foreach (var child in children.Where(node => node != null))
                {
                    CapturePreviewSubtreeState(child, layerBounds, ignoreStates, geometries);
                    if (child.IsVisible && !child.IgnoreAutoLayout)
                        CapturePreviewSubtreePositions(child, starts);
                }
                var frameBounds = new Rect(frame.X, frame.Y, frame.Width, frame.Height);
                AutoLayoutEngine.ReflowRecursive(frame);
                var ends = new Dictionary<LayerNode, Point>();
                foreach (var child in frame.Children ?? new List<LayerNode>())
                {
                    if (child != null && child.IsVisible && !child.IgnoreAutoLayout)
                        CapturePreviewSubtreePositions(child, ends);
                }
                frame.X = frameBounds.X;
                frame.Y = frameBounds.Y;
                frame.Width = frameBounds.Width;
                frame.Height = frameBounds.Height;
                foreach (var item in layerBounds)
                {
                    item.Key.X = item.Value.X;
                    item.Key.Y = item.Value.Y;
                    item.Key.Width = item.Value.Width;
                    item.Key.Height = item.Value.Height;
                    item.Key.IgnoreAutoLayout = ignoreStates[item.Key];
                    VectorGeometry geometry;
                    if (geometries.TryGetValue(item.Key, out geometry))
                        item.Key.Geometry = geometry != null ? geometry.Clone() : null;
                }
                AnimateAutoLayoutPositions(starts, ends, 155.0);
            }
            _autoLayoutLivePreviewFrame = null;
            _autoLayoutLivePreviewMovingLayers.Clear();
        }

        private static List<LayerNode> GetTopLevelLayers(IEnumerable<LayerNode> layers)
        {
            var result = (layers ?? Enumerable.Empty<LayerNode>()).Where(l => l != null).Distinct().ToList();
            return result.Where(l => !result.Any(other => other != l && Kerf.Model.Page.IsDescendant(other, l))).ToList();
        }

        private void UpdateAutoLayoutDropPreview(Point canvasPoint, IEnumerable<LayerNode> movingLayers)
        {
            if (Document == null) return;
            var page = Document.GetActivePage();
            var moving = GetTopLevelLayers(movingLayers);
            if (page == null || moving.Count == 0) return;
            _autoLayoutDraggedLayers = moving;

            var bounds = TransformEngine.UnionBounds(moving);
            LayerNode source = null;
            if (moving.Count == 1 && !string.IsNullOrEmpty(moving[0].ParentId))
            {
                var parent = page.FindLayerById(moving[0].ParentId);
                if (parent != null && parent.AutoLayout != null && parent.AutoLayout.Mode != AutoLayoutMode.None)
                    source = parent;
            }

            LayerNode target = null;
            bool detach = false;
            var frames = page.GetAllLayersFlat().Where(f => f.Type == ElementType.Frame &&
                !moving.Any(m => m == f || Kerf.Model.Page.IsDescendant(m, f)) &&
                !_altOriginalLayers.Any(orig => orig == f || Kerf.Model.Page.IsDescendant(orig, f))).ToList();
            for (int i = frames.Count - 1; i >= 0; i--)
            {
                if (frames[i].GetBounds().Contains(canvasPoint))
                {
                    target = frames[i];
                    break;
                }
            }
            if (_hasAltDuplicated && _altOriginalLayers.Count > 0 && _altOriginalLayers.All(orig => string.IsNullOrEmpty(orig.ParentId)))
            {
                target = null;
            }
            if (target == null && source != null)
            {
                Rect sourceBounds = source.GetBounds();
                double threshold = 24.0 / Math.Max(0.25, _zoomScale);
                Rect sticky = new Rect(sourceBounds.X - threshold, sourceBounds.Y - threshold,
                    sourceBounds.Width + threshold * 2.0, sourceBounds.Height + threshold * 2.0);
                if (sticky.Contains(canvasPoint)) target = source;
            }
            detach = source != null && target == null;

            int index = target != null
                ? (target.AutoLayout != null && target.AutoLayout.Mode != AutoLayoutMode.None
                    ? FindAutoLayoutInsertionIndex(target, moving, canvasPoint)
                    : (target.Children != null ? target.Children.Count : 0))
                : -1;
            bool changedTarget = target != _autoLayoutDropTarget || index != _autoLayoutDropIndex || detach != _autoLayoutDetachPreview;
            _autoLayoutDropSource = source;
            _autoLayoutDropTarget = target;
            _autoLayoutDropIndex = index;
            _autoLayoutDetachPreview = detach;
            _autoLayoutDropPoint = canvasPoint;
            _autoLayoutDropPreviewBounds = target != null
                ? AutoLayoutEngine.PreviewInsertionBounds(target, moving, index, Math.Max(1.0, bounds.Width), Math.Max(1.0, bounds.Height))
                : Rect.Empty;

            if (changedTarget)
            {
                AnimateAutoLayoutOrderPreview(source, target, moving, index);
                _autoLayoutDropPreviewVisible = false;
                _autoLayoutHoverStartUtc = DateTime.UtcNow;
                if (_autoLayoutPreviewTimer == null)
                {
                    _autoLayoutPreviewTimer = new DispatcherTimer();
                    _autoLayoutPreviewTimer.Interval = TimeSpan.FromMilliseconds(720);
                    _autoLayoutPreviewTimer.Tick += (s, e) =>
                    {
                        _autoLayoutPreviewTimer.Stop();
                        if (_autoLayoutDropTarget != null && (_isTransforming || _isDrawingNew))
                        {
                            _autoLayoutDropPreviewVisible = true;
                            RefreshCanvas();
                        }
                    };
                }
                _autoLayoutPreviewTimer.Stop();
                if (target != null) _autoLayoutPreviewTimer.Start();
            }
        }

        private int FindAutoLayoutInsertionIndex(LayerNode frame, List<LayerNode> moving, Point center)
        {
            if (frame == null || frame.Children == null) return 0;
            var movingIds = new HashSet<string>(moving.Where(l => l != null).Select(l => l.Id));
            var allSiblings = frame.Children
                .Where(child => child != null && !movingIds.Contains(child.Id))
                .ToList();
            if (frame.AutoLayout == null || frame.AutoLayout.Mode == AutoLayoutMode.None) return allSiblings.Count;
            var flowSiblings = allSiblings.Where(child => child.IsVisible && !child.IgnoreAutoLayout).ToList();
            if (flowSiblings.Count == 0) return allSiblings.Count;

            int flowInsertionIndex;
            if (frame.AutoLayout.Mode == AutoLayoutMode.Horizontal)
            {
                flowInsertionIndex = flowSiblings.Count(child => center.X >= child.X + child.Width / 2.0);
            }
            else if (frame.AutoLayout.Mode == AutoLayoutMode.Vertical)
            {
                flowInsertionIndex = flowSiblings.Count(child => center.Y >= child.Y + child.Height / 2.0);
            }
            else
            {
                int nearestIndex = 0;
                double nearestDistance = double.MaxValue;
                for (int i = 0; i < flowSiblings.Count; i++)
                {
                    var child = flowSiblings[i];
                    double dx = center.X - (child.X + child.Width / 2.0);
                    double dy = center.Y - (child.Y + child.Height / 2.0);
                    double distance = dx * dx + dy * dy;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestIndex = i;
                    }
                }
                var nearest = flowSiblings[nearestIndex];
                bool sameRow = Math.Abs(center.Y - (nearest.Y + nearest.Height / 2.0)) < Math.Max(3.0, nearest.Height * 0.6);
                bool after = sameRow
                ? center.X >= nearest.X + nearest.Width / 2.0
                : center.Y >= nearest.Y + nearest.Height / 2.0;
                flowInsertionIndex = nearestIndex + (after ? 1 : 0);
            }

            flowInsertionIndex = Math.Max(0, Math.Min(flowSiblings.Count, flowInsertionIndex));
            if (flowInsertionIndex < flowSiblings.Count)
                return allSiblings.IndexOf(flowSiblings[flowInsertionIndex]);
            return allSiblings.IndexOf(flowSiblings[flowSiblings.Count - 1]) + 1;
        }

        private void ClearAutoLayoutDropPreview(bool restoreLivePreview = true)
        {
            if (_autoLayoutPreviewTimer != null) _autoLayoutPreviewTimer.Stop();
            if (restoreLivePreview) RestoreAutoLayoutOrderPreview();
            else
            {
                _autoLayoutLivePreviewFrame = null;
                _autoLayoutLivePreviewMovingLayers.Clear();
            }
            _autoLayoutDropTarget = null;
            _autoLayoutDropSource = null;
            _autoLayoutDropIndex = -1;
            _autoLayoutDropPreviewBounds = Rect.Empty;
            _autoLayoutDropPreviewVisible = false;
            _autoLayoutDetachPreview = false;
            _autoLayoutDraggedLayers.Clear();
        }

        private LayerNode ResolveAutoLayoutSelectionTarget(LayerNode hit)
        {
            if (hit == null) return null;
            bool control = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            if (control) return hit;
            if (hit.Type == ElementType.Frame && hit.AutoLayout != null && hit.AutoLayout.Mode != AutoLayoutMode.None)
                return hit;
            var page = Document != null ? Document.GetActivePage() : null;
            string parentId = hit.ParentId;
            while (page != null && !string.IsNullOrEmpty(parentId))
            {
                var parent = page.FindLayerById(parentId);
                if (parent == null) break;
                if (parent.Type == ElementType.Frame && parent.AutoLayout != null && parent.AutoLayout.Mode != AutoLayoutMode.None)
                    return parent;
                parentId = parent.ParentId;
            }
            return hit;
        }

        private LayerNode ResolveDeepSelectionTarget(LayerNode hit, int clickCount)
        {
            if (hit == null || clickCount < 2) return hit;
            var page = Document != null ? Document.GetActivePage() : null;
            if (page == null) return hit;

            var path = new List<LayerNode>();
            LayerNode current = hit;
            while (current != null)
            {
                path.Add(current);
                if (string.IsNullOrEmpty(current.ParentId)) break;
                current = page.FindLayerById(current.ParentId);
            }
            path.Reverse();

            int rootContainerIndex = -1;
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i].Type == ElementType.Frame || path[i].Type == ElementType.Group)
                {
                    rootContainerIndex = i;
                }
            }
            // The first click inside an auto layout selects its nearest layout frame. Continue
            // into that frame on the next click, then walk one level deeper with each click.
            int nearestAutoLayoutIndex = -1;
            for (int i = path.Count - 1; i >= 0; i--)
            {
                var candidate = path[i];
                if (candidate.Type == ElementType.Frame && candidate.AutoLayout != null && candidate.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    nearestAutoLayoutIndex = i;
                    break;
                }
            }
            if (nearestAutoLayoutIndex >= 0) rootContainerIndex = nearestAutoLayoutIndex;
            if (rootContainerIndex < 0) return hit;

            int targetIndex = Math.Min(path.Count - 1, rootContainerIndex + Math.Max(1, clickCount - 1));
            return path[targetIndex] ?? hit;
        }

        private bool HasAutoLayoutAncestor(LayerNode node)
        {
            if (node == null || Document == null) return false;
            var page = Document.GetActivePage();
            string parentId = node.ParentId;
            while (page != null && !string.IsNullOrEmpty(parentId))
            {
                var parent = page.FindLayerById(parentId);
                if (parent == null) break;
                if (parent.Type == ElementType.Frame && parent.AutoLayout != null && parent.AutoLayout.Mode != AutoLayoutMode.None)
                    return true;
                parentId = parent.ParentId;
            }
            return false;
        }

        private static void SortAutoLayoutChildrenByCanvasOrder(LayerNode frame)
        {
            if (frame == null || frame.Children == null || frame.Children.Count < 2 || frame.AutoLayout == null) return;
            if (frame.AutoLayout.Mode == AutoLayoutMode.Horizontal)
                frame.Children = frame.Children.OrderBy(c => c.X + c.Width / 2.0).ToList();
            else if (frame.AutoLayout.Mode == AutoLayoutMode.Vertical)
                frame.Children = frame.Children.OrderBy(c => c.Y + c.Height / 2.0).ToList();
            else
                frame.Children = frame.Children.OrderBy(c => c.Y + c.Height / 2.0).ThenBy(c => c.X + c.Width / 2.0).ToList();
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
                if (_currentStudioMode == StudioMode.Design && _activeTool == ToolType.Select)
                {
                    var guide = FindGuideNear(canvasPt);
                    if (guide != null)
                    {
                        _activeGuideDrag = guide;
                        if (_canvasHost != null) _canvasHost.CaptureMouse();
                        RefreshCanvas();
                        return;
                    }
                }

                // In Animate mode, route click directly into State Machine
                if (_currentStudioMode == StudioMode.Animate && _interactiveCanvasEnabled && _timelineController != null && _timelineController.ActiveStateMachine != null)
                {
                    var hit = _sceneGraph.HitTest(canvasPt);
                    _stateMachinePointerDownNodeId = 0;
                    if (hit != null)
                    {
                        uint hitId = PropertyBinder.GetNumericNodeId(hit.Id);
                        _stateMachinePointerDownNodeId = hitId;
                        _timelineController.ActiveStateMachine.OnPointerEvent(hitId, PointerEventKind.Down);
                        SetStateMachineBool("isHovered", true);
                    }
                    SetStateMachineBool("isPressed", hit != null);
                    TriggerStateMachineStep(continuePlayback: true);
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
                    Rect effectiveBounds = union;

                    if (_currentStudioMode == StudioMode.Animate && _selectedLayers.Count == 1 && _selectedLayers[0].RenderTransform != null && !_selectedLayers[0].RenderTransform.IsIdentity)
                    {
                        var primary = _selectedLayers[0];
                        double curSx = Math.Max(0.001, primary.RenderTransform.ScaleX);
                        double curSy = Math.Max(0.001, primary.RenderTransform.ScaleY);
                        double visualW = primary.Width * curSx;
                        double visualH = primary.Height * curSy;
                        double visualCenterX = primary.X + primary.Width / 2.0 + primary.RenderTransform.TranslationX;
                        double visualCenterY = primary.Y + primary.Height / 2.0 + primary.RenderTransform.TranslationY;
                        effectiveBounds = new Rect(visualCenterX - visualW / 2.0, visualCenterY - visualH / 2.0, visualW, visualH);
                        rot = primary.Rotation + primary.RenderTransform.Rotation;
                    }

                    var handle = TransformEngine.HitTestHandle(canvasPt, effectiveBounds, rot, _zoomScale);
                    if (handle != HandleType.None)
                    {
                        _activeHandle = handle;
                        _isTransforming = true;
                        _transformStartBounds = effectiveBounds;
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

                // 7. Centralized Canvas Interaction Pipeline: Hit Testing -> Selection Resolution -> Drag Target Resolution
                if (_activeTool == ToolType.Select)
                {
                    var page = Document != null ? Document.GetActivePage() : null;
                    var hitResult = CanvasInteractionController.HitTest(page, canvasPt);

                    // Double-click on an already-selected leaf Path / Text enters direct edit gesture
                    if (clickCount >= 2 && hitResult.HasHit)
                    {
                        var leaf = hitResult.DeepestNode;
                        if (leaf != null && (_selectedLayers.Contains(leaf) || _selectedLayers.Count == 0))
                        {
                            if (leaf.Type == ElementType.Path)
                            {
                                EnterVectorEditMode(leaf);
                                return;
                            }
                            else if (leaf.Type == ElementType.Text)
                            {
                                StartInPlaceTextEditing(leaf);
                                return;
                            }
                        }
                    }

                    if (hitResult.HasHit)
                    {
                        bool control = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                        bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                        // Selection Resolver:
                        // Determines which object should become selected.
                        // Supports progressive parent-to-child selection, maintains hierarchy context.
                        var selectionHit = CanvasInteractionController.ResolveSelection(hitResult, _selectedLayers, clickCount, control, shift);

                        if (control)
                        {
                            _selectedLayers.Clear();
                            if (selectionHit != null) _selectedLayers.Add(selectionHit);
                        }
                        else if (shift)
                        {
                            if (selectionHit != null)
                            {
                                if (_selectedLayers.Contains(selectionHit)) _selectedLayers.Remove(selectionHit);
                                else _selectedLayers.Add(selectionHit);
                            }
                        }
                        else
                        {
                            if (selectionHit != null && !_selectedLayers.Contains(selectionHit))
                            {
                                _selectedLayers.Clear();
                                _selectedLayers.Add(selectionHit);
                            }
                        }

                        // Drag Target Resolver:
                        // Resolves the active transformation target.
                        // Pointer-down over descendants of an already-selected parent maintains that parent as drag target!
                        var dragTargets = CanvasInteractionController.ResolveDragTargets(hitResult, _selectedLayers, selectionHit, control, clickCount);

                        _activeHandle = HandleType.None;
                        _isTransforming = true;
                        _transformStartBounds = TransformEngine.UnionBounds(_selectedLayers);
                        _transformStartRot = 0;
                        _transformStartMouse = canvasPt;
                        _dragInitialPositions.Clear();
                        _dragInitialBounds.Clear();
                        _dragInitialRotations.Clear();
                        _dragInitialGeometries.Clear();
                        foreach (var l in dragTargets)
                        {
                            CollectDragInitialPositions(l, _dragInitialPositions);
                        }
                        ClearAutoLayoutDropPreview();
                        _hasAltDuplicated = false;
                        _altClonedLayers.Clear();
                        _altOriginalLayers.Clear();
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

            if (_activeGuideDrag != null)
            {
                _activeGuideDrag.Position = _activeGuideDrag.IsVertical ? canvasPt.X : canvasPt.Y;
                RefreshCanvas();
                _lastMousePos = screenPt;
                return;
            }

            if (_currentStudioMode == StudioMode.Animate && _interactiveCanvasEnabled && _timelineController != null && _timelineController.ActiveStateMachine != null)
            {
                var hit = _sceneGraph.HitTest(canvasPt);
                uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0;
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
                    SetStateMachineBool("isHovered", hitId != 0);
                    SetStateMachineBool("Hovering", hitId != 0);
                    TriggerStateMachineStep(continuePlayback: true);
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

                bool keepAspect = _selectedLayers.Any(l => l != null && l.LockAspectRatio) || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
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
                        if (_currentStudioMode == StudioMode.Animate)
                        {
                            // In Animate Mode: scaling a Frame/layer must scale its entire visual contents non-destructively
                            // via RenderTransform.ScaleX/Y and translation offsets, without mutating persistent layout dimensions.
                            if (n.RenderTransform == null) n.RenderTransform = new RenderTransform();
                            double sx = res.Bounds.Width / Math.Max(0.001, n.Width);
                            double sy = res.Bounds.Height / Math.Max(0.001, n.Height);
                            n.RenderTransform.ScaleX = Math.Max(0.001, sx);
                            n.RenderTransform.ScaleY = Math.Max(0.001, sy);
                            double newCenterX = res.Bounds.X + res.Bounds.Width / 2.0;
                            double newCenterY = res.Bounds.Y + res.Bounds.Height / 2.0;
                            n.RenderTransform.TranslationX = newCenterX - (n.X + n.Width / 2.0);
                            n.RenderTransform.TranslationY = newCenterY - (n.Y + n.Height / 2.0);
                            n.RenderTransform.Rotation = res.Rotation - n.Rotation;
                            _sceneGraph.InvalidateSubtree(n.Id);
                            RefreshCanvas();
                            RefreshPropertyInspector();
                        }
                        else
                        {
                            Rect oldBounds = new Rect(n.X, n.Y, n.Width, n.Height);
                            bool reflowOwnAutoLayout = false;
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
                                reflowOwnAutoLayout = true;
                            }
                            if (heightResized)
                            {
                                n.AutoLayout.SizingV = SizingMode.Fixed;
                                reflowOwnAutoLayout = true;
                            }
                        }

                        var activePage = Document != null ? Document.GetActivePage() : null;
                        var layoutParent = activePage != null && !string.IsNullOrEmpty(n.ParentId) ? activePage.FindLayerById(n.ParentId) : null;
                        if (layoutParent != null && layoutParent.AutoLayout != null && layoutParent.AutoLayout.Mode != AutoLayoutMode.None)
                        {
                            bool widthResized = (_activeHandle == HandleType.MiddleLeft || _activeHandle == HandleType.MiddleRight ||
                                                 _activeHandle == HandleType.TopLeft || _activeHandle == HandleType.TopRight ||
                                                 _activeHandle == HandleType.BottomLeft || _activeHandle == HandleType.BottomRight);
                            bool heightResized = (_activeHandle == HandleType.TopCenter || _activeHandle == HandleType.BottomCenter ||
                                                  _activeHandle == HandleType.TopLeft || _activeHandle == HandleType.TopRight ||
                                                  _activeHandle == HandleType.BottomLeft || _activeHandle == HandleType.BottomRight);
                            if (widthResized)
                            {
                                n.LayoutSizingH = SizingMode.Fixed;
                                n.AutoLayoutBaseWidth = n.Width;
                            }
                            if (heightResized)
                            {
                                n.LayoutSizingV = SizingMode.Fixed;
                                n.AutoLayoutBaseHeight = n.Height;
                            }
                        }

                        if (n.Type == ElementType.Path && n.Geometry != null)
                        {
                            BezierVectorEngine.ScaleGeometry(n.Geometry, oldBounds, res.Bounds);
                        }
                        ApplyParentTransformToDescendants(n, res.Bounds, res.Rotation);
                        if (reflowOwnAutoLayout) AutoLayoutEngine.ReflowRecursive(n);
                        ReflowAutoLayoutAncestors(n);
                        InvalidateLayoutFrameVisuals(n);
                        }
                    }
                    else
                    {
                        double scaleX = res.Bounds.Width / Math.Max(1, _transformStartBounds.Width);
                        double scaleY = res.Bounds.Height / Math.Max(1, _transformStartBounds.Height);
                        foreach (var n in _selectedLayers)
                        {
                            Rect oldBounds = _dragInitialBounds.ContainsKey(n) ? _dragInitialBounds[n] : new Rect(n.X, n.Y, n.Width, n.Height);
                            Rect newBounds = new Rect(
                                res.Bounds.X + (oldBounds.X - _transformStartBounds.X) * scaleX,
                                res.Bounds.Y + (oldBounds.Y - _transformStartBounds.Y) * scaleY,
                                oldBounds.Width * scaleX,
                                oldBounds.Height * scaleY);
                            n.X = newBounds.X;
                            n.Y = newBounds.Y;
                            n.Width = newBounds.Width;
                            n.Height = newBounds.Height;
                            if (n.Type == ElementType.Path && n.Geometry != null)
                            {
                                VectorGeometry initialGeometry;
                                if (_dragInitialGeometries.TryGetValue(n, out initialGeometry)) n.Geometry = initialGeometry.Clone();
                                BezierVectorEngine.ScaleGeometry(n.Geometry, oldBounds, newBounds);
                            }
                            double initialRotation;
                            if (_dragInitialRotations.TryGetValue(n, out initialRotation)) n.Rotation = initialRotation;
                            ApplyParentTransformToDescendants(n, res.Bounds, n.Rotation);
                            if (n.AutoLayout != null && n.AutoLayout.Mode != AutoLayoutMode.None) AutoLayoutEngine.ReflowRecursive(n);
                            ReflowAutoLayoutAncestors(n);
                            InvalidateLayoutFrameVisuals(n);
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

                            if (page.ShowGuides && page.Guides != null)
                            {
                                foreach (var guide in page.Guides)
                                {
                                    if (guide.IsVertical)
                                    {
                                        foreach (double edge in new[] { proposedLeft, proposedCenterX, proposedRight })
                                        {
                                            double diff = guide.Position - edge;
                                            if (Math.Abs(diff) <= snapThreshold && Math.Abs(diff) < Math.Abs(bestSnapDeltaX))
                                            {
                                                bestSnapDeltaX = diff;
                                                bestSnapX = guide.Position;
                                                snapSpanMinY = proposedTop;
                                                snapSpanMaxY = proposedBottom;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        foreach (double edge in new[] { proposedTop, proposedCenterY, proposedBottom })
                                        {
                                            double diff = guide.Position - edge;
                                            if (Math.Abs(diff) <= snapThreshold && Math.Abs(diff) < Math.Abs(bestSnapDeltaY))
                                            {
                                                bestSnapDeltaY = diff;
                                                bestSnapY = guide.Position;
                                                snapSpanMinX = proposedLeft;
                                                snapSpanMaxX = proposedRight;
                                            }
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
                        _altOriginalLayers = new List<LayerNode>(_selectedLayers);
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
                            LayerNode parent;
                            var container = page.FindContainer(orig, out parent);
                            if (parent != null) orig.ParentId = parent.Id;
                            PreserveDuplicateSizeInAutoLayout(clone, parent);
                            OffsetLayerSubtree(clone, dx, dy);
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
                    var movingSelection = new HashSet<LayerNode>(_dragInitialPositions.Keys);
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
                        OffsetLayerDescendantsExceptSelected(n, shiftX, shiftY, movingSelection);
                        _sceneGraph.InvalidateNode(n.Id);
                    }

                    UpdateAutoLayoutDropPreview(canvasPt, _selectedLayers);
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
                var page = Document != null ? Document.GetActivePage() : null;
                var hitResult = CanvasInteractionController.HitTest(page, canvasPt);
                if (hitResult.HasHit)
                {
                    var selectedAncestor = CanvasInteractionController.FindSelectedAncestor(hitResult, _selectedLayers);
                    var contextTarget = selectedAncestor ?? hitResult.OutermostNode ?? hitResult.DeepestNode;
                    if (!_selectedLayers.Contains(contextTarget))
                    {
                        _selectedLayers.Clear();
                        _selectedLayers.Add(contextTarget);
                        OnSelectionChanged();
                        RefreshCanvas();
                    }
                    var cm = CreateLayerContextMenu(contextTarget);
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

            if (button == MouseButton.Left && _activeGuideDrag != null)
            {
                var page = Document != null ? Document.GetActivePage() : null;
                if (_canvasHost != null && (screenPt.X < 0 || screenPt.Y < 0 || screenPt.X > _canvasHost.ActualWidth || screenPt.Y > _canvasHost.ActualHeight))
                {
                    if (page != null && page.Guides != null) page.Guides.Remove(_activeGuideDrag);
                }
                _activeGuideDrag = null;
                if (_canvasHost != null && _canvasHost.IsMouseCaptured) _canvasHost.ReleaseMouseCapture();
                History.MarkDirty();
                RefreshCanvas();
                RefreshPropertyInspector();
                return;
            }

            if (_currentStudioMode == StudioMode.Animate && _interactiveCanvasEnabled && _timelineController != null && _timelineController.ActiveStateMachine != null)
            {
                Point canvasPt = ScreenToCanvas(screenPt);
                var hit = _sceneGraph.HitTest(canvasPt);
                if (hit != null)
                {
                    uint hitId = PropertyBinder.GetNumericNodeId(hit.Id);
                    _timelineController.ActiveStateMachine.OnPointerEvent(hitId, PointerEventKind.Up);
                    if (hitId == _stateMachinePointerDownNodeId)
                        SetStateMachineTrigger("clickTrigger");
                }
                SetStateMachineBool("isPressed", false);
                _stateMachinePointerDownNodeId = 0;
                TriggerStateMachineStep(continuePlayback: true);
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
                    LayerNode parent = null;
                    if (!string.IsNullOrEmpty(_drawingNode.ParentId))
                    {
                        parent = page.FindLayerById(_drawingNode.ParentId);
                    }
                    int idx = (parent != null && parent.Children != null) ? parent.Children.IndexOf(_drawingNode) : page.Layers.IndexOf(_drawingNode);

                    var parentToReflow = parent;
                    var cmd = new AddLayerCommand(page, _drawingNode, parent != null ? parent.Id : null, idx, () =>
                    {
                        if (parentToReflow != null && parentToReflow.AutoLayout != null && parentToReflow.AutoLayout.Mode != AutoLayoutMode.None)
                        {
                            AutoLayoutEngine.ReflowRecursive(parentToReflow);
                            InvalidateLayoutFrameVisuals(parentToReflow);
                        }
                        _sceneGraph.Synchronize(page);
                        RefreshCanvas();
                        RefreshLayersList();
                    });
                    History.Execute(cmd);

                    if (parent != null && parent.AutoLayout != null && parent.AutoLayout.Mode != AutoLayoutMode.None)
                    {
                        AutoLayoutEngine.ReflowRecursive(parent);
                        ReflowAutoLayoutAncestors(parent);
                        InvalidateLayoutFrameVisuals(parent);
                    }

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
                    RefreshCanvas();
                    RefreshLayersList();
                }
                ClearAutoLayoutDropPreview(restoreLivePreview: false);
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
                        AutoKeyProperty(primary, PropKey.CornerRadius, afterR);
                    }
                    else
                    {
                        History.MarkDirty();
                        RefreshCanvas();
                        RefreshPropertyInspector();
                    }
                    return;
                }

                bool didAutoLayoutDrop = false;
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
                            if (!_hasAltDuplicated)
                            {
                                CheckAutoReparentLayersOnDrop(_selectedLayers);
                                didAutoLayoutDrop = true;
                            }
                        }
                    }
                }

                if (_hasAltDuplicated)
                {
                    var page = Document.GetActivePage();
                    var positionsBeforeDuplicateDrop = page.GetAllLayersFlat().ToDictionary(layer => layer, layer => new Point(layer.X, layer.Y));
                    var affectedLayouts = CommitDuplicateDrop(page, _altClonedLayers);
                    didAutoLayoutDrop = _autoLayoutDropTarget != null || _autoLayoutDetachPreview || _autoLayoutDropSource != null;
                    History.Execute(new AddLayersCommand(page, _altClonedLayers, () =>
                    {
                        foreach (var parent in affectedLayouts)
                        {
                            if (parent.AutoLayout != null && parent.AutoLayout.Mode != AutoLayoutMode.None)
                                AutoLayoutEngine.ReflowRecursive(parent);
                            InvalidateLayoutFrameVisuals(parent);
                        }
                        _sceneGraph.Synchronize(page);
                        foreach (var layer in _altClonedLayers) InvalidateLayoutFrameVisuals(layer);
                        RefreshCanvas();
                        RefreshLayersList();
                    }));
                    AnimateAutoLayoutReflow(positionsBeforeDuplicateDrop, page.GetAllLayersFlat());
                    OnSelectionChanged();
                    RefreshLayersList();
                    RefreshCanvas();
                    History.MarkDirty();
                    if (_currentStudioMode == StudioMode.Animate)
                        foreach (var layer in _altClonedLayers) CaptureAnimationRestPoseForLayer(layer);
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
                                (_dragInitialRotations.ContainsKey(node) && Math.Abs(node.Rotation - _dragInitialRotations[node]) > 0.001) ||
                                (_currentStudioMode == StudioMode.Animate && node.RenderTransform != null && !node.RenderTransform.IsIdentity))
                            {
                                changed = true;
                                break;
                            }
                        }
                        if (changed)
                        {
                            if (_currentStudioMode == StudioMode.Animate)
                            {
                                History.MarkDirty();
                                foreach (var layer in _dragInitialBounds.Keys)
                                {
                                    CaptureAnimationRestPoseForLayer(layer);
                                    if (layer.RenderTransform != null)
                                    {
                                        if (Math.Abs(layer.RenderTransform.ScaleX - 1.0) > 0.001)
                                            AutoKeyProperty(layer, PropKey.ScaleX, layer.RenderTransform.ScaleX);
                                        if (Math.Abs(layer.RenderTransform.ScaleY - 1.0) > 0.001)
                                            AutoKeyProperty(layer, PropKey.ScaleY, layer.RenderTransform.ScaleY);
                                        if (Math.Abs(layer.RenderTransform.TranslationX) > 0.001)
                                            AutoKeyProperty(layer, PropKey.X, layer.X + layer.RenderTransform.TranslationX);
                                        if (Math.Abs(layer.RenderTransform.TranslationY) > 0.001)
                                            AutoKeyProperty(layer, PropKey.Y, layer.Y + layer.RenderTransform.TranslationY);
                                        if (Math.Abs(layer.RenderTransform.Rotation) > 0.001)
                                            AutoKeyProperty(layer, PropKey.Rotation, layer.Rotation + layer.RenderTransform.Rotation);
                                    }
                                    _sceneGraph.InvalidateSubtree(layer.Id);
                                }
                                RefreshCanvas();
                                RefreshPropertyInspector();
                            }
                            else
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
                            if (_currentStudioMode == StudioMode.Animate)
                            {
                                foreach (var layer in afterPositions.Keys)
                                {
                                    CaptureAnimationRestPoseForLayer(layer);
                                    Point before = _dragInitialPositions[layer];
                                    Point after = afterPositions[layer];
                                    if (Math.Abs(before.X - after.X) > 0.001) AutoKeyProperty(layer, PropKey.X, layer.X);
                                    if (Math.Abs(before.Y - after.Y) > 0.001) AutoKeyProperty(layer, PropKey.Y, layer.Y);
                                }
                            }
                        }
                    }
                }

                _dragInitialPositions.Clear();
                _dragInitialBounds.Clear();
                _dragInitialRotations.Clear();
                _dragInitialGeometries.Clear();
                _hasAltDuplicated = false;
                _altClonedLayers.Clear();
                _altOriginalLayers.Clear();
                ClearAutoLayoutDropPreview(restoreLivePreview: !didAutoLayoutDrop);
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
            string defaultColor = "#D9D9D9";

            switch (tool)
            {
                case ToolType.Frame: elemType = ElementType.Frame; defaultColor = "#D9D9D9"; break;
                case ToolType.Rect: elemType = ElementType.Rectangle; defaultColor = "#D9D9D9"; break;
                case ToolType.Ellipse: elemType = ElementType.Ellipse; defaultColor = "#D9D9D9"; break;
                case ToolType.Star: elemType = ElementType.Star; defaultColor = "#D9D9D9"; break;
                case ToolType.Polygon: elemType = ElementType.Polygon; defaultColor = "#D9D9D9"; break;
                case ToolType.Text: elemType = ElementType.Text; defaultColor = "#D9D9D9"; break;
                case ToolType.Line: elemType = ElementType.Line; defaultColor = "#D9D9D9"; break;
                case ToolType.Arrow: elemType = ElementType.Arrow; defaultColor = "#D9D9D9"; break;
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
            var target = InsertionTargetResolver.ResolveTarget(page, _selectedLayers);
            InsertionTargetResolver.InsertObject(page, target, _drawingNode, reflowAutoLayout: false);
            _sceneGraph.Synchronize(page);
        }

        private void HandlePenClick(Point canvasPt)
        {
            var page = Document.GetActivePage();
            if (_activePenNode == null)
            {
                var target = InsertionTargetResolver.ResolveTarget(page, _selectedLayers);
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
                    Fills = new List<Paint> { Paint.Solid("#D9D9D9", 0.25) },
                    Strokes = new List<StrokeData> { new StrokeData { Paint = Paint.Solid("#D9D9D9"), Width = 2.0 } }
                };
                InsertionTargetResolver.InsertObject(page, target, _activePenNode, reflowAutoLayout: false);
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
                LayerNode penParent = null;
                if (!string.IsNullOrEmpty(_activePenNode.ParentId))
                {
                    penParent = page.FindLayerById(_activePenNode.ParentId);
                }
                int penIdx = penParent != null && penParent.Children != null ? penParent.Children.IndexOf(_activePenNode) : page.Layers.IndexOf(_activePenNode);
                var parentToReflow = penParent;
                History.Execute(new AddLayerCommand(page, _activePenNode, penParent != null ? penParent.Id : null, penIdx, () =>
                {
                    if (parentToReflow != null && parentToReflow.AutoLayout != null && parentToReflow.AutoLayout.Mode != AutoLayoutMode.None)
                    {
                        AutoLayoutEngine.ReflowRecursive(parentToReflow);
                        InvalidateLayoutFrameVisuals(parentToReflow);
                    }
                    _sceneGraph.Synchronize(page);
                    RefreshCanvas();
                    RefreshLayersList();
                }));
                if (penParent != null && penParent.AutoLayout != null && penParent.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    AutoLayoutEngine.ReflowRecursive(penParent);
                    ReflowAutoLayoutAncestors(penParent);
                    InvalidateLayoutFrameVisuals(penParent);
                }
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
            var guidePage = Document != null ? Document.GetActivePage() : null;
            if (guidePage != null && guidePage.ShowGuides && guidePage.Guides != null && _canvasHost != null)
            {
                Point topLeft = ScreenToCanvas(new Point(0, 0));
                Point bottomRight = ScreenToCanvas(new Point(_canvasHost.ActualWidth, _canvasHost.ActualHeight));
                foreach (var guide in guidePage.Guides)
                {
                    bool active = guide == _activeGuideDrag;
                    var guidePen = new Pen(new SolidColorBrush(active ? Color.FromRgb(244, 114, 182) : Color.FromRgb(56, 189, 248)), (active ? 1.8 : 1.0) / Math.Max(0.05, _zoomScale));
                    guidePen.Freeze();
                    if (guide.IsVertical)
                        dc.DrawLine(guidePen, new Point(guide.Position, topLeft.Y), new Point(guide.Position, bottomRight.Y));
                    else
                        dc.DrawLine(guidePen, new Point(topLeft.X, guide.Position), new Point(bottomRight.X, guide.Position));
                }
            }

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

                var marqueeFill = new SolidColorBrush(Color.FromArgb(35, 0x46, 0xEF, 0xB1)); // Brand Electric Mint @ 14%
                marqueeFill.Freeze();
                var marqueeStroke = new Pen(new SolidColorBrush(Color.FromArgb(220, 0x46, 0xEF, 0xB1)), 1.2 / _zoomScale);
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

            // Auto Layout drag feedback: pink frame boundary, insertion slot, and delayed onion skin.
            if (_autoLayoutDropTarget != null && _autoLayoutDropTarget.AutoLayout != null && _autoLayoutDropTarget.AutoLayout.Mode != AutoLayoutMode.None)
            {
                Color pink = Color.FromRgb(236, 72, 153);
                var frameBrush = new SolidColorBrush(Color.FromArgb(14, pink.R, pink.G, pink.B));
                var framePen = new Pen(new SolidColorBrush(Color.FromArgb(230, pink.R, pink.G, pink.B)), 1.5 / _zoomScale);
                framePen.DashStyle = new DashStyle(new double[] { 5, 3 }, 0);
                frameBrush.Freeze();
                framePen.Freeze();
                dc.DrawRectangle(frameBrush, framePen, _autoLayoutDropTarget.GetBounds());

                if (!_autoLayoutDropPreviewBounds.IsEmpty)
                {
                    var ghost = _autoLayoutDropPreviewBounds;
                    if (_autoLayoutDropPreviewVisible && _autoLayoutDraggedLayers.Count > 0)
                    {
                        var moving = _autoLayoutDraggedLayers[0];
                        Color silhouette = pink;
                        if (moving.Fills != null && moving.Fills.Count > 0 && !string.IsNullOrEmpty(moving.Fills[0].ColorHex))
                            silhouette = GpuRenderer.ParseHexColor(moving.Fills[0].ColorHex, 0.32);
                        var ghostFill = new SolidColorBrush(Color.FromArgb(72, silhouette.R, silhouette.G, silhouette.B));
                        ghostFill.Freeze();
                        dc.DrawRoundedRectangle(ghostFill, null, new Rect(ghost.X + 3 / _zoomScale, ghost.Y + 4 / _zoomScale, ghost.Width, ghost.Height),
                            Math.Max(2, moving.CornerRadius) / _zoomScale, Math.Max(2, moving.CornerRadius) / _zoomScale);
                        var movingBounds = TransformEngine.UnionBounds(_autoLayoutDraggedLayers);
                        DrawAutoLayoutOnionSkin(dc, _autoLayoutDraggedLayers, ghost.X - movingBounds.X, ghost.Y - movingBounds.Y);
                        var onionPen = new Pen(new SolidColorBrush(Color.FromArgb(220, pink.R, pink.G, pink.B)), 1.4 / _zoomScale);
                        onionPen.DashStyle = DashStyles.Dash;
                        onionPen.Freeze();
                        dc.DrawRoundedRectangle(ghostFill, onionPen, ghost, Math.Max(2, moving.CornerRadius) / _zoomScale, Math.Max(2, moving.CornerRadius) / _zoomScale);
                    }

                    var markerPen = new Pen(new SolidColorBrush(pink), 3.0 / _zoomScale);
                    markerPen.StartLineCap = PenLineCap.Round;
                    markerPen.EndLineCap = PenLineCap.Round;
                    markerPen.Freeze();
                    if (_autoLayoutDropTarget.AutoLayout.Mode == AutoLayoutMode.Vertical)
                    {
                        double y = ghost.Y;
                        dc.DrawLine(markerPen, new Point(ghost.X - 5 / _zoomScale, y), new Point(ghost.Right + 5 / _zoomScale, y));
                    }
                    else
                    {
                        double x = ghost.X;
                        dc.DrawLine(markerPen, new Point(x, ghost.Y - 5 / _zoomScale), new Point(x, ghost.Bottom + 5 / _zoomScale));
                    }
                }
            }
            else if (_autoLayoutDetachPreview && _autoLayoutDropSource != null)
            {
                var text = new FormattedText("Remove from auto layout", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Semibold"), 11 / _zoomScale, Brushes.White);
                var chip = new Rect(_autoLayoutDropPoint.X + 14 / _zoomScale, _autoLayoutDropPoint.Y + 12 / _zoomScale,
                    text.Width + 18 / _zoomScale, text.Height + 10 / _zoomScale);
                var chipBrush = new SolidColorBrush(Color.FromRgb(219, 39, 119));
                chipBrush.Freeze();
                dc.DrawRoundedRectangle(chipBrush, null, chip, 5 / _zoomScale, 5 / _zoomScale);
                dc.DrawText(text, new Point(chip.X + 9 / _zoomScale, chip.Y + 5 / _zoomScale));
            }

            if (_selectedLayers.Count == 0) return;

            // Draw bounding box & 8 transform handles
            var union = TransformEngine.UnionBounds(_selectedLayers);
            if (union.IsEmpty) return;

            double rot = (_selectedLayers.Count == 1) ? _selectedLayers[0].Rotation : 0;
            var center = new Point(union.X + union.Width / 2.0, union.Y + union.Height / 2.0);

            var mat = Matrix.Identity;
            if (_currentStudioMode == StudioMode.Animate && _selectedLayers.Count == 1 && _selectedLayers[0].RenderTransform != null && !_selectedLayers[0].RenderTransform.IsIdentity)
            {
                var rtMat = _selectedLayers[0].GetRenderTransformMatrix();
                if (!rtMat.IsIdentity)
                {
                    mat = rtMat;
                }
                else if (Math.Abs(rot) > 0.001)
                {
                    mat.RotateAt(rot, center.X, center.Y);
                }
            }
            else if (Math.Abs(rot) > 0.001)
            {
                mat.RotateAt(rot, center.X, center.Y);
            }

            dc.PushTransform(new MatrixTransform(mat));

            // Auto Layout uses a pink dashed boundary so it reads as a layout container, separate from frames.
            bool isAutoLayoutSelection = _selectedLayers.Count == 1 && _selectedLayers[0].Type == ElementType.Frame &&
                _selectedLayers[0].AutoLayout != null && _selectedLayers[0].AutoLayout.Mode != AutoLayoutMode.None;
            Color selectionColor = isAutoLayoutSelection ? Color.FromRgb(236, 72, 153) : Color.FromRgb(99, 102, 241);
            var borderPen = new Pen(new SolidColorBrush(selectionColor), 1.5 / _zoomScale);
            if (isAutoLayoutSelection) borderPen.DashStyle = DashStyles.Dash;
            borderPen.Freeze();
            dc.DrawRectangle(null, borderPen, union);
            if (isAutoLayoutSelection)
            {
                string mode = _selectedLayers[0].AutoLayout.Mode.ToString().ToUpperInvariant();
                var tag = new FormattedText("AUTO LAYOUT  ·  " + mode, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Semibold"), 10 / _zoomScale, new SolidColorBrush(selectionColor));
                var tagRect = new Rect(union.X, union.Y - tag.Height - 7 / _zoomScale, tag.Width + 12 / _zoomScale, tag.Height + 5 / _zoomScale);
                var tagBrush = new SolidColorBrush(Color.FromArgb(225, 35, 13, 30));
                tagBrush.Freeze();
                dc.DrawRoundedRectangle(tagBrush, borderPen, tagRect, 3 / _zoomScale, 3 / _zoomScale);
                dc.DrawText(tag, new Point(tagRect.X + 6 / _zoomScale, tagRect.Y + 2 / _zoomScale));
            }

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
            var pillBg = new SolidColorBrush(Color.FromArgb(200, 0x0C, 0x42, 0x47));
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

        private void DrawAutoLayoutOnionSkin(DrawingContext dc, IEnumerable<LayerNode> layers, double offsetX, double offsetY)
        {
            if (dc == null || _renderer == null) return;
            dc.PushOpacity(0.30);
            foreach (var layer in layers ?? Enumerable.Empty<LayerNode>())
                DrawAutoLayoutOnionNode(dc, layer, offsetX, offsetY);
            dc.Pop();
        }

        private void DrawAutoLayoutOnionNode(DrawingContext dc, LayerNode layer, double offsetX, double offsetY)
        {
            if (layer == null || !layer.IsVisible || layer.Opacity <= 0.0) return;
            var visual = new DrawingVisual();
            _renderer.RenderNodeToVisual(layer, visual, _zoomScale);
            if (visual.Drawing != null)
            {
                dc.PushTransform(new TranslateTransform(offsetX, offsetY));
                dc.DrawDrawing(visual.Drawing);
                dc.Pop();
            }
            if (layer.Children != null)
                foreach (var child in layer.Children)
                    DrawAutoLayoutOnionNode(dc, child, offsetX, offsetY);
        }

        private void RenderVectorEditModeOverlay(DrawingContext dc, VectorGeometry geo)
        {
            if (geo == null || geo.Nodes == null) return;

            double aSize = 7.0 / _zoomScale;
            var anchorPen = new Pen(Brushes.White, 1.5 / _zoomScale);
            anchorPen.Freeze();
            var handlePen = new Pen(KerfBrand.MintBrush, 1.2 / _zoomScale);
            handlePen.Freeze();
            var selGlowPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 0x46, 0xEF, 0xB1)), 3.0 / _zoomScale);
            selGlowPen.Freeze();

            var normalAnchorBrush = Brushes.White;
            var normalAnchorPen = new Pen(KerfBrand.TealBrush, 1.5 / _zoomScale);
            normalAnchorPen.Freeze();

            var selAnchorBrush = KerfBrand.MintBrush;
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
                    KerfBrand.MintBrush);
                var badgeBg = new SolidColorBrush(Color.FromArgb(230, 0x0C, 0x42, 0x47));
                badgeBg.Freeze();
                var badgeRect = new Rect(b.X + b.Width / 2.0 - badgeText.Width / 2.0 - 6 / _zoomScale, b.Y - 26 / _zoomScale, badgeText.Width + 12 / _zoomScale, badgeText.Height + 6 / _zoomScale);
                dc.DrawRoundedRectangle(badgeBg, new Pen(KerfBrand.MintBrush, 1.0 / _zoomScale), badgeRect, 4 / _zoomScale, 4 / _zoomScale);
                dc.DrawText(badgeText, new Point(b.X + b.Width / 2.0 - badgeText.Width / 2.0, b.Y - 23 / _zoomScale));
            }
        }

        private void RenderBezierNodesOverlay(DrawingContext dc, VectorGeometry geo)
        {
            if (geo == null || geo.Nodes == null) return;

            var anchorBrush = KerfBrand.MintBrush;
            var anchorPen = new Pen(Brushes.White, 1.5 / _zoomScale);
            var handlePen = new Pen(KerfBrand.TealBrush, 1.0 / _zoomScale);
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
            var normalAnchorPen = new Pen(KerfBrand.TealBrush, 1.5 / _zoomScale);
            normalAnchorPen.Freeze();

            var activeAnchorBrush = KerfBrand.MintBrush;
            var activeAnchorPen = new Pen(Brushes.White, 1.5 / _zoomScale);
            activeAnchorPen.Freeze();

            var selGlowPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0x46, 0xEF, 0xB1)), 3.5 / _zoomScale);
            selGlowPen.Freeze();

            var handlePen = new Pen(KerfBrand.MintBrush, 1.2 / _zoomScale);
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

                var previewPen = new Pen(new SolidColorBrush(Color.FromArgb(190, 0x46, 0xEF, 0xB1)), 1.5 / _zoomScale)
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
                    // Loop closure indicator: glowing amber ring around the first anchor
                    var loopPen = new Pen(KerfBrand.AmberBrush, 2.0 / _zoomScale);
                    loopPen.Freeze();
                    dc.DrawEllipse(null, loopPen, first.Anchor, 9.0 / _zoomScale, 9.0 / _zoomScale);

                    // Loop badge
                    var typeFace = new Typeface("Segoe UI");
                    var loopText = new FormattedText("Close Path", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeFace, 10 / _zoomScale, KerfBrand.ObsidianBrush);
                    var textBg = KerfBrand.AmberBrush;
                    var textRect = new Rect(first.Anchor.X + 12 / _zoomScale, first.Anchor.Y - 8 / _zoomScale, loopText.Width + 8 / _zoomScale, loopText.Height + 4 / _zoomScale);
                    dc.DrawRoundedRectangle(textBg, null, textRect, 3 / _zoomScale, 3 / _zoomScale);
                    dc.DrawText(loopText, new Point(first.Anchor.X + 16 / _zoomScale, first.Anchor.Y - 6 / _zoomScale));
                }
                else
                {
                    // Small crosshair target ring at hover point
                    var targetPen = new Pen(KerfBrand.MintBrush, 1.2 / _zoomScale);
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
                KerfBrand.MintBrush);

            var hudBg = new SolidColorBrush(Color.FromArgb(230, 0x0C, 0x42, 0x47));
            hudBg.Freeze();
            var hudBorderPen = new Pen(KerfBrand.TealBrush, 1.0 / _zoomScale);
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
            if (History != null) History.MarkDirty();
            if (_currentStudioMode == StudioMode.Animate) CaptureAnimationRestPoseForLayer(node);
            ReflowAutoLayoutAncestors(node);
            _sceneGraph.InvalidateSubtree(node.Id);
            RefreshCanvas();
        }

        private void ReflowAutoLayoutAncestors(LayerNode node)
        {
            if (node == null || Document == null) return;
            var page = Document.GetActivePage();
            if (page == null) return;
            var visited = new HashSet<LayerNode>();
            LayerNode parent = !string.IsNullOrEmpty(node.ParentId) ? page.FindLayerById(node.ParentId) : null;
            while (parent != null && visited.Add(parent))
            {
                if (parent.AutoLayout != null && parent.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    // Reflow this container's direct children only. Nested auto-layout
                    // frames own their contents and must keep their internal layout.
                    AutoLayoutEngine.ReflowRecursive(parent);
                    InvalidateLayoutFrameVisuals(parent);
                }
                parent = !string.IsNullOrEmpty(parent.ParentId) ? page.FindLayerById(parent.ParentId) : null;
            }
        }

        private void InvalidateLayoutFrameVisuals(LayerNode frame)
        {
            if (frame == null) return;
            _sceneGraph.InvalidateNode(frame.Id);
            if (frame.Children == null) return;
            foreach (var child in frame.Children) InvalidateLayoutFrameVisuals(child);
        }

        private void SetParentAutoLayoutSizingFixed(LayerNode node, bool horizontal)
        {
            if (node == null || Document == null || string.IsNullOrEmpty(node.ParentId)) return;
            var page = Document.GetActivePage();
            var parent = page != null ? page.FindLayerById(node.ParentId) : null;
            if (parent == null || parent.AutoLayout == null || parent.AutoLayout.Mode == AutoLayoutMode.None) return;
            if (horizontal)
            {
                node.LayoutSizingH = SizingMode.Fixed;
                node.AutoLayoutBaseWidth = node.Width;
            }
            else
            {
                node.LayoutSizingV = SizingMode.Fixed;
                node.AutoLayoutBaseHeight = node.Height;
            }
        }

        private static void SetChildLayoutSizingMode(LayerNode node, bool horizontal, SizingMode mode)
        {
            if (node == null) return;
            if (horizontal)
            {
                if (mode == SizingMode.Fill || mode == SizingMode.Fixed)
                    node.AutoLayoutBaseWidth = node.Width;
                node.LayoutSizingH = mode;
            }
            else
            {
                if (mode == SizingMode.Fill || mode == SizingMode.Fixed)
                    node.AutoLayoutBaseHeight = node.Height;
                node.LayoutSizingV = mode;
            }
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

            return SaveDocumentToPath(CurrentFilePath);
        }

        private bool SaveDocumentToPath(string targetPath)
        {
            if (Document == null || string.IsNullOrEmpty(targetPath)) return false;
            double previewTime = _timelineController != null ? _timelineController.CurrentTime : 0.0;
            bool restorePreviewAfterSave = _animationRestPose.Count > 0;
            if (restorePreviewAfterSave) RestoreAnimationRestPose();

            try
            {
                StorageManager.SaveAtomic(Document, targetPath);
                StorageManager.AddRecentProject(targetPath);
                CurrentFilePath = targetPath;
                History.MarkClean();
                StorageManager.ClearAutosave(targetPath);
                if (DocumentSaved != null) DocumentSaved(targetPath);
                ShowSavedStatus();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save project: " + ex.Message, "Kerf Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                if (restorePreviewAfterSave && _timelineController != null)
                    _timelineController.Seek(previewTime);
            }
        }

        public bool SaveAsCurrentDocument()
        {
            if (Document == null) return false;
            var dlg = new SaveFileDialog
            {
                Filter = "Kerf Design Files (*.kerf)|*.kerf|All Files (*.*)|*.*",
                FileName = (string.IsNullOrEmpty(Document.Name) ? "Untitled Project" : Document.Name) + ".kerf",
                Title = "Save Kerf Design As...",
                DefaultExt = ".kerf",
                AddExtension = true,
                OverwritePrompt = true,
                ValidateNames = true
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
                string previousName = Document.Name;
                Document.Name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                _titleBlock.Text = Document.Name;
                _titleTextBox.Text = Document.Name;
                if (SaveDocumentToPath(dlg.FileName))
                {
                    return true;
                }
                Document.Name = previousName;
                _titleBlock.Text = Document.Name;
                _titleTextBox.Text = Document.Name;
                UpdateSaveStatus();
                return false;
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

        private static void OffsetLayerSubtree(LayerNode layer, double dx, double dy)
        {
            if (layer == null || (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)) return;
            layer.X += dx;
            layer.Y += dy;
            if (layer.Type == ElementType.Path && layer.Geometry != null)
                BezierVectorEngine.OffsetGeometry(layer.Geometry, dx, dy);
            if (layer.Children == null) return;
            foreach (var child in layer.Children)
                OffsetLayerSubtree(child, dx, dy);
        }

        private static void PreserveDuplicateSizeInAutoLayout(LayerNode duplicate, LayerNode parent)
        {
            if (duplicate == null || parent == null || parent.AutoLayout == null || parent.AutoLayout.Mode == AutoLayoutMode.None) return;
            if (duplicate.LayoutSizingH == SizingMode.Fill)
            {
                duplicate.LayoutSizingH = SizingMode.Fixed;
                duplicate.AutoLayoutBaseWidth = duplicate.Width;
            }
            if (duplicate.LayoutSizingV == SizingMode.Fill)
            {
                duplicate.LayoutSizingV = SizingMode.Fixed;
                duplicate.AutoLayoutBaseHeight = duplicate.Height;
            }
        }

        private void OffsetLayerDescendantsExceptSelected(LayerNode parent, double dx, double dy, HashSet<LayerNode> selected)
        {
            if (parent == null || parent.Children == null || (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001)) return;
            foreach (var child in parent.Children)
            {
                if (child == null || (selected != null && selected.Contains(child))) continue;
                child.X += dx;
                child.Y += dy;
                if (child.Type == ElementType.Path && child.Geometry != null)
                    BezierVectorEngine.OffsetGeometry(child.Geometry, dx, dy);
                _sceneGraph.InvalidateNode(child.Id);
                OffsetLayerDescendantsExceptSelected(child, dx, dy, selected);
            }
        }

        public void DuplicateSelectedLayers()
        {
            if (_selectedLayers.Count == 0) return;
            var page = Document.GetActivePage();
            if (page == null) return;
            var positionsBefore = page.GetAllLayersFlat().ToDictionary(layer => layer, layer => new Point(layer.X, layer.Y));
            var autoLayoutParents = new HashSet<LayerNode>();

            // Adobe Illustrator "Transform Again" logic:
            // Use the last drag/duplicate delta (dx, dy) if available, or default (24, 24)
            double offX = (_lastTransformDelta.X != 0 || _lastTransformDelta.Y != 0) ? _lastTransformDelta.X : 24.0;
            double offY = (_lastTransformDelta.X != 0 || _lastTransformDelta.Y != 0) ? _lastTransformDelta.Y : 24.0;

            var duplicates = new List<LayerNode>();
            foreach (var orig in _selectedLayers.ToArray())
            {
                var clone = orig.Clone();
                clone.Name = orig.Name;

                LayerNode parent;
                var container = page.FindContainer(orig, out parent);
                PreserveDuplicateSizeInAutoLayout(clone, parent);
                OffsetLayerSubtree(clone, offX, offY);
                CapturePreviewSubtreePositions(clone, positionsBefore);
                if (container != null)
                {
                    int idx = container.IndexOf(orig);
                    clone.ParentId = parent != null ? parent.Id : null;
                    if (parent != null && parent.AutoLayout != null && parent.AutoLayout.Mode != AutoLayoutMode.None)
                        autoLayoutParents.Add(parent);
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
            History.Execute(new AddLayersCommand(page, duplicates, () =>
            {
                foreach (var parent in autoLayoutParents)
                    AutoLayoutEngine.ReflowRecursive(parent);
                _sceneGraph.Synchronize(page);
                foreach (var parent in autoLayoutParents)
                    InvalidateLayoutFrameVisuals(parent);
                RefreshCanvas();
                RefreshLayersList();
            }));
            if (autoLayoutParents.Count > 0)
                AnimateAutoLayoutReflow(positionsBefore, page.GetAllLayersFlat());
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
            var target = InsertionTargetResolver.ResolveTarget(page, _selectedLayers);
            InsertionTargetResolver.InsertObject(page, target, instance, reflowAutoLayout: true);
            var parentToReflow = target.Container;
            History.Execute(new AddLayerCommand(page, instance, parentToReflow != null ? parentToReflow.Id : null, target.InsertionIndex, () =>
            {
                if (parentToReflow != null && parentToReflow.AutoLayout != null && parentToReflow.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    AutoLayoutEngine.ReflowRecursive(parentToReflow);
                    InvalidateLayoutFrameVisuals(parentToReflow);
                }
                _sceneGraph.Synchronize(page);
                RefreshCanvas();
                RefreshLayersList();
            }));

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
                _sceneGraph.InvalidateAll();
                _sceneGraph.Synchronize(page);
                RefreshCanvas();
                RefreshLayersList();
                RefreshPropertyInspector();
            });

            if (cmd != null)
            {
                History.Execute(cmd);
                var alCmd = cmd as AutoLayoutSelectionCommand;
                if (alCmd != null && alCmd.CreatedFrame != null)
                {
                    InvalidateLayoutFrameVisuals(alCmd.CreatedFrame);
                    _selectedLayers.Clear();
                    _selectedLayers.Add(alCmd.CreatedFrame);
                    OnSelectionChanged();
                }
                _sceneGraph.InvalidateAll();
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

            var target = InsertionTargetResolver.ResolveTarget(page, _selectedLayers);
            var pasted = new List<LayerNode>();
            var idMap = new Dictionary<string, LayerNode>();

            foreach (var orig in _clipboardLayers)
            {
                var clone = orig.Clone();
                clone.X += 24;
                clone.Y += 24;
                if (clone.Type == ElementType.Path && clone.Geometry != null)
                {
                    BezierVectorEngine.OffsetGeometry(clone.Geometry, 24, 24);
                }
                idMap[orig.Id] = clone;
                pasted.Add(clone);
            }

            // Preserve internal parent-child relations among pasted layers
            var topLevelPasted = new List<LayerNode>();
            foreach (var orig in _clipboardLayers)
            {
                var clone = idMap[orig.Id];
                if (!string.IsNullOrEmpty(orig.ParentId) && idMap.ContainsKey(orig.ParentId))
                {
                    var parentClone = idMap[orig.ParentId];
                    clone.ParentId = parentClone.Id;
                    if (parentClone.Children == null) parentClone.Children = new List<LayerNode>();
                    if (!parentClone.Children.Contains(clone)) parentClone.Children.Add(clone);
                }
                else
                {
                    topLevelPasted.Add(clone);
                }
            }

            InsertionTargetResolver.InsertMultipleObjects(page, target, topLevelPasted, reflowAutoLayout: true);

            var targetContainer = target.Container;
            History.Execute(new AddLayersCommand(page, pasted, () =>
            {
                if (targetContainer != null && targetContainer.AutoLayout != null && targetContainer.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    AutoLayoutEngine.ReflowRecursive(targetContainer);
                    InvalidateLayoutFrameVisuals(targetContainer);
                }
                _sceneGraph.Synchronize(page);
                RefreshCanvas();
                RefreshLayersList();
            }));

            _selectedLayers.Clear();
            _selectedLayers.AddRange(topLevelPasted);
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

            InsertionTargetResult target;
            if (_selectedLayers.Count > 0)
            {
                target = InsertionTargetResolver.ResolveTarget(page, _selectedLayers);
            }
            else
            {
                var hit = CanvasInteractionController.HitTest(page, canvasPt);
                target = InsertionTargetResolver.ResolveTarget(page, _selectedLayers, hit.OutermostNode);
            }

            var pasted = new List<LayerNode>();
            var idMap = new Dictionary<string, LayerNode>();

            foreach (var orig in _clipboardLayers)
            {
                var clone = orig.Clone();
                clone.X += offsetX;
                clone.Y += offsetY;
                if (clone.Type == ElementType.Path && clone.Geometry != null)
                {
                    BezierVectorEngine.OffsetGeometry(clone.Geometry, offsetX, offsetY);
                }
                idMap[orig.Id] = clone;
                pasted.Add(clone);
            }

            var topLevelPasted = new List<LayerNode>();
            foreach (var orig in _clipboardLayers)
            {
                var clone = idMap[orig.Id];
                if (!string.IsNullOrEmpty(orig.ParentId) && idMap.ContainsKey(orig.ParentId))
                {
                    var parentClone = idMap[orig.ParentId];
                    clone.ParentId = parentClone.Id;
                    if (parentClone.Children == null) parentClone.Children = new List<LayerNode>();
                    if (!parentClone.Children.Contains(clone)) parentClone.Children.Add(clone);
                }
                else
                {
                    topLevelPasted.Add(clone);
                }
            }

            InsertionTargetResolver.InsertMultipleObjects(page, target, topLevelPasted, reflowAutoLayout: true);

            var targetContainer = target.Container;
            History.Execute(new AddLayersCommand(page, pasted, () =>
            {
                if (targetContainer != null && targetContainer.AutoLayout != null && targetContainer.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    AutoLayoutEngine.ReflowRecursive(targetContainer);
                    InvalidateLayoutFrameVisuals(targetContainer);
                }
                _sceneGraph.Synchronize(page);
                RefreshCanvas();
                RefreshLayersList();
            }));

            _selectedLayers.Clear();
            _selectedLayers.AddRange(topLevelPasted);
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
            var page = Document != null ? Document.GetActivePage() : null;
            var layer = page != null ? page.FindLayerById(layerId) : null;
            if (layer == null) return;
            _renamingLayerId = layerId;
            _renamingDraftName = layer.Name;
            _renameTextBox = null;
            _renameFocusReady = false;
            _isFinishingRename = false;
            RefreshLayersList();
        }

        public void FinishRename(string layerId, string newName)
        {
            if (_renamingLayerId != layerId) return;
            _isFinishingRename = true;
            _renamingLayerId = null;
            _renameTextBox = null;
            _renameFocusReady = false;

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
            _renamingDraftName = null;
            RefreshLayersList();
            _isFinishingRename = false;
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
            Key shortcutKey = e.Key == Key.System ? e.SystemKey : e.Key;

            if (_currentStudioMode == StudioMode.Animate)
            {
                if (isCtrl && shortcutKey == Key.A && _timelineCanvasHost != null && _timelineCanvasHost.IsKeyboardFocusWithin)
                {
                    SelectAllTimelineKeyframes();
                    e.Handled = true;
                    return;
                }
                if (isCtrl && !isShift && !isAlt && shortcutKey == Key.C && _selectedTimelineKeyframes.Count > 0)
                {
                    CopySelectedTimelineKeyframes();
                    e.Handled = true;
                    return;
                }
                if (isCtrl && !isShift && !isAlt && shortcutKey == Key.V && KeyframeClipboard.HasData)
                {
                    PasteTimelineKeyframesAtPlayhead();
                    e.Handled = true;
                    return;
                }
                if (isCtrl && !isShift && !isAlt && shortcutKey == Key.D && _selectedTimelineKeyframes.Count > 0)
                {
                    DuplicateSelectedTimelineKeyframes();
                    e.Handled = true;
                    return;
                }
                if (!isCtrl && !isShift && !isAlt && shortcutKey == Key.J && _timelineController != null)
                {
                    JumpToPreviousKeyframe();
                    e.Handled = true;
                    return;
                }
                if (!isCtrl && !isShift && !isAlt && shortcutKey == Key.K && _timelineController != null)
                {
                    JumpToNextKeyframe();
                    e.Handled = true;
                    return;
                }
                if (isAlt && isShift && shortcutKey == Key.S && _selectedTimelineKeyframes.Count > 1)
                {
                    StaggerSelectedTimelineKeyframes(false);
                    e.Handled = true;
                    return;
                }
                if (isAlt && _selectedTimelineKeyframes.Count > 0 && (shortcutKey == Key.Left || shortcutKey == Key.Right))
                {
                    OffsetSelectedTimelineKeyframes((shortcutKey == Key.Left ? -1 : 1) * (isShift ? 10 : 1));
                    e.Handled = true;
                    return;
                }
                if (!isCtrl && !isAlt && !isShift && _selectedTimelineKeyframes.Count > 0 && (shortcutKey == Key.Delete || shortcutKey == Key.Back))
                {
                    DeleteSelectedTimelineKeyframes();
                    e.Handled = true;
                    return;
                }
            }

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
                Background = KerfBrand.FigmaCardBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
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
                Background = KerfBrand.MintBrush, // Active Mint (#46EFB1)
                Padding = new Thickness(12, 4, 12, 4),
                Cursor = Cursors.Hand
            };
            var designText = new TextBlock
            {
                Text = "Design",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.TextBlackBrush // Crisp black text on light green pill
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
                Foreground = KerfBrand.TextMutedBrush
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
            if (_currentStudioMode == mode) return;
            _currentStudioMode = mode;
            if (mode == StudioMode.Animate)
            {
                if (_defaultInterpolationPanel != null)
                    _defaultInterpolationPanel.Visibility = _isAssetsTabActive ? Visibility.Collapsed : Visibility.Visible;
                DrawDefaultInterpolationGraph();
                if (_designTabButton != null)
                {
                    _designTabButton.Background = Brushes.Transparent;
                    var dt = _designTabButton.Child as TextBlock;
                    if (dt != null) dt.Foreground = KerfBrand.TextMutedBrush;
                }
                if (_animateTabButton != null)
                {
                    _animateTabButton.Background = KerfBrand.MintBrush;
                    var at = _animateTabButton.Child as TextBlock;
                    if (at != null) at.Foreground = KerfBrand.TextBlackBrush;
                }

                if (_timelinePanel != null)
                {
                    _timelinePanel.Visibility = Visibility.Visible;
                }

                SanitizeAnimationClips(Document);
                EnsureTimelineControllerInitialized();
                CaptureAnimationRestPose();
                _isStateMachineMode = true;
                if (_timelineController != null) _timelineController.UseStateMachine = true;
                if (_timelineTracksContainer != null) _timelineTracksContainer.Visibility = Visibility.Collapsed;
                if (_stateMachineScrollContainer != null) _stateMachineScrollContainer.Visibility = Visibility.Visible;
                _interactiveCanvasEnabled = true;
                if (_canvasInteractText != null) _canvasInteractText.Text = "Preview: On";
                if (_canvasInteractBtn != null)
                {
                    _canvasInteractBtn.Background = KerfBrand.MintBrush;
                    _canvasInteractBtn.BorderBrush = KerfBrand.MintBrush;
                    if (_canvasInteractText != null) _canvasInteractText.Foreground = KerfBrand.TextBlackBrush;
                }
                UpdateTimelineModeTabs();
                _timelineController.Seek(_timelineController.CurrentTime);
                BuildStateMachineInputsControls();
                BuildStateMachineListeners();
                BuildStateMachineStatesGraph();
                RefreshTimelineUI();
            }
            else
            {
                if (_defaultInterpolationPanel != null) _defaultInterpolationPanel.Visibility = Visibility.Collapsed;
                PauseAnimation();
                RestoreAnimationRestPose();
                _animationRestPose.Clear();
                _animationNodesById.Clear();
                if (_timelineController != null) _timelineController.UseStateMachine = false;
                if (_designTabButton != null)
                {
                    _designTabButton.Background = KerfBrand.MintBrush;
                    var dt = _designTabButton.Child as TextBlock;
                    if (dt != null) dt.Foreground = KerfBrand.TextBlackBrush;
                }
                if (_animateTabButton != null)
                {
                    _animateTabButton.Background = Brushes.Transparent;
                    var at = _animateTabButton.Child as TextBlock;
                    if (at != null) at.Foreground = KerfBrand.TextMutedBrush;
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
                Height = 300,
                Background = KerfBrand.DarkSlateBrush, // #0C4247
                BorderBrush = KerfBrand.TealBrush,     // #0A717B
                BorderThickness = new Thickness(0, 1, 0, 0),
                Visibility = Visibility.Collapsed // Default Design mode
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) }); // Two-row transport bar
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
                Background = KerfBrand.DarkSlateBrush,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(0, 0, 1, 0)
            };
            var leftTrackGrid = new Grid();
            leftTrackGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            leftTrackGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(92) });
            var trackScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _timelineTracksStack = new StackPanel { Margin = new Thickness(0) };
            trackScroll.Content = _timelineTracksStack;
            Grid.SetRow(trackScroll, 0);
            leftTrackGrid.Children.Add(trackScroll);
            var interpolationPanel = CreateInterpolationGraphPanel(false);
            Grid.SetRow(interpolationPanel, 1);
            leftTrackGrid.Children.Add(interpolationPanel);
            leftTrackBorder.Child = leftTrackGrid;
            Grid.SetColumn(leftTrackBorder, 0);
            bodyGrid.Children.Add(leftTrackBorder);

            // Right: Ruler & Lanes Host Canvas
            var rightCanvasBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 21, 29)),
                ClipToBounds = true
            };
            var timelineCanvasScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            _timelineCanvasHost = new Canvas
            {
                Height = 230,
                Background = Brushes.Transparent,
                Cursor = Cursors.Arrow,
                Focusable = true
            };

            _timelineCanvasHost.PreviewMouseLeftButtonDown += HandleTimelineCanvasPreviewMouseDown;
            _timelineCanvasHost.PreviewMouseMove += HandleTimelineCanvasMouseMove;
            _timelineCanvasHost.PreviewMouseLeftButtonUp += HandleTimelineCanvasMouseUp;
            _timelineCanvasHost.PreviewMouseWheel += HandleTimelineCanvasMouseWheel;

            timelineCanvasScroll.Content = _timelineCanvasHost;

            trackScroll.ScrollChanged += (s, e) =>
            {
                if (e.VerticalChange != 0) timelineCanvasScroll.ScrollToVerticalOffset(trackScroll.VerticalOffset);
            };
            timelineCanvasScroll.ScrollChanged += (s, e) =>
            {
                if (e.VerticalChange != 0) trackScroll.ScrollToVerticalOffset(timelineCanvasScroll.VerticalOffset);
            };

            rightCanvasBorder.Child = timelineCanvasScroll;
            Grid.SetColumn(rightCanvasBorder, 1);
            bodyGrid.Children.Add(rightCanvasBorder);

            _timelineTracksContainer = bodyGrid;
            _timelineTracksContainer.Visibility = _isStateMachineMode ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetRow(_timelineTracksContainer, 1);
            grid.Children.Add(_timelineTracksContainer);

            // State Machine Live Testing View Container (Default & Core Interactive View)
            _stateMachineContainer = CreateStateMachineControlsPanel();
            _stateMachineContainer.Visibility = Visibility.Visible;
            _stateMachineScrollContainer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _stateMachineContainer,
                Visibility = _isStateMachineMode ? Visibility.Visible : Visibility.Collapsed
            };
            Grid.SetRow(_stateMachineScrollContainer, 1);
            grid.Children.Add(_stateMachineScrollContainer);

            panelBorder.Child = grid;
            return panelBorder;
        }

        private FrameworkElement CreateTimelineTransportBar()
        {
            var border = new Border
            {
                Background = KerfBrand.FigmaPanelBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 0, 12, 0)
            };

            var transportGrid = new Grid();
            transportGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            transportGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

            // Left: Transport Buttons
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Play / Pause Button (Primary Light Green Button with Black Text per Brand Mockup)
            var playBtn = new Border
            {
                Background = KerfBrand.MintBrush,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _timelinePlayBtnText = new TextBlock
            {
                Text = "▶ Play",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.TextBlackBrush
            };
            playBtn.Child = _timelinePlayBtnText;
            playBtn.MouseEnter += (s, e) => playBtn.Background = KerfBrand.FreezeBrush(new SolidColorBrush(Color.FromRgb(0x62, 0xF7, 0xC0)));
            playBtn.MouseLeave += (s, e) => playBtn.Background = KerfBrand.MintBrush;
            playBtn.MouseLeftButtonUp += (s, e) => ToggleAnimationPlayback();
            leftStack.Children.Add(playBtn);

            // Stop / Rewind Button
            var stopBtn = CreateSmallTimelineButton("⏹ Stop", () => StopAnimationPlayback());
            leftStack.Children.Add(stopBtn);

            // Loop Toggle Button
            var loopBtn = new Border
            {
                Background = KerfBrand.FigmaPanelBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _timelineLoopText = new TextBlock
            {
                Text = "🔁 Loop: All",
                FontSize = 11,
                Foreground = Brushes.White
            };
            loopBtn.Child = _timelineLoopText;
            loopBtn.MouseEnter += (s, e) => loopBtn.Background = KerfBrand.FigmaBorderBrush;
            loopBtn.MouseLeave += (s, e) => loopBtn.Background = KerfBrand.FigmaPanelBrush;
            loopBtn.MouseLeftButtonUp += (s, e) => CycleTimelineLoopMode();
            leftStack.Children.Add(loopBtn);

            // Time Readout
            _timelineTimeText = new TextBlock
            {
                Text = "0.00s / 2.00s",
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = KerfBrand.MintBrush, // Light Green brand accent
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

            Grid.SetRow(leftStack, 0);
            transportGrid.Children.Add(leftStack);

            // Right: Clip selector, Keyframe button, Mode switch (Tracks vs State Machine)
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Active Clip Badge
            var clipBadge = new Border
            {
                Background = KerfBrand.FigmaCardBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _timelineClipText = new TextBlock
            {
                Text = "Clip: Default Animation",
                FontSize = 11,
                Foreground = Brushes.White
            };
            clipBadge.Child = _timelineClipText;
            clipBadge.Cursor = Cursors.Hand;
            clipBadge.ToolTip = "Choose an animation clip";
            clipBadge.MouseLeftButtonUp += (s, e) => ShowAnimationClipMenu(clipBadge);
            rightStack.Children.Add(clipBadge);

            rightStack.Children.Add(CreateSmallTimelineButton("+ Clip", () => AddAnimationClip()));

            // Add Keyframe Button (Primary Light Green Button with Black Text per Brand Mockup)
            var addKeyBtn = new Border
            {
                Background = KerfBrand.MintBrush,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            var addKeyText = new TextBlock
            {
                Text = "◆ + Keyframe",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = KerfBrand.TextBlackBrush
            };
            addKeyBtn.Child = addKeyText;
            addKeyBtn.MouseEnter += (s, e) => addKeyBtn.Background = KerfBrand.FreezeBrush(new SolidColorBrush(Color.FromRgb(0x62, 0xF7, 0xC0)));
            addKeyBtn.MouseLeave += (s, e) => addKeyBtn.Background = KerfBrand.MintBrush;
            addKeyBtn.MouseLeftButtonUp += (s, e) => AddKeyframeForSelectedLayer();
            rightStack.Children.Add(addKeyBtn);

            // Speed Selector Button
            var speedBtn = new Border
            {
                Background = KerfBrand.FigmaPanelBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            _timelineSpeedText = new TextBlock
            {
                Text = "1.0x",
                FontSize = 11,
                Foreground = Brushes.White
            };
            speedBtn.Child = _timelineSpeedText;
            speedBtn.MouseEnter += (s, e) => speedBtn.Background = KerfBrand.FigmaBorderBrush;
            speedBtn.MouseLeave += (s, e) => speedBtn.Background = KerfBrand.FigmaPanelBrush;
            speedBtn.MouseLeftButtonUp += (s, e) => CycleTimelineSpeed();
            rightStack.Children.Add(speedBtn);

            // Mode Tabs: [ ⚙ State Machine ] vs [ ⏱ Timeline Clips ]
            var modePill = new Border
            {
                Background = KerfBrand.FigmaCardBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(2),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var modeStack = new StackPanel { Orientation = Orientation.Horizontal };

            _tabStateMachineBtn = new Border
            {
                Background = _isStateMachineMode ? KerfBrand.MintBrush : Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand
            };
            var smText = new TextBlock
            {
                Text = "⚙ State Machine",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = _isStateMachineMode ? KerfBrand.TextBlackBrush : KerfBrand.TextMutedBrush
            };
            _tabStateMachineBtn.Child = smText;
            _tabStateMachineBtn.MouseLeftButtonUp += (s, e) => SwitchAnimationSubMode(true);

            _tabTimelineClipsBtn = new Border
            {
                Background = !_isStateMachineMode ? KerfBrand.MintBrush : Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand
            };
            var tcText = new TextBlock
            {
                Text = "⏱ Timeline Clips",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = !_isStateMachineMode ? KerfBrand.TextBlackBrush : KerfBrand.TextMutedBrush
            };
            _tabTimelineClipsBtn.Child = tcText;
            _tabTimelineClipsBtn.MouseLeftButtonUp += (s, e) => SwitchAnimationSubMode(false);

            modeStack.Children.Add(_tabStateMachineBtn);
            modeStack.Children.Add(_tabTimelineClipsBtn);
            modePill.Child = modeStack;
            rightStack.Children.Insert(0, modePill);

            // Dynamic State Machine State Tabs Bar: allows direct 1-click opening of multiple state timelines
            var stateTabsScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _timelineStateTabsStack = new StackPanel { Orientation = Orientation.Horizontal };
            stateTabsScroll.Content = _timelineStateTabsStack;
            rightStack.Children.Insert(1, stateTabsScroll);

            Grid.SetRow(rightStack, 1);
            transportGrid.Children.Add(rightStack);

            border.Child = transportGrid;
            return border;
        }

        private Border CreateSmallTimelineButton(string text, Action onClick)
        {
            var btn = new Border
            {
                Background = KerfBrand.FigmaPanelBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = Brushes.White
            };
            btn.Child = tb;
            btn.MouseEnter += (s, e) => btn.Background = KerfBrand.FigmaBorderBrush;
            btn.MouseLeave += (s, e) => btn.Background = KerfBrand.FigmaPanelBrush;
            btn.MouseLeftButtonUp += (s, e) => onClick();
            return btn;
        }

        private FrameworkElement CreateInterpolationGraphPanel(bool stateMachine)
        {
            var border = new Border { Background = new SolidColorBrush(Color.FromRgb(26, 30, 39)), BorderBrush = new SolidColorBrush(Color.FromRgb(48, 55, 68)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 3, 5, 4), Margin = new Thickness(0, 4, 0, 0) };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var header = new DockPanel();
            var caption = new TextBlock { Text = stateMachine ? "Transition interpolation" : "Interpolation · nearest key", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(154, 167, 188)), VerticalAlignment = VerticalAlignment.Center };
            if (stateMachine) _stateMachineInterpolationGraphCaption = caption; else _timelineInterpolationGraphCaption = caption;
            DockPanel.SetDock(caption, Dock.Left);
            header.Children.Add(caption);
            var mode = new ComboBox { Width = 82, Height = 20, FontSize = 9, Padding = new Thickness(2, 0, 2, 0), Background = new SolidColorBrush(Color.FromRgb(36, 41, 52)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(61, 70, 85)), HorizontalAlignment = HorizontalAlignment.Right };
            mode.Items.Add(new ComboBoxItem { Content = "Hold", Tag = InterpMode.Hold });
            mode.Items.Add(new ComboBoxItem { Content = "Linear", Tag = InterpMode.Linear });
            mode.Items.Add(new ComboBoxItem { Content = "Cubic", Tag = InterpMode.Cubic });
            if (stateMachine) _stateMachineInterpolationModeCombo = mode; else _timelineInterpolationModeCombo = mode;
            mode.SelectionChanged += (s, e) => ApplyInterpolationMode(stateMachine, mode);
            DockPanel.SetDock(mode, Dock.Right);
            header.Children.Add(mode);
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var canvas = new Canvas { Height = 48, MinWidth = 130, Background = new SolidColorBrush(Color.FromRgb(19, 22, 29)), ClipToBounds = true, Tag = stateMachine };
            if (stateMachine) _stateMachineInterpolationGraphCanvas = canvas; else _timelineInterpolationGraphCanvas = canvas;
            canvas.MouseLeftButtonDown += HandleInterpolationGraphMouseDown;
            canvas.MouseMove += HandleInterpolationGraphMouseMove;
            canvas.MouseLeftButtonUp += HandleInterpolationGraphMouseUp;
            canvas.SizeChanged += (s, e) => RefreshInterpolationGraphPanels();
            Grid.SetRow(canvas, 1);
            grid.Children.Add(canvas);
            border.Child = grid;
            return border;
        }

        private void ApplyInterpolationMode(bool stateMachine, ComboBox combo)
        {
            if (_isRefreshingInterpolationPanels || combo == null) return;
            var item = combo.SelectedItem as ComboBoxItem;
            if (item == null || !(item.Tag is InterpMode)) return;
            var mode = (InterpMode)item.Tag;
            if (stateMachine)
            {
                if (_selectedGraphTransition == null) return;
                _selectedGraphTransition.Interpolation = mode;
            }
            else
            {
                if (_editingInterpolationKeyframe == null) return;
                _editingInterpolationKeyframe.Interp = mode;
            }
            if (History != null) History.MarkDirty();
            RefreshInterpolationGraphPanels();
            RefreshTimelineCanvas();
            if (stateMachine) TriggerStateMachineStep();
        }

        private void RefreshInterpolationGraphPanels()
        {
            FindTimelineInterpolationKey();
            RefreshInterpolationGraphPanel(false);
            RefreshInterpolationGraphPanel(true);
            RefreshDefaultInterpolationPresetButtons();
        }

        private void FindTimelineInterpolationKey()
        {
            if (_timelineController == null || _timelineController.ActiveClip == null || _selectedLayers.Count == 0)
            {
                _editingInterpolationKeyframe = null;
                _editingInterpolationTrack = null;
                return;
            }
            uint selectedId = PropertyBinder.GetNumericNodeId(_selectedLayers[0].Id);
            if (_editingInterpolationKeyframe != null && _editingInterpolationTrack != null &&
                _editingInterpolationTrack.NodeId == selectedId && _timelineController.ActiveClip.Tracks.Contains(_editingInterpolationTrack) &&
                _editingInterpolationTrack.Keys != null && _editingInterpolationTrack.Keys.Contains(_editingInterpolationKeyframe)) return;
            _editingInterpolationKeyframe = null;
            _editingInterpolationTrack = null;
            var candidates = new List<Tuple<AnimTrack, Keyframe, double>>();
            foreach (var track in _timelineController.ActiveClip.Tracks ?? new List<AnimTrack>())
            {
                if (track == null || track.NodeId != selectedId || track.Keys == null) continue;
                foreach (var key in track.Keys)
                    if (key != null) candidates.Add(Tuple.Create(track, key, Math.Abs(key.Time - _timelineController.CurrentTime)));
            }
            var nearest = candidates.OrderBy(candidate => candidate.Item3).FirstOrDefault();
            if (nearest != null)
            {
                _editingInterpolationTrack = nearest.Item1;
                _editingInterpolationKeyframe = nearest.Item2;
            }
        }

        private void RefreshInterpolationGraphPanel(bool stateMachine)
        {
            Canvas canvas = stateMachine ? _stateMachineInterpolationGraphCanvas : _timelineInterpolationGraphCanvas;
            ComboBox combo = stateMachine ? _stateMachineInterpolationModeCombo : _timelineInterpolationModeCombo;
            TextBlock caption = stateMachine ? _stateMachineInterpolationGraphCaption : _timelineInterpolationGraphCaption;
            if (canvas == null) return;

            InterpMode mode = InterpMode.Linear;
            double x1 = 0.25, y1 = 0.1, x2 = 0.25, y2 = 1.0;
            bool hasTarget = false;
            if (stateMachine && _selectedGraphTransition != null)
            {
                var transition = _selectedGraphTransition;
                mode = transition.Interpolation;
                x1 = transition.X1; y1 = transition.Y1; x2 = transition.X2; y2 = transition.Y2;
                hasTarget = true;
                if (caption != null) caption.Text = "Transition · " + TransitionConditionSummary(transition);
            }
            else if (!stateMachine && _editingInterpolationKeyframe != null)
            {
                var key = _editingInterpolationKeyframe;
                mode = key.Interp;
                x1 = key.X1; y1 = key.Y1; x2 = key.X2; y2 = key.Y2;
                hasTarget = true;
                if (caption != null) caption.Text = string.Format("{0} · {1:0.00}s", _editingInterpolationTrack != null ? ((PropKey)_editingInterpolationTrack.Prop).ToString() : "Key", key.Time);
            }
            else if (caption != null)
            {
                caption.Text = stateMachine ? "Select a transition" : "Select a layer and key a property";
            }

            _isRefreshingInterpolationPanels = true;
            if (combo != null)
            {
                combo.IsEnabled = hasTarget;
                combo.SelectedIndex = (int)mode;
            }
            _isRefreshingInterpolationPanels = false;
            DrawInterpolationCurve(canvas, hasTarget, mode, x1, y1, x2, y2);
        }

        private void DrawInterpolationCurve(Canvas canvas, bool hasTarget, InterpMode mode, double x1, double y1, double x2, double y2)
        {
            if (canvas == null) return;
            canvas.Children.Clear();
            double width = Math.Max(130.0, canvas.ActualWidth > 20.0 ? canvas.ActualWidth : canvas.MinWidth);
            double height = Math.Max(42.0, canvas.ActualHeight > 20.0 ? canvas.ActualHeight : canvas.Height);
            double left = 8.0, right = width - 8.0, top = 5.0, bottom = height - 5.0;
            var gridLine = new SolidColorBrush(Color.FromRgb(49, 56, 69));
            canvas.Children.Add(new Line { X1 = left, Y1 = bottom, X2 = right, Y2 = bottom, Stroke = gridLine, StrokeThickness = 1 });
            canvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = right, Y2 = top, Stroke = new SolidColorBrush(Color.FromRgb(39, 45, 57)), StrokeThickness = 1 });
            canvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = bottom, Stroke = gridLine, StrokeThickness = 1 });
            canvas.Children.Add(new Line { X1 = right, Y1 = top, X2 = right, Y2 = bottom, Stroke = gridLine, StrokeThickness = 1 });
            if (!hasTarget)
            {
                var hint = new TextBlock { Text = "No curve selected", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(104, 117, 137)), HorizontalAlignment = HorizontalAlignment.Center };
                Canvas.SetLeft(hint, Math.Max(10, width / 2.0 - 44));
                Canvas.SetTop(hint, Math.Max(4, height / 2.0 - 6));
                canvas.Children.Add(hint);
                return;
            }
            x1 = Math.Max(0.0, Math.Min(1.0, x1)); x2 = Math.Max(0.0, Math.Min(1.0, x2));
            y1 = Math.Max(0.0, Math.Min(1.0, y1)); y2 = Math.Max(0.0, Math.Min(1.0, y2));
            Point start = new Point(left, bottom), end = new Point(right, top);
            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            if (mode == InterpMode.Hold)
            {
                figure.Segments.Add(new LineSegment(new Point(right, bottom), true));
                figure.Segments.Add(new LineSegment(end, true));
            }
            else if (mode == InterpMode.Linear)
            {
                figure.Segments.Add(new LineSegment(end, true));
            }
            else
            {
                Point c1 = new Point(left + x1 * (right - left), bottom - y1 * (bottom - top));
                Point c2 = new Point(left + x2 * (right - left), bottom - y2 * (bottom - top));
                figure.Segments.Add(new BezierSegment(c1, c2, end, true));
                canvas.Children.Add(new Line { X1 = start.X, Y1 = start.Y, X2 = c1.X, Y2 = c1.Y, Stroke = new SolidColorBrush(Color.FromRgb(89, 99, 118)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 2, 2 } });
                canvas.Children.Add(new Line { X1 = end.X, Y1 = end.Y, X2 = c2.X, Y2 = c2.Y, Stroke = new SolidColorBrush(Color.FromRgb(89, 99, 118)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 2, 2 } });
                AddInterpolationHandle(canvas, c1, 1);
                AddInterpolationHandle(canvas, c2, 2);
            }
            geometry.Figures.Add(figure);
            canvas.Children.Add(new System.Windows.Shapes.Path { Data = geometry, Stroke = new SolidColorBrush(Color.FromRgb(80, 180, 255)), StrokeThickness = 2, Fill = Brushes.Transparent, IsHitTestVisible = false });
        }

        private static void AddInterpolationHandle(Canvas canvas, Point point, int id)
        {
            var handle = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Color.FromRgb(245, 112, 175)), Stroke = new SolidColorBrush(Color.FromRgb(250, 205, 225)), StrokeThickness = 1, Tag = id, IsHitTestVisible = false };
            Canvas.SetLeft(handle, point.X - 3.5);
            Canvas.SetTop(handle, point.Y - 3.5);
            canvas.Children.Add(handle);
        }

        private void HandleInterpolationGraphMouseDown(object sender, MouseButtonEventArgs e)
        {
            var canvas = sender as Canvas;
            if (canvas == null) return;
            bool stateMachine = canvas.Tag is bool && (bool)canvas.Tag;
            if (stateMachine ? _selectedGraphTransition == null : _editingInterpolationKeyframe == null) return;
            if (stateMachine && _selectedGraphTransition.Interpolation != InterpMode.Cubic) return;
            if (!stateMachine && _editingInterpolationKeyframe.Interp != InterpMode.Cubic) return;
            Point point = e.GetPosition(canvas);
            double width = Math.Max(130.0, canvas.ActualWidth > 20.0 ? canvas.ActualWidth : canvas.MinWidth);
            double height = Math.Max(42.0, canvas.ActualHeight > 20.0 ? canvas.ActualHeight : canvas.Height);
            double left = 8.0, right = width - 8.0, top = 5.0, bottom = height - 5.0;
            double x1, y1, x2, y2;
            GetInterpolationControlPoints(stateMachine, out x1, out y1, out x2, out y2);
            Point p1 = new Point(left + x1 * (right - left), bottom - y1 * (bottom - top));
            Point p2 = new Point(left + x2 * (right - left), bottom - y2 * (bottom - top));
            _interpolationControlHandle = (point - p1).Length < (point - p2).Length ? 1 : 2;
            canvas.CaptureMouse();
            e.Handled = true;
        }

        private void GetInterpolationControlPoints(bool stateMachine, out double x1, out double y1, out double x2, out double y2)
        {
            if (stateMachine && _selectedGraphTransition != null)
            {
                x1 = _selectedGraphTransition.X1; y1 = _selectedGraphTransition.Y1; x2 = _selectedGraphTransition.X2; y2 = _selectedGraphTransition.Y2;
            }
            else if (!stateMachine && _editingInterpolationKeyframe != null)
            {
                x1 = _editingInterpolationKeyframe.X1; y1 = _editingInterpolationKeyframe.Y1; x2 = _editingInterpolationKeyframe.X2; y2 = _editingInterpolationKeyframe.Y2;
            }
            else { x1 = 0.25; y1 = 0.1; x2 = 0.25; y2 = 1.0; }
        }

        private void HandleInterpolationGraphMouseMove(object sender, MouseEventArgs e)
        {
            if (_interpolationControlHandle < 0) return;
            var canvas = sender as Canvas;
            if (canvas == null || !canvas.IsMouseCaptured) return;
            bool stateMachine = canvas.Tag is bool && (bool)canvas.Tag;
            Point point = e.GetPosition(canvas);
            double width = Math.Max(130.0, canvas.ActualWidth > 20.0 ? canvas.ActualWidth : canvas.MinWidth);
            double height = Math.Max(42.0, canvas.ActualHeight > 20.0 ? canvas.ActualHeight : canvas.Height);
            double x = Math.Max(0.0, Math.Min(1.0, (point.X - 8.0) / Math.Max(1.0, width - 16.0)));
            double y = Math.Max(0.0, Math.Min(1.0, 1.0 - (point.Y - 5.0) / Math.Max(1.0, height - 10.0)));
            if (stateMachine && _selectedGraphTransition != null)
            {
                if (_interpolationControlHandle == 1) { _selectedGraphTransition.X1 = x; _selectedGraphTransition.Y1 = y; }
                else { _selectedGraphTransition.X2 = x; _selectedGraphTransition.Y2 = y; }
            }
            else if (!stateMachine && _editingInterpolationKeyframe != null)
            {
                if (_interpolationControlHandle == 1) { _editingInterpolationKeyframe.X1 = x; _editingInterpolationKeyframe.Y1 = y; }
                else { _editingInterpolationKeyframe.X2 = x; _editingInterpolationKeyframe.Y2 = y; }
            }
            double x1, y1, x2, y2;
            GetInterpolationControlPoints(stateMachine, out x1, out y1, out x2, out y2);
            InterpMode mode = stateMachine && _selectedGraphTransition != null ? _selectedGraphTransition.Interpolation : (_editingInterpolationKeyframe != null ? _editingInterpolationKeyframe.Interp : InterpMode.Cubic);
            DrawInterpolationCurve(canvas, true, mode, x1, y1, x2, y2);
            if (History != null) History.MarkDirty();
            e.Handled = true;
        }

        private void HandleInterpolationGraphMouseUp(object sender, MouseButtonEventArgs e)
        {
            var canvas = sender as Canvas;
            if (canvas != null && canvas.IsMouseCaptured) canvas.ReleaseMouseCapture();
            _interpolationControlHandle = -1;
            if (canvas != null)
            {
                bool stateMachine = canvas.Tag is bool && (bool)canvas.Tag;
                RefreshInterpolationGraphPanel(stateMachine);
                if (stateMachine) TriggerStateMachineStep();
            }
        }

        private FrameworkElement CreateStateMachineControlsPanel()
        {
            var border = new Border
            {
                Background = KerfBrand.FigmaPanelBrush,
                BorderBrush = KerfBrand.FigmaBorderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(8, 6, 8, 8)
            };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new DockPanel { LastChildFill = false, Margin = new Thickness(4, 0, 4, 6) };
            var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new TextBlock { Text = "State Machine", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
            _stateMachineStatusBadge = new TextBlock { Text = "● Entry", FontSize = 10, Foreground = KerfBrand.MintBrush, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(_stateMachineStatusBadge);
            DockPanel.SetDock(title, Dock.Left);
            header.Children.Add(title);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _canvasInteractBtn = new Border
            {
                Background = KerfBrand.MintBrush,
                BorderBrush = KerfBrand.MintBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 3, 7, 3),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            _canvasInteractText = new TextBlock { Text = "Preview: On", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = KerfBrand.TextBlackBrush };
            _canvasInteractBtn.Child = _canvasInteractText;
            _canvasInteractBtn.MouseLeftButtonUp += (s, e) =>
            {
                _interactiveCanvasEnabled = !_interactiveCanvasEnabled;
                _canvasInteractText.Text = _interactiveCanvasEnabled ? "Preview: On" : "Preview: Off";
                _canvasInteractBtn.Background = _interactiveCanvasEnabled ? KerfBrand.MintBrush : KerfBrand.FigmaPanelBrush;
                _canvasInteractBtn.BorderBrush = _interactiveCanvasEnabled ? KerfBrand.MintBrush : KerfBrand.FigmaBorderBrush;
                _canvasInteractText.Foreground = _interactiveCanvasEnabled ? KerfBrand.TextBlackBrush : KerfBrand.TextMutedBrush;
            };
            actions.Children.Add(_canvasInteractBtn);
            var graphZoomControls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0) };
            var zoomOut = CreateSmallTimelineButton("−", () => ZoomStateGraphBy(1.0 / 1.15));
            zoomOut.Padding = new Thickness(7, 4, 7, 4);
            var zoomIn = CreateSmallTimelineButton("+", () => ZoomStateGraphBy(1.15));
            zoomIn.Padding = new Thickness(7, 4, 7, 4);
            _stateMachineGraphZoomText = new TextBlock { Text = "100%", Width = 38, TextAlignment = TextAlignment.Center, FontSize = 9, Foreground = KerfBrand.TextMutedBrush, VerticalAlignment = VerticalAlignment.Center };
            graphZoomControls.Children.Add(zoomOut);
            graphZoomControls.Children.Add(_stateMachineGraphZoomText);
            graphZoomControls.Children.Add(zoomIn);
            graphZoomControls.Children.Add(CreateSmallTimelineButton("Fit", FitStateGraphToView));
            actions.Children.Add(graphZoomControls);

            var addStateBtn = KerfBrand.CreatePrimaryButton("+ State", AddStateMachineState, 5);
            addStateBtn.Height = 24;
            addStateBtn.Padding = new Thickness(10, 2, 10, 2);
            addStateBtn.FontSize = 11;
            actions.Children.Add(addStateBtn);

            var addTransBtn = KerfBrand.CreateSecondaryButton("+ Transition", ShowAddTransitionDialog, 5);
            addTransBtn.Height = 24;
            addTransBtn.Padding = new Thickness(10, 2, 10, 2);
            addTransBtn.FontSize = 11;
            addTransBtn.Margin = new Thickness(6, 0, 0, 0);
            actions.Children.Add(addTransBtn);

            DockPanel.SetDock(actions, Dock.Right);
            header.Children.Add(actions);
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
            var graph = new Grid { Margin = new Thickness(0, 0, 8, 0) };
            graph.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            graph.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) });
            _stateMachineGraphCanvas = new Canvas { Width = 780, Height = 230, Background = KerfBrand.FigmaCanvasBrush, LayoutTransform = _stateMachineGraphScale };
            _stateMachineGraphCanvas.PreviewMouseDown += HandleStateGraphPanStart;
            _stateMachineGraphCanvas.PreviewMouseUp += HandleStateGraphPanEnd;
            _stateMachineGraphCanvas.MouseMove += HandleStateGraphMouseMove;
            _stateMachineGraphCanvas.MouseLeftButtonUp += HandleStateGraphMouseUp;
            _stateMachineGraphScrollViewer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _stateMachineGraphCanvas };
            _stateMachineGraphScrollViewer.PreviewMouseWheel += HandleStateGraphMouseWheel;
            var graphBorder = new Border { Background = KerfBrand.FigmaCanvasBrush, BorderBrush = KerfBrand.FigmaBorderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Child = _stateMachineGraphScrollViewer };
            Grid.SetRow(graphBorder, 0);
            graph.Children.Add(graphBorder);
            var curvePanel = CreateInterpolationGraphPanel(true);
            Grid.SetRow(curvePanel, 1);
            graph.Children.Add(curvePanel);
            Grid.SetColumn(graph, 0);
            body.Children.Add(graph);

            var side = new StackPanel();
            _stateMachineInputsStack = new StackPanel();
            var inputs = CreateStateMachineCollapsibleSection("Inputs", _stateMachineInputsStack, ShowAddViewModelPropertyDialog, out _stateMachineInputSearch);
            side.Children.Add(inputs);
            _stateMachineListenersStack = new StackPanel();
            var listeners = CreateStateMachineCollapsibleSection("Listeners", _stateMachineListenersStack, ShowAddStateMachineListenerDialog, out _stateMachineListenerSearch);
            listeners.Margin = new Thickness(0, 6, 0, 0);
            side.Children.Add(listeners);
            side.Children.Add(new TextBlock { Text = "Drag a port to another state to add a transition. Double-click a state to edit its timeline.", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(112, 126, 148)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 8, 4, 0) });
            var sideScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = side };
            Grid.SetColumn(sideScroll, 1);
            body.Children.Add(sideScroll);
            Grid.SetRow(body, 1);
            grid.Children.Add(body);

            border.Child = grid;
            BuildStateMachineInputsControls();
            BuildStateMachineListeners();
            BuildStateMachineStatesGraph();
            return border;
        }

        private FrameworkElement CreateStateMachineCollapsibleSection(string title, Panel items, Action addAction, out TextBox searchBox)
        {
            var section = new Border { Background = KerfBrand.FigmaCardBrush, BorderBrush = KerfBrand.FigmaBorderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(6) };
            var content = new StackPanel();
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            var toggle = new TextBlock { Text = "⌄  " + title, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            DockPanel.SetDock(toggle, Dock.Left);
            header.Children.Add(toggle);
            var add = CreateSmallTimelineButton("+", addAction);
            add.Width = 24;
            add.Height = 22;
            DockPanel.SetDock(add, Dock.Right);
            header.Children.Add(add);
            content.Children.Add(header);
            searchBox = new TextBox { Text = "Search " + title, ToolTip = "Search " + title, Height = 23, Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 0, 0, 4), Background = KerfBrand.FigmaCanvasBrush, Foreground = KerfBrand.TextMutedBrush, BorderBrush = KerfBrand.FigmaBorderBrush, FontSize = 10 };
            var search = searchBox;
            content.Children.Add(searchBox);
            string placeholder = "Search " + title;
            search.GotKeyboardFocus += (s, e) =>
            {
                if (string.Equals(search.Text, placeholder, StringComparison.Ordinal))
                {
                    search.Text = string.Empty;
                    search.Foreground = new SolidColorBrush(Color.FromRgb(205, 213, 224));
                }
            };
            search.LostKeyboardFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(search.Text))
                {
                    search.Text = placeholder;
                    search.Foreground = new SolidColorBrush(Color.FromRgb(118, 130, 149));
                }
            };
            var list = new ScrollViewer { Height = 83, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = items };
            content.Children.Add(list);
            toggle.MouseLeftButtonUp += (s, e) =>
            {
                bool open = search.Visibility == Visibility.Visible;
                search.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
                list.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
                toggle.Text = (open ? "›  " : "⌄  ") + title;
            };
            search.TextChanged += (s, e) => FilterStateMachineItems(items, search.Text);
            section.Child = content;
            return section;
        }

        private void FilterStateMachineItems(Panel items, string query)
        {
            if (items == null) return;
            string filter = (query ?? string.Empty).Trim();
            if (string.Equals(filter, "Search Inputs", StringComparison.OrdinalIgnoreCase) || string.Equals(filter, "Search Listeners", StringComparison.OrdinalIgnoreCase)) filter = string.Empty;
            foreach (UIElement item in items.Children)
            {
                var element = item as FrameworkElement;
                if (element == null) continue;
                string name = element.Tag as string ?? string.Empty;
                element.Visibility = string.IsNullOrEmpty(filter) || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private StateMachineRuntime EnsureStateMachineRuntime()
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            if (_timelineController == null) return null;
            if (_timelineController.ActiveStateMachine != null) return _timelineController.ActiveStateMachine;
            if (Document == null) return null;
            if (Document.Animation == null) Document.Animation = new AnimationProjectData();
            if (Document.Animation.Clips == null) Document.Animation.Clips = new List<Clip>();
            if (Document.Animation.Clips.Count == 0)
            {
                var clip = new Clip(100, "Default Animation", Math.Max(0.1, _timelineController.Duration), _timelineController.Fps, LoopMode.Loop);
                Document.Animation.Clips.Add(clip);
                _timelineController.ActiveClip = clip;
            }
            int activeClipIndex = Document.Animation.Clips.IndexOf(_timelineController.ActiveClip);
            if (activeClipIndex < 0) activeClipIndex = 0;
            var definition = new StateMachineDefinition("State Machine 1");
            definition.AddInput(MachineInput.Bool("Hovering", false));
            definition.AddInput(MachineInput.Trigger("Clicked"));
            definition.AddInput(MachineInput.Number("Speed", 1.0));
            definition.AddViewModelProperty(new ViewModelPropertyDefinition("Hovering", ViewModelPropertyKind.Boolean));
            definition.AddViewModelProperty(new ViewModelPropertyDefinition("Clicked", ViewModelPropertyKind.Trigger));
            definition.AddViewModelProperty(new ViewModelPropertyDefinition("Speed", ViewModelPropertyKind.Number));
            var layer = new MachineLayer("Base Layer");
            int entry = layer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int idle = layer.AddState(new MachineState(1, "Idle", StateKind.Animation, activeClipIndex));
            layer.AddState(new MachineState(2, "Any State", StateKind.Any));
            layer.AddState(new MachineState(3, "Exit", StateKind.Exit));
            layer.AddTransition(new StateTransition(entry, idle, 0.0));
            definition.AddLayer(layer);
            Document.Animation.StateMachine = definition;
            _timelineController.ActiveStateMachine = new StateMachineRuntime(definition, Document.Animation.Clips);
            if (History != null) History.MarkDirty();
            return _timelineController.ActiveStateMachine;
        }

        private void BuildStateMachineInputsControls()
        {
            if (_stateMachineInputsStack == null) return;
            _stateMachineInputsStack.Children.Clear();

            if (_timelineController == null || _timelineController.ActiveStateMachine == null)
            {
                EnsureTimelineControllerInitialized();
            }

            var sm = EnsureStateMachineRuntime();
            if (sm == null || sm.Definition == null) return;

            for (int i = 0; i < sm.Definition.Inputs.Count; i++)
            {
                int inIdx = i;
                var input = sm.Definition.Inputs[i];
                if (input == null) continue;

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
                        TriggerStateMachineStep(continuePlayback: true);
                    };
                    AddStateMachineInputElement(trgBtn, input.Name);
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
                        TriggerStateMachineStep(continuePlayback: true);
                    };
                    AddStateMachineInputElement(boolBtn, input.Name);
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
                        TriggerStateMachineStep(continuePlayback: true);
                    };
                    numStack.Children.Add(slider);
                    AddStateMachineInputElement(numStack, input.Name);
                }
            }

            if (sm.Definition.ViewModelProperties != null)
            {
                foreach (var property in sm.Definition.ViewModelProperties)
                {
                    if (property == null) continue;
                    string propertyName = property.Name;
                    if (property.Kind == ViewModelPropertyKind.Trigger)
                    {
                        var triggerButton = CreateSmallTimelineButton("⚡ " + propertyName, () =>
                        {
                            sm.FireViewModelTrigger(propertyName);
                            TriggerStateMachineStep();
                        });
                        AddStateMachineInputElement(triggerButton, propertyName);
                    }
                    else if (property.Kind == ViewModelPropertyKind.Boolean)
                    {
                        bool current = sm.ViewModel.GetBool(propertyName);
                        var booleanInput = new CheckBox
                        {
                            Content = propertyName,
                            IsChecked = current,
                            Foreground = new SolidColorBrush(Color.FromRgb(202, 211, 224)),
                            FontSize = 10.5,
                            Margin = new Thickness(1, 2, 0, 2),
                            Cursor = Cursors.Hand
                        };
                        booleanInput.Checked += (s, e) =>
                        {
                            sm.SetViewModelBool(propertyName, true);
                            TriggerStateMachineStep();
                        };
                        booleanInput.Unchecked += (s, e) =>
                        {
                            sm.SetViewModelBool(propertyName, false);
                            TriggerStateMachineStep();
                        };
                        AddStateMachineInputElement(booleanInput, propertyName);
                    }
                    else if (property.Kind == ViewModelPropertyKind.Number)
                    {
                        var numberPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
                        double current = sm.GetViewModelNumber(propertyName);
                        var label = new TextBlock
                        {
                            Text = propertyName + "  " + current.ToString("0.##"),
                            FontSize = 10.5,
                            Foreground = new SolidColorBrush(Color.FromRgb(180, 194, 216)),
                            VerticalAlignment = VerticalAlignment.Center,
                            MinWidth = 74
                        };
                        var slider = new Slider { Minimum = 0.0, Maximum = string.Equals(propertyName, "playbackSpeed", StringComparison.OrdinalIgnoreCase) ? 4.0 : 1.0,
                            Value = Math.Max(0.0, Math.Min(string.Equals(propertyName, "playbackSpeed", StringComparison.OrdinalIgnoreCase) ? 4.0 : 1.0, current)),
                            Width = 88, VerticalAlignment = VerticalAlignment.Center };
                        slider.ValueChanged += (s, e) =>
                        {
                            sm.SetViewModelNumber(propertyName, slider.Value);
                            label.Text = propertyName + "  " + slider.Value.ToString("0.##");
                            TriggerStateMachineStep();
                        };
                        numberPanel.Children.Add(label);
                        numberPanel.Children.Add(slider);
                        AddStateMachineInputElement(numberPanel, propertyName);
                    }
                    else if (property.Kind == ViewModelPropertyKind.Text)
                    {
                        var textPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                        textPanel.Children.Add(new TextBlock { Text = propertyName, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(180, 194, 216)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
                        var textInput = new TextBox { Text = sm.ViewModel.GetText(propertyName), Width = 110, Height = 25, Padding = new Thickness(6, 3, 6, 3), Background = new SolidColorBrush(Color.FromRgb(35, 41, 56)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(67, 78, 99)) };
                        textInput.LostKeyboardFocus += (s, e) => sm.SetViewModelText(propertyName, textInput.Text);
                        textInput.KeyDown += (s, e) =>
                        {
                            if (e.Key == Key.Enter)
                            {
                                sm.SetViewModelText(propertyName, textInput.Text);
                                Keyboard.ClearFocus();
                                TriggerStateMachineStep();
                            }
                        };
                        textPanel.Children.Add(textInput);
                        AddStateMachineInputElement(textPanel, propertyName);
                    }
                }
            }
            if (_stateMachineInputSearch != null)
                FilterStateMachineItems(_stateMachineInputsStack, _stateMachineInputSearch.Text);
        }

        private void AddStateMachineInputElement(UIElement element, string name)
        {
            var framework = element as FrameworkElement;
            if (framework != null)
            {
                framework.Tag = name ?? string.Empty;
                framework.Margin = new Thickness(0, 2, 0, 2);
            }
            if (_stateMachineInputsStack != null && element != null) _stateMachineInputsStack.Children.Add(element);
        }

        private void AddStateMachineState()
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return;
            var sm = _timelineController.ActiveStateMachine;
            if (sm.Definition == null || sm.Definition.Layers == null || sm.Definition.Layers.Count == 0) return;
            var layer = sm.Definition.Layers[0];
            string name = "State " + (layer.States.Count + 1);
            uint clipId = unchecked((uint)(DateTime.UtcNow.Ticks & 0xffffffff));
            var clip = new Clip(clipId, name, Math.Max(0.1, _timelineController.Duration), _timelineController.Fps, LoopMode.Loop);
            int clipIndex = sm.Clips.Count;
            sm.Clips.Add(clip);
            if (Document.Animation == null) Document.Animation = new AnimationProjectData();
            if (Document.Animation.Clips == null) Document.Animation.Clips = new List<Clip>();
            if (!ReferenceEquals(Document.Animation.Clips, sm.Clips)) Document.Animation.Clips.Add(clip);
            int stateIndex = layer.AddState(new MachineState(layer.States.Count, name, StateKind.Animation, clipIndex));
            int entryIndex = layer.States.FindIndex(state => state != null && state.Kind == StateKind.Entry);
            if (entryIndex >= 0 && !layer.Transitions.Any(transition => transition != null && transition.FromState == entryIndex))
                layer.AddTransition(new StateTransition(entryIndex, stateIndex, 0.0));
            _selectedGraphStateIndex = stateIndex;
            History.MarkDirty();
            BuildStateMachineStatesGraph();
            RefreshTimelineUI();
        }

        private void ShowAddViewModelPropertyDialog()
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return;
            var sm = _timelineController.ActiveStateMachine;
            var dialog = new Window
            {
                Title = "Add View Model Property",
                Width = 330,
                Height = 235,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = KerfBrand.FigmaPanelBrush,
                Foreground = Brushes.White
            };
            var owner = Window.GetWindow(this);
            if (owner != null) dialog.Owner = owner;
            var form = new StackPanel { Margin = new Thickness(18) };
            var nameBox = new TextBox { Text = "newProperty", Height = 28, Padding = new Thickness(7, 4, 7, 4), Background = KerfBrand.FigmaCanvasBrush, Foreground = Brushes.White, BorderBrush = KerfBrand.FigmaBorderBrush, Margin = new Thickness(0, 0, 0, 10) };
            form.Children.Add(CreateTransitionFormRow("Name", nameBox));
            var kindBox = CreateModernComboBox(30);
            foreach (var kind in new ViewModelPropertyKind[] { ViewModelPropertyKind.Boolean, ViewModelPropertyKind.Number, ViewModelPropertyKind.Trigger, ViewModelPropertyKind.Text })
                kindBox.Items.Add(new ComboBoxItem { Content = kind.ToString(), Tag = kind });
            kindBox.SelectedIndex = 0;
            form.Children.Add(CreateTransitionFormRow("Type", kindBox));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var cancel = KerfBrand.CreateSecondaryButton("Cancel", () => dialog.Close(), 6);
            cancel.Width = 72;
            cancel.Height = 26;
            cancel.Padding = new Thickness(0);
            var create = KerfBrand.CreatePrimaryButton("Create", () =>
            {
                string propertyName = nameBox.Text.Trim();
                if (string.IsNullOrEmpty(propertyName)) return;
                if (sm.Definition.ViewModelProperties.Any(p => p != null && string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("A View Model property with this name already exists.", "State Machine", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var selected = kindBox.SelectedItem as ComboBoxItem;
                var kind = selected != null ? (ViewModelPropertyKind)selected.Tag : ViewModelPropertyKind.Boolean;
                sm.Definition.AddViewModelProperty(new ViewModelPropertyDefinition(propertyName, kind));
                InputKind ik = (kind == ViewModelPropertyKind.Boolean) ? InputKind.Bool : ((kind == ViewModelPropertyKind.Number) ? InputKind.Number : InputKind.Trigger);
                if (sm.Definition.Inputs == null) sm.Definition.Inputs = new List<MachineInput>();
                if (!sm.Definition.Inputs.Any(inp => inp != null && string.Equals(inp.Name, propertyName, StringComparison.OrdinalIgnoreCase)))
                {
                    sm.Definition.AddInput(new MachineInput(propertyName, ik, 0.0));
                }
                sm.RebuildViewModel();
                History.MarkDirty();
                BuildStateMachineInputsControls();
                BuildStateMachineStatesGraph();
                dialog.Close();
            }, 6);
            create.Width = 78;
            create.Height = 26;
            create.Padding = new Thickness(0);
            create.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(cancel);
            buttons.Children.Add(create);
            form.Children.Add(buttons);
            dialog.Content = form;
            dialog.ShowDialog();
        }

        private void ShowAddStateMachineListenerDialog()
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null || Document == null) return;
            var sm = _timelineController.ActiveStateMachine;
            var page = Document.GetActivePage();
            var layers = page != null ? page.GetAllLayersFlat().Where(l => l != null).ToList() : new List<LayerNode>();
            if (layers.Count == 0 || sm.Definition.ViewModelProperties == null || sm.Definition.ViewModelProperties.Count == 0) return;

            var dialog = new Window
            {
                Title = "Add Pointer Listener",
                Width = 380,
                Height = 370,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = KerfBrand.FigmaPanelBrush,
                Foreground = Brushes.White
            };
            var owner = Window.GetWindow(this);
            if (owner != null) dialog.Owner = owner;
            var form = new StackPanel { Margin = new Thickness(18) };
            var targetBox = CreateModernComboBox(30);
            foreach (var layer in layers) targetBox.Items.Add(new ComboBoxItem { Content = layer.Name, Tag = layer });
            targetBox.SelectedIndex = 0;
            form.Children.Add(CreateTransitionFormRow("Layer", targetBox));
            var eventBox = CreateModernComboBox(30);
            foreach (var kind in new PointerEventKind[] { PointerEventKind.Down, PointerEventKind.Up, PointerEventKind.Enter, PointerEventKind.Exit, PointerEventKind.Move })
                eventBox.Items.Add(new ComboBoxItem { Content = kind.ToString(), Tag = kind });
            eventBox.SelectedIndex = 2;
            form.Children.Add(CreateTransitionFormRow("Event", eventBox));
            var propertyBox = CreateModernComboBox(30);
            foreach (var property in sm.Definition.ViewModelProperties)
                if (property != null) propertyBox.Items.Add(new ComboBoxItem { Content = property.Name + " · " + property.Kind, Tag = property });
            if (propertyBox.Items.Count > 0) propertyBox.SelectedIndex = 0;
            form.Children.Add(CreateTransitionFormRow("Property", propertyBox));
            var valueBox = new TextBox { Text = "1", Height = 28, Padding = new Thickness(7, 4, 7, 4), Background = KerfBrand.FigmaCanvasBrush, Foreground = Brushes.White, BorderBrush = KerfBrand.FigmaBorderBrush };
            form.Children.Add(CreateTransitionFormRow("Set value", valueBox));
            form.Children.Add(new TextBlock { Text = "Click a listener chip below to remove it.", FontSize = 10, Foreground = KerfBrand.TextMutedBrush, Margin = new Thickness(0, 0, 0, 8) });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var cancel = KerfBrand.CreateSecondaryButton("Cancel", () => dialog.Close(), 6);
            cancel.Width = 72;
            cancel.Height = 26;
            cancel.Padding = new Thickness(0);
            var create = KerfBrand.CreatePrimaryButton("Create", () =>
            {
                var layerItem = targetBox.SelectedItem as ComboBoxItem;
                var eventItem = eventBox.SelectedItem as ComboBoxItem;
                var propertyItem = propertyBox.SelectedItem as ComboBoxItem;
                if (layerItem == null || eventItem == null || propertyItem == null) return;
                var target = layerItem.Tag as LayerNode;
                var property = propertyItem.Tag as ViewModelPropertyDefinition;
                if (target == null || property == null) return;
                double numeric;
                if (!double.TryParse(valueBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out numeric)) numeric = 1.0;
                var listener = new NodeListener(PropertyBinder.GetNumericNodeId(target.Id), (PointerEventKind)eventItem.Tag);
                ListenerAction action;
                if (property.Kind == ViewModelPropertyKind.Boolean)
                    action = new ListenerAction(ActionKind.SetViewModelBool, property.Name, numeric > 0.5 ? 1.0 : 0.0);
                else if (property.Kind == ViewModelPropertyKind.Number)
                    action = new ListenerAction(ActionKind.SetViewModelNumber, property.Name, numeric);
                else if (property.Kind == ViewModelPropertyKind.Trigger)
                    action = new ListenerAction(ActionKind.FireViewModelTrigger, property.Name);
                else
                {
                    action = new ListenerAction(ActionKind.SetViewModelText, property.Name);
                    action.TextValue = valueBox.Text;
                }
                listener.Actions.Add(action);
                sm.Definition.AddListener(listener);
                History.MarkDirty();
                BuildStateMachineListeners();
                dialog.Close();
            }, 6);
            create.Width = 78;
            create.Height = 26;
            create.Padding = new Thickness(0);
            create.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(cancel);
            buttons.Children.Add(create);
            form.Children.Add(buttons);
            dialog.Content = form;
            dialog.ShowDialog();
        }

        private void BuildStateMachineListeners()
        {
            if (_stateMachineListenersStack == null) return;
            _stateMachineListenersStack.Children.Clear();
            var sm = _timelineController != null ? _timelineController.ActiveStateMachine : null;
            if (sm == null || sm.Definition == null || sm.Definition.Listeners == null) return;
            var page = Document != null ? Document.GetActivePage() : null;
            foreach (var listener in sm.Definition.Listeners.ToList())
            {
                if (listener == null) continue;
                var layer = page != null ? page.GetAllLayersFlat().FirstOrDefault(l => PropertyBinder.GetNumericNodeId(l.Id) == listener.TargetNodeId) : null;
                string actionText = listener.Actions != null && listener.Actions.Count > 0 && listener.Actions[0] != null
                    ? listener.Actions[0].PropertyName : "action";
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(27, 32, 46)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(48, 58, 80)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(7, 5, 7, 5),
                    Margin = new Thickness(0, 2, 0, 2),
                    Child = new TextBlock { Text = (layer != null ? layer.Name : "Layer") + " · " + listener.Kind + " → " + actionText, FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(165, 178, 201)) },
                    ToolTip = "Click to remove listener",
                    Cursor = Cursors.Hand
                };
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    sm.Definition.Listeners.Remove(listener);
                    History.MarkDirty();
                    BuildStateMachineListeners();
                };
                chip.Tag = (layer != null ? layer.Name : "Layer") + " " + listener.Kind + " " + actionText;
                _stateMachineListenersStack.Children.Add(chip);
            }
            if (_stateMachineListenerSearch != null)
                FilterStateMachineItems(_stateMachineListenersStack, _stateMachineListenerSearch.Text);
        }

        private void ShowAddTransitionDialog()
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return;
            var sm = _timelineController.ActiveStateMachine;
            if (sm.Definition == null || sm.Definition.Layers == null || sm.Definition.Layers.Count == 0) return;
            var layer = sm.Definition.Layers[0];
            if (layer.States == null || layer.States.Count < 2) return;

            var dialog = new Window
            {
                Title = "Add Transition",
                Width = 400,
                Height = 500,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = KerfBrand.FigmaPanelBrush,
                Foreground = Brushes.White
            };
            var owner = Window.GetWindow(this);
            if (owner != null) dialog.Owner = owner;
            var form = new StackPanel { Margin = new Thickness(18) };
            ComboBox fromBox = CreateModernComboBox(30);
            ComboBox toBox = CreateModernComboBox(30);
            for (int i = 0; i < layer.States.Count; i++)
            {
                var fromItem = new ComboBoxItem { Content = layer.States[i].Name, Tag = i };
                fromBox.Items.Add(fromItem);
                if (layer.States[i].Kind != StateKind.Entry && layer.States[i].Kind != StateKind.Any)
                    toBox.Items.Add(new ComboBoxItem { Content = layer.States[i].Name, Tag = i });
            }
            if (fromBox.Items.Count > 0) fromBox.SelectedIndex = 0;
            if (toBox.Items.Count > 0) toBox.SelectedIndex = Math.Min(1, toBox.Items.Count - 1);
            form.Children.Add(CreateTransitionFormRow("From", fromBox));
            form.Children.Add(CreateTransitionFormRow("To", toBox));

            var durationBox = new TextBox { Text = "0.2", Height = 28, Padding = new Thickness(7, 4, 7, 4), Background = KerfBrand.FigmaCanvasBrush, Foreground = Brushes.White, BorderBrush = KerfBrand.FigmaBorderBrush };
            form.Children.Add(CreateTransitionFormRow("Blend (s)", durationBox));

            var interpolationBox = CreateModernComboBox(30);
            interpolationBox.Items.Add(new ComboBoxItem { Content = "Hold", Tag = InterpMode.Hold });
            interpolationBox.Items.Add(new ComboBoxItem { Content = "Linear", Tag = InterpMode.Linear });
            interpolationBox.Items.Add(new ComboBoxItem { Content = "Cubic", Tag = InterpMode.Cubic });
            interpolationBox.SelectedIndex = 2;
            form.Children.Add(CreateTransitionFormRow("Interpolation", interpolationBox));

            var conditionPicker = CreateModernComboBox(30);
            conditionPicker.Items.Add(new ComboBoxItem { Content = "Always", Tag = null });
            foreach (var property in sm.Definition.ViewModelProperties ?? new List<ViewModelPropertyDefinition>())
                if (property != null) conditionPicker.Items.Add(new ComboBoxItem { Content = property.Name, Tag = property });
            for (int i = 0; i < sm.Definition.Inputs.Count; i++)
                if (sm.Definition.Inputs[i] != null) conditionPicker.Items.Add(new ComboBoxItem { Content = sm.Definition.Inputs[i].Name, Tag = i });
            conditionPicker.SelectedIndex = 0;
            form.Children.Add(CreateTransitionFormRow("When", conditionPicker));

            var opBox = CreateModernComboBox(30);
            foreach (var op in new ConditionOp[] { ConditionOp.Eq, ConditionOp.Ne, ConditionOp.Gt, ConditionOp.Ge, ConditionOp.Lt, ConditionOp.Le })
                opBox.Items.Add(new ComboBoxItem { Content = op.ToString(), Tag = op });
            opBox.SelectedIndex = 0;
            var valueBox = new TextBox { Text = "1", Width = 74, Height = 28, Padding = new Thickness(7, 4, 7, 4), Background = KerfBrand.FigmaCanvasBrush, Foreground = Brushes.White, BorderBrush = KerfBrand.FigmaBorderBrush };
            var conditionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            conditionRow.Children.Add(opBox);
            conditionRow.Children.Add(valueBox);
            form.Children.Add(conditionRow);

            var conditions = new List<TransitionCondition>();
            var conditionTokens = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var addConditionButton = CreateSmallButton("+ AND condition", "Add another condition to this transition", () =>
            {
                var selected = conditionPicker.SelectedItem as ComboBoxItem;
                if (selected == null || selected.Tag == null) return;
                double threshold;
                if (!double.TryParse(valueBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold)) threshold = 1.0;
                var opItem = opBox.SelectedItem as ComboBoxItem;
                var operation = opItem != null ? (ConditionOp)opItem.Tag : ConditionOp.Eq;
                var property = selected.Tag as ViewModelPropertyDefinition;
                TransitionCondition condition;
                if (property != null)
                {
                    condition = new TransitionCondition(property.Name, operation, threshold);
                    if (property.Kind == ViewModelPropertyKind.Text) condition.TextValue = valueBox.Text;
                }
                else if (selected.Tag is int)
                {
                    condition = new TransitionCondition((int)selected.Tag, operation, threshold);
                }
                else return;
                conditions.Add(condition);
                var token = new Border
                {
                    Background = KerfBrand.FigmaCardBrush,
                    BorderBrush = KerfBrand.FigmaBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(7, 3, 7, 3),
                    Margin = new Thickness(0, 0, 5, 4),
                    Child = new TextBlock { Text = (property != null ? property.Name : selected.Content.ToString()) + " " + operation.ToString() + " " + (property != null && property.Kind == ViewModelPropertyKind.Text ? valueBox.Text : threshold.ToString("0.##")), FontSize = 9.5, Foreground = Brushes.White }
                };
                token.ToolTip = "Click to remove condition";
                token.Cursor = Cursors.Hand;
                token.MouseLeftButtonUp += (s, e) =>
                {
                    conditions.Remove(condition);
                    conditionTokens.Children.Remove(token);
                };
                conditionTokens.Children.Add(token);
            });
            form.Children.Add(addConditionButton);
            form.Children.Add(conditionTokens);

            var bidirectional = new CheckBox { Content = "Also add a reverse transition", Foreground = KerfBrand.TextMutedBrush, FontSize = 10.5, Margin = new Thickness(0, 2, 0, 4) };
            form.Children.Add(bidirectional);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var cancelButton = KerfBrand.CreateSecondaryButton("Cancel", () => dialog.Close(), 6);
            cancelButton.Width = 72;
            cancelButton.Height = 26;
            cancelButton.Padding = new Thickness(0);
            var addButton = KerfBrand.CreatePrimaryButton("Create", () =>
            {
                var from = fromBox.SelectedItem as ComboBoxItem;
                var to = toBox.SelectedItem as ComboBoxItem;
                if (from == null || to == null) return;
                double duration;
                if (!double.TryParse(durationBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out duration)) duration = 0.2;
                int fromIndex = (int)from.Tag;
                int toIndex = (int)to.Tag;
                var transition = new StateTransition(fromIndex, toIndex, Math.Max(0.0, duration));
                var interpolation = interpolationBox.SelectedItem as ComboBoxItem;
                if (interpolation != null && interpolation.Tag is InterpMode) transition.Interpolation = (InterpMode)interpolation.Tag;
                transition.Conditions.AddRange(conditions);
                layer.AddTransition(transition);
                _selectedGraphTransition = transition;
                _selectedGraphTransitionLayer = layer;
                if (bidirectional.IsChecked == true) AddReverseTransition(layer, transition);
                History.MarkDirty();
                BuildStateMachineStatesGraph();
                RefreshTimelineUI();
                dialog.Close();
            }, 6);
            addButton.Width = 78;
            addButton.Height = 26;
            addButton.Padding = new Thickness(0);
            addButton.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(cancelButton);
            buttons.Children.Add(addButton);
            form.Children.Add(buttons);
            dialog.Content = form;
            dialog.ShowDialog();
        }

        private void ShowEditTransitionDialog(StateTransition transition, MachineLayer layer)
        {
            if (transition == null || layer == null || _timelineController == null || _timelineController.ActiveStateMachine == null) return;
            var sm = _timelineController.ActiveStateMachine;
            var dialog = new Window { Title = "Transition", Width = 360, Height = 330, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = KerfBrand.FigmaPanelBrush, Foreground = Brushes.White };
            var owner = Window.GetWindow(this); if (owner != null) dialog.Owner = owner;
            var form = new StackPanel { Margin = new Thickness(16) };
            var duration = new TextBox { Text = transition.Duration.ToString("0.##", CultureInfo.InvariantCulture), Height = 28, Padding = new Thickness(7, 4, 7, 4), Background = KerfBrand.FigmaCanvasBrush, Foreground = Brushes.White, BorderBrush = KerfBrand.FigmaBorderBrush };
            form.Children.Add(CreateTransitionFormRow("Blend (s)", duration));
            var interpolation = CreateModernComboBox(30);
            foreach (InterpMode mode in new[] { InterpMode.Hold, InterpMode.Linear, InterpMode.Cubic }) interpolation.Items.Add(new ComboBoxItem { Content = mode.ToString(), Tag = mode });
            interpolation.SelectedIndex = (int)transition.Interpolation;
            form.Children.Add(CreateTransitionFormRow("Interpolation", interpolation));

            var conditionPicker = CreateModernComboBox(30);
            conditionPicker.Items.Add(new ComboBoxItem { Content = "Always", Tag = null });
            foreach (var property in sm.Definition.ViewModelProperties ?? new List<ViewModelPropertyDefinition>())
                if (property != null) conditionPicker.Items.Add(new ComboBoxItem { Content = property.Name, Tag = property });
            for (int i = 0; i < sm.Definition.Inputs.Count; i++)
                if (sm.Definition.Inputs[i] != null) conditionPicker.Items.Add(new ComboBoxItem { Content = sm.Definition.Inputs[i].Name, Tag = i });
            var existingCondition = transition.Conditions != null ? transition.Conditions.FirstOrDefault() : null;
            if (existingCondition == null) conditionPicker.SelectedIndex = 0;
            else
            {
                conditionPicker.SelectedIndex = conditionPicker.Items.Cast<ComboBoxItem>().Select((item, index) => new { item, index }).Where(pair =>
                {
                    var property = pair.item.Tag as ViewModelPropertyDefinition;
                    if (property != null) return string.Equals(property.Name, existingCondition.PropertyName, StringComparison.OrdinalIgnoreCase);
                    if (pair.item.Tag is int) return (int)pair.item.Tag == existingCondition.InputIndex;
                    return false;
                }).Select(pair => pair.index).FirstOrDefault();
            }
            form.Children.Add(CreateTransitionFormRow("When", conditionPicker));
            var operators = CreateModernComboBox(30);
            foreach (var op in new[] { ConditionOp.Eq, ConditionOp.Ne, ConditionOp.Gt, ConditionOp.Ge, ConditionOp.Lt, ConditionOp.Le }) operators.Items.Add(new ComboBoxItem { Content = op.ToString(), Tag = op });
            var initialOp = existingCondition != null ? (int)existingCondition.Op : 0;
            operators.SelectedIndex = Math.Max(0, Math.Min(5, initialOp));
            var value = new TextBox { Text = existingCondition != null ? (existingCondition.TextValue ?? existingCondition.Value.ToString("0.##", CultureInfo.InvariantCulture)) : "1", Width = 100, Height = 28, Padding = new Thickness(7, 4, 7, 4), Background = KerfBrand.FigmaCanvasBrush, Foreground = Brushes.White, BorderBrush = KerfBrand.FigmaBorderBrush };
            var conditionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            conditionRow.Children.Add(operators); conditionRow.Children.Add(value); form.Children.Add(conditionRow);
            bool reverseExists = layer.Transitions.Any(candidate => candidate != null && candidate != transition && candidate.FromState == transition.ToState && candidate.ToState == transition.FromState);
            var bidirectional = new CheckBox { Content = "Add reverse transition", IsChecked = reverseExists, IsEnabled = !reverseExists, Foreground = KerfBrand.TextMutedBrush, FontSize = 10.5, Margin = new Thickness(0, 0, 0, 8) };
            form.Children.Add(bidirectional);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var editCancelBtn = KerfBrand.CreateSecondaryButton("Cancel", () => dialog.Close(), 6);
            editCancelBtn.Width = 72;
            editCancelBtn.Height = 26;
            editCancelBtn.Padding = new Thickness(0);
            buttons.Children.Add(editCancelBtn);
            var saveButton = KerfBrand.CreatePrimaryButton("Save", () =>
            {
                double blend;
                if (!double.TryParse(duration.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out blend)) blend = transition.Duration;
                transition.Duration = Math.Max(0.0, blend);
                var mode = interpolation.SelectedItem as ComboBoxItem;
                if (mode != null && mode.Tag is InterpMode) transition.Interpolation = (InterpMode)mode.Tag;
                var reverseTransition = layer.Transitions.FirstOrDefault(candidate => candidate != null && candidate != transition && candidate.FromState == transition.ToState && candidate.ToState == transition.FromState);
                if (reverseTransition != null)
                {
                    reverseTransition.Duration = transition.Duration;
                    reverseTransition.Interpolation = transition.Interpolation;
                    reverseTransition.X1 = transition.X1; reverseTransition.Y1 = transition.Y1;
                    reverseTransition.X2 = transition.X2; reverseTransition.Y2 = transition.Y2;
                }
                var retainedConditions = transition.Conditions.Skip(1).Where(condition => condition != null).ToList();
                transition.Conditions.Clear();
                var selected = conditionPicker.SelectedItem as ComboBoxItem;
                var opItem = operators.SelectedItem as ComboBoxItem;
                var operation = opItem != null ? (ConditionOp)opItem.Tag : ConditionOp.Eq;
                double threshold;
                if (!double.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold)) threshold = 1.0;
                if (selected != null && selected.Tag != null)
                {
                    var property = selected.Tag as ViewModelPropertyDefinition;
                    if (property != null)
                    {
                        var condition = new TransitionCondition(property.Name, operation, threshold);
                        if (property.Kind == ViewModelPropertyKind.Text) condition.TextValue = value.Text;
                        transition.Conditions.Add(condition);
                    }
                    else if (selected.Tag is int) transition.Conditions.Add(new TransitionCondition((int)selected.Tag, operation, threshold));
                    transition.Conditions.AddRange(retainedConditions);
                }
                if (reverseTransition != null && transition.Conditions.Count == 1)
                {
                    var condition = transition.Conditions[0];
                    reverseTransition.Conditions.Clear();
                    reverseTransition.Conditions.Add(new TransitionCondition(condition.InputIndex, InvertCondition(condition.Op), condition.Value) { PropertyName = condition.PropertyName, TextValue = condition.TextValue });
                }
                else if (reverseTransition != null && transition.Conditions.Count == 0)
                {
                    layer.Transitions.Remove(reverseTransition);
                }
                _selectedGraphTransition = transition;
                _selectedGraphTransitionLayer = layer;
                if (bidirectional.IsChecked == true && !reverseExists) AddReverseTransition(layer, transition);
                History.MarkDirty();
                BuildStateMachineStatesGraph();
                dialog.Close();
            }, 6);
            saveButton.Width = 78;
            saveButton.Height = 26;
            saveButton.Padding = new Thickness(0);
            saveButton.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(saveButton);
            form.Children.Add(buttons);
            dialog.Content = form;
            dialog.ShowDialog();
        }

        private FrameworkElement CreateTransitionFormRow(string label, FrameworkElement control)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(165, 178, 201)), VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
            Grid.SetColumn(text, 0);
            Grid.SetColumn(control, 1);
            grid.Children.Add(text);
            grid.Children.Add(control);
            return grid;
        }

        private MachineLayer EnsureStateMachineGraphLayer(StateMachineDefinition definition)
        {
            if (definition == null) return null;
            bool changed = false;
            if (definition.Layers == null) definition.Layers = new List<MachineLayer>();
            if (definition.Layers.Count == 0) { definition.Layers.Add(new MachineLayer("Base Layer")); changed = true; }
            var layer = definition.Layers[0];
            if (layer.States == null) layer.States = new List<MachineState>();
            if (layer.Transitions == null) layer.Transitions = new List<StateTransition>();
            if (!layer.States.Any(state => state != null && state.Kind == StateKind.Entry)) { layer.AddState(new MachineState(layer.States.Count, "Entry", StateKind.Entry)); changed = true; }
            if (!layer.States.Any(state => state != null && state.Kind == StateKind.Any)) { layer.AddState(new MachineState(layer.States.Count, "Any State", StateKind.Any)); changed = true; }
            if (!layer.States.Any(state => state != null && state.Kind == StateKind.Exit)) { layer.AddState(new MachineState(layer.States.Count, "Exit", StateKind.Exit)); changed = true; }
            for (int i = 0; i < layer.States.Count; i++) if (layer.States[i] != null) layer.States[i].Id = i;
            int entryIndex = layer.States.FindIndex(state => state != null && state.Kind == StateKind.Entry);
            int initialAnimationIndex = layer.States.FindIndex(state => state != null && (state.Kind == StateKind.Animation || state.Kind == StateKind.Blend1D || state.Kind == StateKind.BlendDirect));
            if (entryIndex >= 0 && initialAnimationIndex >= 0 && !layer.Transitions.Any(transition => transition != null && transition.FromState == entryIndex))
            {
                layer.AddTransition(new StateTransition(entryIndex, initialAnimationIndex, 0.0));
                changed = true;
            }
            if (changed && _timelineController != null && _timelineController.ActiveStateMachine != null)
            {
                _timelineController.ActiveStateMachine = new StateMachineRuntime(definition, _timelineController.ActiveStateMachine.Clips);
                if (History != null) History.MarkDirty();
            }
            return layer;
        }

        private void EnsureStateGraphPositions(MachineLayer layer)
        {
            if (layer == null || layer.States == null) return;
            int animationIndex = 0;
            bool changed = false;
            for (int i = 0; i < layer.States.Count; i++)
            {
                var state = layer.States[i];
                if (state == null || (state.GraphX >= 0 && state.GraphY >= 0)) continue;
                if (state.Kind == StateKind.Entry) { state.GraphX = 24; state.GraphY = 88; }
                else if (state.Kind == StateKind.Any) { state.GraphX = 510; state.GraphY = 18; }
                else if (state.Kind == StateKind.Exit) { state.GraphX = 680; state.GraphY = 88; }
                else
                {
                    state.GraphX = 166 + (animationIndex % 3) * 150;
                    state.GraphY = 70 + (animationIndex / 3) * 82;
                    animationIndex++;
                }
                changed = true;
            }
            if (changed && History != null) History.MarkDirty();
        }

        private void UpdateStateGraphCanvasSize(MachineLayer layer)
        {
            if (_stateMachineGraphCanvas == null || layer == null || layer.States == null) return;
            double width = 780.0;
            double height = 230.0;
            foreach (var state in layer.States)
            {
                if (state == null) continue;
                double nodeWidth = state.Kind == StateKind.Entry ? 88.0 : state.Kind == StateKind.Any ? 116.0 : state.Kind == StateKind.Exit ? 78.0 : 132.0;
                double nodeHeight = state.Kind == StateKind.Entry || state.Kind == StateKind.Exit ? 40.0 : 48.0;
                width = Math.Max(width, state.GraphX + nodeWidth + 56.0);
                height = Math.Max(height, state.GraphY + nodeHeight + 56.0);
            }
            _stateMachineGraphCanvas.Width = width;
            _stateMachineGraphCanvas.Height = height;
        }

        private Rect GetStateGraphBounds()
        {
            var bounds = Rect.Empty;
            var states = _timelineController != null && _timelineController.ActiveStateMachine != null && _timelineController.ActiveStateMachine.Definition.Layers.Count > 0
                ? _timelineController.ActiveStateMachine.Definition.Layers[0].States
                : null;
            if (states != null)
            {
                foreach (var state in states)
                {
                    if (state == null) continue;
                    double width = state.Kind == StateKind.Entry ? 88.0 : state.Kind == StateKind.Any ? 116.0 : state.Kind == StateKind.Exit ? 78.0 : 132.0;
                    double height = state.Kind == StateKind.Entry || state.Kind == StateKind.Exit ? 40.0 : 48.0;
                    bounds.Union(new Rect(state.GraphX, state.GraphY, width, height));
                }
            }
            return bounds.IsEmpty ? new Rect(0.0, 0.0, 780.0, 230.0) : bounds;
        }

        private void UpdateStateGraphZoomLabel()
        {
            if (_stateMachineGraphZoomText != null)
                _stateMachineGraphZoomText.Text = Math.Round(_stateMachineGraphZoom * 100.0).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private void SetStateGraphZoom(double zoom, Point viewportAnchor)
        {
            if (_stateMachineGraphScrollViewer == null) return;
            zoom = Math.Max(0.25, Math.Min(2.5, zoom));
            double oldZoom = _stateMachineGraphZoom;
            if (Math.Abs(zoom - oldZoom) < 0.001) return;
            double anchorX = (_stateMachineGraphScrollViewer.HorizontalOffset + viewportAnchor.X) / oldZoom;
            double anchorY = (_stateMachineGraphScrollViewer.VerticalOffset + viewportAnchor.Y) / oldZoom;
            _stateMachineGraphZoom = zoom;
            _stateMachineGraphScale.ScaleX = zoom;
            _stateMachineGraphScale.ScaleY = zoom;
            UpdateStateGraphZoomLabel();
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (_stateMachineGraphScrollViewer == null) return;
                _stateMachineGraphScrollViewer.ScrollToHorizontalOffset(anchorX * zoom - viewportAnchor.X);
                _stateMachineGraphScrollViewer.ScrollToVerticalOffset(anchorY * zoom - viewportAnchor.Y);
            }));
        }

        private void ZoomStateGraphBy(double factor)
        {
            if (_stateMachineGraphScrollViewer == null) return;
            SetStateGraphZoom(_stateMachineGraphZoom * factor, new Point(_stateMachineGraphScrollViewer.ViewportWidth / 2.0, _stateMachineGraphScrollViewer.ViewportHeight / 2.0));
        }

        private void FitStateGraphToView()
        {
            if (_stateMachineGraphScrollViewer == null) return;
            var bounds = GetStateGraphBounds();
            double viewWidth = Math.Max(1.0, _stateMachineGraphScrollViewer.ViewportWidth - 40.0);
            double viewHeight = Math.Max(1.0, _stateMachineGraphScrollViewer.ViewportHeight - 40.0);
            double zoom = Math.Min(viewWidth / Math.Max(1.0, bounds.Width), viewHeight / Math.Max(1.0, bounds.Height));
            zoom = Math.Max(0.25, Math.Min(2.5, zoom));
            _stateMachineGraphZoom = zoom;
            _stateMachineGraphScale.ScaleX = zoom;
            _stateMachineGraphScale.ScaleY = zoom;
            UpdateStateGraphZoomLabel();
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (_stateMachineGraphScrollViewer == null) return;
                _stateMachineGraphScrollViewer.ScrollToHorizontalOffset(bounds.X * zoom + bounds.Width * zoom / 2.0 - _stateMachineGraphScrollViewer.ViewportWidth / 2.0);
                _stateMachineGraphScrollViewer.ScrollToVerticalOffset(bounds.Y * zoom + bounds.Height * zoom / 2.0 - _stateMachineGraphScrollViewer.ViewportHeight / 2.0);
            }));
        }

        private void HandleStateGraphMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || _stateMachineGraphScrollViewer == null) return;
            Point anchor = e.GetPosition(_stateMachineGraphScrollViewer);
            SetStateGraphZoom(_stateMachineGraphZoom * (e.Delta > 0 ? 1.12 : 1.0 / 1.12), anchor);
            e.Handled = true;
        }

        private void HandleStateGraphPanStart(object sender, MouseButtonEventArgs e)
        {
            bool spacePan = e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space);
            bool middlePan = e.ChangedButton == MouseButton.Middle;
            if ((!spacePan && !middlePan) || _stateMachineGraphCanvas == null || _stateMachineGraphScrollViewer == null) return;
            _isPanningStateGraph = true;
            _stateGraphPanStart = e.GetPosition(_stateMachineGraphScrollViewer);
            _stateGraphPanStartHorizontalOffset = _stateMachineGraphScrollViewer.HorizontalOffset;
            _stateGraphPanStartVerticalOffset = _stateMachineGraphScrollViewer.VerticalOffset;
            _stateMachineGraphCanvas.Cursor = Cursors.ScrollAll;
            _stateMachineGraphCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void HandleStateGraphPanEnd(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanningStateGraph || (e.ChangedButton != MouseButton.Left && e.ChangedButton != MouseButton.Middle)) return;
            _isPanningStateGraph = false;
            if (_stateMachineGraphCanvas != null)
            {
                if (_stateMachineGraphCanvas.IsMouseCaptured) _stateMachineGraphCanvas.ReleaseMouseCapture();
                _stateMachineGraphCanvas.Cursor = Cursors.Arrow;
            }
            e.Handled = true;
        }

        private void BuildStateMachineStatesGraph()
        {
            if (_stateMachineGraphCanvas == null) return;
            var sm = EnsureStateMachineRuntime();
            if (sm == null || sm.Definition == null) return;
            var layer = EnsureStateMachineGraphLayer(sm.Definition);
            if (layer == null) return;
            EnsureStateGraphPositions(layer);
            UpdateStateGraphCanvasSize(layer);
            _stateMachineGraphCanvas.Children.Clear();
            _stateGraphNodes.Clear();
            DrawStateMachineConnections(layer);

            int currentState = sm.LayerRuntimes.Count > 0 ? sm.LayerRuntimes[0].CurrentState : -1;
            int targetState = sm.LayerRuntimes.Count > 0 && sm.LayerRuntimes[0].ActiveTransition != null ? sm.LayerRuntimes[0].ActiveTransition.ToState : -1;
            _lastGraphStateMachine = sm;
            _lastGraphCurrentState = currentState;
            _lastGraphTransitionTarget = targetState;

            for (int i = 0; i < layer.States.Count; i++)
            {
                int stateIndex = i;
                var state = layer.States[i];
                if (state == null) continue;
                double width = state.Kind == StateKind.Entry ? 88 : state.Kind == StateKind.Any ? 116 : state.Kind == StateKind.Exit ? 78 : 132;
                double height = state.Kind == StateKind.Entry || state.Kind == StateKind.Exit ? 40 : 48;
                bool active = stateIndex == currentState || stateIndex == targetState;
                bool selected = stateIndex == _selectedGraphStateIndex;
                var node = new Border
                {
                    Width = width,
                    Height = height,
                    Background = KerfBrand.FigmaCardBrush,
                    BorderBrush = (active || selected) ? KerfBrand.MintBrush : KerfBrand.FigmaBorderBrush,
                    BorderThickness = new Thickness(active || selected ? 1.5 : 1),
                    CornerRadius = new CornerRadius(5),
                    Cursor = state.Kind == StateKind.Exit ? Cursors.Arrow : Cursors.SizeAll,
                    ToolTip = state.Kind + (state.Kind == StateKind.Animation ? " · double-click to edit timeline" : string.Empty)
                };
                var content = new Grid { Margin = new Thickness(8, 4, state.Kind == StateKind.Exit ? 8 : 18, 4) };
                var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var name = new TextBlock { Text = state.Name, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = (state.Kind == StateKind.Entry || active) ? KerfBrand.MintBrush : Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis };
                labels.Children.Add(name);
                if (state.Kind == StateKind.Animation || state.Kind == StateKind.Blend1D || state.Kind == StateKind.BlendDirect)
                {
                    string clipName = state.ClipIndex >= 0 && state.ClipIndex < sm.Clips.Count && sm.Clips[state.ClipIndex] != null ? sm.Clips[state.ClipIndex].Name : state.Kind.ToString();
                    labels.Children.Add(new TextBlock { Text = clipName, FontSize = 8.5, Foreground = new SolidColorBrush(Color.FromRgb(132, 141, 156)), TextTrimming = TextTrimming.CharacterEllipsis });
                }
                content.Children.Add(labels);
                if (state.Kind != StateKind.Exit)
                {
                    var port = new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.FromRgb(167, 176, 191)), Stroke = new SolidColorBrush(Color.FromRgb(35, 39, 47)), StrokeThickness = 2, Cursor = Cursors.Cross, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Tag = stateIndex };
                    port.MouseLeftButtonDown += (s, e) => { StartStateGraphConnection(stateIndex, e.GetPosition(_stateMachineGraphCanvas)); e.Handled = true; };
                    content.Children.Add(port);
                }
                node.Child = content;
                Canvas.SetLeft(node, state.GraphX);
                Canvas.SetTop(node, state.GraphY);
                Panel.SetZIndex(node, 10);
                node.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount >= 2 && state.Kind == StateKind.Animation)
                    {
                        OpenStateTimeline(state);
                        e.Handled = true;
                        return;
                    }
                    _selectedGraphStateIndex = stateIndex;
                    if (state.Kind != StateKind.Exit) StartStateGraphNodeDrag(stateIndex, e.GetPosition(_stateMachineGraphCanvas));
                    e.Handled = true;
                };
                node.MouseRightButtonUp += (s, e) => ShowStateNodeContextMenu(node, layer, state, stateIndex);
                _stateGraphNodes[stateIndex] = node;
                _stateMachineGraphCanvas.Children.Add(node);
            }
            AddStateGraphBackgroundHint();
            RefreshInterpolationGraphPanels();
        }

        private void AddStateGraphBackgroundHint()
        {
            if (_stateMachineGraphCanvas == null) return;
            var hint = new TextBlock { Text = "ENTRY  →  STATES  →  EXIT     ·     ANY STATE can enter a state at any time", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(78, 88, 104)), IsHitTestVisible = false };
            Canvas.SetLeft(hint, 24);
            Canvas.SetTop(hint, 206);
            _stateMachineGraphCanvas.Children.Add(hint);
        }

        private void DrawStateMachineConnections(MachineLayer layer)
        {
            if (_stateMachineGraphCanvas == null || layer == null || layer.States == null || layer.Transitions == null) return;
            foreach (var transition in layer.Transitions)
            {
                if (transition == null || transition.FromState < 0 || transition.FromState >= layer.States.Count || transition.ToState < 0 || transition.ToState >= layer.States.Count) continue;
                var from = layer.States[transition.FromState];
                var to = layer.States[transition.ToState];
                if (from == null || to == null) continue;
                double fromWidth = from.Kind == StateKind.Entry ? 88 : from.Kind == StateKind.Any ? 116 : from.Kind == StateKind.Exit ? 78 : 132;
                double fromHeight = from.Kind == StateKind.Entry || from.Kind == StateKind.Exit ? 40 : 48;
                double toWidth = to.Kind == StateKind.Entry ? 88 : to.Kind == StateKind.Any ? 116 : to.Kind == StateKind.Exit ? 78 : 132;
                double toHeight = to.Kind == StateKind.Entry || to.Kind == StateKind.Exit ? 40 : 48;
                Point start = new Point(from.GraphX + fromWidth, from.GraphY + fromHeight / 2.0);
                Point end = new Point(to.GraphX, to.GraphY + toHeight / 2.0);
                bool selfTransition = transition.ToState == transition.FromState;
                bool backwards = !selfTransition && end.X < start.X - 20;
                if (backwards) end = new Point(to.GraphX + toWidth, to.GraphY + toHeight / 2.0);
                double reach = Math.Max(32.0, Math.Abs(end.X - start.X) * 0.42);
                Point c1 = selfTransition ? new Point(start.X + 38.0, start.Y - 38.0) : new Point(start.X + (backwards ? -reach : reach), start.Y);
                Point c2 = selfTransition ? new Point(end.X - 38.0, end.Y - 38.0) : new Point(end.X + (backwards ? reach : -reach), end.Y);
                var figure = new PathFigure { StartPoint = start, IsClosed = false };
                figure.Segments.Add(new BezierSegment(c1, c2, end, true));
                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                bool selected = ReferenceEquals(_selectedGraphTransition, transition);
                var line = new System.Windows.Shapes.Path { Data = geometry, Stroke = selected ? KerfBrand.MintBrush : new SolidColorBrush(Color.FromRgb(120, 130, 145)), StrokeThickness = selected ? 2 : 1.35, Fill = Brushes.Transparent, Cursor = Cursors.Hand, Tag = transition, ToolTip = from.Name + " → " + to.Name + " · " + TransitionConditionSummary(transition) };
                line.MouseLeftButtonDown += (s, e) => { SelectGraphTransition(transition, layer); e.Handled = true; };
                line.MouseRightButtonUp += (s, e) => ShowTransitionContextMenu(line, layer, transition);
                Panel.SetZIndex(line, 0);
                _stateMachineGraphCanvas.Children.Add(line);
                Vector direction = end - c2;
                if (direction.Length < 0.001) direction = new Vector(1, 0);
                direction.Normalize();
                Vector perpendicular = new Vector(-direction.Y, direction.X);
                Point arrowBase = end - direction * 8.0;
                var arrow = new Polygon
                {
                    Points = new PointCollection { end, arrowBase + perpendicular * 4.0, arrowBase - perpendicular * 4.0 },
                    Fill = selected ? KerfBrand.MintBrush : new SolidColorBrush(Color.FromRgb(120, 130, 145)),
                    Tag = transition,
                    IsHitTestVisible = false
                };
                Panel.SetZIndex(arrow, 11);
                _stateMachineGraphCanvas.Children.Add(arrow);
                Point middle = new Point((start.X + 3 * c1.X + 3 * c2.X + end.X) / 8.0, (start.Y + 3 * c1.Y + 3 * c2.Y + end.Y) / 8.0);
                var label = new Border { Background = KerfBrand.FigmaCardBrush, BorderBrush = selected ? KerfBrand.MintBrush : KerfBrand.FigmaBorderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(5, 2, 5, 2), Tag = transition, Cursor = Cursors.Hand, Child = new TextBlock { Text = string.Format("{0:0.##}s", transition.Duration), FontSize = 8, Foreground = Brushes.White }, ToolTip = TransitionConditionSummary(transition) + " · " + transition.Interpolation };
                Canvas.SetLeft(label, middle.X - 17);
                Canvas.SetTop(label, middle.Y - 8);
                label.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount >= 2) ShowEditTransitionDialog(transition, layer);
                    else SelectGraphTransition(transition, layer);
                    e.Handled = true;
                };
                label.MouseRightButtonUp += (s, e) => ShowTransitionContextMenu(label, layer, transition);
                Panel.SetZIndex(label, 2);
                _stateMachineGraphCanvas.Children.Add(label);
            }
        }

        private string TransitionConditionSummary(StateTransition transition)
        {
            if (transition == null || transition.Conditions == null || transition.Conditions.Count == 0) return "always";
            var definition = _timelineController != null && _timelineController.ActiveStateMachine != null ? _timelineController.ActiveStateMachine.Definition : null;
            return string.Join(" AND ", transition.Conditions.Select(condition =>
            {
                if (condition == null) return "condition";
                if (!string.IsNullOrEmpty(condition.PropertyName)) return condition.PropertyName + " " + condition.Op + " " + (condition.TextValue ?? condition.Value.ToString("0.##"));
                if (definition == null || condition.InputIndex < 0 || condition.InputIndex >= definition.Inputs.Count) return "input?";
                return definition.Inputs[condition.InputIndex].Name + " " + condition.Op + " " + condition.Value.ToString("0.##");
            }).ToArray());
        }

        private void SelectGraphTransition(StateTransition transition, MachineLayer layer)
        {
            _selectedGraphTransition = transition;
            _selectedGraphTransitionLayer = layer;
            BuildStateMachineStatesGraph();
        }

        private void StartStateGraphNodeDrag(int stateIndex, Point point)
        {
            _stateGraphPointerMode = 1;
            _stateGraphPointerStateIndex = stateIndex;
            _stateGraphPointerOrigin = point;
            var state = _timelineController.ActiveStateMachine.Definition.Layers[0].States[stateIndex];
            _stateGraphNodeOrigin = new Point(state.GraphX, state.GraphY);
            _stateGraphMoved = false;
            _stateMachineGraphCanvas.CaptureMouse();
        }

        private void StartStateGraphConnection(int stateIndex, Point point)
        {
            _stateGraphPointerMode = 2;
            _stateGraphPointerStateIndex = stateIndex;
            _stateMachineGraphCanvas.CaptureMouse();
            DrawStateGraphConnectionPreview(point);
        }

        private void DrawStateGraphConnectionPreview(Point point)
        {
            if (_stateMachineGraphCanvas == null || _timelineController == null || _timelineController.ActiveStateMachine == null) return;
            var layer = _timelineController.ActiveStateMachine.Definition.Layers[0];
            foreach (var child in _stateMachineGraphCanvas.Children.OfType<FrameworkElement>().Where(element => string.Equals(element.Tag as string, "__connectionPreview", StringComparison.Ordinal)).ToList())
                _stateMachineGraphCanvas.Children.Remove(child);
            if (_stateGraphPointerStateIndex < 0 || _stateGraphPointerStateIndex >= layer.States.Count) return;
            var state = layer.States[_stateGraphPointerStateIndex];
            double width = state.Kind == StateKind.Entry ? 88 : state.Kind == StateKind.Any ? 116 : state.Kind == StateKind.Exit ? 78 : 132;
            double height = state.Kind == StateKind.Entry || state.Kind == StateKind.Exit ? 40 : 48;
            Point start = new Point(state.GraphX + width, state.GraphY + height / 2.0);
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new BezierSegment(new Point(start.X + 40, start.Y), new Point(point.X - 40, point.Y), point, true));
            var geometry = new PathGeometry(); geometry.Figures.Add(figure);
            var path = new System.Windows.Shapes.Path { Data = geometry, Stroke = KerfBrand.MintBrush, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 3, 2 }, IsHitTestVisible = false, Tag = "__connectionPreview" };
            Panel.SetZIndex(path, 20);
            _stateMachineGraphCanvas.Children.Add(path);
        }

        private void HandleStateGraphMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanningStateGraph && _stateMachineGraphScrollViewer != null)
            {
                Point current = e.GetPosition(_stateMachineGraphScrollViewer);
                Vector delta = current - _stateGraphPanStart;
                _stateMachineGraphScrollViewer.ScrollToHorizontalOffset(_stateGraphPanStartHorizontalOffset - delta.X);
                _stateMachineGraphScrollViewer.ScrollToVerticalOffset(_stateGraphPanStartVerticalOffset - delta.Y);
                return;
            }
            if (_stateGraphPointerMode == 0 || _stateMachineGraphCanvas == null || !_stateMachineGraphCanvas.IsMouseCaptured) return;
            Point point = e.GetPosition(_stateMachineGraphCanvas);
            if (_stateGraphPointerMode == 1)
            {
                Vector delta = point - _stateGraphPointerOrigin;
                if (delta.Length > 3.0) _stateGraphMoved = true;
                if (!_stateGraphMoved) return;
                var layer = _timelineController.ActiveStateMachine.Definition.Layers[0];
                var state = layer.States[_stateGraphPointerStateIndex];
                state.GraphX = Math.Max(4.0, _stateGraphNodeOrigin.X + delta.X);
                state.GraphY = Math.Max(4.0, Math.Min(174.0, _stateGraphNodeOrigin.Y + delta.Y));
                var node = _stateGraphNodes[_stateGraphPointerStateIndex];
                Canvas.SetLeft(node, state.GraphX);
                Canvas.SetTop(node, state.GraphY);
                RefreshStateGraphConnectionVisuals(layer);
            }
            else if (_stateGraphPointerMode == 2)
            {
                DrawStateGraphConnectionPreview(point);
            }
        }

        private void RefreshStateGraphConnectionVisuals(MachineLayer layer)
        {
            if (_stateMachineGraphCanvas == null) return;
            foreach (var child in _stateMachineGraphCanvas.Children.OfType<FrameworkElement>().Where(element => element.Tag is StateTransition).ToList())
                _stateMachineGraphCanvas.Children.Remove(child);
            DrawStateMachineConnections(layer);
            foreach (var node in _stateGraphNodes.Values) Panel.SetZIndex(node, 10);
        }

        private void HandleStateGraphMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_stateGraphPointerMode == 0 || _stateMachineGraphCanvas == null) return;
            int mode = _stateGraphPointerMode;
            int stateIndex = _stateGraphPointerStateIndex;
            Point point = e.GetPosition(_stateMachineGraphCanvas);
            if (_stateMachineGraphCanvas.IsMouseCaptured) _stateMachineGraphCanvas.ReleaseMouseCapture();
            _stateGraphPointerMode = 0;
            _stateGraphPointerStateIndex = -1;
            if (mode == 1)
            {
                if (_stateGraphMoved)
                {
                    if (History != null) History.MarkDirty();
                    RefreshStateGraphConnectionVisuals(_timelineController.ActiveStateMachine.Definition.Layers[0]);
                }
                else PreviewStateMachineNode(stateIndex);
            }
            else if (mode == 2)
            {
                foreach (var child in _stateMachineGraphCanvas.Children.OfType<FrameworkElement>().Where(element => string.Equals(element.Tag as string, "__connectionPreview", StringComparison.Ordinal)).ToList())
                    _stateMachineGraphCanvas.Children.Remove(child);
                int target = HitTestStateGraphNode(point);
                if (target >= 0 && target != stateIndex) CreateStateGraphTransition(stateIndex, target);
            }
            BuildStateMachineStatesGraph();
            e.Handled = true;
        }

        private int HitTestStateGraphNode(Point point)
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return -1;
            var states = _timelineController.ActiveStateMachine.Definition.Layers[0].States;
            for (int i = states.Count - 1; i >= 0; i--)
            {
                var state = states[i];
                if (state == null) continue;
                double width = state.Kind == StateKind.Entry ? 88 : state.Kind == StateKind.Any ? 116 : state.Kind == StateKind.Exit ? 78 : 132;
                double height = state.Kind == StateKind.Entry || state.Kind == StateKind.Exit ? 40 : 48;
                if (new Rect(state.GraphX, state.GraphY, width, height).Contains(point)) return i;
            }
            return -1;
        }

        private void PreviewStateMachineNode(int stateIndex)
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return;
            var sm = _timelineController.ActiveStateMachine;
            if (sm.Definition.Layers.Count == 0 || stateIndex < 0 || stateIndex >= sm.Definition.Layers[0].States.Count) return;
            var state = sm.Definition.Layers[0].States[stateIndex];
            _selectedGraphStateIndex = stateIndex;
            if (state.Kind == StateKind.Entry) sm.Reset();
            else if (state.Kind == StateKind.Animation || state.Kind == StateKind.Blend1D || state.Kind == StateKind.BlendDirect)
            {
                var runtime = sm.LayerRuntimes[0];
                runtime.CurrentState = stateIndex;
                runtime.CurTime = new ClipInstance(state.ClipIndex);
                runtime.ActiveTransition = null;
                runtime.IsFinished = false;
            }
            TriggerStateMachineStep();
        }

        private void OpenStateTimeline(MachineState state)
        {
            if (state == null || _timelineController == null || _timelineController.ActiveStateMachine == null || state.Kind != StateKind.Animation) return;
            var machine = _timelineController.ActiveStateMachine;
            if (state.ClipIndex < 0 || state.ClipIndex >= machine.Clips.Count || machine.Clips[state.ClipIndex] == null)
            {
                if (Document.Animation == null) Document.Animation = new AnimationProjectData();
                if (Document.Animation.Clips == null) Document.Animation.Clips = new List<Clip>();
                uint id = Document.Animation.Clips.Count == 0 ? 100u : Document.Animation.Clips.Max(clipItem => clipItem == null ? 0u : clipItem.Id) + 1u;
                var newClip = new Clip(id, state.Name, Math.Max(0.1, _timelineController.Duration), _timelineController.Fps, LoopMode.Loop);
                if (!machine.Clips.Contains(newClip)) machine.Clips.Add(newClip);
                if (!Document.Animation.Clips.Contains(newClip)) Document.Animation.Clips.Add(newClip);
                state.ClipIndex = machine.Clips.Count - 1;
                History.MarkDirty();
                BuildStateMachineStatesGraph();
            }
            var clip = machine.Clips[state.ClipIndex];
            if (clip != null) SelectAnimationClip(clip);
        }

        private void CreateStateGraphTransition(int fromIndex, int toIndex)
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return;
            var sm = _timelineController.ActiveStateMachine;
            var layer = sm.Definition.Layers[0];
            if (layer.States[toIndex].Kind == StateKind.Entry || layer.States[toIndex].Kind == StateKind.Any) return;
            var existing = layer.Transitions.FirstOrDefault(candidateTransition => candidateTransition != null && candidateTransition.FromState == fromIndex && candidateTransition.ToState == toIndex);
            if (existing != null) { SelectGraphTransition(existing, layer); ShowEditTransitionDialog(existing, layer); return; }
            var transition = new StateTransition(fromIndex, toIndex, 0.25);
            layer.AddTransition(transition);
            _selectedGraphTransition = transition;
            _selectedGraphTransitionLayer = layer;
            History.MarkDirty();
            BuildStateMachineStatesGraph();
            ShowEditTransitionDialog(transition, layer);
        }

        private void ShowTransitionContextMenu(FrameworkElement target, MachineLayer layer, StateTransition transition)
        {
            var menu = new ContextMenu();
            var edit = new MenuItem { Header = "Edit transition…" };
            edit.Click += (s, e) => ShowEditTransitionDialog(transition, layer);
            menu.Items.Add(edit);
            var reverse = new MenuItem { Header = "Add reverse transition" };
            reverse.Click += (s, e) => AddReverseTransition(layer, transition);
            menu.Items.Add(reverse);
            var remove = new MenuItem { Header = "Delete transition" };
            remove.Click += (s, e) =>
            {
                layer.Transitions.Remove(transition);
                if (ReferenceEquals(_selectedGraphTransition, transition)) { _selectedGraphTransition = null; _selectedGraphTransitionLayer = null; }
                History.MarkDirty();
                BuildStateMachineStatesGraph();
            };
            menu.Items.Add(remove);
            menu.PlacementTarget = target;
            menu.IsOpen = true;
        }

        private void AddReverseTransition(MachineLayer layer, StateTransition transition)
        {
            if (layer == null || transition == null) return;
            if (transition.Conditions == null || transition.Conditions.Count != 1)
            {
                MessageBox.Show("A reverse link needs one condition that can be inverted. Add a single input condition, then create the reverse link.", "State Machine", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var reverse = new StateTransition(transition.ToState, transition.FromState, transition.Duration)
            {
                Interpolation = transition.Interpolation, X1 = transition.X1, Y1 = transition.Y1, X2 = transition.X2, Y2 = transition.Y2,
                Interrupt = transition.Interrupt, ExitTime = null
            };
            if (transition.Conditions != null)
            {
                foreach (var condition in transition.Conditions)
                {
                    if (condition == null) continue;
                    reverse.Conditions.Add(new TransitionCondition(condition.InputIndex, InvertCondition(condition.Op), condition.Value)
                    {
                        PropertyName = condition.PropertyName, TextValue = condition.TextValue
                    });
                }
            }
            layer.AddTransition(reverse);
            _selectedGraphTransition = reverse;
            _selectedGraphTransitionLayer = layer;
            History.MarkDirty();
            BuildStateMachineStatesGraph();
        }

        private static ConditionOp InvertCondition(ConditionOp operation)
        {
            switch (operation)
            {
                case ConditionOp.Eq: return ConditionOp.Ne;
                case ConditionOp.Ne: return ConditionOp.Eq;
                case ConditionOp.Lt: return ConditionOp.Ge;
                case ConditionOp.Le: return ConditionOp.Gt;
                case ConditionOp.Gt: return ConditionOp.Le;
                case ConditionOp.Ge: return ConditionOp.Lt;
                default: return operation;
            }
        }

        private void ShowStateNodeContextMenu(FrameworkElement target, MachineLayer layer, MachineState state, int stateIndex)
        {
            var menu = new ContextMenu();
            if (state.Kind == StateKind.Animation)
            {
                var open = new MenuItem { Header = "Open timeline" };
                open.Click += (s, e) => OpenStateTimeline(state);
                menu.Items.Add(open);
                var rename = new MenuItem { Header = "Rename state" };
                rename.Click += (s, e) => ShowRenameStateDialog(layer, state);
                menu.Items.Add(rename);
                var remove = new MenuItem { Header = "Delete state" };
                remove.Click += (s, e) =>
                {
                    var machine = _timelineController.ActiveStateMachine;
                    int removedClipIndex = state.ClipIndex;
                    Clip removedClip = removedClipIndex >= 0 && removedClipIndex < machine.Clips.Count ? machine.Clips[removedClipIndex] : null;
                    layer.Transitions.RemoveAll(transition => transition == null || transition.FromState == stateIndex || transition.ToState == stateIndex);
                    layer.States.RemoveAt(stateIndex);
                    for (int i = 0; i < layer.States.Count; i++) layer.States[i].Id = i;
                    foreach (var transition in layer.Transitions)
                    {
                        if (transition.FromState > stateIndex) transition.FromState--;
                        if (transition.ToState > stateIndex) transition.ToState--;
                    }
                    if (removedClip != null)
                    {
                        machine.Clips.RemoveAt(removedClipIndex);
                        if (Document != null && Document.Animation != null && Document.Animation.Clips != null && !ReferenceEquals(Document.Animation.Clips, machine.Clips))
                            Document.Animation.Clips.Remove(removedClip);
                        foreach (var remaining in layer.States)
                            if (remaining != null && remaining.ClipIndex > removedClipIndex) remaining.ClipIndex--;
                    }
                    _selectedGraphTransition = null;
                    _selectedGraphTransitionLayer = null;
                    _selectedGraphStateIndex = -1;
                    _timelineController.ActiveStateMachine = new StateMachineRuntime(machine.Definition, machine.Clips);
                    History.MarkDirty();
                    BuildStateMachineStatesGraph();
                };
                menu.Items.Add(remove);
            }
            menu.PlacementTarget = target;
            menu.IsOpen = true;
        }

        private void ShowRenameStateDialog(MachineLayer layer, MachineState state)
        {
            if (state == null) return;
            var dialog = new Window { Title = "Rename State", Width = 300, Height = 135, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = KerfBrand.FigmaPanelBrush, Foreground = Brushes.White };
            var owner = Window.GetWindow(this); if (owner != null) dialog.Owner = owner;
            var stack = new StackPanel { Margin = new Thickness(14) };
            var input = new TextBox { Text = state.Name, Height = 27, Padding = new Thickness(6, 4, 6, 4), Background = KerfBrand.FigmaCanvasBrush, Foreground = Brushes.White, BorderBrush = KerfBrand.FigmaBorderBrush };
            stack.Children.Add(input);
            var save = KerfBrand.CreatePrimaryButton("Save", () => { state.Name = input.Text.Trim(); if (History != null) History.MarkDirty(); BuildStateMachineStatesGraph(); dialog.Close(); }, 6);
            save.Height = 26;
            save.Padding = new Thickness(16, 0, 16, 0);
            save.Margin = new Thickness(0, 10, 0, 0);
            save.HorizontalAlignment = HorizontalAlignment.Right;
            stack.Children.Add(save);
            dialog.Content = stack; dialog.ShowDialog();
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

            int transitionTarget = lr.ActiveTransition != null ? lr.ActiveTransition.ToState : -1;
            if (!ReferenceEquals(_lastGraphStateMachine, sm) ||
                _lastGraphCurrentState != lr.CurrentState ||
                _lastGraphTransitionTarget != transitionTarget)
                BuildStateMachineStatesGraph();
        }

        private void TriggerStateMachineStep(bool continuePlayback = false)
        {
            if (_timelineController != null)
            {
                _timelineController.UseStateMachine = true;
                // Input and pointer changes update the paused preview. Playback remains explicit.
                _timelineController.Step(0.0);
                UpdateStateMachineStatusUI();
                RefreshTimelineCanvas();
            }
        }

        private void SetStateMachineBool(string inputName, bool value)
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return;
            if (_timelineController.ActiveStateMachine.SetViewModelBool(inputName, value)) return;
            StateMachineDefinition def = _timelineController.ActiveStateMachine.Definition;
            for (int i = 0; def != null && i < def.Inputs.Count; i++)
            {
                if (def.Inputs[i].Kind == InputKind.Bool && string.Equals(def.Inputs[i].Name, inputName, StringComparison.OrdinalIgnoreCase))
                {
                    _timelineController.ActiveStateMachine.SetBool(i, value);
                    return;
                }
            }
        }

        private void SetStateMachineTrigger(string inputName)
        {
            if (_timelineController == null || _timelineController.ActiveStateMachine == null) return;
            if (_timelineController.ActiveStateMachine.FireViewModelTrigger(inputName)) return;
            StateMachineDefinition def = _timelineController.ActiveStateMachine.Definition;
            for (int i = 0; def != null && i < def.Inputs.Count; i++)
            {
                if (def.Inputs[i].Kind == InputKind.Trigger && string.Equals(def.Inputs[i].Name, inputName, StringComparison.OrdinalIgnoreCase))
                {
                    _timelineController.ActiveStateMachine.FireTrigger(i);
                    return;
                }
            }
        }

        private void SwitchAnimationSubMode(bool stateMachineMode)
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            _isStateMachineMode = stateMachineMode;
            if (_timelineController != null) _timelineController.UseStateMachine = stateMachineMode;
            if (stateMachineMode)
            {
                if (_timelineTracksContainer != null) _timelineTracksContainer.Visibility = Visibility.Collapsed;
                if (_stateMachineScrollContainer != null) _stateMachineScrollContainer.Visibility = Visibility.Visible;
                BuildStateMachineInputsControls();
                BuildStateMachineListeners();
                BuildStateMachineStatesGraph();
            }
            else
            {
                if (_stateMachineScrollContainer != null) _stateMachineScrollContainer.Visibility = Visibility.Collapsed;
                if (_timelineTracksContainer != null) _timelineTracksContainer.Visibility = Visibility.Visible;
                RefreshTimelineUI();
            }
            UpdateTimelineModeTabs();
            if (_timelineController != null) _timelineController.Seek(_timelineController.CurrentTime);
        }

        private void UpdateTimelineModeTabs()
        {
            if (_tabStateMachineBtn != null)
            {
                _tabStateMachineBtn.Background = _isStateMachineMode ? KerfBrand.MintBrush : Brushes.Transparent;
                var tb = _tabStateMachineBtn.Child as TextBlock;
                if (tb != null) tb.Foreground = _isStateMachineMode ? KerfBrand.TextBlackBrush : KerfBrand.TextMutedBrush;
            }
            if (_tabTimelineClipsBtn != null)
            {
                _tabTimelineClipsBtn.Background = !_isStateMachineMode ? KerfBrand.MintBrush : Brushes.Transparent;
                var tb = _tabTimelineClipsBtn.Child as TextBlock;
                if (tb != null) tb.Foreground = !_isStateMachineMode ? KerfBrand.TextBlackBrush : KerfBrand.TextMutedBrush;
            }
        }

        private void ToggleTimelineMode()
        {
            SwitchAnimationSubMode(!_isStateMachineMode);
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
            if (_timelineController.ActiveClip != null)
            {
                _timelineController.ActiveClip.LoopMode = _timelineController.LoopMode;
                History.MarkDirty();
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

        private void BeginTimelineKeyframeDrag(AnimTrack track, Keyframe keyframe, MouseButtonEventArgs e)
        {
            if (_timelineCanvasHost == null || track == null || keyframe == null) return;
            _timelineCanvasHost.Focus();
            bool toggle = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool additive = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            if (toggle && _selectedTimelineKeyframes.Contains(keyframe))
            {
                _selectedTimelineKeyframes.Remove(keyframe);
                if (ReferenceEquals(_editingInterpolationKeyframe, keyframe))
                {
                    _editingInterpolationKeyframe = null;
                    _editingInterpolationTrack = null;
                }
                RefreshTimelineCanvas();
                e.Handled = true;
                return;
            }
            if (!additive && !toggle && !_selectedTimelineKeyframes.Contains(keyframe))
                _selectedTimelineKeyframes.Clear();
            _selectedTimelineKeyframes.Add(keyframe);
            _timelineKeyframeOwners[keyframe] = track;
            _editingInterpolationTrack = track;
            _editingInterpolationKeyframe = keyframe;
            _timelineKeyframeDragStart = e.GetPosition(_timelineCanvasHost);
            _timelineKeyframeDragStartTimes = new Dictionary<Keyframe, double>();
            foreach (var selected in _selectedTimelineKeyframes)
            {
                if (_timelineKeyframeOwners.ContainsKey(selected))
                    _timelineKeyframeDragStartTimes[selected] = selected.Time;
            }
            _timelineKeyframeDragMoved = false;
            _isDraggingTimelineKeyframes = true;
            _timelineCanvasHost.CaptureMouse();
            RefreshInterpolationGraphPanels();
            RefreshTimelineCanvas();
            e.Handled = true;
        }

        private void HandleTimelineCanvasPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var hitTarget = e.OriginalSource as FrameworkElement;
            var keyframe = hitTarget != null ? hitTarget.Tag as Keyframe : null;
            AnimTrack track;
            if (keyframe != null && _timelineKeyframeOwners.TryGetValue(keyframe, out track))
            {
                BeginTimelineKeyframeDrag(track, keyframe, e);
                e.Handled = true;
                return;
            }

            HandleTimelineCanvasMouseDown(sender, e);
        }

        private void HandleTimelineCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _timelineCanvasHost == null) return;
            Point point = e.GetPosition(_timelineCanvasHost);
            _timelineCanvasHost.Focus();
            _timelinePointerDown = true;
            _timelineCandidateMarquee = point.Y > 22.0;
            _timelineMarqueeAdditive = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            _timelineMarqueeStartPoint = point;
            _timelineMarqueeInitialSelection = new HashSet<Keyframe>(_selectedTimelineKeyframes);
            _timelineKeyframeDragMoved = false;
            _isScrubbingTimeline = point.Y <= 22.0;
            _timelineCanvasHost.CaptureMouse();
            if (_isScrubbingTimeline) ScrubTimelineAtPoint(point);
        }

        private void HandleTimelineCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (_timelineCanvasHost == null || !_timelineCanvasHost.IsMouseCaptured) return;
            Point point = e.GetPosition(_timelineCanvasHost);
            if (_isDraggingTimelineKeyframes)
            {
                Vector drag = point - _timelineKeyframeDragStart;
                if (!_timelineKeyframeDragMoved && drag.Length < 3.0) return;
                _timelineKeyframeDragMoved = true;
                double fps = _timelineController != null ? Math.Max(1.0, _timelineController.Fps) : 60.0;
                double rawDelta = drag.X / Math.Max(1.0, _timelinePixelsPerSecond);
                double delta = (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                    ? rawDelta
                    : Math.Round(rawDelta * fps) / fps;
                double duration = _timelineController != null ? _timelineController.Duration : 10.0;
                double minStart = _timelineKeyframeDragStartTimes.Count == 0 ? 0.0 : _timelineKeyframeDragStartTimes.Values.Min();
                double maxStart = _timelineKeyframeDragStartTimes.Count == 0 ? duration : _timelineKeyframeDragStartTimes.Values.Max();
                delta = Math.Max(-minStart, Math.Min(duration - maxStart, delta));
                foreach (var entry in _timelineKeyframeDragStartTimes)
                    entry.Key.Time = Math.Max(0.0, Math.Min(duration, entry.Value + delta));
                SortSelectedKeyframeTracks();
                if (_timelineController != null) _timelineController.Seek(_timelineController.CurrentTime);
                RefreshTimelineCanvas();
                return;
            }
            if (_timelineCandidateMarquee && _timelinePointerDown)
            {
                Vector drag = point - _timelineMarqueeStartPoint;
                if (!_isTimelineMarqueeSelecting && drag.Length >= 4.0)
                {
                    _isTimelineMarqueeSelecting = true;
                    _isScrubbingTimeline = false;
                    if (!_timelineMarqueeAdditive) _selectedTimelineKeyframes.Clear();
                }
                if (_isTimelineMarqueeSelecting)
                {
                    SelectTimelineKeyframesInMarquee(point);
                    UpdateTimelineKeyframeDiamondStyles();
                    UpdateTimelineMarqueeVisual(point);
                    RefreshInterpolationGraphPanels();
                    return;
                }
            }
            if (_isScrubbingTimeline) ScrubTimelineAtPoint(point);
        }

        private void HandleTimelineCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _timelineCanvasHost == null) return;
            Point point = e.GetPosition(_timelineCanvasHost);
            if (_isDraggingTimelineKeyframes)
            {
                _isDraggingTimelineKeyframes = false;
                if (_timelineCanvasHost.IsMouseCaptured) _timelineCanvasHost.ReleaseMouseCapture();
                if (_timelineKeyframeDragMoved)
                {
                    var before = new Dictionary<Keyframe, double>(_timelineKeyframeDragStartTimes);
                    var after = before.Keys.ToDictionary(keyframe => keyframe, keyframe => keyframe.Time);
                    ExecuteKeyframeTimeChange(before, after);
                }
                else
                {
                    RefreshTimelineCanvas();
                }
                _timelineKeyframeDragStartTimes.Clear();
                _timelineKeyframeDragMoved = false;
                e.Handled = true;
                return;
            }
            if (_isTimelineMarqueeSelecting)
            {
                _isTimelineMarqueeSelecting = false;
                _timelineCandidateMarquee = false;
                _timelinePointerDown = false;
                RemoveTimelineMarqueeVisual();
                if (_timelineCanvasHost.IsMouseCaptured) _timelineCanvasHost.ReleaseMouseCapture();
                RefreshTimelineCanvas();
                e.Handled = true;
                return;
            }
            if (_timelineCandidateMarquee && _timelinePointerDown)
            {
                if (!_timelineMarqueeAdditive) _selectedTimelineKeyframes.Clear();
                _isScrubbingTimeline = true;
                ScrubTimelineAtPoint(point);
            }
            _timelinePointerDown = false;
            _timelineCandidateMarquee = false;
            _isScrubbingTimeline = false;
            if (_timelineCanvasHost.IsMouseCaptured) _timelineCanvasHost.ReleaseMouseCapture();
            RefreshTimelineCanvas();
        }

        private void SelectTimelineKeyframesInMarquee(Point currentPoint)
        {
            if (_timelineController == null || _timelineController.ActiveClip == null) return;
            var selectionRect = CreateTimelineSelectionRect(_timelineMarqueeStartPoint, currentPoint);
            _selectedTimelineKeyframes.Clear();
            if (_timelineMarqueeAdditive)
                foreach (var keyframe in _timelineMarqueeInitialSelection) _selectedTimelineKeyframes.Add(keyframe);

            var lanes = BuildTimelineLanes();
            foreach (var lane in lanes)
            {
                if (lane.IsMaster || lane.Track == null) continue;
                double centerY = lane.Y + 11.0;
                foreach (var keyframe in lane.Track.Keys ?? new List<Keyframe>())
                {
                    var keyRect = new Rect(keyframe.Time * _timelinePixelsPerSecond - 6.0, centerY - 6.0, 12.0, 12.0);
                    if (selectionRect.IntersectsWith(keyRect))
                    {
                        _selectedTimelineKeyframes.Add(keyframe);
                        _timelineKeyframeOwners[keyframe] = lane.Track;
                    }
                }
            }
            var first = _selectedTimelineKeyframes.FirstOrDefault();
            if (first != null && _timelineKeyframeOwners.ContainsKey(first))
            {
                _editingInterpolationKeyframe = first;
                _editingInterpolationTrack = _timelineKeyframeOwners[first];
            }
        }

        private void UpdateTimelineKeyframeDiamondStyles()
        {
            if (_timelineCanvasHost == null) return;
            foreach (var diamond in _timelineCanvasHost.Children.OfType<Polygon>())
            {
                var keyframe = diamond.Tag as Keyframe;
                if (keyframe == null) continue;
                diamond.Fill = _selectedTimelineKeyframes.Contains(keyframe)
                    ? new SolidColorBrush(Color.FromRgb(56, 189, 248))
                    : ReferenceEquals(keyframe, _editingInterpolationKeyframe)
                        ? new SolidColorBrush(Color.FromRgb(90, 170, 220))
                        : new SolidColorBrush(Color.FromRgb(251, 191, 36));
            }
        }

        private void UpdateTimelineMarqueeVisual(Point currentPoint)
        {
            RemoveTimelineMarqueeVisual();
            var rect = CreateTimelineSelectionRect(_timelineMarqueeStartPoint, currentPoint);
            _timelineMarqueeVisual = new Rectangle
            {
                Width = Math.Max(1.0, rect.Width),
                Height = Math.Max(1.0, rect.Height),
                Fill = new SolidColorBrush(Color.FromArgb(36, 0x46, 0xEF, 0xB1)),
                Stroke = KerfBrand.MintBrush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_timelineMarqueeVisual, rect.Left);
            Canvas.SetTop(_timelineMarqueeVisual, rect.Top);
            _timelineCanvasHost.Children.Add(_timelineMarqueeVisual);
        }

        private static Rect CreateTimelineSelectionRect(Point start, Point end)
        {
            return new Rect(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Abs(end.X - start.X),
                Math.Abs(end.Y - start.Y));
        }

        private void RemoveTimelineMarqueeVisual()
        {
            if (_timelineMarqueeVisual != null && _timelineCanvasHost != null)
                _timelineCanvasHost.Children.Remove(_timelineMarqueeVisual);
            _timelineMarqueeVisual = null;
        }

        private void SortSelectedKeyframeTracks()
        {
            foreach (var track in _selectedTimelineKeyframes
                .Where(keyframe => _timelineKeyframeOwners.ContainsKey(keyframe))
                .Select(keyframe => _timelineKeyframeOwners[keyframe]).Distinct())
                track.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        private void ExecuteKeyframeTimeChange(Dictionary<Keyframe, double> before, Dictionary<Keyframe, double> after)
        {
            if (before == null || after == null || before.Count == 0) return;
            bool changed = before.Any(entry => after.ContainsKey(entry.Key) && Math.Abs(entry.Value - after[entry.Key]) > 0.000001);
            if (!changed) return;
            var owners = new Dictionary<Keyframe, AnimTrack>();
            foreach (var keyframe in before.Keys)
                if (_timelineKeyframeOwners.ContainsKey(keyframe)) owners[keyframe] = _timelineKeyframeOwners[keyframe];
            History.Execute(new MoveKeyframesCommand(before, after, owners, () =>
            {
                foreach (var track in owners.Values.Distinct()) track.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
                if (_timelineController != null) _timelineController.Seek(_timelineController.CurrentTime);
                RefreshTimelineCanvas();
                RefreshTimelineTracks();
                RefreshInterpolationGraphPanels();
            }));
        }

        private void OffsetSelectedTimelineKeyframes(int frames)
        {
            if (_selectedTimelineKeyframes.Count == 0 || _timelineController == null) return;
            double frameDuration = 1.0 / Math.Max(1.0, _timelineController.Fps);
            double delta = frames * frameDuration;
            double duration = _timelineController.Duration;
            double minTime = _selectedTimelineKeyframes.Min(keyframe => keyframe.Time);
            double maxTime = _selectedTimelineKeyframes.Max(keyframe => keyframe.Time);
            delta = Math.Max(-minTime, Math.Min(duration - maxTime, delta));
            var before = _selectedTimelineKeyframes.ToDictionary(keyframe => keyframe, keyframe => keyframe.Time);
            var after = before.ToDictionary(entry => entry.Key, entry => Math.Max(0.0, Math.Min(duration, entry.Value + delta)));
            ExecuteKeyframeTimeChange(before, after);
        }

        private void StaggerSelectedTimelineKeyframes(bool reverse)
        {
            if (_selectedTimelineKeyframes.Count < 2 || _timelineController == null) return;
            double frameDuration = 1.0 / Math.Max(1.0, _timelineController.Fps);
            double duration = _timelineController.Duration;
            var ordered = _selectedTimelineKeyframes
                .Where(keyframe => _timelineKeyframeOwners.ContainsKey(keyframe))
                .OrderBy(keyframe => GetPropRowIndex(_timelineKeyframeOwners[keyframe].Prop))
                .ThenBy(keyframe => keyframe.Time)
                .ToList();
            if (ordered.Count < 2) return;
            double firstTime = ordered.Min(keyframe => keyframe.Time);
            var before = ordered.ToDictionary(keyframe => keyframe, keyframe => keyframe.Time);
            var after = new Dictionary<Keyframe, double>();
            for (int i = 0; i < ordered.Count; i++)
            {
                int offsetIndex = reverse ? ordered.Count - 1 - i : i;
                after[ordered[i]] = Math.Min(duration, firstTime + offsetIndex * frameDuration);
            }
            ExecuteKeyframeTimeChange(before, after);
        }

        private void SelectAllTimelineKeyframes()
        {
            _selectedTimelineKeyframes.Clear();
            foreach (var keyframe in _timelineKeyframeOwners.Keys) _selectedTimelineKeyframes.Add(keyframe);
            var first = _selectedTimelineKeyframes.FirstOrDefault();
            if (first != null && _timelineKeyframeOwners.ContainsKey(first))
            {
                _editingInterpolationKeyframe = first;
                _editingInterpolationTrack = _timelineKeyframeOwners[first];
            }
            UpdateTimelineKeyframeDiamondStyles();
            RefreshInterpolationGraphPanels();
        }

        private void DeleteSelectedTimelineKeyframes()
        {
            if (_selectedTimelineKeyframes.Count == 0) return;
            var before = new Dictionary<Keyframe, AnimTrack>();
            foreach (var keyframe in _selectedTimelineKeyframes)
                if (_timelineKeyframeOwners.ContainsKey(keyframe)) before[keyframe] = _timelineKeyframeOwners[keyframe];
            if (before.Count == 0) return;
            _selectedTimelineKeyframes.Clear();
            if (_editingInterpolationKeyframe != null && before.ContainsKey(_editingInterpolationKeyframe))
            {
                _editingInterpolationKeyframe = null;
                _editingInterpolationTrack = null;
            }
            History.Execute(new DeleteKeyframesCommand(before, () =>
            {
                if (_timelineController != null) _timelineController.Seek(_timelineController.CurrentTime);
                RefreshTimelineCanvas();
                RefreshTimelineTracks();
                RefreshInterpolationGraphPanels();
            }));
        }

        private void ShowTimelineKeyframeContextMenu(AnimTrack track, Keyframe keyframe, FrameworkElement target)
        {
            if (!_selectedTimelineKeyframes.Contains(keyframe))
            {
                _selectedTimelineKeyframes.Clear();
                _selectedTimelineKeyframes.Add(keyframe);
            }
            _editingInterpolationTrack = track;
            _editingInterpolationKeyframe = keyframe;
            RefreshInterpolationGraphPanels();
            UpdateTimelineKeyframeDiamondStyles();
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Offset −1 frame" });
            ((MenuItem)menu.Items[0]).Click += (s, e) => OffsetSelectedTimelineKeyframes(-1);
            menu.Items.Add(new MenuItem { Header = "Offset +1 frame" });
            ((MenuItem)menu.Items[1]).Click += (s, e) => OffsetSelectedTimelineKeyframes(1);
            menu.Items.Add(new MenuItem { Header = "Stagger selected keys" });
            ((MenuItem)menu.Items[2]).Click += (s, e) => StaggerSelectedTimelineKeyframes(false);
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Select all visible keys" });
            ((MenuItem)menu.Items[4]).Click += (s, e) => SelectAllTimelineKeyframes();
            menu.Items.Add(new MenuItem { Header = "Delete selected keys" });
            ((MenuItem)menu.Items[5]).Click += (s, e) => DeleteSelectedTimelineKeyframes();
            menu.PlacementTarget = target;
            menu.IsOpen = true;
        }

        private void ScrubTimelineAtPoint(Point pt)
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            double rawTime = Math.Max(0.0, Math.Min(_timelineController.Duration, pt.X / _timelinePixelsPerSecond));
            double time = _timelineController.SnapToFrames ? _timelineController.SnapTime(rawTime) : rawTime;
            _timelineController.Seek(time);
            UpdateTimelineTimeReadout();
            UpdateTimelineScrubberPosition();
            RefreshTimelineCanvas();
        }

        private void UpdateTimelineTimeReadout()
        {
            if (_timelineController == null || _timelineTimeText == null) return;
            int currentFrame = (int)Math.Round(_timelineController.CurrentTime * _timelineController.Fps);
            int totalFrames = (int)Math.Round(_timelineController.Duration * _timelineController.Fps);
            _timelineTimeText.Text = string.Format("{0}f ({1:F2}s) / {2}f ({3:F2}s)", currentFrame, _timelineController.CurrentTime, totalFrames, _timelineController.Duration);
        }

        private void UpdateTimelineScrubberPosition()
        {
            if (_timelineScrubber == null || _timelineController == null) return;
            _timelineScrubber.Value = _timelineController.CurrentTime;
        }

        private void ShowAnimationClipMenu(FrameworkElement target)
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            if (Document == null || Document.Animation == null || Document.Animation.Clips == null) return;
            var menu = new ContextMenu();
            foreach (var clip in Document.Animation.Clips.Where(c => c != null))
            {
                var item = new MenuItem { Header = clip.Name, IsCheckable = true, IsChecked = clip == _timelineController.ActiveClip };
                item.Click += (s, e) => SelectAnimationClip(clip);
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            var clearAllItem = new MenuItem { Header = "Clear All Tracks in Current Clip" };
            clearAllItem.Click += (s, e) =>
            {
                if (_timelineController != null && _timelineController.ActiveClip != null)
                {
                    _timelineController.ActiveClip.Tracks.Clear();
                    History.MarkDirty();
                    RefreshTimelineUI();
                }
            };
            menu.Items.Add(clearAllItem);

            menu.PlacementTarget = target;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void SelectAnimationClip(Clip clip)
        {
            if (clip == null || _timelineController == null) return;
            PauseAnimation();
            _timelineController.ActiveClip = clip;
            _timelineController.LoopMode = clip.LoopMode;
            _timelineController.UseStateMachine = false;
            _isStateMachineMode = false;
            if (_timelineController.ActiveStateMachine != null && Document.Animation != null && Document.Animation.StateMachine != null)
                _timelineController.ActiveStateMachine = new StateMachineRuntime(Document.Animation.StateMachine, Document.Animation.Clips);
            if (Document.Animation != null) Document.Animation.ActiveClipId = clip.Id;
            if (_timelineScrubber != null)
            {
                _timelineScrubber.Maximum = _timelineController.Duration;
                _timelineScrubber.Value = 0.0;
            }
            string stateName = GetActiveStateNameForClip(clip);
            if (_timelineClipText != null)
            {
                _timelineClipText.Text = !string.IsNullOrEmpty(stateName)
                    ? "State: [" + stateName + "] · Clip: " + clip.Name
                    : "Clip: " + clip.Name;
            }
            if (_timelineLoopText != null) _timelineLoopText.Text = clip.LoopMode == LoopMode.Loop ? "🔁 Loop: All" : (clip.LoopMode == LoopMode.PingPong ? "🔁 Loop: PingPong" : "🔁 Loop: Once");
            if (_stateMachineScrollContainer != null) _stateMachineScrollContainer.Visibility = Visibility.Collapsed;
            if (_timelineTracksContainer != null) _timelineTracksContainer.Visibility = Visibility.Visible;
            _timelineController.Seek(0.0);
            History.MarkDirty();
            RefreshTimelineUI();
        }

        private void AddAnimationClip()
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            if (Document.Animation == null) Document.Animation = new AnimationProjectData();
            if (Document.Animation.Clips == null) Document.Animation.Clips = new List<Clip>();
            uint id = Document.Animation.Clips.Count == 0 ? 100 : Document.Animation.Clips.Max(c => c == null ? 0u : c.Id) + 1u;
            var clip = new Clip(id, "Animation " + (Document.Animation.Clips.Count + 1), Math.Max(0.1, _timelineController.Duration), _timelineController.Fps, _timelineController.LoopMode);
            Document.Animation.Clips.Add(clip);
            SelectAnimationClip(clip);
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
                if (_timelineController.UseStateMachine)
                    UpdateStateMachineStatusUI();
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
                            DateTime now = DateTime.UtcNow;
                            double dt = _lastAnimationTickUtc == DateTime.MinValue
                                ? 1.0 / Math.Max(1.0, _timelineController.Fps)
                                : Math.Max(0.0, Math.Min(0.1, (now - _lastAnimationTickUtc).TotalSeconds));
                            _lastAnimationTickUtc = now;
                            _timelineController.Step(dt);
                            if (_timelineController.UseStateMachine)
                                UpdateStateMachineStatusUI();
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
                _lastAnimationTickUtc = DateTime.UtcNow;
                _animationTimer.Start();
            }
        }

        private void CaptureAnimationRestPose()
        {
            if (Document == null) return;
            _animationRestPose.Clear();
            _animationNodesById.Clear();
            var page = Document.GetActivePage();
            if (page == null) return;
            foreach (var layer in page.GetAllLayersFlat())
            {
                CaptureAnimationRestPoseForLayer(layer);
            }
        }

        private void CaptureAnimationRestPoseForLayer(LayerNode layer)
        {
            if (layer == null) return;
            uint id = PropertyBinder.GetNumericNodeId(layer.Id);
            _animationRestPose[layer] = new AnimationRestPose
            {
                X = layer.X,
                Y = layer.Y,
                Width = layer.Width,
                Height = layer.Height,
                Rotation = layer.Rotation,
                Opacity = layer.Opacity,
                CornerRadius = layer.CornerRadius,
                CornerRadiusTopLeft = layer.CornerRadiusTopLeft,
                CornerRadiusTopRight = layer.CornerRadiusTopRight,
                CornerRadiusBottomRight = layer.CornerRadiusBottomRight,
                CornerRadiusBottomLeft = layer.CornerRadiusBottomLeft,
                Geometry = layer.Geometry != null ? layer.Geometry.Clone() : null
            };
            _animationNodesById[id] = layer;
        }

        private void RestoreAnimationRestPose()
        {
            foreach (var pair in _animationRestPose)
            {
                LayerNode layer = pair.Key;
                AnimationRestPose pose = pair.Value;
                if (layer == null || pose == null) continue;
                layer.X = pose.X;
                layer.Y = pose.Y;
                layer.Width = pose.Width;
                layer.Height = pose.Height;
                layer.Rotation = pose.Rotation;
                layer.Opacity = pose.Opacity;
                layer.CornerRadius = pose.CornerRadius;
                layer.CornerRadiusTopLeft = pose.CornerRadiusTopLeft;
                layer.CornerRadiusTopRight = pose.CornerRadiusTopRight;
                layer.CornerRadiusBottomRight = pose.CornerRadiusBottomRight;
                layer.CornerRadiusBottomLeft = pose.CornerRadiusBottomLeft;
                if (pose.Geometry != null) layer.Geometry = pose.Geometry.Clone();
                if (layer.RenderTransform != null) layer.RenderTransform.Reset();
            }
        }

        private void ApplyAnimationPose(Kerf.Model.Page page, Dictionary<uint, Dictionary<ushort, double>> pose)
        {
            if (page == null) return;
            var layers = page.GetAllLayersFlat().ToList();
            foreach (var layer in layers)
            {
                uint id = PropertyBinder.GetNumericNodeId(layer.Id);
                _animationNodesById[id] = layer;
                if (layer.RenderTransform == null) layer.RenderTransform = new RenderTransform();
                layer.RenderTransform.Reset();
            }

            if (pose != null)
            {
                foreach (var item in pose)
                {
                    LayerNode layer;
                    if (_animationNodesById.TryGetValue(item.Key, out layer))
                        PropertyBinder.ApplyPoseToLayer(layer, item.Key, pose);
                }
            }
        }

        private void ApplyAnimatedParentTransformToChildren(LayerNode parent)
        {
            // Non-destructive transform: Direct2D and WPF composition automatically apply the parent's
            // RenderTransform to all visual children without mutating layout geometry or vector points.
        }

        private static bool IsLegacyPresetDemoTrack(AnimTrack track)
        {
            if (track == null || track.Keys == null) return false;
            // Legacy preset rotation: 0s -> 0, 1s -> 180, 2s -> 360
            if (track.Prop == (ushort)PropKey.Rotation)
            {
                if (track.Keys.Count == 3)
                {
                    bool matchTimes = Math.Abs(track.Keys[0].Time - 0.0) < 0.05 &&
                                      Math.Abs(track.Keys[1].Time - 1.0) < 0.05 &&
                                      Math.Abs(track.Keys[2].Time - 2.0) < 0.05;
                    bool matchVals = Math.Abs(track.Keys[0].Value - 0.0) < 0.05 &&
                                     Math.Abs(track.Keys[1].Value - 180.0) < 0.05 &&
                                     Math.Abs(track.Keys[2].Value - 360.0) < 0.05;
                    if (matchTimes && matchVals) return true;
                }
            }
            // Legacy preset opacity: 0s -> 1.0, 1s -> 0.5, 2s -> 1.0
            if (track.Prop == (ushort)PropKey.Opacity)
            {
                if (track.Keys.Count == 3)
                {
                    bool matchTimes = Math.Abs(track.Keys[0].Time - 0.0) < 0.05 &&
                                      Math.Abs(track.Keys[1].Time - 1.0) < 0.05 &&
                                      Math.Abs(track.Keys[2].Time - 2.0) < 0.05;
                    bool matchVals = Math.Abs(track.Keys[0].Value - 1.0) < 0.05 &&
                                     Math.Abs(track.Keys[1].Value - 0.5) < 0.05 &&
                                     Math.Abs(track.Keys[2].Value - 1.0) < 0.05;
                    if (matchTimes && matchVals) return true;
                }
            }
            return false;
        }

        private void SanitizeAnimationClips(KerfDocument doc)
        {
            if (doc == null || doc.Animation == null || doc.Animation.Clips == null) return;
            bool removedAny = false;
            foreach (var clip in doc.Animation.Clips)
            {
                if (clip == null || clip.Tracks == null) continue;
                int countBefore = clip.Tracks.Count;
                clip.Tracks.RemoveAll(IsLegacyPresetDemoTrack);
                if (clip.Tracks.Count != countBefore) removedAny = true;
            }
            if (removedAny && History != null)
            {
                History.MarkDirty();
            }
        }

        private double GetEvaluatedPropertyRaw(LayerNode layer, ushort prop)
        {
            if (layer == null) return 0.0;
            switch ((PropKey)prop)
            {
                case PropKey.X: return layer.X + (layer.RenderTransform != null ? layer.RenderTransform.TranslationX : 0.0);
                case PropKey.Y: return layer.Y + (layer.RenderTransform != null ? layer.RenderTransform.TranslationY : 0.0);
                case PropKey.ScaleX: return layer.RenderTransform != null ? layer.RenderTransform.ScaleX : 1.0;
                case PropKey.ScaleY: return layer.RenderTransform != null ? layer.RenderTransform.ScaleY : 1.0;
                case PropKey.Rotation: return layer.Rotation + (layer.RenderTransform != null ? layer.RenderTransform.Rotation : 0.0);
                case PropKey.AnchorX: return layer.RenderTransform != null ? layer.RenderTransform.AnchorX : 0.0;
                case PropKey.AnchorY: return layer.RenderTransform != null ? layer.RenderTransform.AnchorY : 0.0;
                case PropKey.Opacity: return layer.Opacity;
                case PropKey.Width:
                case PropKey.VisualWidth: return layer.RenderTransform != null && layer.RenderTransform.VisualWidth > 0 ? layer.RenderTransform.VisualWidth : layer.Width;
                case PropKey.Height:
                case PropKey.VisualHeight: return layer.RenderTransform != null && layer.RenderTransform.VisualHeight > 0 ? layer.RenderTransform.VisualHeight : layer.Height;
                case PropKey.CornerRadius: return layer.CornerRadius;
                default: return 0.0;
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
                    ApplyAnimationPose(page, pose);
                    _sceneGraph.InvalidateAll();
                    RefreshCanvas();
                }
            };
            _timelineController.PlaybackStopped += (s, e) =>
            {
                RestoreAnimationRestPose();
                _sceneGraph.InvalidateAll();
                RefreshCanvas();
            };

            if (Document.Animation == null) Document.Animation = new AnimationProjectData();
            if (Document.Animation.Clips == null) Document.Animation.Clips = new List<Clip>();
            SanitizeAnimationClips(Document);
            if (Document.Animation.Clips.Count > 0)
            {
                Clip activeClip = Document.Animation.Clips.FirstOrDefault(c => c.Id == Document.Animation.ActiveClipId);
                if (activeClip == null) activeClip = Document.Animation.Clips[0];
                Document.Animation.ActiveClipId = activeClip.Id;
                _timelineController.ActiveClip = activeClip;
                _timelineController.Duration = Math.Max(0.001, activeClip.Duration);
                if (Document.Animation.StateMachine != null)
                    _timelineController.ActiveStateMachine = new StateMachineRuntime(Document.Animation.StateMachine, Document.Animation.Clips);
                if (_timelineScrubber != null) _timelineScrubber.Maximum = _timelineController.Duration;
                if (_timelineClipText != null) _timelineClipText.Text = "Clip: " + activeClip.Name;
                BuildStateMachineInputsControls();
                BuildStateMachineStatesGraph();
                return;
            }

            var defaultClip = new Clip(100, "Default Animation", 2.0, 60.0, LoopMode.Loop);
            // Start with an empty clip: no animations applied before the user explicitly creates keyframes
            defaultClip.Tracks.Clear();

            _timelineController.ActiveClip = defaultClip;
            _timelineController.Duration = defaultClip.Duration;

            // Minimal, editable starter graph: Entry → Idle, plus an input-driven interrupt state.
            var interruptClip = new Clip(101, "interrupt", defaultClip.Duration, 60.0, LoopMode.OneShot);
            var smDef = new StateMachineDefinition("State Machine 1");
            smDef.AddViewModelProperty(new ViewModelPropertyDefinition("Hovering", ViewModelPropertyKind.Boolean));
            var smLayer = new MachineLayer("Base Layer");
            int stEntry = smLayer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int stIdle = smLayer.AddState(new MachineState(1, "Idle", StateKind.Animation, 0));
            int stInterrupt = smLayer.AddState(new MachineState(2, "interrupt", StateKind.Animation, 1));
            smLayer.AddState(new MachineState(3, "Any State", StateKind.Any));
            smLayer.AddState(new MachineState(4, "Exit", StateKind.Exit));
            smLayer.AddTransition(new StateTransition(stEntry, stIdle, 0.0));
            var idleToInterrupt = new StateTransition(stIdle, stInterrupt, 0.25);
            idleToInterrupt.Conditions.Add(new TransitionCondition("Hovering", ConditionOp.Eq, 1.0));
            smLayer.AddTransition(idleToInterrupt);
            var interruptToIdle = new StateTransition(stInterrupt, stIdle, 0.25);
            interruptToIdle.Conditions.Add(new TransitionCondition("Hovering", ConditionOp.Eq, 0.0));
            smLayer.AddTransition(interruptToIdle);
            smDef.AddLayer(smLayer);
            var clips = new List<Clip> { defaultClip, interruptClip };
            _timelineController.ActiveStateMachine = new StateMachineRuntime(smDef, clips);
            Document.Animation.Clips = clips;
            Document.Animation.StateMachine = smDef;
            Document.Animation.ActiveClipId = defaultClip.Id;            if (_timelineScrubber != null) _timelineScrubber.Maximum = _timelineController.Duration;
            if (_timelineClipText != null) _timelineClipText.Text = "Clip: " + defaultClip.Name;
            BuildStateMachineInputsControls();
            BuildStateMachineStatesGraph();
        }

        private void AddKeyframeForSelectedLayer()
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            var clip = _timelineController.ActiveClip;
            if (clip == null)
            {
                clip = new Clip(100, "Default Animation", _timelineController.Duration);
                _timelineController.ActiveClip = clip;
            }

            var sel = _selectedLayers.FirstOrDefault();
            if (sel == null)
            {
                MessageBox.Show("Please select a layer on canvas or in the layers list to record animation keyframes.", "Kerf Studio - Animation", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double time = _timelineController.CurrentTime;
            uint nodeId = PropertyBinder.GetNumericNodeId(sel.Id);

            var existingTracks = clip.GetTracksForNode(nodeId);
            if (existingTracks != null && existingTracks.Count > 0)
            {
                foreach (var tr in existingTracks)
                {
                    double propVal = GetEvaluatedPropertyRaw(sel, tr.Prop);
                    AddOrUpdateTrackKeyframe(clip, nodeId, tr.Prop, time, propVal);
                }
            }
            else
            {
                // No tracks yet: key ScaleX and ScaleY (or Position if translated),
                // without adding uninvited rotation or opacity presets.
                double sx = sel.RenderTransform != null ? sel.RenderTransform.ScaleX : 1.0;
                double sy = sel.RenderTransform != null ? sel.RenderTransform.ScaleY : 1.0;
                AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)PropKey.ScaleX, time, sx);
                AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)PropKey.ScaleY, time, sy);
            }

            if (Document.Animation == null) Document.Animation = new AnimationProjectData();
            if (Document.Animation.Clips == null) Document.Animation.Clips = new List<Clip>();
            if (!Document.Animation.Clips.Contains(clip)) Document.Animation.Clips.Add(clip);
            Document.Animation.ActiveClipId = clip.Id;
            _timelineController.UseStateMachine = false;
            History.MarkDirty();

            RefreshTimelineUI();
        }

        private FrameworkElement CreateAnimatedPropertyField(FrameworkElement field, LayerNode layer, PropKey property)
        {
            if (field == null || _currentStudioMode != StudioMode.Animate) return field;
            var grid = new Grid { Margin = new Thickness(0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Input field
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // Jump Prev ◀
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Diamond ◆/◇
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // Jump Next ▶
            Grid.SetColumn(field, 0);
            grid.Children.Add(field);

            uint nodeId = layer != null ? PropertyBinder.GetNumericNodeId(layer.Id) : 0;
            var clip = _timelineController != null ? _timelineController.ActiveClip : null;
            AnimTrack track = (clip != null && nodeId != 0) ? clip.GetTrack(nodeId, (ushort)property) : null;
            bool hasTrack = (track != null && track.Keys != null && track.Keys.Count > 0);
            double currentTime = _timelineController != null ? _timelineController.CurrentTime : 0.0;
            Keyframe existingKey = (track != null) ? track.FindKeyframeAt(currentTime, 0.02) : null;
            bool hasKey = (existingKey != null);

            // Prev Button
            var prevBtn = new Button
            {
                Content = "◀",
                Width = 14,
                Height = 22,
                Padding = new Thickness(0),
                Margin = new Thickness(1, 0, 1, 0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = hasTrack ? new SolidColorBrush(Color.FromRgb(140, 160, 195)) : new SolidColorBrush(Color.FromRgb(60, 70, 90)),
                FontSize = 8,
                Cursor = hasTrack ? Cursors.Hand : Cursors.Arrow,
                ToolTip = "Jump to previous keyframe"
            };
            prevBtn.Click += (s, e) =>
            {
                if (track != null)
                {
                    double? prev = track.GetPreviousKeyframeTime(currentTime);
                    if (prev.HasValue && _timelineController != null)
                    {
                        _timelineController.Seek(prev.Value);
                        UpdateTimelineTimeReadout();
                        UpdateTimelineScrubberPosition();
                        RefreshTimelineCanvas();
                        RefreshPropertyInspector();
                    }
                }
            };
            Grid.SetColumn(prevBtn, 1);
            grid.Children.Add(prevBtn);

            // Diamond Button
            var diamondBtn = new Button
            {
                Content = hasKey ? "◆" : "◇",
                Width = 18,
                Height = 22,
                Padding = new Thickness(0),
                Margin = new Thickness(1, 0, 1, 0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = hasKey
                    ? KerfBrand.MintBrush // Solid Mint ◆
                    : hasTrack
                        ? new SolidColorBrush(Color.FromRgb(70, 239, 177)) // Hollow Mint ◇
                        : new SolidColorBrush(Color.FromRgb(100, 115, 135)), // Dim Gray ◇
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                ToolTip = hasKey
                    ? "Keyframe at playhead (click to delete keyframe)"
                    : hasTrack
                        ? "Property animated (click to insert keyframe at playhead)"
                        : "Not keyed (click to insert keyframe and create track)"
            };
            diamondBtn.Click += (s, e) =>
            {
                if (hasKey)
                {
                    if (track != null)
                    {
                        track.RemoveKeyframeAt(currentTime, 0.02);
                        if (_timelineController != null) _timelineController.Seek(currentTime);
                        RefreshTimelineUI();
                        RefreshPropertyInspector();
                    }
                }
                else
                {
                    double val = GetPropertyValue(layer, property);
                    AddKeyframeForProperty(layer, property, val);
                    RefreshTimelineUI();
                    RefreshPropertyInspector();
                }
            };
            Grid.SetColumn(diamondBtn, 2);
            grid.Children.Add(diamondBtn);

            // Next Button
            var nextBtn = new Button
            {
                Content = "▶",
                Width = 14,
                Height = 22,
                Padding = new Thickness(0),
                Margin = new Thickness(1, 0, 1, 0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = hasTrack ? new SolidColorBrush(Color.FromRgb(140, 160, 195)) : new SolidColorBrush(Color.FromRgb(60, 70, 90)),
                FontSize = 8,
                Cursor = hasTrack ? Cursors.Hand : Cursors.Arrow,
                ToolTip = "Jump to next keyframe"
            };
            nextBtn.Click += (s, e) =>
            {
                if (track != null)
                {
                    double? next = track.GetNextKeyframeTime(currentTime);
                    if (next.HasValue && _timelineController != null)
                    {
                        _timelineController.Seek(next.Value);
                        UpdateTimelineTimeReadout();
                        UpdateTimelineScrubberPosition();
                        RefreshTimelineCanvas();
                        RefreshPropertyInspector();
                    }
                }
            };
            Grid.SetColumn(nextBtn, 3);
            grid.Children.Add(nextBtn);

            return grid;
        }

        private bool IsPropertyAnimated(LayerNode layer, PropKey property)
        {
            var clip = _timelineController != null ? _timelineController.ActiveClip : null;
            uint nodeId = layer != null ? PropertyBinder.GetNumericNodeId(layer.Id) : 0;
            return clip != null && clip.Tracks != null && clip.Tracks.Any(t => t != null && t.NodeId == nodeId && t.Prop == (ushort)property);
        }

        private bool HasPropertyKeyAtPlayhead(LayerNode layer, PropKey property)
        {
            var clip = _timelineController != null ? _timelineController.ActiveClip : null;
            if (clip == null || layer == null) return false;
            uint nodeId = PropertyBinder.GetNumericNodeId(layer.Id);
            var track = clip.Tracks.FirstOrDefault(t => t != null && t.NodeId == nodeId && t.Prop == (ushort)property);
            return track != null && track.Keys != null && track.Keys.Any(k => Math.Abs(k.Time - _timelineController.CurrentTime) <= 0.02);
        }

        private static double GetPropertyValue(LayerNode layer, PropKey property)
        {
            if (layer == null) return 0.0;
            switch (property)
            {
                case PropKey.X:
                    return layer.X + (layer.RenderTransform != null ? layer.RenderTransform.TranslationX : 0.0);
                case PropKey.Y:
                    return layer.Y + (layer.RenderTransform != null ? layer.RenderTransform.TranslationY : 0.0);
                case PropKey.ScaleX:
                    return layer.RenderTransform != null ? layer.RenderTransform.ScaleX : 1.0;
                case PropKey.ScaleY:
                    return layer.RenderTransform != null ? layer.RenderTransform.ScaleY : 1.0;
                case PropKey.Rotation:
                    return layer.Rotation + (layer.RenderTransform != null ? layer.RenderTransform.Rotation : 0.0);
                case PropKey.AnchorX:
                    return layer.RenderTransform != null ? layer.RenderTransform.AnchorX : 0.0;
                case PropKey.AnchorY:
                    return layer.RenderTransform != null ? layer.RenderTransform.AnchorY : 0.0;
                case PropKey.Width:
                case PropKey.VisualWidth:
                    return layer.RenderTransform != null && layer.RenderTransform.VisualWidth > 0 ? layer.RenderTransform.VisualWidth : layer.Width;
                case PropKey.Height:
                case PropKey.VisualHeight:
                    return layer.RenderTransform != null && layer.RenderTransform.VisualHeight > 0 ? layer.RenderTransform.VisualHeight : layer.Height;
                case PropKey.Opacity:
                    return layer.Opacity;
                case PropKey.CornerRadius:
                    return layer.CornerRadius;
                default:
                    return 0.0;
            }
        }

        private void AutoKeyProperty(LayerNode layer, PropKey property, double value)
        {
            if (_currentStudioMode != StudioMode.Animate || layer == null) return;
            AddKeyframeForProperty(layer, property, value);
        }

        private void AddKeyframeForProperty(LayerNode layer, PropKey property, double value)
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            if (_timelineController == null || layer == null) return;
            var clip = _timelineController.ActiveClip;
            if (clip == null)
            {
                clip = new Clip(100, "Default Animation", _timelineController.Duration);
                _timelineController.ActiveClip = clip;
            }
            uint nodeId = PropertyBinder.GetNumericNodeId(layer.Id);
            AddOrUpdateTrackKeyframe(clip, nodeId, (ushort)property, _timelineController.CurrentTime, value);
            if (Document.Animation == null) Document.Animation = new AnimationProjectData();
            if (Document.Animation.Clips == null) Document.Animation.Clips = new List<Clip>();
            if (!Document.Animation.Clips.Contains(clip)) Document.Animation.Clips.Add(clip);
            Document.Animation.ActiveClipId = clip.Id;
            _timelineController.UseStateMachine = false;
            History.MarkDirty();
            RefreshTimelineCanvas();
        }

        private void AddOrUpdateTrackKeyframe(Clip clip, uint nodeId, ushort propertyId, double time, double val)
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
                track.AddKeyframe(new Keyframe(time, val, _defaultInterpolationMode, _defaultInterpolationX1, _defaultInterpolationY1, _defaultInterpolationX2, _defaultInterpolationY2));
            }
        }

        private string GetActiveStateNameForClip(Clip clip)
        {
            if (clip == null || Document == null || Document.Animation == null || Document.Animation.StateMachine == null)
                return null;

            var sm = Document.Animation.StateMachine;
            int clipIdx = Document.Animation.Clips != null ? Document.Animation.Clips.IndexOf(clip) : -1;
            if (clipIdx >= 0)
            {
                foreach (var layer in sm.Layers)
                {
                    foreach (var state in layer.States)
                    {
                        if (state.Kind == StateKind.Animation && state.ClipIndex == clipIdx)
                        {
                            return state.Name;
                        }
                    }
                }
            }
            return null;
        }

        private void RefreshTimelineStateTabs()
        {
            if (_timelineStateTabsStack == null) return;
            _timelineStateTabsStack.Children.Clear();

            var sm = Document != null && Document.Animation != null ? Document.Animation.StateMachine : null;
            if (sm == null || sm.Layers.Count == 0 || Document.Animation.Clips == null) return;

            var layer = sm.Layers[0];
            var activeClip = _timelineController != null ? _timelineController.ActiveClip : null;

            foreach (var state in layer.States)
            {
                if (state.Kind != StateKind.Animation) continue;

                bool isActive = false;
                if (activeClip != null && state.ClipIndex >= 0 && state.ClipIndex < Document.Animation.Clips.Count)
                {
                    isActive = ReferenceEquals(Document.Animation.Clips[state.ClipIndex], activeClip);
                }

                var tab = new Border
                {
                    Background = isActive ? KerfBrand.MintBrush : KerfBrand.DarkSlateBrush,
                    BorderBrush = isActive ? KerfBrand.MintBrush : KerfBrand.TealBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 0, 4, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = "State: " + state.Name + " (Click to open state timeline)"
                };

                var tabText = new TextBlock
                {
                    Text = (isActive ? "◉ " : "○ ") + state.Name,
                    FontSize = 10.5,
                    FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = isActive ? KerfBrand.TextBlackBrush : KerfBrand.TextWhiteBrush
                };
                tab.Child = tabText;

                var capturedState = state;
                tab.MouseLeftButtonUp += (s, e) =>
                {
                    OpenStateTimeline(capturedState);
                };

                _timelineStateTabsStack.Children.Add(tab);
            }

            var addStateBtn = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = KerfBrand.TealBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = Cursors.Hand,
                ToolTip = "Add a new animation state and open its timeline"
            };
            addStateBtn.Child = new TextBlock
            {
                Text = "+ State",
                FontSize = 10.5,
                Foreground = KerfBrand.MintBrush
            };
            addStateBtn.MouseLeftButtonUp += (s, e) =>
            {
                AddNewStateWithTimeline();
            };
            _timelineStateTabsStack.Children.Add(addStateBtn);

            if (_timelineClipText != null && activeClip != null)
            {
                string stateName = GetActiveStateNameForClip(activeClip);
                _timelineClipText.Text = !string.IsNullOrEmpty(stateName)
                    ? "State: [" + stateName + "] · Clip: " + activeClip.Name
                    : "Clip: " + activeClip.Name;
            }
        }

        private void AddNewStateWithTimeline()
        {
            if (_timelineController == null) EnsureTimelineControllerInitialized();
            var sm = EnsureStateMachineRuntime();
            if (sm == null || sm.Definition == null || sm.Definition.Layers.Count == 0) return;

            var layer = sm.Definition.Layers[0];
            int nextIndex = layer.States.Count;
            string stateName = "State " + nextIndex;

            if (Document.Animation == null) Document.Animation = new AnimationProjectData();
            if (Document.Animation.Clips == null) Document.Animation.Clips = new List<Clip>();

            uint clipId = Document.Animation.Clips.Count == 0 ? 100u : Document.Animation.Clips.Max(c => c == null ? 0u : c.Id) + 1u;
            var newClip = new Clip(clipId, stateName, Math.Max(0.1, _timelineController.Duration), _timelineController.Fps, LoopMode.Loop);
            Document.Animation.Clips.Add(newClip);
            if (!sm.Clips.Contains(newClip)) sm.Clips.Add(newClip);

            var newState = new MachineState(nextIndex, stateName, StateKind.Animation, sm.Clips.IndexOf(newClip));
            layer.AddState(newState);

            History.MarkDirty();
            BuildStateMachineStatesGraph();
            SelectAnimationClip(newClip);
        }

        public void RefreshTimelineUI()
        {
            RefreshTimelineTracks();
            RefreshTimelineCanvas();
            RefreshInterpolationGraphPanels();
            UpdateTimelineTimeReadout();
            UpdateTimelineScrubberPosition();
            RefreshTimelineStateTabs();
        }

        private void RefreshTimelineTracks()
        {
            if (_timelineTracksStack == null) return;
            _timelineTracksStack.Children.Clear();

            // 1. Top Ruler Header Spacer (Height: 22px)
            var rulerHeader = new Border
            {
                Height = 22,
                Background = new SolidColorBrush(Color.FromRgb(15, 20, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 66)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 0, 8, 0)
            };
            string stateTitle = GetActiveStateNameForClip(_timelineController != null ? _timelineController.ActiveClip : null);
            string rulerTitle = !string.IsNullOrEmpty(stateTitle)
                ? "STATE: " + stateTitle.ToUpper() + " · TRACKS"
                : "OBJECTS / TRACKS";
            var headerText = new TextBlock
            {
                Text = rulerTitle,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = !string.IsNullOrEmpty(stateTitle) ? KerfBrand.MintBrush : new SolidColorBrush(Color.FromRgb(140, 160, 195)),
                VerticalAlignment = VerticalAlignment.Center
            };
            rulerHeader.Child = headerText;
            _timelineTracksStack.Children.Add(rulerHeader);

            var lanes = BuildTimelineLanes();
            if (lanes.Count == 0)
            {
                var hint = new TextBlock
                {
                    Text = "Select a layer on canvas or create keyframes to view tracks.",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 135, 160)),
                    Margin = new Thickness(8, 12, 8, 8),
                    TextWrapping = TextWrapping.Wrap
                };
                _timelineTracksStack.Children.Add(hint);
                return;
            }

            foreach (var lane in lanes)
            {
                if (lane.IsMaster)
                {
                    bool isExpanded = !_collapsedTimelineObjects.Contains(lane.NodeId);
                    _timelineTracksStack.Children.Add(CreateMasterObjectRow(lane.NodeId, isExpanded));
                }
                else
                {
                    _timelineTracksStack.Children.Add(CreatePropertyTrackRow(lane.NodeId, lane.Track));
                }
            }
        }

        private List<TimelineLaneInfo> BuildTimelineLanes()
        {
            var lanes = new List<TimelineLaneInfo>();
            if (_timelineController == null || _timelineController.ActiveClip == null) return lanes;

            var nodeIds = GetTimelineObjectNodeIds();
            double currentY = 22.0; // Starts immediately below 22px ruler

            foreach (var nodeId in nodeIds)
            {
                lanes.Add(new TimelineLaneInfo
                {
                    IsMaster = true,
                    NodeId = nodeId,
                    Track = null,
                    Y = currentY,
                    Height = 22.0
                });
                currentY += 22.0;

                if (!_collapsedTimelineObjects.Contains(nodeId))
                {
                    var tracks = _timelineController.ActiveClip.GetTracksForNode(nodeId);
                    foreach (var track in tracks)
                    {
                        lanes.Add(new TimelineLaneInfo
                        {
                            IsMaster = false,
                            NodeId = nodeId,
                            Track = track,
                            Y = currentY,
                            Height = 22.0
                        });
                        currentY += 22.0;
                    }
                }
            }

            return lanes;
        }

        private List<uint> GetTimelineObjectNodeIds()
        {
            var list = new List<uint>();
            if (_timelineController != null && _timelineController.ActiveClip != null)
            {
                var sel = _selectedLayers.FirstOrDefault();
                uint selId = sel != null ? PropertyBinder.GetNumericNodeId(sel.Id) : 0;
                if (selId != 0)
                {
                    list.Add(selId);
                }
                foreach (var track in _timelineController.ActiveClip.Tracks)
                {
                    if (track.NodeId != 0 && !list.Contains(track.NodeId))
                    {
                        list.Add(track.NodeId);
                    }
                }
            }
            return list;
        }

        private LayerNode FindLayerByNumericId(uint nodeId)
        {
            var page = Document != null ? Document.GetActivePage() : null;
            if (page == null) return null;
            return page.GetAllLayersFlat().FirstOrDefault(l => PropertyBinder.GetNumericNodeId(l.Id) == nodeId);
        }

        private static string GetLayerTypeBadge(LayerNode layer)
        {
            if (layer == null) return "•";
            switch (layer.Type)
            {
                case ElementType.Frame: return "F";
                case ElementType.Group: return "G";
                case ElementType.Rectangle: return "R";
                case ElementType.Ellipse: return "O";
                case ElementType.Text: return "T";
                case ElementType.Path:
                case ElementType.Line:
                case ElementType.Arrow:
                case ElementType.Polygon:
                case ElementType.Star: return "V";
                case ElementType.Image: return "I";
                case ElementType.ComponentInstance: return "C";
                default: return "■";
            }
        }

        private static string GetPropertyName(ushort prop)
        {
            switch ((PropKey)prop)
            {
                case PropKey.X: return "Position X";
                case PropKey.Y: return "Position Y";
                case PropKey.ScaleX: return "Scale X";
                case PropKey.ScaleY: return "Scale Y";
                case PropKey.Rotation: return "Rotation";
                case PropKey.AnchorX: return "Anchor X";
                case PropKey.AnchorY: return "Anchor Y";
                case PropKey.Opacity: return "Opacity";
                case PropKey.Width: return "Width";
                case PropKey.Height: return "Height";
                case PropKey.VisualWidth: return "Visual Width";
                case PropKey.VisualHeight: return "Visual Height";
                case PropKey.CornerRadius: return "Corner Radius";
                default: return ((PropKey)prop).ToString();
            }
        }

        private string GetEvaluatedPropertyString(uint nodeId, ushort prop)
        {
            var layer = FindLayerByNumericId(nodeId);
            double val = 0.0;
            bool hasVal = false;

            if (_timelineController != null && _timelineController.ActiveClip != null)
            {
                var track = _timelineController.ActiveClip.GetTrack(nodeId, prop);
                if (track != null && track.Keys.Count > 0)
                {
                    val = track.Sample(_timelineController.CurrentTime);
                    hasVal = true;
                }
            }

            if (!hasVal && layer != null)
            {
                switch ((PropKey)prop)
                {
                    case PropKey.X: val = layer.X + (layer.RenderTransform != null ? layer.RenderTransform.TranslationX : 0); break;
                    case PropKey.Y: val = layer.Y + (layer.RenderTransform != null ? layer.RenderTransform.TranslationY : 0); break;
                    case PropKey.ScaleX: val = layer.RenderTransform != null ? layer.RenderTransform.ScaleX : 1.0; break;
                    case PropKey.ScaleY: val = layer.RenderTransform != null ? layer.RenderTransform.ScaleY : 1.0; break;
                    case PropKey.Rotation: val = layer.Rotation + (layer.RenderTransform != null ? layer.RenderTransform.Rotation : 0); break;
                    case PropKey.AnchorX: val = layer.RenderTransform != null ? layer.RenderTransform.AnchorX : 0.0; break;
                    case PropKey.AnchorY: val = layer.RenderTransform != null ? layer.RenderTransform.AnchorY : 0.0; break;
                    case PropKey.Opacity: val = layer.Opacity; break;
                    case PropKey.Width:
                    case PropKey.VisualWidth: val = layer.RenderTransform != null && layer.RenderTransform.VisualWidth > 0 ? layer.RenderTransform.VisualWidth : layer.Width; break;
                    case PropKey.Height:
                    case PropKey.VisualHeight: val = layer.RenderTransform != null && layer.RenderTransform.VisualHeight > 0 ? layer.RenderTransform.VisualHeight : layer.Height; break;
                    case PropKey.CornerRadius: val = layer.CornerRadius; break;
                }
            }

            switch ((PropKey)prop)
            {
                case PropKey.X:
                case PropKey.Y:
                case PropKey.Width:
                case PropKey.Height:
                case PropKey.VisualWidth:
                case PropKey.VisualHeight:
                case PropKey.CornerRadius:
                    return string.Format("{0:F1} px", val);
                case PropKey.Rotation:
                    return string.Format("{0:F1} °", val);
                case PropKey.Opacity:
                    return string.Format("{0:F0} %", val * 100);
                case PropKey.ScaleX:
                case PropKey.ScaleY:
                case PropKey.AnchorX:
                case PropKey.AnchorY:
                    return string.Format("{0:F2}", val);
                default:
                    return string.Format("{0:F1}", val);
            }
        }

        private FrameworkElement CreateMasterObjectRow(uint nodeId, bool isExpanded)
        {
            var layer = FindLayerByNumericId(nodeId);
            string layerName = layer != null ? layer.Name : string.Format("Object #{0}", nodeId);
            string typeBadge = GetLayerTypeBadge(layer);
            var tracks = (_timelineController != null && _timelineController.ActiveClip != null)
                ? _timelineController.ActiveClip.GetTracksForNode(nodeId)
                : new List<AnimTrack>();

            bool isSelected = _selectedLayers.Any(l => PropertyBinder.GetNumericNodeId(l.Id) == nodeId);

            var grid = new Grid
            {
                Height = 22,
                Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(24, 48, 64))
                    : new SolidColorBrush(Color.FromRgb(16, 20, 28)),
                Cursor = Cursors.Hand
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Expander button
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); // Icon badge
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Track count

            // 1. Expander arrow
            var expandBtn = new Border
            {
                Width = 16,
                Height = 16,
                Background = Brushes.Transparent,
                Child = new TextBlock
                {
                    Text = isExpanded ? "▼" : "▶",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 160, 195)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            expandBtn.MouseLeftButtonDown += (s, e) =>
            {
                if (_collapsedTimelineObjects.Contains(nodeId))
                    _collapsedTimelineObjects.Remove(nodeId);
                else
                    _collapsedTimelineObjects.Add(nodeId);

                RefreshTimelineUI();
                e.Handled = true;
            };
            Grid.SetColumn(expandBtn, 0);
            grid.Children.Add(expandBtn);

            // 2. Type badge
            var badge = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromRgb(32, 40, 56)),
                Child = new TextBlock
                {
                    Text = typeBadge,
                    FontSize = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = KerfBrand.MintBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);

            // 3. Name
            var nameBlock = new TextBlock
            {
                Text = layerName,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = isSelected ? KerfBrand.MintBrush : Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameBlock, 2);
            grid.Children.Add(nameBlock);

            // 4. Track count badge
            var countBlock = new TextBlock
            {
                Text = string.Format("({0})", tracks.Count),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 120, 150)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(countBlock, 3);
            grid.Children.Add(countBlock);

            grid.MouseLeftButtonDown += (s, e) =>
            {
                if (layer != null)
                {
                    _selectedLayers.Clear();
                    _selectedLayers.Add(layer);
                    OnSelectionChanged();
                    RefreshCanvas();
                    RefreshTimelineUI();
                }
            };

            return grid;
        }

        private FrameworkElement CreatePropertyTrackRow(uint nodeId, AnimTrack track)
        {
            var grid = new Grid
            {
                Height = 22,
                Background = new SolidColorBrush(Color.FromRgb(14, 17, 24))
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); // Indent
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Prop name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Value readout
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); // Delete button

            var indentBullet = new TextBlock
            {
                Text = "└",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 75, 105)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(indentBullet, 0);
            grid.Children.Add(indentBullet);

            string propDisplay = (track != null && !string.IsNullOrEmpty(track.PropertyPath) && track.PropertyPath != "0")
                ? track.PropertyPath
                : GetPropertyName(track != null ? track.Prop : (ushort)0);

            var nameBlock = new TextBlock
            {
                Text = propDisplay,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 220)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameBlock, 1);
            grid.Children.Add(nameBlock);

            var valBlock = new TextBlock
            {
                Text = track != null ? GetEvaluatedPropertyString(nodeId, track.Prop) : string.Empty,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 145, 175)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(valBlock, 2);
            grid.Children.Add(valBlock);

            var delBtn = new Border
            {
                Width = 16,
                Height = 16,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Delete this animation track",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            var delText = new TextBlock
            {
                Text = "×",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 115, 140)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            delBtn.Child = delText;
            delBtn.MouseEnter += (s, e) => delText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            delBtn.MouseLeave += (s, e) => delText.Foreground = new SolidColorBrush(Color.FromRgb(100, 115, 140));

            var capturedTrack = track;
            delBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (_timelineController != null && _timelineController.ActiveClip != null && capturedTrack != null)
                {
                    _timelineController.ActiveClip.Tracks.Remove(capturedTrack);
                    History.MarkDirty();
                    RefreshTimelineUI();
                }
            };
            Grid.SetColumn(delBtn, 3);
            grid.Children.Add(delBtn);

            var menu = new ContextMenu();
            var delItem = new MenuItem { Header = "Delete Track" };
            delItem.Click += (s, e) =>
            {
                if (_timelineController != null && _timelineController.ActiveClip != null && capturedTrack != null)
                {
                    _timelineController.ActiveClip.Tracks.Remove(capturedTrack);
                    History.MarkDirty();
                    RefreshTimelineUI();
                }
            };
            menu.Items.Add(delItem);

            var clearItem = new MenuItem { Header = "Clear All Keyframes on Track" };
            clearItem.Click += (s, e) =>
            {
                if (capturedTrack != null && capturedTrack.Keys != null)
                {
                    capturedTrack.Keys.Clear();
                    History.MarkDirty();
                    RefreshTimelineUI();
                }
            };
            menu.Items.Add(clearItem);

            grid.ContextMenu = menu;

            return grid;
        }

        private void RefreshTimelineCanvas()
        {
            if (_timelineCanvasHost == null) return;
            _timelineCanvasHost.Children.Clear();
            _timelineKeyframeOwners.Clear();

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

            double fps = (_timelineController != null) ? Math.Max(1.0, _timelineController.Fps) : 60.0;
            double frameDuration = 1.0 / fps;
            int totalFrames = (int)Math.Ceiling(duration * fps);

            int frameStep = 1;
            if (pps < 120) frameStep = Math.Max(1, (int)fps / 2);
            else if (pps < 250) frameStep = Math.Max(1, (int)(fps / 4));
            else if (pps < 500) frameStep = Math.Max(1, (int)(fps / 12));
            else frameStep = 1;

            for (int f = 0; f <= totalFrames; f += frameStep)
            {
                double t = f * frameDuration;
                if (t > duration + 1e-4) break;
                double x = t * pps;

                bool isSecondMark = (f % (int)fps == 0);
                bool isHalfSecond = (f % Math.Max(1, (int)(fps / 2)) == 0);
                bool isMajor = isSecondMark || isHalfSecond;

                var tick = new Line
                {
                    X1 = x,
                    Y1 = isMajor ? 6 : 14,
                    X2 = x,
                    Y2 = 22,
                    Stroke = new SolidColorBrush(isMajor ? Color.FromRgb(140, 155, 185) : Color.FromRgb(50, 60, 80)),
                    StrokeThickness = 1
                };
                _timelineCanvasHost.Children.Add(tick);

                if (isMajor || (pps >= 400 && f % 5 == 0))
                {
                    var label = new TextBlock
                    {
                        Text = isSecondMark ? string.Format("{0}f ({1:0.0}s)", f, t) : string.Format("{0}f", f),
                        FontSize = 9,
                        Foreground = new SolidColorBrush(isMajor ? Color.FromRgb(140, 155, 185) : Color.FromRgb(90, 105, 130)),
                        FontFamily = new FontFamily("Consolas, Courier New")
                    };
                    Canvas.SetLeft(label, x + 3);
                    Canvas.SetTop(label, 4);
                    _timelineCanvasHost.Children.Add(label);
                }
            }

            // 2. Track Lanes and Keyframe Diamonds
            var lanes = BuildTimelineLanes();
            var sel = _selectedLayers.FirstOrDefault();
            uint selId = (sel != null) ? PropertyBinder.GetNumericNodeId(sel.Id) : 0;

            foreach (var lane in lanes)
            {
                double laneY = lane.Y;
                double laneH = lane.Height;

                var laneLine = new Line
                {
                    X1 = 0,
                    Y1 = laneY + laneH,
                    X2 = Math.Max(totalWidth + 100, 1000),
                    Y2 = laneY + laneH,
                    Stroke = new SolidColorBrush(lane.IsMaster ? Color.FromRgb(36, 44, 60) : Color.FromRgb(26, 32, 44)),
                    StrokeThickness = 1
                };
                _timelineCanvasHost.Children.Add(laneLine);

                if (lane.IsMaster)
                {
                    if (selId == lane.NodeId)
                    {
                        var laneHighlight = new Rectangle
                        {
                            Width = Math.Max(totalWidth + 100, 1000),
                            Height = laneH,
                            Fill = new SolidColorBrush(Color.FromArgb(16, 70, 239, 177)),
                            IsHitTestVisible = false
                        };
                        Canvas.SetLeft(laneHighlight, 0);
                        Canvas.SetTop(laneHighlight, laneY);
                        _timelineCanvasHost.Children.Add(laneHighlight);
                    }

                    var nodeTracks = (_timelineController != null && _timelineController.ActiveClip != null)
                        ? _timelineController.ActiveClip.GetTracksForNode(lane.NodeId)
                        : new List<AnimTrack>();

                    var summaryTimes = new HashSet<double>();
                    foreach (var tr in nodeTracks)
                        foreach (var k in tr.Keys)
                            summaryTimes.Add(k.Time);

                    double masterCenterY = laneY + 11.0;
                    foreach (var time in summaryTimes)
                    {
                        double kx = time * pps;
                        var keysAtTime = new List<Tuple<AnimTrack, Keyframe>>();
                        foreach (var tr in nodeTracks)
                        {
                            var match = tr.FindKeyframeAt(time, 1e-4);
                            if (match != null) keysAtTime.Add(Tuple.Create(tr, match));
                        }

                        bool anySelected = keysAtTime.Any(pair => _selectedTimelineKeyframes.Contains(pair.Item2));

                        var sumDiamond = new Polygon
                        {
                            Points = new PointCollection
                            {
                                new Point(kx, masterCenterY - 5),
                                new Point(kx + 5, masterCenterY),
                                new Point(kx, masterCenterY + 5),
                                new Point(kx - 5, masterCenterY)
                            },
                            Fill = anySelected ? KerfBrand.MintBrush : new SolidColorBrush(Color.FromRgb(160, 175, 205)),
                            Stroke = KerfBrand.ObsidianBrush,
                            StrokeThickness = 1,
                            ToolTip = string.Format("Summary · {0:0.00}s ({1} keys)", time, keysAtTime.Count),
                            Cursor = Cursors.SizeWE
                        };

                        var hitTarget = new Ellipse
                        {
                            Width = 18,
                            Height = 18,
                            Fill = Brushes.Transparent,
                            Cursor = Cursors.SizeWE,
                            ToolTip = sumDiamond.ToolTip
                        };
                        Canvas.SetLeft(hitTarget, kx - 9);
                        Canvas.SetTop(hitTarget, masterCenterY - 9);

                        var capturedPairs = keysAtTime;
                        hitTarget.PreviewMouseLeftButtonDown += (s, e) =>
                        {
                            bool additive = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                            if (!additive) _selectedTimelineKeyframes.Clear();
                            foreach (var p in capturedPairs)
                            {
                                _selectedTimelineKeyframes.Add(p.Item2);
                                _timelineKeyframeOwners[p.Item2] = p.Item1;
                            }
                            if (capturedPairs.Count > 0)
                            {
                                BeginTimelineKeyframeDrag(capturedPairs[0].Item1, capturedPairs[0].Item2, e);
                            }
                            e.Handled = true;
                        };

                        _timelineCanvasHost.Children.Add(hitTarget);
                        _timelineCanvasHost.Children.Add(sumDiamond);
                    }
                }
                else if (lane.Track != null)
                {
                    double propCenterY = laneY + 11.0;
                    var track = lane.Track;

                    foreach (var kf in track.Keys)
                    {
                        _timelineKeyframeOwners[kf] = track;
                        double kx = kf.Time * pps;
                        var keyframe = kf;
                        var keyTrack = track;

                        var hitTarget = new Ellipse
                        {
                            Width = 20,
                            Height = 20,
                            Fill = Brushes.Transparent,
                            Cursor = Cursors.SizeWE,
                            ToolTip = string.Format("Drag to move {0} keyframe", keyTrack.PropertyPath),
                            Tag = keyframe
                        };
                        Canvas.SetLeft(hitTarget, kx - 10);
                        Canvas.SetTop(hitTarget, propCenterY - 10);
                        _timelineCanvasHost.Children.Add(hitTarget);

                        int kfFrame = (int)Math.Round(kf.Time * fps);
                        var diamond = new Polygon
                        {
                            Points = new PointCollection
                            {
                                new Point(kx, propCenterY - 6),
                                new Point(kx + 6, propCenterY),
                                new Point(kx, propCenterY + 6),
                                new Point(kx - 6, propCenterY)
                            },
                            Fill = _selectedTimelineKeyframes.Contains(kf)
                                ? KerfBrand.MintBrush
                                : ReferenceEquals(kf, _editingInterpolationKeyframe) ? KerfBrand.TealBrush : KerfBrand.AmberBrush,
                            Stroke = KerfBrand.ObsidianBrush,
                            StrokeThickness = 1,
                            ToolTip = string.Format("{0} · {1:F2}s ({2}f) · {3:F1}", keyTrack.PropertyPath, kf.Time, kfFrame, kf.Value),
                            Cursor = Cursors.SizeWE,
                            Tag = keyframe
                        };
                        diamond.MouseRightButtonUp += (s, e) => ShowTimelineKeyframeContextMenu(keyTrack, keyframe, diamond);
                        _timelineCanvasHost.Children.Add(diamond);
                    }
                }
            }

            // 3. Playhead Line & Scrubber Needle
            if (_timelineController != null)
            {
                double px = _timelineController.CurrentTime * pps;
                double playheadHeight = Math.Max(220.0, lanes.Count > 0 ? (lanes.Last().Y + 30.0) : 220.0);

                var playheadLine = new Line
                {
                    X1 = px,
                    Y1 = 12,
                    X2 = px,
                    Y2 = playheadHeight,
                    Stroke = KerfBrand.MintBrush,
                    StrokeThickness = 1.5,
                    IsHitTestVisible = false
                };
                _timelineCanvasHost.Children.Add(playheadLine);

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
                    Fill = KerfBrand.MintBrush,
                    IsHitTestVisible = false
                };
                _timelineCanvasHost.Children.Add(playheadHead);
            }

            double requiredHeight = Math.Max(230.0, lanes.Count > 0 ? (lanes.Last().Y + 30.0) : 230.0);
            _timelineCanvasHost.Width = Math.Max(totalWidth + 200, 1200);
            _timelineCanvasHost.Height = requiredHeight;

            _selectedTimelineKeyframes.RemoveWhere(keyframe => !_timelineKeyframeOwners.ContainsKey(keyframe));
            RefreshInterpolationGraphPanels();
        }

        private void CopySelectedTimelineKeyframes()
        {
            if (_selectedTimelineKeyframes.Count == 0) return;
            var pairs = new List<Tuple<AnimTrack, Keyframe>>();
            foreach (var kf in _selectedTimelineKeyframes)
            {
                AnimTrack track;
                if (_timelineKeyframeOwners.TryGetValue(kf, out track))
                {
                    pairs.Add(Tuple.Create(track, kf));
                }
            }
            KeyframeClipboard.Copy(pairs);
        }

        private void PasteTimelineKeyframesAtPlayhead()
        {
            if (_timelineController == null || _timelineController.ActiveClip == null || !KeyframeClipboard.HasData) return;
            var sel = _selectedLayers.FirstOrDefault();
            uint targetNodeId = sel != null ? PropertyBinder.GetNumericNodeId(sel.Id) : 0;
            double playheadTime = _timelineController.CurrentTime;

            var pasted = KeyframeClipboard.Paste(_timelineController.ActiveClip, targetNodeId, playheadTime);
            if (pasted.Count > 0)
            {
                _selectedTimelineKeyframes.Clear();
                foreach (var kf in pasted)
                {
                    _selectedTimelineKeyframes.Add(kf);
                }
                _timelineController.Seek(playheadTime);
                RefreshTimelineUI();
            }
        }

        private void DuplicateSelectedTimelineKeyframes()
        {
            if (_timelineController == null || _timelineController.ActiveClip == null || _selectedTimelineKeyframes.Count == 0) return;
            var pairs = new List<Tuple<AnimTrack, Keyframe>>();
            foreach (var kf in _selectedTimelineKeyframes)
            {
                AnimTrack track;
                if (_timelineKeyframeOwners.TryGetValue(kf, out track))
                {
                    pairs.Add(Tuple.Create(track, kf));
                }
            }
            double offset = 1.0 / Math.Max(1.0, _timelineController.Fps);
            var duplicated = KeyframeClipboard.Duplicate(_timelineController.ActiveClip, pairs, offset);
            if (duplicated.Count > 0)
            {
                _selectedTimelineKeyframes.Clear();
                foreach (var kf in duplicated)
                {
                    _selectedTimelineKeyframes.Add(kf);
                }
                _timelineController.Seek(_timelineController.CurrentTime + offset);
                RefreshTimelineUI();
            }
        }

        private void JumpToPreviousKeyframe()
        {
            if (_timelineController == null || _timelineController.ActiveClip == null) return;
            double t = _timelineController.CurrentTime;
            double best = -1.0;
            foreach (var track in _timelineController.ActiveClip.Tracks)
            {
                double? candidate = track.GetPreviousKeyframeTime(t);
                if (candidate.HasValue && candidate.Value < t - 1e-4)
                {
                    if (candidate.Value > best) best = candidate.Value;
                }
            }
            if (best >= 0.0)
            {
                _timelineController.Seek(best);
                UpdateTimelineTimeReadout();
                UpdateTimelineScrubberPosition();
                RefreshTimelineCanvas();
            }
        }

        private void JumpToNextKeyframe()
        {
            if (_timelineController == null || _timelineController.ActiveClip == null) return;
            double t = _timelineController.CurrentTime;
            double best = double.MaxValue;
            foreach (var track in _timelineController.ActiveClip.Tracks)
            {
                double? candidate = track.GetNextKeyframeTime(t);
                if (candidate.HasValue && candidate.Value > t + 1e-4)
                {
                    if (candidate.Value < best) best = candidate.Value;
                }
            }
            if (best < double.MaxValue)
            {
                _timelineController.Seek(best);
                UpdateTimelineTimeReadout();
                UpdateTimelineScrubberPosition();
                RefreshTimelineCanvas();
            }
        }

        private void HandleTimelineCanvasMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                double factor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);
                _timelinePixelsPerSecond = Math.Max(60.0, Math.Min(1200.0, _timelinePixelsPerSecond * factor));
                RefreshTimelineCanvas();
                e.Handled = true;
            }
        }

        private static int GetPropRowIndex(ushort propKey)
        {
            switch ((PropKey)propKey)
            {
                case PropKey.X: return 0;
                case PropKey.Y: return 1;
                case PropKey.ScaleX: return 2;
                case PropKey.ScaleY: return 3;
                case PropKey.Rotation: return 4;
                case PropKey.AnchorX: return 5;
                case PropKey.AnchorY: return 6;
                case PropKey.Opacity: return 7;
                case PropKey.Width:
                case PropKey.VisualWidth: return 8;
                case PropKey.Height:
                case PropKey.VisualHeight: return 9;
                case PropKey.CornerRadius: return 10;
                default: return (int)propKey;
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
            btn.Content = VectorIcons.CreateIcon(iconGeo, KerfBrand.TextWhiteBrush, null, 16);
            btn.MouseEnter += (s, e) => btn.Background = KerfBrand.TealBrush;
            btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;
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
                Background = KerfBrand.DarkSlateBrush,
                Foreground = KerfBrand.TextWhiteBrush,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 2, 0),
                Template = CreateRoundedButtonTemplate(5)
            };
            btn.MouseEnter += (s, e) => btn.Background = KerfBrand.TealBrush;
            btn.MouseLeave += (s, e) => btn.Background = KerfBrand.DarkSlateBrush;
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
                Background = KerfBrand.DarkSlateBrush,
                Foreground = KerfBrand.TextMutedBrush,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Margin = new Thickness(2, 0, 2, 0),
                Template = CreateRoundedButtonTemplate(5)
            };
            btn.MouseEnter += (s, e) => btn.Background = KerfBrand.TealBrush;
            btn.MouseLeave += (s, e) => btn.Background = KerfBrand.DarkSlateBrush;
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
            string bgHex = page != null ? page.BackgroundColor : "#1E082E";
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
