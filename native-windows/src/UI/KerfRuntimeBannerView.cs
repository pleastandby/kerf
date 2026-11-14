using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Kerf.Engine;
using Kerf.Engine.Animation;
using Kerf.Model;
using Kerf.Runtime;

namespace Kerf.UI
{
    /// <summary>
    /// Plays the bundled KerfBanner package in the native Kerf runtime and scene renderer.
    /// </summary>
    internal sealed class KerfRuntimeBannerView : FrameworkElement
    {
        private sealed class LayerRestPose
        {
            public RenderTransform Transform;
            public double Opacity;
            public double CornerRadius;
            public double CornerRadiusTopLeft;
            public double CornerRadiusTopRight;
            public double CornerRadiusBottomRight;
            public double CornerRadiusBottomLeft;
            public List<StrokeRestPose> Strokes;
        }

        private sealed class StrokeRestPose
        {
            public double Width;
            public double TrimStart;
            public double TrimEnd;
            public double TrimOffset;
            public double TaperStart;
            public double TaperEnd;
        }

        private readonly KerfRuntime _runtime;
        private readonly RenderPose _pose;
        private readonly Page _page;
        private readonly GpuRenderer _renderer;
        private readonly RetainedSceneGraph _sceneGraph;
        private readonly VisualCollection _visuals;
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _clock;
        private readonly Dictionary<LayerNode, LayerRestPose> _restPose;
        private readonly Dictionary<uint, LayerNode> _layersByNumericId;
        private readonly HashSet<uint> _activePointerTargets;
        private readonly HashSet<uint> _pressedPointerTargets;
        private readonly Rect _contentBounds;
        private Matrix _rootTransform;

        public KerfRuntimeBannerView()
        {
            Focusable = true;
            ClipToBounds = true;

            _runtime = new KerfRuntime();
            _pose = new RenderPose();
            _clock = Stopwatch.StartNew();
            _restPose = new Dictionary<LayerNode, LayerRestPose>();
            _layersByNumericId = new Dictionary<uint, LayerNode>();
            _activePointerTargets = new HashSet<uint>();
            _pressedPointerTargets = new HashSet<uint>();

            string packagePath = WriteEmbeddedPackageToTempFile();
            try
            {
                _runtime.LoadPackage(packagePath);
            }
            finally
            {
                try { File.Delete(packagePath); }
                catch { }
            }

            _page = _runtime.Document != null ? _runtime.Document.GetActivePage() : null;
            if (_page == null) throw new InvalidDataException("KerfBanner has no active page.");

            if (_runtime.ActiveClip != null)
            {
                _runtime.ActiveClip.LoopMode = LoopMode.Loop;
                double lastKeyTime = _runtime.ActiveClip.Tracks == null
                    ? 0.0
                    : _runtime.ActiveClip.Tracks
                        .Where(t => t != null && t.Keys != null)
                        .SelectMany(t => t.Keys)
                        .Where(k => k != null)
                        .Select(k => k.Time)
                        .DefaultIfEmpty(0.0)
                        .Max();
                if (lastKeyTime > _runtime.ActiveClip.Duration)
                {
                    _runtime.ActiveClip.Duration = lastKeyTime;
                    _runtime.ActiveClip.WorkAreaEnd = lastKeyTime;
                }
            }

            foreach (var layer in _page.GetAllLayersFlat())
            {
                _restPose[layer] = CaptureRestPose(layer);
                _layersByNumericId[PropertyBinder.GetNumericNodeId(layer.Id)] = layer;
            }

            _contentBounds = FindContentBounds(_page);
            _renderer = new GpuRenderer();
            _renderer.CurrentPage = _page;
            _sceneGraph = new RetainedSceneGraph(_renderer);
            _sceneGraph.Synchronize(_page);
            _visuals = new VisualCollection(this);
            _visuals.Add(_sceneGraph.RootVisual);

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += OnFrameTick;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            MouseMove += OnMouseMove;
            MouseLeave += OnMouseLeave;
            MouseDown += OnMouseDown;
            MouseUp += OnMouseUp;
            SizeChanged += (s, e) => RenderScene();
        }

