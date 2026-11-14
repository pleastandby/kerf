using System;
using System.Collections.Generic;
using System.Windows;
using Kerf.Engine.Animation;
using Kerf.Model;
using Kerf.Tools;

namespace Kerf.Engine
{
    public class InteractiveEventDispatcher
    {
        private uint _lastHoverNodeId = 0u;
        private readonly List<uint> _lastHoverHierarchy = new List<uint>();
        private uint _pointerDownNodeId = 0u;
        private readonly List<uint> _pointerDownHierarchy = new List<uint>();
        private Point _pointerDownPoint;
        private bool _pointerIsDown = false;
        private bool _isDragging = false;
        private Page _canvasBoundsPage;
        private Rect? _cachedCanvasBounds;
        private const double DragThreshold = 4.0;

        public uint LastHoverNodeId
        {
            get { return _lastHoverNodeId; }
        }

        public uint PointerDownNodeId
        {
            get { return _pointerDownNodeId; }
        }

        public bool IsDragging
        {
            get { return _isDragging; }
        }

        private LayerNode ResolveHit(RetainedSceneGraph sceneGraph, Page page, Point canvasPt, out List<uint> hierarchy)
        {
            hierarchy = new List<uint>();
            LayerNode hit = null;

            if (page != null)
            {
                var hitRes = CanvasInteractionController.HitTest(page, canvasPt);
                hit = hitRes.DeepestNode ?? hitRes.OutermostNode;
                if (hitRes.HierarchyPath != null)
                {
                    for (int i = 0; i < hitRes.HierarchyPath.Count; i++)
                    {
                        var n = hitRes.HierarchyPath[i];
                        if (n != null)
                        {
                            uint nid = PropertyBinder.GetNumericNodeId(n.Id);
                            if (nid != 0u && !hierarchy.Contains(nid)) hierarchy.Add(nid);
                        }
                    }
                }
            }

            if (hit == null && sceneGraph != null)
            {
                hit = sceneGraph.HitTest(canvasPt);
            }

            if (hit != null)
            {
                uint hitId = PropertyBinder.GetNumericNodeId(hit.Id);
                if (hitId != 0u && !hierarchy.Contains(hitId)) hierarchy.Add(hitId);
            }

            return hit;
        }

        private static Rect? GetLayerBounds(LayerNode layer)
        {
            if (layer == null) return null;
            return new Rect(layer.X, layer.Y, Math.Max(1.0, layer.Width), Math.Max(1.0, layer.Height));
        }

        private Rect? GetCanvasBounds(Page page)
        {
            if (page == null) return null;
            if (!ReferenceEquals(page, _canvasBoundsPage) || !_cachedCanvasBounds.HasValue)
            {
                _canvasBoundsPage = page;
                _cachedCanvasBounds = HighQualityImageRenderer.ComputeVisualBounds(page, null);
            }
            return _cachedCanvasBounds;
        }

        private static IList<uint> GetLocalTargets(List<uint> hierarchy, uint hitId)
        {
            if (hierarchy != null && hierarchy.Count > 0) return hierarchy;
            return hitId != 0u ? (IList<uint>)new List<uint> { hitId } : new List<uint>();
        }

