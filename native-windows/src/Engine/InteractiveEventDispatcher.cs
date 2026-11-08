using System;
using System.Windows;
using Kerf.Engine.Animation;

namespace Kerf.Engine
{
    public class InteractiveEventDispatcher
    {
        private uint _lastHoverNodeId = 0u;
        private uint _pointerDownNodeId = 0u;
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

        public void OnPointerMove(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt)
        {
            if (runtime == null || sceneGraph == null) return;
            var hit = sceneGraph.HitTest(canvasPt);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;

            // Hover tracking: Enter and Exit
            if (hitId != _lastHoverNodeId)
            {
                if (_lastHoverNodeId != 0u)
                {
                    runtime.OnPointerEvent(_lastHoverNodeId, PointerEventKind.Exit);
                }
                if (hitId != 0u)
                {
                    runtime.OnPointerEvent(hitId, PointerEventKind.Enter);
                }
                _lastHoverNodeId = hitId;
            }
            else if (hitId != 0u)
            {
                runtime.OnPointerEvent(hitId, PointerEventKind.Move);
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
                    runtime.OnPointerEvent(_pointerDownNodeId, PointerEventKind.DragStart);
                }
                if (_isDragging)
                {
                    runtime.OnPointerEvent(_pointerDownNodeId, PointerEventKind.Drag);
                }
            }
        }

        public void OnPointerDown(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt)
        {
            if (runtime == null || sceneGraph == null) return;
            var hit = sceneGraph.HitTest(canvasPt);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;
            _pointerDownNodeId = hitId;
            _pointerDownPoint = canvasPt;
            _isDragging = false;

            if (hitId != 0u)
            {
                runtime.OnPointerEvent(hitId, PointerEventKind.Down);
            }
        }

        public void OnPointerUp(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt)
        {
            if (runtime == null || sceneGraph == null) return;
            var hit = sceneGraph.HitTest(canvasPt);
            uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;

            if (_isDragging)
            {
                if (_pointerDownNodeId != 0u)
                {
                    runtime.OnPointerEvent(_pointerDownNodeId, PointerEventKind.DragEnd);
                }
                _isDragging = false;
            }
            else if (_pointerDownNodeId != 0u && _pointerDownNodeId == hitId)
            {
                runtime.OnPointerEvent(hitId, PointerEventKind.Click);
                runtime.OnPointerEvent(hitId, PointerEventKind.Tap);
            }

            if (hitId != 0u)
            {
                runtime.OnPointerEvent(hitId, PointerEventKind.Up);
            }
            _pointerDownNodeId = 0u;
        }

        public void Reset()
        {
            _lastHoverNodeId = 0u;
            _pointerDownNodeId = 0u;
            _isDragging = false;
        }
    }
}
