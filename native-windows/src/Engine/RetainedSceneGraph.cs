using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Kerf.Model;

namespace Kerf.Engine
{
    public class SceneDiagnostics
    {
        public int TotalNodes { get; set; }
        public int VisibleNodes { get; set; }
        public int CulledNodes { get; set; }
        public int CacheHits { get; set; }
        public int RebuiltNodes { get; set; }
        public double FrameTimeMicroseconds { get; set; }
        public int RenderingTier { get; set; }
        public string HardwareRenderer { get; set; }
        public bool IsHardwareAccelerated { get; set; }
        public long Timestamp { get; set; }

        public override string ToString()
        {
            string rustBadge = RustEngineInterop.IsAvailable
                ? string.Format(" | 🚀 Rust Core ({0})", RustEngineInterop.SimdDescription)
                : "";

            return string.Format("GPU Tier {0} ({1}){2} | {3:F1} µs | Vis: {4}/{5} | Culled: {6}",
                RenderingTier,
                IsHardwareAccelerated ? "Hardware Direct3D" : "Software Fallback",
                rustBadge,
                FrameTimeMicroseconds,
                VisibleNodes,
                TotalNodes,
                CulledNodes);
        }
    }

    public class SceneVisualNode
    {
        public LayerNode ModelNode { get; set; }
        public DrawingVisual Visual { get; private set; }
        public Rect WorldBounds { get; set; }
        public bool IsDirty { get; set; }
        public bool IsVisibleInViewport { get; set; }
        public List<SceneVisualNode> Children { get; private set; }
        public SceneVisualNode Parent { get; set; }

        public SceneVisualNode(LayerNode modelNode)
        {
            ModelNode = modelNode;
            Visual = new DrawingVisual();
            Children = new List<SceneVisualNode>();
            IsDirty = true;
            IsVisibleInViewport = true;
        }

        public void Invalidate()
        {
            IsDirty = true;
            if (Parent != null)
            {
                // Parent bounds might change if child moved
                Parent.IsDirty = true;
            }
        }
    }

    public class RetainedSceneGraph
    {
        private readonly ContainerVisual _rootVisual;
        private readonly Dictionary<string, SceneVisualNode> _nodeLookup;
        private readonly Dictionary<Visual, SceneVisualNode> _visualLookup;
        private readonly GpuRenderer _renderer;
        private readonly Stopwatch _frameStopwatch;

        public ContainerVisual RootVisual { get { return _rootVisual; } }
        public SceneDiagnostics LastDiagnostics { get; private set; }

        public RetainedSceneGraph(GpuRenderer renderer)
        {
            if (renderer == null) throw new ArgumentNullException("renderer");
            _renderer = renderer;
            _rootVisual = new ContainerVisual();
            _nodeLookup = new Dictionary<string, SceneVisualNode>();
            _visualLookup = new Dictionary<Visual, SceneVisualNode>();
            _frameStopwatch = new Stopwatch();
            LastDiagnostics = new SceneDiagnostics();
        }

        public void Clear()
        {
            _rootVisual.Children.Clear();
            _nodeLookup.Clear();
            _visualLookup.Clear();
        }