        private static bool AreHierarchiesEqual(List<uint> a, List<uint> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        public void OnPointerMove(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt, Page page = null)
        {
            if (runtime == null) return;
            List<uint> hierarchy;
            LayerNode hit = ResolveHit(sceneGraph, page, canvasPt, out hierarchy);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;

            // Hover tracking: Enter and Exit per node in hierarchy
            if (hitId != _lastHoverNodeId || !AreHierarchiesEqual(_lastHoverHierarchy, hierarchy))
            {
                Rect? targetBounds = GetLayerBounds(hit);

                for (int i = 0; i < _lastHoverHierarchy.Count; i++)
                {
                    uint oldId = _lastHoverHierarchy[i];
                    if (!hierarchy.Contains(oldId))
                    {
                        runtime.OnPointerEvent(new List<uint> { oldId }, PointerEventKind.Exit, canvasPt, targetBounds, null);
                    }
                }

                for (int i = 0; i < hierarchy.Count; i++)
                {
                    uint newId = hierarchy[i];
                    if (!_lastHoverHierarchy.Contains(newId))
                    {
                        runtime.OnPointerEvent(new List<uint> { newId }, PointerEventKind.Enter, canvasPt, targetBounds, null);
                        runtime.OnPointerEvent(new List<uint> { newId }, PointerEventKind.Move, canvasPt, targetBounds, null);
                    }
                    else
                    {
                        runtime.OnPointerEvent(new List<uint> { newId }, PointerEventKind.Move, canvasPt, targetBounds, null);
                    }
                }

                _lastHoverHierarchy.Clear();
                _lastHoverHierarchy.AddRange(hierarchy);
                _lastHoverNodeId = hitId;
            }
            else if (hierarchy.Count > 0)
            {
                Rect? targetBounds = GetLayerBounds(hit);
                runtime.OnPointerEvent(hierarchy, PointerEventKind.Move, canvasPt, targetBounds, null);
            }

            // Also fire global/canvas-wide PointerMove listeners (TargetNodeId == 0)
            runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.Move, canvasPt, GetCanvasBounds(page), null);

            // Drag tracking
            if (_pointerIsDown)
            {
                double dx = canvasPt.X - _pointerDownPoint.X;
                double dy = canvasPt.Y - _pointerDownPoint.Y;
                double distSq = dx * dx + dy * dy;

                if (!_isDragging && distSq >= DragThreshold * DragThreshold)
                {
                    _isDragging = true;
                    var dragTargets = GetLocalTargets(_pointerDownHierarchy, _pointerDownNodeId);
                    if (dragTargets.Count > 0) runtime.OnPointerEvent(dragTargets, PointerEventKind.DragStart, canvasPt, GetLayerBounds(hit), null);
                    runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.DragStart, canvasPt, GetCanvasBounds(page), null);
                }
                if (_isDragging)
                {
                    var dragTargets = GetLocalTargets(_pointerDownHierarchy, _pointerDownNodeId);
                    if (dragTargets.Count > 0) runtime.OnPointerEvent(dragTargets, PointerEventKind.Drag, canvasPt, GetLayerBounds(hit), null);
                    runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.Drag, canvasPt, GetCanvasBounds(page), null);
                }
            }
        }

        public void OnPointerLeave(StateMachineRuntime runtime)
        {
            if (runtime == null) return;
            for (int i = 0; i < _lastHoverHierarchy.Count; i++)
            {
                runtime.OnPointerEvent(new List<uint> { _lastHoverHierarchy[i] }, PointerEventKind.PointerExit, null, null, null);
            }
            runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.PointerExit, null, null, null);
            _lastHoverHierarchy.Clear();
            _lastHoverNodeId = 0u;
        }

        public void OnPointerDown(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt, Page page = null)
        {
            if (runtime == null) return;
            List<uint> hierarchy;
            LayerNode hit = ResolveHit(sceneGraph, page, canvasPt, out hierarchy);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;

            _pointerDownNodeId = hitId;
            _pointerDownHierarchy.Clear();
            _pointerDownHierarchy.AddRange(hierarchy);
            _pointerDownPoint = canvasPt;
            _pointerIsDown = true;
            _isDragging = false;

            if (hitId != 0u)
            {
                runtime.OnFocus(hitId);
            }
            else if (runtime.FocusedNodeId != 0u)
            {
                runtime.OnBlur(runtime.FocusedNodeId);
            }

            var targets = GetLocalTargets(hierarchy, hitId);
            if (targets.Count > 0) runtime.OnPointerEvent(targets, PointerEventKind.Down, canvasPt, GetLayerBounds(hit), null);
            runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.PointerDown, canvasPt, GetCanvasBounds(page), null);
        }

        public void OnPointerUp(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt, Page page = null)
        {
            if (runtime == null) return;
            List<uint> hierarchy;
            LayerNode hit = ResolveHit(sceneGraph, page, canvasPt, out hierarchy);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;

            if (_isDragging)
            {
                if (_pointerDownNodeId != 0u || _pointerDownHierarchy.Count > 0)
                {
                    var dragTargets = GetLocalTargets(_pointerDownHierarchy, _pointerDownNodeId);
                    if (dragTargets.Count > 0) runtime.OnPointerEvent(dragTargets, PointerEventKind.DragEnd, canvasPt, GetLayerBounds(hit), null);
                }
                runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.DragEnd, canvasPt, GetCanvasBounds(page), null);
                _isDragging = false;
            }
            else
            {
                bool hasDownTarget = _pointerDownNodeId != 0u || _pointerDownHierarchy.Count > 0;
                bool isClickMatch = !hasDownTarget && hitId == 0u;
                if (hasDownTarget && _pointerDownNodeId == hitId)
                {
                    isClickMatch = true;
                }
                else if (hasDownTarget && hitId != 0u && _pointerDownHierarchy.Contains(hitId))
                {
                    isClickMatch = true;
                }
                else if (hasDownTarget && _pointerDownNodeId != 0u && hierarchy.Contains(_pointerDownNodeId))
                {
                    isClickMatch = true;
                }

                if (isClickMatch)
                {
                    var clickTargets = GetLocalTargets(hierarchy, hitId);
                    if (clickTargets.Count > 0)
                    {
                        runtime.OnPointerEvent(clickTargets, PointerEventKind.Click, canvasPt, GetLayerBounds(hit), null);
                        runtime.OnPointerEvent(clickTargets, PointerEventKind.Tap, canvasPt, GetLayerBounds(hit), null);
                    }
                    runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.Click, canvasPt, GetCanvasBounds(page), null);
                    runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.Tap, canvasPt, GetCanvasBounds(page), null);
                }
            }

            var upTargets = GetLocalTargets(hierarchy, hitId);
            if (upTargets.Count > 0) runtime.OnPointerEvent(upTargets, PointerEventKind.Up, canvasPt, GetLayerBounds(hit), null);
            runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.PointerUp, canvasPt, GetCanvasBounds(page), null);
            _pointerDownNodeId = 0u;
            _pointerDownHierarchy.Clear();
            _pointerIsDown = false;
        }

        public void OnClick(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt, Page page = null)
        {
            if (runtime == null) return;
            List<uint> hierarchy;
            LayerNode hit = ResolveHit(sceneGraph, page, canvasPt, out hierarchy);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;
            var targets = GetLocalTargets(hierarchy, hitId);
            if (targets.Count > 0) runtime.OnPointerEvent(targets, PointerEventKind.Click, canvasPt, GetLayerBounds(hit), null);
            runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.Click, canvasPt, GetCanvasBounds(page), null);
        }

        public void OnTap(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt, Page page = null)
        {
            if (runtime == null) return;
            List<uint> hierarchy;
            LayerNode hit = ResolveHit(sceneGraph, page, canvasPt, out hierarchy);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;
            var targets = GetLocalTargets(hierarchy, hitId);
            if (targets.Count > 0) runtime.OnPointerEvent(targets, PointerEventKind.Tap, canvasPt, GetLayerBounds(hit), null);
            runtime.OnPointerEvent(new List<uint> { 0u }, PointerEventKind.Tap, canvasPt, GetCanvasBounds(page), null);
        }

        public void OnKeyDown(StateMachineRuntime runtime, string key)
        {
            if (runtime == null) return;
            runtime.OnKeyDown(key);
        }

        public void OnTextInput(StateMachineRuntime runtime, string text)
        {
            if (runtime == null) return;
            runtime.OnTextInput(text);
        }

        public void OnFocus(StateMachineRuntime runtime, uint nodeId)
        {
            if (runtime == null) return;
            runtime.OnFocus(nodeId);
        }

        public void OnBlur(StateMachineRuntime runtime, uint nodeId)
        {
            if (runtime == null) return;
            runtime.OnBlur(nodeId);
        }

        public void OnGamepad(StateMachineRuntime runtime, string button)
        {
            if (runtime == null) return;
            runtime.OnGamepad(button);
        }

        public void OnSemanticAction(StateMachineRuntime runtime, string actionName)
        {
            if (runtime == null) return;
            runtime.OnSemanticAction(actionName);
        }

        public void Reset()
        {
            _lastHoverNodeId = 0u;
            _lastHoverHierarchy.Clear();
            _pointerDownNodeId = 0u;
            _pointerDownHierarchy.Clear();
            _pointerIsDown = false;
            _isDragging = false;
            _canvasBoundsPage = null;
            _cachedCanvasBounds = null;
        }
    }
}
