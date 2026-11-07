using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Kerf.Model;

namespace Kerf.Tools
{
    /// <summary>
    /// Structured result of a hierarchical canvas hit-test.
    /// Encapsulates deepest and outermost selectable layers,
    /// the full ancestor hierarchy path, and all overlapping candidates.
    /// </summary>
    public class CanvasHitTestResult
    {
        /// <summary>
        /// Deepest visible, unlocked selectable leaf or inner container under the pointer.
        /// </summary>
        public LayerNode DeepestNode { get; set; }

        /// <summary>
        /// Outermost root-level container or top-level element containing the hit point.
        /// </summary>
        public LayerNode OutermostNode { get; set; }

        /// <summary>
        /// Full ancestor hierarchy from outermost root container down to DeepestNode.
        /// [OutermostNode, ..., DeepestNode]
        /// </summary>
        public List<LayerNode> HierarchyPath { get; set; }

        /// <summary>
        /// All eligible candidate objects under the pointer in z-order (topmost to bottommost),
        /// respecting clipping, visibility, and locked state.
        /// </summary>
        public List<LayerNode> Candidates { get; set; }

        /// <summary>
        /// True if any selectable layer was hit.
        /// </summary>
        public bool HasHit
        {
            get { return DeepestNode != null; }
        }

        public CanvasHitTestResult()
        {
            HierarchyPath = new List<LayerNode>();
            Candidates = new List<LayerNode>();
        }
    }

    /// <summary>
    /// Centralized Canvas Interaction Controller.
    /// Consolidates pointer interaction into a shared, predictable pipeline:
    /// Pointer Down -> Hierarchical Hit Testing -> Selection Resolution -> Drag Target Resolution -> Pointer Move -> Transform Application -> Pointer Up -> Commit History.
    /// Provides Figma-like hierarchical selection and manipulation across Frames, Groups, Auto Layout, and Shapes.
    /// </summary>
    public static class CanvasInteractionController
    {
        public const double DragThreshold = 3.0;

        /// <summary>
        /// Hierarchical hit-testing that traverses the document scene graph.
        /// Identifies all eligible objects beneath the pointer in z-order,
        /// builds complete ancestor hierarchies, and strictly respects clipping, visibility, and locked status.
        /// </summary>
        public static CanvasHitTestResult HitTest(Page page, Point canvasPt)
        {
            var result = new CanvasHitTestResult();
            if (page == null || page.Layers == null || page.Layers.Count == 0)
            {
                return result;
            }

            var path = new List<LayerNode>();

            // Top-level layers in reverse z-order (topmost on screen first)
            for (int i = page.Layers.Count - 1; i >= 0; i--)
            {
                var topLayer = page.Layers[i];
                if (CheckSubtree(topLayer, canvasPt, path, result))
                {
                    break;
                }
            }

            // Also collect all eligible overlapping candidates under the pointer
            for (int i = page.Layers.Count - 1; i >= 0; i--)
            {
                CollectCandidates(page.Layers[i], canvasPt, result.Candidates);
            }

            return result;
        }

        private static bool CheckSubtree(LayerNode node, Point pt, List<LayerNode> currentPath, CanvasHitTestResult result)
        {
            if (node == null || !node.IsVisible || node.IsLocked)
            {
                return false;
            }

            bool selfContains = ContainsPoint(node, pt);

            // Container clipping: if container clips content and pointer is outside container,
            // none of its children can be hit.
            if (node.ClipContent && !selfContains)
            {
                return false;
            }

            currentPath.Add(node);

            // Test children in reverse z-order (topmost child first)
            if (node.Children != null && node.Children.Count > 0)
            {
                for (int i = node.Children.Count - 1; i >= 0; i--)
                {
                    if (CheckSubtree(node.Children[i], pt, currentPath, result))
                    {
                        return true;
                    }
                }
            }

            // If no child was hit, test if this container / shape itself contains the point
            // Note: Groups have no background geometry and cannot be hit in empty space
            if (selfContains && node.Type != ElementType.Group)
            {
                result.DeepestNode = node;
                result.OutermostNode = currentPath[0];
                result.HierarchyPath = new List<LayerNode>(currentPath);
                return true;
            }

            currentPath.RemoveAt(currentPath.Count - 1);
            return false;
        }

        private static void CollectCandidates(LayerNode node, Point pt, List<LayerNode> candidates)
        {
            if (node == null || !node.IsVisible || node.IsLocked) return;

            bool selfContains = ContainsPoint(node, pt);
            if (node.ClipContent && !selfContains) return;

            if (node.Children != null && node.Children.Count > 0)
            {
                for (int i = node.Children.Count - 1; i >= 0; i--)
                {
                    CollectCandidates(node.Children[i], pt, candidates);
                }
            }

            if (selfContains && node.Type != ElementType.Group && !candidates.Contains(node))
            {
                candidates.Add(node);
            }
        }

