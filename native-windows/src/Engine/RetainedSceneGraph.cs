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
            WorldBounds = modelNode != null ? modelNode.GetBounds() : Rect.Empty;
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

        public void ResetVisual()
        {
            Visual = new DrawingVisual();
            IsDirty = true;
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
            foreach (var vNode in _nodeLookup.Values)
            {
                SafeDetachVisual(vNode.Visual);
                vNode.Visual.Children.Clear();
                vNode.Children.Clear();
                vNode.Parent = null;
            }
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
                }
                SafeDetachVisual(vNode.Visual);
                vNode.Visual.Children.Clear();
                vNode.Children.Clear();
                vNode.Parent = null;
                _visualLookup.Remove(vNode.Visual);
                _nodeLookup.Remove(id);
            }

            // CRITICAL: Detach ALL existing visuals from their parents and clear their children
            // before reconstructing the visual hierarchy. In WPF, a Visual can only have one parent.
            // If layers were reordered, reparented, or undone/redone across frames, existing child
            // visuals would still be attached to their old parents, causing:
            // "System.ArgumentException: Specified Visual is already a child of another Visual"
            _rootVisual.Children.Clear();
            foreach (var vNode in _nodeLookup.Values)
            {
                SafeDetachVisual(vNode.Visual);
                vNode.Visual.Children.Clear();
                vNode.Children.Clear();
                vNode.Parent = null;
            }

            // Synchronize tree structure
            var visited = new HashSet<string>();
            foreach (var layer in page.Layers)
            {
                var vNode = SyncLayerHierarchy(layer, null, visited);
                if (vNode != null)
                {
                    SafeAttachVisual(_rootVisual, vNode);
                }
            }
        }

        private static void SafeDetachVisual(Visual child)
        {
            if (child == null) return;
            try
            {
                var parent = VisualTreeHelper.GetParent(child) as ContainerVisual;
                if (parent != null)
                {
                    parent.Children.Remove(child);
                }
            }
            catch
            {
                // Ignore any detach exceptions
            }
        }

        private void SafeAttachVisual(ContainerVisual parent, SceneVisualNode vNode)
        {
            if (parent == null || vNode == null || vNode.Visual == null) return;
            var child = vNode.Visual;
            try
            {
                var currentParent = VisualTreeHelper.GetParent(child) as ContainerVisual;
                if (currentParent != null)
                {
                    if (currentParent == parent && parent.Children.Contains(child))
                    {
                        return; // Already attached to this parent
                    }
                    currentParent.Children.Remove(child);
                }
                if (!parent.Children.Contains(child))
                {
                    parent.Children.Add(child);
                }
            }
            catch (Exception)
            {
                // In the event of lingering visual parenting or multi-thread composition attachment,
                // allocate a fresh, unparented DrawingVisual for this node. A new visual is guaranteed
                // to have no parent, eliminating VisualCollection.Add ArgumentException completely.
                try
                {
                    _visualLookup.Remove(child);
                    vNode.ResetVisual();
                    _visualLookup[vNode.Visual] = vNode;
                    parent.Children.Add(vNode.Visual);
                }
                catch
                {
                    // Fail-safe to guarantee studio never crashes on visual hierarchy sync
                }
            }
        }

        private void CollectNodeIds(IEnumerable<LayerNode> layers, HashSet<string> ids)
        {
            if (layers == null) return;
            foreach (var layer in layers)
            {
                if (layer == null || string.IsNullOrEmpty(layer.Id)) continue;
                if (!ids.Add(layer.Id))
                {
                    // Guard against cyclic data model references
                    continue;
                }
                if (layer.Children != null && layer.Children.Count > 0)
                {
                    CollectNodeIds(layer.Children, ids);
                }
            }
        }

        private SceneVisualNode SyncLayerHierarchy(LayerNode layer, SceneVisualNode parent, HashSet<string> visited)
        {
            if (layer == null || string.IsNullOrEmpty(layer.Id) || visited.Contains(layer.Id))
            {
                return null;
            }
            visited.Add(layer.Id);

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
                vNode.IsDirty = true;
            }

            vNode.Parent = parent;

            // Sync children
            vNode.Children.Clear();
            vNode.Visual.Children.Clear();

            if (layer.Children != null)
            {
                foreach (var child in layer.Children)
                {
                    if (child == null || string.IsNullOrEmpty(child.Id) || visited.Contains(child.Id))
                    {
                        continue;
                    }
                    var childVNode = SyncLayerHierarchy(child, vNode, visited);
                    if (childVNode != null)
                    {
                        vNode.Children.Add(childVNode);
                        SafeAttachVisual(vNode.Visual, childVNode);
                    }
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

        public void InvalidateSubtree(string nodeId)
        {
            SceneVisualNode vNode;
            if (_nodeLookup.TryGetValue(nodeId, out vNode))
            {
                InvalidateNodeRecursive(vNode);
            }
        }

        private void InvalidateNodeRecursive(SceneVisualNode vNode)
        {
            if (vNode == null) return;
            vNode.IsDirty = true;
            for (int i = 0; i < vNode.Children.Count; i++)
            {
                InvalidateNodeRecursive(vNode.Children[i]);
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

        public static Point CanvasToLocalPoint(LayerNode layer, Point canvasPt, Page page = null)
        {
            if (layer == null) return canvasPt;
            var mat = (page != null) ? layer.GetWorldRenderTransformMatrix(page) : layer.GetRenderTransformMatrix();
            if (mat.IsIdentity) return canvasPt;
            if (mat.HasInverse)
            {
                mat.Invert();
                return mat.Transform(canvasPt);
            }
            return canvasPt;
        }

        public static Point LocalToCanvasPoint(LayerNode layer, Point localPt, Page page = null)
        {
            if (layer == null) return localPt;
            var mat = (page != null) ? layer.GetWorldRenderTransformMatrix(page) : layer.GetRenderTransformMatrix();
            if (mat.IsIdentity) return localPt;
            return mat.Transform(localPt);
        }

        private LayerNode HitTestSubtree(SceneVisualNode vNode, Point point)
        {
            if (!vNode.ModelNode.IsVisible || vNode.ModelNode.IsLocked) return null;

            Point localPt = CanvasToLocalPoint(vNode.ModelNode, point);
            var bounds = vNode.WorldBounds.IsEmpty && vNode.ModelNode != null ? vNode.ModelNode.GetBounds() : vNode.WorldBounds;
            // Respect container clipping: if container clips content and point is outside, children cannot be hit
            if (vNode.ModelNode.ClipContent && !bounds.Contains(localPt)) return null;

            // Check children first (topmost child first)
            for (int i = vNode.Children.Count - 1; i >= 0; i--)
            {
                var hit = HitTestSubtree(vNode.Children[i], localPt);
                if (hit != null) return hit;
            }

            // Check self bounds (Groups have no background geometry and cannot be hit in empty space)
            if (vNode.ModelNode.Type != ElementType.Group && bounds.Contains(localPt))
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
