using System;
using System.Collections.Generic;
using System.Linq;
using Kerf.Model;
using Kerf.Engine;

namespace Kerf.Tools
{
    public static class AutoLayoutEngine
    {
        public static void ReflowAll(Page page)
        {
            if (page == null || page.Layers == null) return;
            foreach (var root in page.Layers)
            {
                ReflowSubtreeStabilized(root);
            }
        }

        public static void ReflowHierarchy(Page page, LayerNode node)
        {
            if (node == null) return;

            // Find topmost root ancestor in the page hierarchy
            LayerNode root = node;
            if (page != null)
            {
                var visited = new HashSet<string>();
                while (!string.IsNullOrEmpty(root.ParentId))
                {
                    if (visited.Contains(root.ParentId)) break;
                    visited.Add(root.ParentId);
                    var p = page.FindLayerById(root.ParentId);
                    if (p == null) break;
                    root = p;
                }
            }

            ReflowSubtreeStabilized(root);
        }

        public static void ReflowRecursive(LayerNode node)
        {
            if (node == null) return;
            ReflowSubtreeStabilized(node);
        }

        public static void ReflowSubtreeStabilized(LayerNode root, int maxPasses = 3)
        {
            if (root == null) return;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool changed = ReflowPass(root);
                if (!changed) break; // Converged
            }
        }

