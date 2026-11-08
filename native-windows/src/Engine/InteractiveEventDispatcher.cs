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
        private bool _isDragging = false;
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
                for (int i = 0; i < _lastHoverHierarchy.Count; i++)
                {
                    uint oldId = _lastHoverHierarchy[i];
                    if (!hierarchy.Contains(oldId))
                    {
                        runtime.OnPointerEvent(oldId, PointerEventKind.Exit);
                    }
                }

                for (int i = 0; i < hierarchy.Count; i++)
                {
                    uint newId = hierarchy[i];
                    if (!_lastHoverHierarchy.Contains(newId))
                    {
                        runtime.OnPointerEvent(newId, PointerEventKind.Enter);
                    }
                    else
                    {
                        runtime.OnPointerEvent(newId, PointerEventKind.Move);
                    }
                }

                _lastHoverHierarchy.Clear();
                _lastHoverHierarchy.AddRange(hierarchy);
                _lastHoverNodeId = hitId;
            }
            else if (hierarchy.Count > 0)
            {
                runtime.OnPointerEvent(hierarchy, PointerEventKind.Move);
            }

            // Drag tracking
            if (_pointerDownNodeId != 0u)
            {
                double dx = canvasPt.X - _pointerDownPoint.X;
                double dy = canvasPt.Y - _pointerDownPoint.Y;
                double distSq = dx * dx + dy * dy;

                if (!_isDragging && distSq >= DragThreshold * DragThreshold)
                {
                    _isDragging = true;
                    var dragTargets = _pointerDownHierarchy.Count > 0 ? (IList<uint>)_pointerDownHierarchy : new List<uint> { _pointerDownNodeId };
                    runtime.OnPointerEvent(dragTargets, PointerEventKind.DragStart);
                }
                if (_isDragging)
                {
                    var dragTargets = _pointerDownHierarchy.Count > 0 ? (IList<uint>)_pointerDownHierarchy : new List<uint> { _pointerDownNodeId };
                    runtime.OnPointerEvent(dragTargets, PointerEventKind.Drag);
                }
            }
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
            _isDragging = false;

            var targets = hierarchy.Count > 0 ? (IList<uint>)hierarchy : new List<uint> { hitId };
            runtime.OnPointerEvent(targets, PointerEventKind.Down);
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
                    var dragTargets = _pointerDownHierarchy.Count > 0 ? (IList<uint>)_pointerDownHierarchy : new List<uint> { _pointerDownNodeId };
                    runtime.OnPointerEvent(dragTargets, PointerEventKind.DragEnd);
                }
                _isDragging = false;
            }
            else
            {
                bool isClickMatch = false;
                if (_pointerDownNodeId == hitId)
                {
                    isClickMatch = true;
                }
                else if (hitId != 0u && _pointerDownHierarchy.Contains(hitId))
                {
                    isClickMatch = true;
                }
                else if (_pointerDownNodeId != 0u && hierarchy.Contains(_pointerDownNodeId))
                {
                    isClickMatch = true;
                }

                if (isClickMatch)
                {
                    var clickTargets = hierarchy.Count > 0 ? (IList<uint>)hierarchy : new List<uint> { hitId };
                    runtime.OnPointerEvent(clickTargets, PointerEventKind.Click);
                    runtime.OnPointerEvent(clickTargets, PointerEventKind.Tap);
                }
            }

            var upTargets = hierarchy.Count > 0 ? (IList<uint>)hierarchy : new List<uint> { hitId };
            runtime.OnPointerEvent(upTargets, PointerEventKind.Up);
            _pointerDownNodeId = 0u;
            _pointerDownHierarchy.Clear();
        }

        public void OnClick(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt, Page page = null)
        {
            if (runtime == null) return;
            List<uint> hierarchy;
            LayerNode hit = ResolveHit(sceneGraph, page, canvasPt, out hierarchy);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;
            var targets = hierarchy.Count > 0 ? (IList<uint>)hierarchy : new List<uint> { hitId };
            runtime.OnPointerEvent(targets, PointerEventKind.Click);
        }

        public void OnTap(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt, Page page = null)
        {
            if (runtime == null) return;
            List<uint> hierarchy;
            LayerNode hit = ResolveHit(sceneGraph, page, canvasPt, out hierarchy);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;
            var targets = hierarchy.Count > 0 ? (IList<uint>)hierarchy : new List<uint> { hitId };
            runtime.OnPointerEvent(targets, PointerEventKind.Tap);
        }

        public void Reset()
        {
            _lastHoverNodeId = 0u;
            _lastHoverHierarchy.Clear();
            _pointerDownNodeId = 0u;
            _pointerDownHierarchy.Clear();
            _isDragging = false;
        }
    }
}