        protected override int VisualChildrenCount
        {
            get { return _visuals.Count; }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? 1200.0 : Math.Max(1.0, availableSize.Width);
            double scale = width / Math.Max(1.0, _contentBounds.Width);
            if (!double.IsInfinity(availableSize.Height))
                scale = Math.Min(scale, Math.Max(0.0, availableSize.Height) / Math.Max(1.0, _contentBounds.Height));
            scale = Math.Max(0.001, scale);
            return new Size(_contentBounds.Width * scale, _contentBounds.Height * scale);
        }

        protected override Visual GetVisualChild(int index)
        {
            return _visuals[index];
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var background = new SolidColorBrush(GpuRenderer.ParseHexColor(_page.BackgroundColor, 1.0));
            background.Freeze();
            dc.DrawRectangle(background, null, new Rect(0, 0, ActualWidth, ActualHeight));
            UpdateRootTransform();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _clock.Restart();
            if (!_timer.IsEnabled) _timer.Start();
            RenderScene();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_timer.IsEnabled) _timer.Stop();
            DispatchPointerExit();
        }

        private void OnFrameTick(object sender, EventArgs e)
        {
            double dt = Math.Min(0.05, Math.Max(0.0, _clock.Elapsed.TotalSeconds));
            _clock.Restart();
            RestoreRestPose();
            _runtime.Update(dt, _pose);
            RenderScene();
        }

        private void RenderScene()
        {
            if (_sceneGraph == null || ActualWidth <= 0 || ActualHeight <= 0) return;
            _renderer.CurrentPage = _page;
            UpdateRootTransform();
            double scale = Math.Max(0.001, Math.Min(ActualWidth / Math.Max(1.0, _contentBounds.Width),
                                                    ActualHeight / Math.Max(1.0, _contentBounds.Height)));
            _sceneGraph.InvalidateAll();
            _sceneGraph.UpdateAndRender(_contentBounds, scale);
            InvalidateVisual();
        }

        private void UpdateRootTransform()
        {
            if (_sceneGraph == null || ActualWidth <= 0 || ActualHeight <= 0) return;
            double scale = Math.Max(0.001, Math.Min(ActualWidth / Math.Max(1.0, _contentBounds.Width),
                                                    ActualHeight / Math.Max(1.0, _contentBounds.Height)));
            double offsetX = (ActualWidth - _contentBounds.Width * scale) * 0.5 - _contentBounds.X * scale;
            double offsetY = (ActualHeight - _contentBounds.Height * scale) * 0.5 - _contentBounds.Y * scale;
            _rootTransform = new Matrix(scale, 0, 0, scale, offsetX, offsetY);
            _sceneGraph.RootVisual.Transform = new MatrixTransform(_rootTransform);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var stateMachine = _runtime.StateMachine;
            if (stateMachine == null) return;

            System.Windows.Point worldPoint = GetWorldPoint(e);

            var hitLayer = _contentBounds.Contains(worldPoint) ? _sceneGraph.HitTest(worldPoint) : null;
            var currentTargets = new Dictionary<uint, Rect>();
            if (hitLayer != null)
            {
                uint hitId = PropertyBinder.GetNumericNodeId(hitLayer.Id);
                currentTargets[hitId] = hitLayer.GetBounds();
            }

            if (stateMachine.Definition != null && stateMachine.Definition.Listeners != null)
            {
                foreach (var listener in stateMachine.Definition.Listeners)
                {
                    if (listener == null || !listener.Enabled || !listener.IsActive || listener.Kind != PointerEventKind.PointerMove) continue;
                    LayerNode listenerTarget;
                    if (!_layersByNumericId.TryGetValue(listener.TargetNodeId, out listenerTarget) || listenerTarget == null) continue;
                    Rect bounds = listenerTarget.GetBounds();
                    if (bounds.Contains(worldPoint)) currentTargets[listener.TargetNodeId] = bounds;
                }
            }

            foreach (uint oldTarget in _activePointerTargets.ToList())
                if (!currentTargets.ContainsKey(oldTarget)) stateMachine.OnPointerEvent(oldTarget, PointerEventKind.PointerExit);
            foreach (var currentTarget in currentTargets)
            {
                if (!_activePointerTargets.Contains(currentTarget.Key))
                    stateMachine.OnPointerEvent(currentTarget.Key, PointerEventKind.PointerEnter);
                stateMachine.OnPointerEvent(new List<uint> { currentTarget.Key }, PointerEventKind.PointerMove,
                    worldPoint, currentTarget.Value, null);
            }
            _activePointerTargets.Clear();
            foreach (uint targetId in currentTargets.Keys) _activePointerTargets.Add(targetId);

            stateMachine.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.PointerMove, worldPoint, _contentBounds, null);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var stateMachine = _runtime.StateMachine;
            if (stateMachine == null) return;