        /// <summary>
        /// Exact 2D point containment test respecting layer dimensions and rotation.
        /// </summary>
        public static bool ContainsPoint(LayerNode layer, Point pt)
        {
            if (layer == null || layer.Width <= 0 || layer.Height <= 0) return false;

            if (layer.RenderTransform != null && !layer.RenderTransform.IsIdentity)
            {
                var rtMat = layer.GetRenderTransformMatrix();
                if (rtMat.HasInverse)
                {
                    rtMat.Invert();
                    pt = rtMat.Transform(pt);
                }
            }

            if (Math.Abs(layer.Rotation) < 0.001)
            {
                return pt.X >= layer.X && pt.X <= layer.X + layer.Width &&
                       pt.Y >= layer.Y && pt.Y <= layer.Y + layer.Height;
            }

            double cx = layer.X + layer.Width / 2.0;
            double cy = layer.Y + layer.Height / 2.0;
            double rad = -layer.Rotation * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double dx = pt.X - cx;
            double dy = pt.Y - cy;
            double unrotX = cx + (dx * cos - dy * sin);
            double unrotY = cy + (dx * sin + dy * cos);

            return unrotX >= layer.X && unrotX <= layer.X + layer.Width &&
                   unrotY >= layer.Y && unrotY <= layer.Y + layer.Height;
        }

        /// <summary>
        /// Finds the deepest ancestor in hitResult.HierarchyPath that is in currentSelection.
        /// </summary>
        public static LayerNode FindSelectedAncestor(CanvasHitTestResult hitResult, IList<LayerNode> currentSelection)
        {
            if (hitResult == null || hitResult.HierarchyPath == null || currentSelection == null || currentSelection.Count == 0)
            {
                return null;
            }

            // Reverse search: pick the deepest selected node in the ancestor path
            for (int i = hitResult.HierarchyPath.Count - 1; i >= 0; i--)
            {
                var candidate = hitResult.HierarchyPath[i];
                if (currentSelection.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Selection Resolver:
        /// Determines which object should become selected.
        /// Supports progressive parent-to-child selection, Ctrl deep-selection,
        /// and maintains the current hierarchy context.
        /// </summary>
        public static LayerNode ResolveSelection(
            CanvasHitTestResult hitResult,
            IList<LayerNode> currentSelection,
            int clickCount,
            bool isCtrl,
            bool isShift)
        {
            if (hitResult == null || !hitResult.HasHit)
            {
                return null;
            }

            // 1. Explicit Direct Deep-Selection (Ctrl held)
            if (isCtrl)
            {
                return hitResult.DeepestNode;
            }

            var path = hitResult.HierarchyPath;
            var selectedAncestor = FindSelectedAncestor(hitResult, currentSelection);

            // 2. An ancestor in the hierarchy is already in the active selection
            if (selectedAncestor != null)
            {
                int selectedIndex = path.IndexOf(selectedAncestor);
                if (clickCount >= 2 && selectedIndex >= 0 && selectedIndex + 1 < path.Count)
                {
                    // Progressive double-click: drill down one level into child
                    return path[selectedIndex + 1];
                }

                // Single click over an already-selected parent (or any of its descendants):
                // Preserve the currently selected parent!
                return selectedAncestor;
            }

            // 3. Clicking from outside (no ancestor currently selected)
            if (clickCount >= 2 && path.Count > 1)
            {
                // Rapid double-click from outside drills down to child
                return path[1];
            }

            // Default single-click from outside: select outermost container
            return hitResult.OutermostNode ?? hitResult.DeepestNode;
        }

        /// <summary>
        /// Drag Resolver:
        /// Determines which objects should receive the transformation.
        /// Uses the active selection as the drag target.
        /// Once a parent is selected, pointer-down over its descendants initiates
        /// a drag of that parent unless the user explicitly deep-selected a child.
        /// </summary>
        public static List<LayerNode> ResolveDragTargets(
            CanvasHitTestResult hitResult,
            IList<LayerNode> currentSelection,
            LayerNode resolvedSelection,
            bool isCtrl,
            int clickCount)
        {
            var targets = new List<LayerNode>();

            if (currentSelection != null && currentSelection.Count > 0)
            {
                foreach (var layer in currentSelection)
                {
                    if (layer != null && !targets.Contains(layer))
                    {
                        targets.Add(layer);
                    }
                }
            }
            else if (resolvedSelection != null)
            {
                targets.Add(resolvedSelection);
            }

            return targets;
        }
    }
}