        private static bool ReflowPass(LayerNode node)
        {
            if (node == null) return false;
            bool changed = false;

            // 1. Bottom-up: Reflow children first so parent hug dimensions are based on current child sizes
            if (node.Children != null && node.Children.Count > 0)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    if (ReflowPass(node.Children[i]))
                    {
                        changed = true;
                    }
                }
            }

            // 2. Reflow current container
            if (node.AutoLayout != null && node.AutoLayout.Mode != AutoLayoutMode.None)
            {
                double oldW = node.Width;
                double oldH = node.Height;
                double oldX = node.X;
                double oldY = node.Y;

                Reflow(node);

                if (Math.Abs(node.Width - oldW) > 0.001 ||
                    Math.Abs(node.Height - oldH) > 0.001 ||
                    Math.Abs(node.X - oldX) > 0.001 ||
                    Math.Abs(node.Y - oldY) > 0.001)
                {
                    changed = true;
                }

                // 3. Top-down: If any child has Fill sizing and also has auto layout, reflow it with updated dimensions
                if (node.Children != null && node.Children.Count > 0)
                {
                    for (int i = 0; i < node.Children.Count; i++)
                    {
                        var child = node.Children[i];
                        if (child != null && child.AutoLayout != null && child.AutoLayout.Mode != AutoLayoutMode.None)
                        {
                            if (child.LayoutSizingH == SizingMode.Fill || child.LayoutSizingV == SizingMode.Fill)
                            {
                                double childOldW = child.Width;
                                double childOldH = child.Height;

                                Reflow(child);

                                if (Math.Abs(child.Width - childOldW) > 0.001 ||
                                    Math.Abs(child.Height - childOldH) > 0.001)
                                {
                                    changed = true;
                                    ReflowPass(child);
                                }
                            }
                        }
                    }
                }
            }

            return changed;
        }

        public static void Reflow(LayerNode frame)
        {
            if (frame == null || frame.AutoLayout == null) return;
            if (frame.AutoLayout.Mode == AutoLayoutMode.None) return;
            if (frame.Children == null || frame.Children.Count == 0) return;

            var visibleChildren = frame.Children.Where(c => c.IsVisible && !c.IgnoreAutoLayout).ToList();
            if (visibleChildren.Count == 0)
            {
                if (frame.AutoLayout.SizingH == SizingMode.Hug)
                {
                    frame.Width = Math.Max(frame.MinWidth, Math.Min(frame.MaxWidth, Math.Max(1.0, frame.AutoLayout.PaddingLeft + frame.AutoLayout.PaddingRight)));
                }
                if (frame.AutoLayout.SizingV == SizingMode.Hug)
                {
                    frame.Height = Math.Max(frame.MinHeight, Math.Min(frame.MaxHeight, Math.Max(1.0, frame.AutoLayout.PaddingTop + frame.AutoLayout.PaddingBottom)));
                }
                return;
            }

            var originalBounds = new Dictionary<LayerNode, System.Windows.Rect>();
            var originalPathGeometry = new Dictionary<LayerNode, VectorGeometry>();
            foreach (var child in visibleChildren)
            {
                originalBounds[child] = new System.Windows.Rect(child.X, child.Y, Math.Max(1.0, child.Width), Math.Max(1.0, child.Height));
                if (child.Type == ElementType.Path && child.Geometry != null)
                    originalPathGeometry[child] = child.Geometry.Clone();
            }

            var layout = frame.AutoLayout;

            switch (layout.Mode)
            {
                case AutoLayoutMode.Horizontal:
                    ReflowLinear(frame, visibleChildren, layout, true);
                    break;
                case AutoLayoutMode.Vertical:
                    ReflowLinear(frame, visibleChildren, layout, false);
                    break;
                case AutoLayoutMode.Wrap:
                    ReflowWrap(frame, visibleChildren, layout);
                    break;
                case AutoLayoutMode.Grid:
                    ReflowGrid(frame, visibleChildren, layout);
                    break;
            }

            foreach (var child in visibleChildren)
            {
                System.Windows.Rect oldBounds;
                VectorGeometry oldGeometry;
                if (child.Type == ElementType.Path && originalBounds.TryGetValue(child, out oldBounds) && originalPathGeometry.TryGetValue(child, out oldGeometry))
                {
                    child.Geometry = oldGeometry.Clone();
                    BezierVectorEngine.ScaleGeometry(
                        child.Geometry,
                        oldBounds,
                        new System.Windows.Rect(child.X, child.Y, Math.Max(1.0, child.Width), Math.Max(1.0, child.Height)));
                }
            }
        }

        private static void ReflowLinear(LayerNode frame, List<LayerNode> children, AutoLayoutData layout, bool horizontal)
        {
            if (children == null || children.Count == 0) return;

            double gap = IsFinite(layout.Gap) ? Math.Max(0.0, layout.Gap) : 0.0;
            double padL = SafePadding(layout.PaddingLeft);
            double padR = SafePadding(layout.PaddingRight);
            double padT = SafePadding(layout.PaddingTop);
            double padB = SafePadding(layout.PaddingBottom);
            // Sizing dependency rule: A parent set to Hug cannot remain Hug on an axis when a child is set to Fill on that same axis
            if (horizontal)
            {
                if (children.Any(c => c.LayoutSizingH == SizingMode.Fill) && layout.SizingH == SizingMode.Hug)
                    layout.SizingH = SizingMode.Fixed;
                if (children.Any(c => c.LayoutSizingV == SizingMode.Fill) && layout.SizingV == SizingMode.Hug)
                    layout.SizingV = SizingMode.Fixed;
            }
            else
            {
                if (children.Any(c => c.LayoutSizingV == SizingMode.Fill) && layout.SizingV == SizingMode.Hug)
                    layout.SizingV = SizingMode.Fixed;
                if (children.Any(c => c.LayoutSizingH == SizingMode.Fill) && layout.SizingH == SizingMode.Hug)
                    layout.SizingH = SizingMode.Fixed;
            }

            bool hugW = layout.SizingH == SizingMode.Hug;
            bool hugH = layout.SizingV == SizingMode.Hug;

            foreach (var child in children)
            {
                if (hugW && child.LayoutSizingH == SizingMode.Fill)
                    child.Width = ClampSize(GetBaseWidth(child), child.MinWidth, child.MaxWidth);
                if (hugH && child.LayoutSizingV == SizingMode.Fill)
                    child.Height = ClampSize(GetBaseHeight(child), child.MinHeight, child.MaxHeight);
                child.Width = ClampSize(child.Width, child.MinWidth, child.MaxWidth);
                child.Height = ClampSize(child.Height, child.MinHeight, child.MaxHeight);
            }

            double naturalMain = 0.0;
            double naturalCross = 0.0;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                naturalMain += horizontal ? child.Width : child.Height;
                naturalCross = Math.Max(naturalCross, horizontal ? child.Height : child.Width);
                if (i > 0) naturalMain += gap;
            }

            if (hugW)
            {
                double desired = horizontal ? naturalMain + padL + padR : naturalCross + padL + padR;
                frame.Width = ClampSize(desired, frame.MinWidth, frame.MaxWidth);
            }
            else frame.Width = ClampSize(frame.Width, frame.MinWidth, frame.MaxWidth);

            if (hugH)
            {
                double desired = horizontal ? naturalCross + padT + padB : naturalMain + padT + padB;
                frame.Height = ClampSize(desired, frame.MinHeight, frame.MaxHeight);
            }
            else frame.Height = ClampSize(frame.Height, frame.MinHeight, frame.MaxHeight);

            double innerMain = Math.Max(0.0, (horizontal ? frame.Width - padL - padR : frame.Height - padT - padB));
            double innerCross = Math.Max(0.0, (horizontal ? frame.Height - padT - padB : frame.Width - padL - padR));
            if (!(horizontal ? hugW : hugH))
            {
                DistributeFill(children, horizontal, innerMain, gap);
            }
            if (!(horizontal ? hugH : hugW))
            {
                foreach (var child in children)
                {
                    if (horizontal && child.LayoutSizingV == SizingMode.Fill)
                        child.Height = ClampSize(innerCross, child.MinHeight, child.MaxHeight);
                    else if (!horizontal && child.LayoutSizingH == SizingMode.Fill)
                        child.Width = ClampSize(innerCross, child.MinWidth, child.MaxWidth);
                }
            }

            double usedMain = 0.0;
            double maxBaseline = 0.0;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                usedMain += horizontal ? child.Width : child.Height;
                if (i > 0) usedMain += gap;
                if (layout.AlignItems == AlignItems.Baseline)
                {
                    double baseline = child.Type == ElementType.Text && child.Typography != null
                        ? child.Typography.FontSize * 0.85
                        : (horizontal ? child.Height : child.Width);
                    maxBaseline = Math.Max(maxBaseline, baseline);
                }
            }

            double extra = Math.Max(0.0, innerMain - usedMain);
            double cursor = horizontal ? frame.X + padL : frame.Y + padT;
            double effectiveGap = gap;
            switch (layout.JustifyContent)
            {
                case JustifyContent.Center:
                    cursor += extra / 2.0;
                    break;
                case JustifyContent.End:
                    cursor += extra;
                    break;
                case JustifyContent.SpaceBetween:
                    if (children.Count > 1) effectiveGap += extra / (children.Count - 1);
                    break;
                case JustifyContent.SpaceAround:
                    if (children.Count > 0)
                    {
                        double step = extra / children.Count;
                        cursor += step / 2.0;
                        effectiveGap += step;
                    }
                    break;
                case JustifyContent.SpaceEvenly:
                    if (children.Count > 0)
                    {
                        double step = extra / (children.Count + 1.0);
                        cursor += step;
                        effectiveGap += step;
                    }
                    break;
            }

            foreach (var child in children)
            {
                double childMain = horizontal ? child.Width : child.Height;
                double childCross = horizontal ? child.Height : child.Width;
                double crossStart = horizontal ? frame.Y + padT : frame.X + padL;
                double crossPosition = crossStart;
                double crossFree = innerCross - childCross;
                switch (layout.AlignItems)
                {
                    case AlignItems.Center:
                        crossPosition += crossFree / 2.0;
                        break;
                    case AlignItems.End:
                        crossPosition += crossFree;
                        break;
                    case AlignItems.Baseline:
                        double baseline = child.Type == ElementType.Text && child.Typography != null
                            ? child.Typography.FontSize * 0.85
                            : childCross;
                        crossPosition += maxBaseline - baseline;
                        break;
                }

                if (horizontal)
                    SetChildPosition(child, cursor, crossPosition);
                else
                    SetChildPosition(child, crossPosition, cursor);
                cursor += childMain + effectiveGap;
            }
        }

        private static void DistributeFill(List<LayerNode> children, bool horizontal, double innerMain, double gap)
        {
            var fillItems = children.Where(c => (horizontal ? c.LayoutSizingH : c.LayoutSizingV) == SizingMode.Fill).ToList();
            if (fillItems.Count == 0) return;

            double fixedTotal = 0.0;
            foreach (var child in children)
            {
                if ((horizontal ? child.LayoutSizingH : child.LayoutSizingV) != SizingMode.Fill)
                    fixedTotal += horizontal ? child.Width : child.Height;
            }

            double remaining = Math.Max(0.0, innerMain - fixedTotal - gap * Math.Max(0, children.Count - 1));
            var active = new List<LayerNode>(fillItems);
            while (active.Count > 0)
            {
                double share = remaining / active.Count;
                bool frozeAny = false;
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    var child = active[i];
                    double min = horizontal ? child.MinWidth : child.MinHeight;
                    double max = horizontal ? child.MaxWidth : child.MaxHeight;
                    max = Math.Max(min, max);
                    if (share < min || share > max)
                    {
                        double value = Math.Max(min, Math.Min(max, share));
                        if (horizontal) child.Width = value; else child.Height = value;
                        remaining = Math.Max(0.0, remaining - value);
                        active.RemoveAt(i);
                        frozeAny = true;
                    }
                }
                if (!frozeAny)
                {
                    foreach (var child in active)
                    {
                        if (horizontal) child.Width = share; else child.Height = share;
                    }
                    break;
                }
            }
        }

        private static double ClampSize(double value, double min, double max)
        {
            if (!IsFinite(value)) value = 0.0;
            if (!IsFinite(min) || min < 0.0) min = 0.0;
            if (!IsFinite(max) || max < min) max = min;
            return Math.Max(min, Math.Min(max, value));
        }

        private static double SafePadding(double value)
        {
            return IsFinite(value) ? Math.Max(0.0, value) : 0.0;
        }

        private static double GetBaseWidth(LayerNode child)
        {
            return child.AutoLayoutBaseWidth > 0.0 ? child.AutoLayoutBaseWidth : child.Width;
        }

        private static double GetBaseHeight(LayerNode child)
        {
            return child.AutoLayoutBaseHeight > 0.0 ? child.AutoLayoutBaseHeight : child.Height;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void SetChildPosition(LayerNode child, double x, double y)
        {
            if (child == null) return;
            double dx = x - child.X;
            double dy = y - child.Y;
            child.X = x;
            child.Y = y;
            if (Math.Abs(dx) < 0.0001 && Math.Abs(dy) < 0.0001) return;
            if (child.Type == ElementType.Path && child.Geometry != null)
                BezierVectorEngine.OffsetGeometry(child.Geometry, dx, dy);
            OffsetDescendants(child, dx, dy);
        }

        private static void OffsetDescendants(LayerNode parent, double dx, double dy)
        {
            if (parent == null || parent.Children == null) return;
            foreach (var child in parent.Children)
            {
                if (child == null) continue;
                child.X += dx;
                child.Y += dy;
                if (child.Type == ElementType.Path && child.Geometry != null)
                    BezierVectorEngine.OffsetGeometry(child.Geometry, dx, dy);
                OffsetDescendants(child, dx, dy);
            }
        }

        private static double MeasureWrapGap(List<LayerNode> ordered)
        {
            if (ordered == null || ordered.Count < 2) return 0.0;
            var gaps = new List<double>();
            for (int i = 0; i < ordered.Count; i++)
            {
                LayerNode current = ordered[i];
                if (current == null) continue;
                for (int j = i + 1; j < ordered.Count; j++)
                {
                    LayerNode next = ordered[j];
                    if (next == null) continue;
                    double centerDeltaY = Math.Abs((current.Y + current.Height / 2.0) - (next.Y + next.Height / 2.0));
                    double sameRowTolerance = Math.Max(2.0, Math.Min(current.Height, next.Height) * 0.45);
                    if (centerDeltaY <= sameRowTolerance)
                    {
                        double gap = next.X - (current.X + current.Width);
                        if (gap >= 0.0) gaps.Add(gap);
                        break;
                    }
                    if (next.Y > current.Y + current.Height) break;
                }
            }
            return gaps.Count == 0 ? 0.0 : Math.Max(0.0, gaps.OrderBy(value => value).ElementAt(gaps.Count / 2));
        }

        public static System.Windows.Rect PreviewInsertionBounds(LayerNode frame, List<LayerNode> movingLayers, int insertionIndex, double width, double height)
        {
            if (frame == null || frame.AutoLayout == null || frame.AutoLayout.Mode == AutoLayoutMode.None)
                return new System.Windows.Rect(frame == null ? 0.0 : frame.X, frame == null ? 0.0 : frame.Y, Math.Max(1.0, width), Math.Max(1.0, height));

            var movingIds = new HashSet<string>((movingLayers ?? new List<LayerNode>()).Where(n => n != null).Select(n => n.Id));
            var previewFrame = new LayerNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = frame.Type,
                X = frame.X,
                Y = frame.Y,
                Width = frame.Width,
                Height = frame.Height,
                AutoLayout = frame.AutoLayout.Clone(),
                Children = new List<LayerNode>(),
                MinWidth = frame.MinWidth,
                MaxWidth = frame.MaxWidth,
                MinHeight = frame.MinHeight,
                MaxHeight = frame.MaxHeight
            };
            foreach (var child in frame.Children ?? new List<LayerNode>())
            {
                if (child == null || movingIds.Contains(child.Id)) continue;
                var clone = child.Clone();
                clone.ParentId = previewFrame.Id;
                previewFrame.Children.Add(clone);
            }

            var ghost = new LayerNode
            {
                Name = "Drop preview",
                Type = ElementType.Rectangle,
                Width = Math.Max(1.0, width),
                Height = Math.Max(1.0, height),
                Fills = new List<Paint>(),
                Children = new List<LayerNode>()
            };
            int index = Math.Max(0, Math.Min(insertionIndex, previewFrame.Children.Count));
            previewFrame.Children.Insert(index, ghost);
            ghost.ParentId = previewFrame.Id;
            Reflow(previewFrame);
            return ghost.GetBounds();
        }

        private static void ReflowHorizontal(LayerNode frame, List<LayerNode> children, AutoLayoutData layout)
        {
            // Calculate space consumed by non-fill children
            int fillCountH = 0;
            double nonFillWidth = 0.0;
            foreach (var child in children)
            {
                if (child.LayoutSizingH == SizingMode.Fill)
                {
                    fillCountH++;
                }
                else
                {
                    nonFillWidth += child.Width;
                }
            }

            // Sizing dependency rule: A parent set to Hug cannot remain Hug on an axis when a child is set to Fill on that same axis
            if (fillCountH > 0 && layout.SizingH == SizingMode.Hug)
            {
                layout.SizingH = SizingMode.Fixed;
            }
            if (children.Any(c => c.LayoutSizingV == SizingMode.Fill) && layout.SizingV == SizingMode.Hug)
            {
                layout.SizingV = SizingMode.Fixed;
            }

            double totalGaps = (children.Count - 1) * layout.Gap;
            double availWidth = Math.Max(0.0, frame.Width - layout.PaddingLeft - layout.PaddingRight);

            // Fill sizing distribution
            if (fillCountH > 0)
            {
                double remaining = availWidth - nonFillWidth - totalGaps;
                double spacePerFill = (remaining > 0) ? (remaining / fillCountH) : 1.0;
                foreach (var child in children)
                {
                    if (child.LayoutSizingH == SizingMode.Fill)
                    {
                        child.Width = Math.Max(1.0, Math.Min(child.MaxWidth, Math.Max(child.MinWidth, spacePerFill)));
                    }
                }
            }

            // Cross-axis Fill sizing
            double availInnerH = Math.Max(0.0, frame.Height - layout.PaddingTop - layout.PaddingBottom);
            foreach (var child in children)
            {
                if (child.LayoutSizingV == SizingMode.Fill && availInnerH > 0)
                {
                    child.Height = Math.Max(1.0, Math.Min(child.MaxHeight, Math.Max(child.MinHeight, availInnerH)));
                }
            }

            // Baseline alignment calculation
            double maxBaseline = 0.0;
            if (layout.AlignItems == AlignItems.Baseline)
            {
                foreach (var child in children)
                {
                    double b = (child.Type == ElementType.Text && child.Typography != null) ? (child.Typography.FontSize * 0.85) : child.Height;
                    maxBaseline = Math.Max(maxBaseline, b);
                }
            }

            // First pass: measure total content width and maxHeight
            double maxHeight = 0.0;
            double totalWidth = 0.0;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                maxHeight = Math.Max(maxHeight, child.Height);
                totalWidth += child.Width;
                if (i > 0) totalWidth += layout.Gap;
            }

            double extraSpace = Math.Max(0.0, availWidth - totalWidth);
            double startX = frame.X + layout.PaddingLeft;
            double gap = layout.Gap;

            if (layout.JustifyContent == JustifyContent.Center)
            {
                startX += extraSpace / 2.0;
            }
            else if (layout.JustifyContent == JustifyContent.End)
            {
                startX += extraSpace;
            }
            else if (layout.JustifyContent == JustifyContent.SpaceBetween)
            {
                if (children.Count > 1)
                {
                    gap += extraSpace / (children.Count - 1);
                }
            }
            else if (layout.JustifyContent == JustifyContent.SpaceAround)
            {
                if (children.Count > 0)
                {
                    double step = extraSpace / (children.Count * 2.0);
                    startX += step;
                    gap += step * 2.0;
                }
            }
            else if (layout.JustifyContent == JustifyContent.SpaceEvenly)
            {
                if (children.Count > 0)
                {
                    double step = extraSpace / (children.Count + 1.0);
                    startX += step;
                    gap += step;
                }
            }

            double curX = startX;

            // Second pass: position children equally
            foreach (var child in children)
            {
                child.X = curX;

                double innerY = frame.Y + layout.PaddingTop;
                switch (layout.AlignItems)
                {
                    case AlignItems.Center:
                        child.Y = innerY + (availInnerH - child.Height) / 2.0;
                        break;
                    case AlignItems.End:
                        child.Y = innerY + (availInnerH - child.Height);
                        break;
                    case AlignItems.Baseline:
                        double childBaseline = (child.Type == ElementType.Text && child.Typography != null) ? (child.Typography.FontSize * 0.85) : child.Height;
                        child.Y = innerY + (maxBaseline - childBaseline);
                        break;
                    case AlignItems.Start:
                    default:
                        child.Y = innerY;
                        break;
                }

                curX += child.Width + gap;
            }

            // Hug sizing on parent with Min/Max limits
            if (layout.SizingH == SizingMode.Hug)
            {
                double desiredW = totalWidth + layout.PaddingLeft + layout.PaddingRight;
                frame.Width = Math.Max(frame.MinWidth, Math.Min(frame.MaxWidth, Math.Max(1.0, desiredW)));
            }
            if (layout.SizingV == SizingMode.Hug)
            {
                double desiredH = maxHeight + layout.PaddingTop + layout.PaddingBottom;
                frame.Height = Math.Max(frame.MinHeight, Math.Min(frame.MaxHeight, Math.Max(1.0, desiredH)));
            }
        }

        private static void ReflowVertical(LayerNode frame, List<LayerNode> children, AutoLayoutData layout)
        {
            // Calculate space consumed by non-fill children
            int fillCountV = 0;
            double nonFillHeight = 0.0;
            foreach (var child in children)
            {
                if (child.LayoutSizingV == SizingMode.Fill)
                {
                    fillCountV++;
                }
                else
                {
                    nonFillHeight += child.Height;
                }
            }

            // Sizing dependency rule: A parent set to Hug cannot remain Hug on an axis when a child is set to Fill on that same axis
            if (fillCountV > 0 && layout.SizingV == SizingMode.Hug)
            {
                layout.SizingV = SizingMode.Fixed;
            }
            if (children.Any(c => c.LayoutSizingH == SizingMode.Fill) && layout.SizingH == SizingMode.Hug)
            {
                layout.SizingH = SizingMode.Fixed;
            }

            double totalGaps = (children.Count - 1) * layout.Gap;
            double availHeight = Math.Max(0.0, frame.Height - layout.PaddingTop - layout.PaddingBottom);

            // Fill sizing distribution
            if (fillCountV > 0)
            {
                double remaining = availHeight - nonFillHeight - totalGaps;
                double spacePerFill = (remaining > 0) ? (remaining / fillCountV) : 1.0;
                foreach (var child in children)
                {
                    if (child.LayoutSizingV == SizingMode.Fill)
                    {
                        child.Height = Math.Max(1.0, Math.Min(child.MaxHeight, Math.Max(child.MinHeight, spacePerFill)));
                    }
                }
            }

            // Cross-axis Fill sizing
            double availInnerW = Math.Max(0.0, frame.Width - layout.PaddingLeft - layout.PaddingRight);
            foreach (var child in children)
            {
                if (child.LayoutSizingH == SizingMode.Fill && availInnerW > 0)
                {
                    child.Width = Math.Max(1.0, Math.Min(child.MaxWidth, Math.Max(child.MinWidth, availInnerW)));
                }
            }

            // First pass: measure total content height and maxWidth
            double maxWidth = 0.0;
            double totalHeight = 0.0;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                maxWidth = Math.Max(maxWidth, child.Width);
                totalHeight += child.Height;
                if (i > 0) totalHeight += layout.Gap;
            }

            double extraSpace = Math.Max(0.0, availHeight - totalHeight);
            double startY = frame.Y + layout.PaddingTop;
            double gap = layout.Gap;

            if (layout.JustifyContent == JustifyContent.Center)
            {
                startY += extraSpace / 2.0;
            }
            else if (layout.JustifyContent == JustifyContent.End)
            {
                startY += extraSpace;
            }
            else if (layout.JustifyContent == JustifyContent.SpaceBetween)
            {
                if (children.Count > 1)
                {
                    gap += extraSpace / (children.Count - 1);
                }
            }
            else if (layout.JustifyContent == JustifyContent.SpaceAround)
            {
                if (children.Count > 0)
                {
                    double step = extraSpace / (children.Count * 2.0);
                    startY += step;
                    gap += step * 2.0;
                }
            }
            else if (layout.JustifyContent == JustifyContent.SpaceEvenly)
            {
                if (children.Count > 0)
                {
                    double step = extraSpace / (children.Count + 1.0);
                    startY += step;
                    gap += step;
                }
            }

            double curY = startY;

            // Second pass: position children equally
            foreach (var child in children)
            {
                child.Y = curY;

                double innerX = frame.X + layout.PaddingLeft;
                switch (layout.AlignItems)
                {
                    case AlignItems.Center:
                        child.X = innerX + (availInnerW - child.Width) / 2.0;
                        break;
                    case AlignItems.End:
                        child.X = innerX + (availInnerW - child.Width);
                        break;
                    case AlignItems.Start:
                    default:
                        child.X = innerX;
                        break;
                }

                curY += child.Height + gap;
            }

            // Hug sizing on parent with Min/Max limits
            if (layout.SizingH == SizingMode.Hug)
            {
                double desiredW = maxWidth + layout.PaddingLeft + layout.PaddingRight;
                frame.Width = Math.Max(frame.MinWidth, Math.Min(frame.MaxWidth, Math.Max(1.0, desiredW)));
            }
            if (layout.SizingV == SizingMode.Hug)
            {
                double desiredH = totalHeight + layout.PaddingTop + layout.PaddingBottom;
                frame.Height = Math.Max(frame.MinHeight, Math.Min(frame.MaxHeight, Math.Max(1.0, desiredH)));
            }
        }

        private static void ReflowWrap(LayerNode frame, List<LayerNode> children, AutoLayoutData layout)
        {
            double padL = SafePadding(layout.PaddingLeft);
            double padR = SafePadding(layout.PaddingRight);
            double padT = SafePadding(layout.PaddingTop);
            double padB = SafePadding(layout.PaddingBottom);
            double gap = IsFinite(layout.Gap) ? Math.Max(0.0, layout.Gap) : 0.0;
            foreach (var child in children)
            {
                if (layout.SizingH == SizingMode.Hug && child.LayoutSizingH == SizingMode.Fill)
                    child.Width = ClampSize(GetBaseWidth(child), child.MinWidth, child.MaxWidth);
                if (layout.SizingV == SizingMode.Hug && child.LayoutSizingV == SizingMode.Fill)
                    child.Height = ClampSize(GetBaseHeight(child), child.MinHeight, child.MaxHeight);
            }
            double naturalWidth = children.Sum(c => ClampSize(c.LayoutSizingH == SizingMode.Fill ? GetBaseWidth(c) : c.Width, c.MinWidth, c.MaxWidth)) + Math.Max(0, children.Count - 1) * gap;
            if (layout.SizingH == SizingMode.Hug)
                frame.Width = ClampSize(naturalWidth + padL + padR, frame.MinWidth, frame.MaxWidth);
            double availWidth = Math.Max(0.0, frame.Width - padL - padR);
            var rows = new List<List<LayerNode>>();
            var row = new List<LayerNode>();
            double rowWidth = 0.0;
            double rowHeight = 0.0;
            foreach (var child in children)
            {
                double nextWidth = rowWidth + (row.Count > 0 ? gap : 0.0) + child.Width;
                if (row.Count > 0 && nextWidth > availWidth)
                {
                    rows.Add(row);
                    row = new List<LayerNode>();
                    rowWidth = 0.0;
                    rowHeight = 0.0;
                }
                row.Add(child);
                rowWidth += child.Width + (row.Count > 1 ? gap : 0.0);
                rowHeight = Math.Max(rowHeight, child.Height);
            }
            if (row.Count > 0) rows.Add(row);

            double curY = frame.Y + padT;
            double maxRowW = 0.0;
            foreach (var items in rows)
            {
                double usedWidth = items.Sum(c => c.Width) + Math.Max(0, items.Count - 1) * gap;
                double extra = Math.Max(0.0, availWidth - usedWidth);
                double cursor = frame.X + padL;
                double effectiveGap = gap;
                switch (layout.JustifyContent)
                {
                    case JustifyContent.Center:
                        cursor += extra / 2.0;
                        break;
                    case JustifyContent.End:
                        cursor += extra;
                        break;
                    case JustifyContent.SpaceBetween:
                        if (items.Count > 1) effectiveGap += extra / (items.Count - 1.0);
                        break;
                    case JustifyContent.SpaceAround:
                        if (items.Count > 0)
                        {
                            double step = extra / items.Count;
                            cursor += step / 2.0;
                            effectiveGap += step;
                        }
                        break;
                    case JustifyContent.SpaceEvenly:
                        if (items.Count > 0)
                        {
                            double step = extra / (items.Count + 1.0);
                            cursor += step;
                            effectiveGap += step;
                        }
                        break;
                }

                double currentRowH = items.Count > 0 ? items.Max(c => c.Height) : 0.0;
                foreach (var child in items)
                {
                    double crossOffset = layout.AlignItems == AlignItems.Center
                        ? (currentRowH - child.Height) / 2.0
                        : layout.AlignItems == AlignItems.End ? currentRowH - child.Height : 0.0;
                    SetChildPosition(child, cursor, curY + crossOffset);
                    cursor += child.Width + effectiveGap;
                }

                maxRowW = Math.Max(maxRowW, usedWidth);
                curY += currentRowH + gap;
            }

            double totalH = rows.Count == 0 ? padT + padB : curY - frame.Y - gap + padB;

            if (layout.SizingH == SizingMode.Hug)
            {
                frame.Width = ClampSize(maxRowW + padL + padR, frame.MinWidth, frame.MaxWidth);
            }
            if (layout.SizingV == SizingMode.Hug)
            {
                frame.Height = ClampSize(totalH, frame.MinHeight, frame.MaxHeight);
            }
        }

        private static void ReflowGrid(LayerNode frame, List<LayerNode> children, AutoLayoutData layout)
        {
            int cols = Math.Max(1, layout.GridColumns);
            double padL = SafePadding(layout.PaddingLeft);
            double padR = SafePadding(layout.PaddingRight);
            double padT = SafePadding(layout.PaddingTop);
            double padB = SafePadding(layout.PaddingBottom);
            double gap = IsFinite(layout.Gap) ? Math.Max(0.0, layout.Gap) : 0.0;
            foreach (var child in children)
            {
                if (layout.SizingH == SizingMode.Hug && child.LayoutSizingH == SizingMode.Fill)
                    child.Width = ClampSize(GetBaseWidth(child), child.MinWidth, child.MaxWidth);
                if (layout.SizingV == SizingMode.Hug && child.LayoutSizingV == SizingMode.Fill)
                    child.Height = ClampSize(GetBaseHeight(child), child.MinHeight, child.MaxHeight);
            }
            double naturalColumnWidth = children.Count == 0 ? 0.0 : children.Max(c => ClampSize(c.LayoutSizingH == SizingMode.Fill ? GetBaseWidth(c) : c.Width, c.MinWidth, c.MaxWidth));
            if (layout.SizingH == SizingMode.Hug)
                frame.Width = ClampSize(naturalColumnWidth * cols + Math.Max(0, cols - 1) * gap + padL + padR, frame.MinWidth, frame.MaxWidth);
            double availWidth = Math.Max(0.0, frame.Width - padL - padR);
            double colWidth = Math.Max(0.0, (availWidth - (cols - 1) * gap) / cols);

            double startX = frame.X + padL;
            double curY = frame.Y + padT;
            double rowMaxH = 0.0;
            double totalHeight = 0.0;

            for (int i = 0; i < children.Count; i++)
            {
                int col = i % cols;
                if (col == 0 && i > 0)
                {
                    curY += rowMaxH + gap;
                    totalHeight += rowMaxH + gap;
                    rowMaxH = 0.0;
                }

                var child = children[i];
                SetChildPosition(child, startX + col * (colWidth + gap), curY);
                if (child.LayoutSizingH == SizingMode.Fill)
                    child.Width = ClampSize(colWidth, child.MinWidth, child.MaxWidth);
                if (child.LayoutSizingV == SizingMode.Fill && layout.SizingV != SizingMode.Hug)
                    child.Height = ClampSize(Math.Max(0.0, frame.Height - padT - padB), child.MinHeight, child.MaxHeight);

                rowMaxH = Math.Max(rowMaxH, child.Height);
            }

            totalHeight += rowMaxH;
            if (layout.SizingV == SizingMode.Hug)
            {
                frame.Height = ClampSize(totalHeight + padT + padB, frame.MinHeight, frame.MaxHeight);
            }
        }

        public static AutoLayoutMode DetectSmartLayoutMode(List<LayerNode> layers)
        {
            if (layers == null || layers.Count < 2) return AutoLayoutMode.Horizontal;

            // 1. Calculate centers and spreads
            var centers = layers.Select(l => new System.Windows.Point(l.X + l.Width / 2.0, l.Y + l.Height / 2.0)).ToList();
            double minCenterX = centers.Min(p => p.X);
            double maxCenterX = centers.Max(p => p.X);
            double minCenterY = centers.Min(p => p.Y);
            double maxCenterY = centers.Max(p => p.Y);
            double deltaCenterX = maxCenterX - minCenterX;
            double deltaCenterY = maxCenterY - minCenterY;

            // 2. Check 2D grid/flow pattern (for 3+ items):
            // If items form distinct rows AND distinct columns, it's a Wrap flow!
            if (layers.Count >= 3)
            {
                double avgH = layers.Average(l => l.Height);
                double avgW = layers.Average(l => l.Width);
                double rowTol = Math.Max(8.0, avgH * 0.45);
                double colTol = Math.Max(8.0, avgW * 0.45);

                var rowGroups = new List<List<LayerNode>>();
                foreach (var l in layers.OrderBy(l => l.Y))
                {
                    var match = rowGroups.FirstOrDefault(rg => Math.Abs((rg[0].Y + rg[0].Height / 2.0) - (l.Y + l.Height / 2.0)) <= rowTol);
                    if (match != null) match.Add(l);
                    else rowGroups.Add(new List<LayerNode> { l });
                }

                var colGroups = new List<List<LayerNode>>();
                foreach (var l in layers.OrderBy(l => l.X))
                {
                    var match = colGroups.FirstOrDefault(cg => Math.Abs((cg[0].X + cg[0].Width / 2.0) - (l.X + l.Width / 2.0)) <= colTol);
                    if (match != null) match.Add(l);
                    else colGroups.Add(new List<LayerNode> { l });
                }

                if (rowGroups.Count >= 2 && colGroups.Count >= 2 &&
                    rowGroups.Any(rg => rg.Count >= 2) && colGroups.Any(cg => cg.Count >= 2))
                {
                    return AutoLayoutMode.Wrap;
                }
            }

            // 3. For 2 items or linear arrangements:
            // Calculate spatial overlap along X and Y axes
            double totalXOverlap = 0.0;
            double totalYOverlap = 0.0;
            int pairCount = 0;

            for (int i = 0; i < layers.Count; i++)
            {
                for (int j = i + 1; j < layers.Count; j++)
                {
                    var a = layers[i];
                    var b = layers[j];

                    double xOverlap = Math.Max(0.0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X));
                    double yOverlap = Math.Max(0.0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));

                    totalXOverlap += xOverlap;
                    totalYOverlap += yOverlap;
                    pairCount++;
                }
            }

            if (pairCount > 0)
            {
                if (totalXOverlap > 0 && totalYOverlap == 0) return AutoLayoutMode.Vertical;
                if (totalYOverlap > 0 && totalXOverlap == 0) return AutoLayoutMode.Horizontal;

                if (totalXOverlap > totalYOverlap * 1.5) return AutoLayoutMode.Vertical;
                if (totalYOverlap > totalXOverlap * 1.5) return AutoLayoutMode.Horizontal;
            }

            // 4. Fallback to center delta spread
            return (deltaCenterX >= deltaCenterY) ? AutoLayoutMode.Horizontal : AutoLayoutMode.Vertical;
        }

        public static LayerNode FindBackgroundShapeForSelection(List<LayerNode> layers)
        {
            if (layers == null || layers.Count < 2) return null;

            // Candidates are shapes that can act as a background button/card container:
            // Rectangle, or Frame without active AutoLayout (or with Mode == None).
            var candidates = layers.Where(l => 
                l != null && (
                    l.Type == ElementType.Rectangle || 
                    (l.Type == ElementType.Frame && (l.AutoLayout == null || l.AutoLayout.Mode == AutoLayoutMode.None))
                )
            ).ToList();

            if (candidates.Count == 0) return null;

            LayerNode bestBg = null;
            double bestArea = -1;

            foreach (var cand in candidates)
            {
                var others = layers.Where(l => l != cand && l != null).ToList();
                if (others.Count == 0) continue;

                double fgMinX = others.Min(l => l.X);
                double fgMaxX = others.Max(l => l.X + Math.Max(1.0, l.Width));
                double fgMinY = others.Min(l => l.Y);
                double fgMaxY = others.Max(l => l.Y + Math.Max(1.0, l.Height));
                double fgW = Math.Max(1.0, fgMaxX - fgMinX);
                double fgH = Math.Max(1.0, fgMaxY - fgMinY);

                double candW = Math.Max(1.0, cand.Width);
                double candH = Math.Max(1.0, cand.Height);

                // Tolerance for slight overlap/overflow:
                double tolX = Math.Max(16.0, candW * 0.20);
                double tolY = Math.Max(12.0, candH * 0.25);

                bool withinX = (fgMinX >= cand.X - tolX) && (fgMaxX <= cand.X + candW + tolX);
                bool withinY = (fgMinY >= cand.Y - tolY) && (fgMaxY <= cand.Y + candH + tolY);

                // Foreground center should fall within candidate bounds
                double fgCenterX = fgMinX + fgW / 2.0;
                double fgCenterY = fgMinY + fgH / 2.0;
                bool centerInside = (fgCenterX >= cand.X - 4.0 && fgCenterX <= cand.X + candW + 4.0 &&
                                     fgCenterY >= cand.Y - 4.0 && fgCenterY <= cand.Y + candH + 4.0);

                if (withinX && withinY && centerInside)
                {
                    double area = candW * candH;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestBg = cand;
                    }
                }
            }

            return bestBg;
        }

        private static LayerNode BuildButtonFrameFromBackground(LayerNode bgShape, List<LayerNode> fgLayers, out List<LayerNode> orderedFg)
        {
            var mode = (fgLayers.Count >= 2) ? DetectSmartLayoutMode(fgLayers) : AutoLayoutMode.Horizontal;

            if (mode == AutoLayoutMode.Horizontal)
                orderedFg = fgLayers.OrderBy(l => l.X).ThenBy(l => l.Y).ToList();
            else if (mode == AutoLayoutMode.Vertical)
                orderedFg = fgLayers.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
            else
                orderedFg = fgLayers.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();

            double fgMinX = orderedFg.Min(l => l.X);
            double fgMaxX = orderedFg.Max(l => l.X + Math.Max(1.0, l.Width));
            double fgMinY = orderedFg.Min(l => l.Y);
            double fgMaxY = orderedFg.Max(l => l.Y + Math.Max(1.0, l.Height));
            double fgW = Math.Max(1.0, fgMaxX - fgMinX);
            double fgH = Math.Max(1.0, fgMaxY - fgMinY);

            double measuredGap = 8.0;
            if (orderedFg.Count > 1)
            {
                if (mode == AutoLayoutMode.Horizontal)
                {
                    double sumW = orderedFg.Sum(l => l.Width);
                    measuredGap = Math.Max(0.0, (fgW - sumW) / (orderedFg.Count - 1));
                }
                else if (mode == AutoLayoutMode.Vertical)
                {
                    double sumH = orderedFg.Sum(l => l.Height);
                    measuredGap = Math.Max(0.0, (fgH - sumH) / (orderedFg.Count - 1));
                }
                else
                {
                    measuredGap = MeasureWrapGap(orderedFg);
                }
            }

            double rawPadLeft = Math.Max(0.0, fgMinX - bgShape.X);
            double rawPadRight = Math.Max(0.0, (bgShape.X + bgShape.Width) - fgMaxX);
            double rawPadTop = Math.Max(0.0, fgMinY - bgShape.Y);
            double rawPadBottom = Math.Max(0.0, (bgShape.Y + bgShape.Height) - fgMaxY);

            // Centering analysis: text/objects centered over the shape align in center of the frame
            double centerDeltaX = Math.Abs((bgShape.X + bgShape.Width / 2.0) - (fgMinX + fgW / 2.0));
            bool isHorizCentered = (centerDeltaX <= Math.Max(10.0, bgShape.Width * 0.20)) || (Math.Abs(rawPadLeft - rawPadRight) <= Math.Max(14.0, bgShape.Width * 0.25));

            double centerDeltaY = Math.Abs((bgShape.Y + bgShape.Height / 2.0) - (fgMinY + fgH / 2.0));
            bool isVertCentered = (centerDeltaY <= Math.Max(8.0, bgShape.Height * 0.25)) || (Math.Abs(rawPadTop - rawPadBottom) <= Math.Max(10.0, bgShape.Height * 0.25));

            double padLeft, padRight, padTop, padBottom;
            JustifyContent justifyContent;
            AlignItems alignItems;

            if (mode == AutoLayoutMode.Horizontal || mode == AutoLayoutMode.Wrap)
            {
                if (isHorizCentered)
                {
                    justifyContent = JustifyContent.Center;
                    double padH = Math.Round((rawPadLeft + rawPadRight) / 2.0);
                    padLeft = padH;
                    padRight = padH;
                }
                else if (rawPadLeft <= rawPadRight)
                {
                    justifyContent = JustifyContent.Start;
                    padLeft = Math.Round(rawPadLeft);
                    padRight = Math.Round(rawPadRight);
                }
                else
                {
                    justifyContent = JustifyContent.End;
                    padLeft = Math.Round(rawPadLeft);
                    padRight = Math.Round(rawPadRight);
                }

                if (isVertCentered)
                {
                    alignItems = AlignItems.Center;
                    double padV = Math.Round((rawPadTop + rawPadBottom) / 2.0);
                    padTop = padV;
                    padBottom = padV;
                }
                else if (rawPadTop <= rawPadBottom)
                {
                    alignItems = AlignItems.Start;
                    padTop = Math.Round(rawPadTop);
                    padBottom = Math.Round(rawPadBottom);
                }
                else
                {
                    alignItems = AlignItems.End;
                    padTop = Math.Round(rawPadTop);
                    padBottom = Math.Round(rawPadBottom);
                }
            }
            else // Vertical
            {
                if (isVertCentered)
                {
                    justifyContent = JustifyContent.Center;
                    double padV = Math.Round((rawPadTop + rawPadBottom) / 2.0);
                    padTop = padV;
                    padBottom = padV;
                }
                else if (rawPadTop <= rawPadBottom)
                {
                    justifyContent = JustifyContent.Start;
                    padTop = Math.Round(rawPadTop);
                    padBottom = Math.Round(rawPadBottom);
                }
                else
                {
                    justifyContent = JustifyContent.End;
                    padTop = Math.Round(rawPadTop);
                    padBottom = Math.Round(rawPadBottom);
                }

                if (isHorizCentered)
                {
                    alignItems = AlignItems.Center;
                    double padH = Math.Round((rawPadLeft + rawPadRight) / 2.0);
                    padLeft = padH;
                    padRight = padH;
                }
                else if (rawPadLeft <= rawPadRight)
                {
                    alignItems = AlignItems.Start;
                    padLeft = Math.Round(rawPadLeft);
                    padRight = Math.Round(rawPadRight);
                }
                else
                {
                    alignItems = AlignItems.End;
                    padLeft = Math.Round(rawPadLeft);
                    padRight = Math.Round(rawPadRight);
                }
            }

            var frame = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = (orderedFg.Count == 1 && !string.IsNullOrEmpty(orderedFg[0].Name) && orderedFg[0].Type == ElementType.Text)
                    ? orderedFg[0].Name
                    : (!string.IsNullOrEmpty(bgShape.Name) && bgShape.Name != "Rectangle" && bgShape.Name != "Rectangle 1" ? bgShape.Name : "Button"),
                Type = ElementType.Frame,
                X = bgShape.X,
                Y = bgShape.Y,
                Width = bgShape.Width,
                Height = bgShape.Height,
                Rotation = bgShape.Rotation,
                Opacity = bgShape.Opacity,
                BlendMode = bgShape.BlendMode,
                IsVisible = bgShape.IsVisible,
                IsLocked = bgShape.IsLocked,
                ClipContent = false,
                CornerRadius = bgShape.CornerRadius,
                CornerRadiusTopLeft = bgShape.CornerRadiusTopLeft,
                CornerRadiusTopRight = bgShape.CornerRadiusTopRight,
                CornerRadiusBottomRight = bgShape.CornerRadiusBottomRight,
                CornerRadiusBottomLeft = bgShape.CornerRadiusBottomLeft,
                Fills = new List<Paint>(),
                Strokes = new List<StrokeData>(),
                Effects = new List<EffectData>(),
                AutoLayout = new AutoLayoutData
                {
                    Mode = mode,
                    Gap = Math.Round(measuredGap, 1),
                    PaddingLeft = Math.Round(padLeft, 1),
                    PaddingRight = Math.Round(padRight, 1),
                    PaddingTop = Math.Round(padTop, 1),
                    PaddingBottom = Math.Round(padBottom, 1),
                    JustifyContent = justifyContent,
                    AlignItems = alignItems,
                    SizingH = SizingMode.Hug,
                    SizingV = SizingMode.Hug
                },
                Children = new List<LayerNode>()
            };

            if (bgShape.Fills != null)
            {
                foreach (var f in bgShape.Fills) frame.Fills.Add(f.Clone());
            }
            if (bgShape.Strokes != null)
            {
                foreach (var s in bgShape.Strokes) frame.Strokes.Add(s.Clone());
            }
            if (bgShape.Effects != null)
            {
                foreach (var eff in bgShape.Effects) frame.Effects.Add(eff.Clone());
            }

            return frame;
        }

        public static LayerNode AutoAlignSelection(Page page, List<LayerNode> selectedLayers)
        {
            if (page == null || selectedLayers == null || selectedLayers.Count == 0) return null;

            // Filter out any layer whose ancestor is also selected
            var topLayers = selectedLayers.Where(l => !selectedLayers.Any(other => other != l && Page.IsDescendant(other, l))).ToList();
            if (topLayers.Count == 0) return null;

            // If a single frame is selected, cycle its AutoLayout mode (None -> Horizontal -> Vertical -> Horizontal)
            if (topLayers.Count == 1 && topLayers[0].Type == ElementType.Frame)
            {
                var singleFrame = topLayers[0];
                if (singleFrame.AutoLayout == null) singleFrame.AutoLayout = new AutoLayoutData();

                if (singleFrame.AutoLayout.Mode == AutoLayoutMode.None)
                {
                    var smartMode = (singleFrame.Children != null && singleFrame.Children.Count >= 2)
                        ? DetectSmartLayoutMode(singleFrame.Children)
                        : ((singleFrame.Height > singleFrame.Width * 1.2) ? AutoLayoutMode.Vertical : AutoLayoutMode.Horizontal);
                    singleFrame.AutoLayout.Mode = smartMode;
                    singleFrame.AutoLayout.SizingH = SizingMode.Hug;
                    singleFrame.AutoLayout.SizingV = SizingMode.Hug;
                    singleFrame.AutoLayout.JustifyContent = JustifyContent.Start;
                    singleFrame.AutoLayout.AlignItems = AlignItems.Start;
                }
                else if (singleFrame.AutoLayout.Mode == AutoLayoutMode.Horizontal)
                {
                    singleFrame.AutoLayout.Mode = AutoLayoutMode.Vertical;
                }
                else if (singleFrame.AutoLayout.Mode == AutoLayoutMode.Vertical)
                {
                    singleFrame.AutoLayout.Mode = AutoLayoutMode.Horizontal;
                }
                else
                {
                    singleFrame.AutoLayout.Mode = AutoLayoutMode.Horizontal;
                }

                ReflowHierarchy(page, singleFrame);
                return singleFrame;
            }

            // If a single non-frame layer is selected: wrap into Auto Layout Frame with standard padding
            if (topLayers.Count == 1)
            {
                var single = topLayers[0];
                double pad = 10.0;
                double fw = single.Width + pad * 2.0;
                double fh = single.Height + pad * 2.0;

                var singleWrapFrame = new LayerNode
                {
                    Id = Guid.NewGuid().ToString().Substring(0, 8),
                    Name = "Auto Layout Frame",
                    Type = ElementType.Frame,
                    X = single.X,
                    Y = single.Y,
                    Width = fw,
                    Height = fh,
                    ClipContent = false,
                    AutoLayout = new AutoLayoutData
                    {
                        Mode = AutoLayoutMode.Horizontal,
                        Gap = 0.0,
                        PaddingTop = pad,
                        PaddingRight = pad,
                        PaddingBottom = pad,
                        PaddingLeft = pad,
                        AlignItems = AlignItems.Start,
                        JustifyContent = JustifyContent.Start,
                        SizingH = SizingMode.Hug,
                        SizingV = SizingMode.Hug
                    },
                    Fills = new List<Paint>(),
                    Children = new List<LayerNode>()
                };

                LayerNode p;
                var c = page.FindContainer(single, out p);
                if (c == null) c = page.Layers;
                int insIdx = c.IndexOf(single);
                if (insIdx < 0) insIdx = c.Count;

                page.RemoveLayerEverywhere(single);
                single.ParentId = singleWrapFrame.Id;
                singleWrapFrame.Children.Add(single);
                singleWrapFrame.ParentId = p != null ? p.Id : null;

                if (insIdx <= c.Count) c.Insert(insIdx, singleWrapFrame);
                else c.Add(singleWrapFrame);

                ReflowHierarchy(page, singleWrapFrame);
                return singleWrapFrame;
            }

            // Check if one layer is a background container shape enclosing the other selected layers (e.g. Button pattern)
            var bgShape = FindBackgroundShapeForSelection(topLayers);
            if (bgShape != null)
            {
                var fgLayers = topLayers.Where(l => l != bgShape).ToList();
                List<LayerNode> orderedFg;
                var buttonFrame = BuildButtonFrameFromBackground(bgShape, fgLayers, out orderedFg);

                LayerNode bgParent;
                var bgContainer = page.FindContainer(bgShape, out bgParent);
                if (bgContainer == null) bgContainer = page.Layers;
                int bgInsertIdx = bgContainer.IndexOf(bgShape);
                if (bgInsertIdx < 0) bgInsertIdx = bgContainer.Count;

                page.RemoveLayerEverywhere(bgShape);
                foreach (var child in orderedFg)
                {
                    page.RemoveLayerEverywhere(child);
                    child.ParentId = buttonFrame.Id;
                    buttonFrame.Children.Add(child);
                }

                buttonFrame.ParentId = bgParent != null ? bgParent.Id : null;
                if (bgInsertIdx <= bgContainer.Count)
                    bgContainer.Insert(bgInsertIdx, buttonFrame);
                else
                    bgContainer.Add(buttonFrame);

                ReflowHierarchy(page, buttonFrame);
                return buttonFrame;
            }

            // If multiple layers selected: smartly detect layout mode and wrap full bounding envelope into Auto Layout Frame
            double minX = topLayers.Min(l => l.X);
            double minY = topLayers.Min(l => l.Y);
            double maxX = topLayers.Max(l => l.X + Math.Max(1.0, l.Width));
            double maxY = topLayers.Max(l => l.Y + Math.Max(1.0, l.Height));
            double totalW = Math.Max(1.0, maxX - minX);
            double totalH = Math.Max(1.0, maxY - minY);

            var mode = DetectSmartLayoutMode(topLayers);

            List<LayerNode> ordered;
            double measuredGap = 0.0;
            double padLeft = 0.0, padTop = 0.0, padRight = 0.0, padBottom = 0.0;

            if (mode == AutoLayoutMode.Horizontal)
            {
                ordered = topLayers.OrderBy(l => l.X).ThenBy(l => l.Y).ToList();
                if (ordered.Count > 1)
                {
                    double sumW = ordered.Sum(l => l.Width);
                    measuredGap = Math.Max(0.0, (totalW - sumW) / (ordered.Count - 1));
                }
                padLeft = Math.Max(0.0, ordered.First().X - minX);
                padRight = Math.Max(0.0, maxX - (ordered.Last().X + ordered.Last().Width));
                padTop = Math.Max(0.0, ordered.Min(l => l.Y) - minY);
                padBottom = Math.Max(0.0, maxY - ordered.Max(l => l.Y + l.Height));
            }
            else if (mode == AutoLayoutMode.Vertical)
            {
                ordered = topLayers.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
                if (ordered.Count > 1)
                {
                    double sumH = ordered.Sum(l => l.Height);
                    measuredGap = Math.Max(0.0, (totalH - sumH) / (ordered.Count - 1));
                }
                padTop = Math.Max(0.0, ordered.First().Y - minY);
                padBottom = Math.Max(0.0, maxY - (ordered.Last().Y + ordered.Last().Height));
                padLeft = Math.Max(0.0, ordered.Min(l => l.X) - minX);
                padRight = Math.Max(0.0, maxX - ordered.Max(l => l.X + l.Width));
            }
            else // Wrap
            {
                ordered = topLayers.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
                measuredGap = MeasureWrapGap(ordered);
                padLeft = Math.Max(0.0, ordered.Min(l => l.X) - minX);
                padTop = Math.Max(0.0, ordered.Min(l => l.Y) - minY);
                padRight = Math.Max(0.0, maxX - ordered.Max(l => l.X + l.Width));
                padBottom = Math.Max(0.0, maxY - ordered.Max(l => l.Y + l.Height));
            }

            var frame = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = "Auto Layout Frame",
                Type = ElementType.Frame,
                X = minX,
                Y = minY,
                Width = totalW,
                Height = totalH,
                ClipContent = false,
                AutoLayout = new AutoLayoutData
                {
                    Mode = mode,
                    Gap = Math.Round(measuredGap, 1),
                    PaddingTop = Math.Round(padTop, 1),
                    PaddingRight = Math.Round(padRight, 1),
                    PaddingBottom = Math.Round(padBottom, 1),
                    PaddingLeft = Math.Round(padLeft, 1),
                    AlignItems = AlignItems.Start,
                    JustifyContent = JustifyContent.Start,
                    SizingH = SizingMode.Hug,
                    SizingV = SizingMode.Hug
                },
                Fills = new List<Paint>(),
                Children = new List<LayerNode>()
            };

            var first = ordered[0];
            LayerNode parent;
            var container = page.FindContainer(first, out parent);
            if (container == null) container = page.Layers;
            int insertIdx = container.IndexOf(first);
            if (insertIdx < 0) insertIdx = container.Count;

            foreach (var layer in ordered)
            {
                page.RemoveLayerEverywhere(layer);
                layer.ParentId = frame.Id;
                frame.Children.Add(layer);
            }

            frame.ParentId = parent != null ? parent.Id : null;
            if (insertIdx <= container.Count)
            {
                container.Insert(insertIdx, frame);
            }
            else
            {
                container.Add(frame);
            }

            ReflowHierarchy(page, frame);
            return frame;
        }

        public static ICommand CreateAutoLayoutCommand(Page page, List<LayerNode> selectedLayers, Action onRender)
        {
            if (page == null || selectedLayers == null || selectedLayers.Count == 0) return null;

            // Filter out any layer whose ancestor is also selected
            var topLayers = selectedLayers.Where(l => !selectedLayers.Any(other => other != l && Page.IsDescendant(other, l))).ToList();
            if (topLayers.Count == 0) return null;

            // If a single frame is selected, create ToggleAutoLayoutCommand
            if (topLayers.Count == 1 && topLayers[0].Type == ElementType.Frame)
            {
                var singleFrame = topLayers[0];
                var oldLayout = singleFrame.AutoLayout != null ? singleFrame.AutoLayout.Clone() : new AutoLayoutData();
                var newLayout = oldLayout.Clone();

                if (oldLayout.Mode == AutoLayoutMode.None)
                {
                    var smartMode = (singleFrame.Children != null && singleFrame.Children.Count >= 2)
                        ? DetectSmartLayoutMode(singleFrame.Children)
                        : ((singleFrame.Height > singleFrame.Width * 1.2) ? AutoLayoutMode.Vertical : AutoLayoutMode.Horizontal);
                    newLayout.Mode = smartMode;
                    newLayout.SizingH = SizingMode.Hug;
                    newLayout.SizingV = SizingMode.Hug;
                    newLayout.JustifyContent = JustifyContent.Start;
                    newLayout.AlignItems = AlignItems.Start;
                }
                else if (oldLayout.Mode == AutoLayoutMode.Horizontal)
                {
                    newLayout.Mode = AutoLayoutMode.Vertical;
                }
                else if (oldLayout.Mode == AutoLayoutMode.Vertical)
                {
                    newLayout.Mode = AutoLayoutMode.Horizontal;
                }
                else
                {
                    newLayout.Mode = AutoLayoutMode.Horizontal;
                }

                return new ToggleAutoLayoutCommand(page, singleFrame, oldLayout, newLayout, onRender);
            }

            // Build AutoLayoutSelectionCommand
            if (topLayers.Count == 1)
            {
                var single = topLayers[0];
                double pad = 10.0;
                double fw = single.Width + pad * 2.0;
                double fh = single.Height + pad * 2.0;

                var singleWrapFrame = new LayerNode
                {
                    Id = Guid.NewGuid().ToString().Substring(0, 8),
                    Name = "Auto Layout Frame",
                    Type = ElementType.Frame,
                    X = single.X,
                    Y = single.Y,
                    Width = fw,
                    Height = fh,
                    ClipContent = false,
                    AutoLayout = new AutoLayoutData
                    {
                        Mode = AutoLayoutMode.Horizontal,
                        Gap = 0.0,
                        PaddingTop = pad,
                        PaddingRight = pad,
                        PaddingBottom = pad,
                        PaddingLeft = pad,
                        AlignItems = AlignItems.Start,
                        JustifyContent = JustifyContent.Start,
                        SizingH = SizingMode.Hug,
                        SizingV = SizingMode.Hug
                    },
                    Fills = new List<Paint>(),
                    Children = new List<LayerNode>()
                };

                LayerNode p;
                var c = page.FindContainer(single, out p);
                int insIdx = (c != null) ? c.IndexOf(single) : -1;
                string pId = p != null ? p.Id : null;

                var records = new List<AutoLayoutSelectionCommand.ChildRecord>
                {
                    new AutoLayoutSelectionCommand.ChildRecord
                    {
                        Layer = single,
                        OriginalParentId = pId,
                        OriginalIndex = insIdx,
                        OriginalX = single.X,
                        OriginalY = single.Y,
                        OriginalWidth = single.Width,
                        OriginalHeight = single.Height
                    }
                };

                return new AutoLayoutSelectionCommand(page, singleWrapFrame, records, pId, insIdx, onRender);
            }

            // Check if one layer is a background container shape enclosing the other selected layers (e.g. Button pattern)
            var bgShape = FindBackgroundShapeForSelection(topLayers);
            if (bgShape != null)
            {
                var fgLayers = topLayers.Where(l => l != bgShape).ToList();
                List<LayerNode> orderedFg;
                var buttonFrame = BuildButtonFrameFromBackground(bgShape, fgLayers, out orderedFg);

                LayerNode bgParent;
                var bgContainer = page.FindContainer(bgShape, out bgParent);
                string bgParentId = bgParent != null ? bgParent.Id : null;
                int bgInsertIdx = (bgContainer != null) ? bgContainer.IndexOf(bgShape) : -1;
                if (bgInsertIdx < 0) bgInsertIdx = (bgContainer != null ? bgContainer.Count : 0);

                var bgRec = new AutoLayoutSelectionCommand.ChildRecord
                {
                    Layer = bgShape,
                    OriginalParentId = bgParentId,
                    OriginalIndex = bgInsertIdx,
                    OriginalX = bgShape.X,
                    OriginalY = bgShape.Y,
                    OriginalWidth = bgShape.Width,
                    OriginalHeight = bgShape.Height
                };

                var fgRecs = new List<AutoLayoutSelectionCommand.ChildRecord>();
                for (int i = 0; i < orderedFg.Count; i++)
                {
                    var l = orderedFg[i];
                    LayerNode lp;
                    var lc = page.FindContainer(l, out lp);
                    int li = lc != null ? lc.IndexOf(l) : i;
                    fgRecs.Add(new AutoLayoutSelectionCommand.ChildRecord
                    {
                        Layer = l,
                        OriginalParentId = lp != null ? lp.Id : null,
                        OriginalIndex = li,
                        OriginalX = l.X,
                        OriginalY = l.Y,
                        OriginalWidth = l.Width,
                        OriginalHeight = l.Height
                    });
                }

                return new AutoLayoutSelectionCommand(page, buttonFrame, fgRecs, bgParentId, bgInsertIdx, onRender, bgRec);
            }

            // Multiple layers: smartly detect layout mode and wrap
            double minX = topLayers.Min(l => l.X);
            double minY = topLayers.Min(l => l.Y);
            double maxX = topLayers.Max(l => l.X + Math.Max(1.0, l.Width));
            double maxY = topLayers.Max(l => l.Y + Math.Max(1.0, l.Height));
            double totalW = Math.Max(1.0, maxX - minX);
            double totalH = Math.Max(1.0, maxY - minY);

            var mode = DetectSmartLayoutMode(topLayers);

            List<LayerNode> ordered;
            double measuredGap = 0.0;
            double padLeft = 0.0, padTop = 0.0, padRight = 0.0, padBottom = 0.0;

            if (mode == AutoLayoutMode.Horizontal)
            {
                ordered = topLayers.OrderBy(l => l.X).ThenBy(l => l.Y).ToList();
                if (ordered.Count > 1)
                {
                    double sumW = ordered.Sum(l => l.Width);
                    measuredGap = Math.Max(0.0, (totalW - sumW) / (ordered.Count - 1));
                }
                padLeft = Math.Max(0.0, ordered.First().X - minX);
                padRight = Math.Max(0.0, maxX - (ordered.Last().X + ordered.Last().Width));
                padTop = Math.Max(0.0, ordered.Min(l => l.Y) - minY);
                padBottom = Math.Max(0.0, maxY - ordered.Max(l => l.Y + l.Height));
            }
            else if (mode == AutoLayoutMode.Vertical)
            {
                ordered = topLayers.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
                if (ordered.Count > 1)
                {
                    double sumH = ordered.Sum(l => l.Height);
                    measuredGap = Math.Max(0.0, (totalH - sumH) / (ordered.Count - 1));
                }
                padTop = Math.Max(0.0, ordered.First().Y - minY);
                padBottom = Math.Max(0.0, maxY - (ordered.Last().Y + ordered.Last().Height));
                padLeft = Math.Max(0.0, ordered.Min(l => l.X) - minX);
                padRight = Math.Max(0.0, maxX - ordered.Max(l => l.X + l.Width));
            }
            else // Wrap
            {
                ordered = topLayers.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
                measuredGap = MeasureWrapGap(ordered);
                padLeft = Math.Max(0.0, ordered.Min(l => l.X) - minX);
                padTop = Math.Max(0.0, ordered.Min(l => l.Y) - minY);
                padRight = Math.Max(0.0, maxX - ordered.Max(l => l.X + l.Width));
                padBottom = Math.Max(0.0, maxY - ordered.Max(l => l.Y + l.Height));
            }

            var frame = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = "Auto Layout Frame",
                Type = ElementType.Frame,
                X = minX,
                Y = minY,
                Width = totalW,
                Height = totalH,
                ClipContent = false,
                AutoLayout = new AutoLayoutData
                {
                    Mode = mode,
                    Gap = Math.Round(measuredGap, 1),
                    PaddingTop = Math.Round(padTop, 1),
                    PaddingRight = Math.Round(padRight, 1),
                    PaddingBottom = Math.Round(padBottom, 1),
                    PaddingLeft = Math.Round(padLeft, 1),
                    AlignItems = AlignItems.Start,
                    JustifyContent = JustifyContent.Start,
                    SizingH = SizingMode.Hug,
                    SizingV = SizingMode.Hug
                },
                Fills = new List<Paint>(),
                Children = new List<LayerNode>()
            };

            var first = ordered[0];
            LayerNode parent;
            var container = page.FindContainer(first, out parent);
            int insertIdx = (container != null) ? container.IndexOf(first) : -1;
            string frameParentId = parent != null ? parent.Id : null;

            var recs = new List<AutoLayoutSelectionCommand.ChildRecord>();
            foreach (var l in ordered)
            {
                LayerNode p;
                var c = page.FindContainer(l, out p);
                int idx = (c != null) ? c.IndexOf(l) : -1;
                recs.Add(new AutoLayoutSelectionCommand.ChildRecord
                {
                    Layer = l,
                    OriginalParentId = (p != null) ? p.Id : null,
                    OriginalIndex = idx,
                    OriginalX = l.X,
                    OriginalY = l.Y,
                    OriginalWidth = l.Width,
                    OriginalHeight = l.Height
                });
            }

            return new AutoLayoutSelectionCommand(page, frame, recs, frameParentId, insertIdx, onRender);
        }
    }
}