            Focus();
            Mouse.Capture(this);
            System.Windows.Point worldPoint = GetWorldPoint(e);
            _pressedPointerTargets.Clear();
            foreach (uint targetId in CollectPointerTargets(worldPoint, PointerEventKind.PointerDown, PointerEventKind.DragStart).Keys)
            {
                _pressedPointerTargets.Add(targetId);
                stateMachine.OnPointerEvent(new List<uint> { targetId }, PointerEventKind.PointerDown, worldPoint, _contentBounds, null);
            }
            stateMachine.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.PointerDown, worldPoint, _contentBounds, null);
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            var stateMachine = _runtime.StateMachine;
            if (stateMachine == null) return;

            System.Windows.Point worldPoint = GetWorldPoint(e);
            var currentTargets = CollectPointerTargets(worldPoint, PointerEventKind.PointerUp, PointerEventKind.Click, PointerEventKind.Tap);
            foreach (uint targetId in currentTargets.Keys)
                stateMachine.OnPointerEvent(new List<uint> { targetId }, PointerEventKind.PointerUp, worldPoint, _contentBounds, null);
            stateMachine.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.PointerUp, worldPoint, _contentBounds, null);

            foreach (uint targetId in _pressedPointerTargets)
            {
                if (!currentTargets.ContainsKey(targetId)) continue;
                stateMachine.OnPointerEvent(new List<uint> { targetId }, PointerEventKind.Click, worldPoint, _contentBounds, null);
                stateMachine.OnPointerEvent(new List<uint> { targetId }, PointerEventKind.Tap, worldPoint, _contentBounds, null);
            }
            if (_pressedPointerTargets.Count > 0)
            {
                stateMachine.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.Click, worldPoint, _contentBounds, null);
                stateMachine.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.Tap, worldPoint, _contentBounds, null);
            }
            _pressedPointerTargets.Clear();
            if (Mouse.Captured == this) Mouse.Capture(null);
        }

        private System.Windows.Point GetWorldPoint(MouseEventArgs e)
        {
            var worldPoint = e.GetPosition(this);
            Matrix inverse = _rootTransform;
            if (inverse.HasInverse)
            {
                inverse.Invert();
                worldPoint = inverse.Transform(worldPoint);
            }
            return worldPoint;
        }

        private Dictionary<uint, Rect> CollectPointerTargets(System.Windows.Point worldPoint, params PointerEventKind[] eventKinds)
        {
            var targets = new Dictionary<uint, Rect>();
            if (_sceneGraph == null || !_contentBounds.Contains(worldPoint)) return targets;

            LayerNode hitLayer = _sceneGraph.HitTest(worldPoint);
            if (hitLayer != null)
            {
                uint hitId = PropertyBinder.GetNumericNodeId(hitLayer.Id);
                targets[hitId] = hitLayer.GetBounds();
            }

            var definition = _runtime.StateMachine != null ? _runtime.StateMachine.Definition : null;
            if (definition == null || definition.Listeners == null) return targets;
            foreach (var listener in definition.Listeners)
            {
                if (listener == null || !listener.Enabled || !listener.IsActive ||
                    eventKinds == null || !eventKinds.Contains(listener.Kind)) continue;
                LayerNode target;
                if (!_layersByNumericId.TryGetValue(listener.TargetNodeId, out target) || target == null) continue;
                Rect bounds = target.GetBounds();
                if (bounds.Contains(worldPoint)) targets[listener.TargetNodeId] = bounds;
            }
            return targets;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            DispatchPointerExit();
        }

        private void DispatchPointerExit()
        {
            if (_runtime != null && _runtime.StateMachine != null)
            {
                foreach (uint targetId in _activePointerTargets)
                    _runtime.StateMachine.OnPointerEvent(targetId, PointerEventKind.PointerExit);
            }
            _activePointerTargets.Clear();
            _pressedPointerTargets.Clear();
        }

        private void RestoreRestPose()
        {
            foreach (var entry in _restPose)
            {
                LayerNode layer = entry.Key;
                LayerRestPose rest = entry.Value;
                layer.RenderTransform = rest.Transform != null ? rest.Transform.Clone() : new RenderTransform();
                layer.Opacity = rest.Opacity;
                layer.CornerRadius = rest.CornerRadius;
                layer.CornerRadiusTopLeft = rest.CornerRadiusTopLeft;
                layer.CornerRadiusTopRight = rest.CornerRadiusTopRight;
                layer.CornerRadiusBottomRight = rest.CornerRadiusBottomRight;
                layer.CornerRadiusBottomLeft = rest.CornerRadiusBottomLeft;
                if (layer.Strokes != null && rest.Strokes != null)
                {
                    int count = Math.Min(layer.Strokes.Count, rest.Strokes.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var stroke = layer.Strokes[i];
                        var strokeRest = rest.Strokes[i];
                        if (stroke == null || strokeRest == null) continue;
                        stroke.Width = strokeRest.Width;
                        stroke.TrimStart = strokeRest.TrimStart;
                        stroke.TrimEnd = strokeRest.TrimEnd;
                        stroke.TrimOffset = strokeRest.TrimOffset;
                        stroke.TaperStart = strokeRest.TaperStart;
                        stroke.TaperEnd = strokeRest.TaperEnd;
                    }
                }
            }
        }

        private static LayerRestPose CaptureRestPose(LayerNode layer)
        {
            return new LayerRestPose
            {
                Transform = layer.RenderTransform != null ? layer.RenderTransform.Clone() : new RenderTransform(),
                Opacity = layer.Opacity,
                CornerRadius = layer.CornerRadius,
                CornerRadiusTopLeft = layer.CornerRadiusTopLeft,
                CornerRadiusTopRight = layer.CornerRadiusTopRight,
                CornerRadiusBottomRight = layer.CornerRadiusBottomRight,
                CornerRadiusBottomLeft = layer.CornerRadiusBottomLeft,
                Strokes = layer.Strokes != null
                    ? layer.Strokes.Select(s => s == null ? null : new StrokeRestPose
                    {
                        Width = s.Width,
                        TrimStart = s.TrimStart,
                        TrimEnd = s.TrimEnd,
                        TrimOffset = s.TrimOffset,
                        TaperStart = s.TaperStart,
                        TaperEnd = s.TaperEnd
                    }).ToList()
                    : null
            };
        }

        private static Rect FindContentBounds(Page page)
        {
            var frame = page.GetAllLayersFlat().FirstOrDefault(l => l != null && l.Type == ElementType.Frame && l.Width > 0 && l.Height > 0);
            if (frame != null) return new Rect(frame.X, frame.Y, frame.Width, frame.Height);
            return new Rect(0, 0, 1200, 320);
        }

        private static string WriteEmbeddedPackageToTempFile()
        {
            string path = Path.Combine(Path.GetTempPath(), "KerfBanner-" + Guid.NewGuid().ToString("N") + ".kfx");
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Kerf.KerfBanner"))
            {
                if (resource == null)
                    throw new FileNotFoundException("The bundled KerfBanner package is missing from the Kerf application.");
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    resource.CopyTo(output);
            }
            return path;
        }
    }
}