        public void Synchronize(Page page)
        {
            if (page == null)
            {
                Clear();
                return;
            }

            _renderer.CurrentPage = page;

            var currentIds = new HashSet<string>();
            CollectNodeIds(page.Layers, currentIds);

            // Remove visual nodes no longer in model
            var toRemove = new List<string>();
            foreach (var kvp in _nodeLookup)
            {
                if (!currentIds.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                var vNode = _nodeLookup[id];
                if (vNode.Parent != null)
                {
                    vNode.Parent.Children.Remove(vNode);
                    vNode.Parent.Visual.Children.Remove(vNode.Visual);
                }
                _visualLookup.Remove(vNode.Visual);
                _nodeLookup.Remove(id);
            }

            // Synchronize tree structure
            _rootVisual.Children.Clear();
            foreach (var layer in page.Layers)
            {
                var vNode = SyncLayerHierarchy(layer, null);
                _rootVisual.Children.Add(vNode.Visual);
            }
        }

        private void CollectNodeIds(IEnumerable<LayerNode> layers, HashSet<string> ids)
        {
            foreach (var layer in layers)
            {
                ids.Add(layer.Id);
                if (layer.Children != null && layer.Children.Count > 0)
                {
                    CollectNodeIds(layer.Children, ids);
                }
            }
        }

        private SceneVisualNode SyncLayerHierarchy(LayerNode layer, SceneVisualNode parent)
        {
            SceneVisualNode vNode;
            if (!_nodeLookup.TryGetValue(layer.Id, out vNode))
            {
                vNode = new SceneVisualNode(layer);
                _nodeLookup[layer.Id] = vNode;
                _visualLookup[vNode.Visual] = vNode;
            }
            else
            {
                vNode.ModelNode = layer;
            }

            vNode.Parent = parent;

            // Sync children
            vNode.Children.Clear();
            vNode.Visual.Children.Clear();

            if (layer.Children != null)
            {
                foreach (var child in layer.Children)
                {
                    var childVNode = SyncLayerHierarchy(child, vNode);
                    vNode.Children.Add(childVNode);
                    vNode.Visual.Children.Add(childVNode.Visual);
                }
            }

            return vNode;
        }

        public void InvalidateNode(string nodeId)
        {
            SceneVisualNode vNode;
            if (_nodeLookup.TryGetValue(nodeId, out vNode))
            {
                vNode.Invalidate();
            }
        }

        public void InvalidateAll()
        {
            foreach (var vNode in _nodeLookup.Values)
            {
                vNode.IsDirty = true;
            }
        }

        public SceneDiagnostics UpdateAndRender(Rect viewport, double zoomScale)
        {
            _frameStopwatch.Restart();

            int totalNodes = 0;
            int visibleNodes = 0;
            int culledNodes = 0;
            int cacheHits = 0;
            int rebuiltNodes = 0;

            // Expand viewport slightly as prefetch buffer (200px)
            Rect cullRect = new Rect(
                viewport.X - 200 / zoomScale,
                viewport.Y - 200 / zoomScale,
                viewport.Width + 400 / zoomScale,
                viewport.Height + 400 / zoomScale
            );

            // Traverse root visual children
            for (int i = 0; i < _rootVisual.Children.Count; i++)
            {
                var visual = _rootVisual.Children[i] as DrawingVisual;
                if (visual == null) continue;

                // Find matching node
                var vNode = FindNodeByVisual(visual);
                if (vNode != null)
                {
                    RenderSubtree(vNode, cullRect, zoomScale, ref totalNodes, ref visibleNodes, ref culledNodes, ref cacheHits, ref rebuiltNodes);
                }
            }

            _frameStopwatch.Stop();
            double frameMicros = (_frameStopwatch.ElapsedTicks * 1000000.0) / Stopwatch.Frequency;

            int renderingTier = RenderCapability.Tier >> 16;
            bool isHardware = renderingTier >= 2;

            LastDiagnostics = new SceneDiagnostics
            {
                TotalNodes = totalNodes,
                VisibleNodes = visibleNodes,
                CulledNodes = culledNodes,
                CacheHits = cacheHits,
                RebuiltNodes = rebuiltNodes,
                FrameTimeMicroseconds = frameMicros,
                RenderingTier = renderingTier,
                IsHardwareAccelerated = isHardware,
                HardwareRenderer = isHardware ? "Direct3D 11 / milcore" : (renderingTier == 1 ? "Partial HW / Software" : "Direct3D Tier 0 (Software Rasterizer)"),
                Timestamp = Stopwatch.GetTimestamp()
            };

            return LastDiagnostics;
        }

        private void RenderSubtree(
            SceneVisualNode node,
            Rect cullRect,
            double zoomScale,
            ref int total,
            ref int visible,
            ref int culled,
            ref int cacheHits,
            ref int rebuilt)
        {
            total++;

            // Calculate world bounds of node
            var bounds = node.ModelNode.GetBounds();
            node.WorldBounds = bounds;

            // Spatial culling test
            bool intersects = cullRect.IntersectsWith(bounds);
            node.IsVisibleInViewport = intersects && node.ModelNode.IsVisible;

            if (!node.IsVisibleInViewport)
            {
                culled++;
                if (node.Visual.Opacity > 0.0)
                {
                    node.Visual.Opacity = 0.0; // Hide culled visual cheaply
                }
                return;
            }

            visible++;
            if (node.Visual.Opacity != node.ModelNode.Opacity)
            {
                node.Visual.Opacity = node.ModelNode.Opacity;
            }

            if (node.IsDirty)
            {
                rebuilt++;
                _renderer.RenderNodeToVisual(node.ModelNode, node.Visual, zoomScale);
                node.IsDirty = false;
            }
            else
            {
                cacheHits++;
            }

            // Render children
            for (int i = 0; i < node.Children.Count; i++)
            {
                RenderSubtree(node.Children[i], cullRect, zoomScale, ref total, ref visible, ref culled, ref cacheHits, ref rebuilt);
            }
        }

        private SceneVisualNode FindNodeByVisual(DrawingVisual visual)
        {
            if (visual == null) return null;
            SceneVisualNode node;
            if (_visualLookup.TryGetValue(visual, out node)) return node;
            return null;
        }

        public LayerNode HitTest(Point canvasPoint)
        {
            // Reverse search (topmost layer first)
            for (int i = _rootVisual.Children.Count - 1; i >= 0; i--)
            {
                var visual = _rootVisual.Children[i] as DrawingVisual;
                if (visual == null) continue;

                var vNode = FindNodeByVisual(visual);
                if (vNode != null)
                {
                    var hit = HitTestSubtree(vNode, canvasPoint);
                    if (hit != null) return hit;
                }
            }
            return null;
        }

        private LayerNode HitTestSubtree(SceneVisualNode vNode, Point point)
        {
            if (!vNode.ModelNode.IsVisible || vNode.ModelNode.IsLocked) return null;

            // Check children first (topmost child first)
            for (int i = vNode.Children.Count - 1; i >= 0; i--)
            {
                var hit = HitTestSubtree(vNode.Children[i], point);
                if (hit != null) return hit;
            }

            // Check self bounds
            var bounds = vNode.WorldBounds;
            if (bounds.Contains(point))
            {
                return vNode.ModelNode;
            }

            return null;
        }

        public SceneVisualNode GetVisualNode(string id)
        {
            SceneVisualNode node;
            _nodeLookup.TryGetValue(id, out node);
            return node;
        }
    }
}
