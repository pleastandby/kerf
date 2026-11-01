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
                ReflowRecursive(root);
            }
        }

        public static void ReflowRecursive(LayerNode node)
        {
            if (node == null) return;

            // 1. Reflow nested children first (bottom-up) so containers hugging children get correct dimensions
            if (node.Children != null && node.Children.Count > 0)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    ReflowRecursive(node.Children[i]);
                }
            }

            // 2. Reflow current container
            if (node.AutoLayout != null && node.AutoLayout.Mode != AutoLayoutMode.None)
            {
                Reflow(node);

                // 3. If any child has Fill sizing and also has auto layout, reflow it with updated dimensions
                if (node.Children != null)
                {
                    for (int i = 0; i < node.Children.Count; i++)
                    {
                        var child = node.Children[i];
                        if ((child.LayoutSizingH == SizingMode.Fill || child.LayoutSizingV == SizingMode.Fill) &&
                            child.AutoLayout != null && child.AutoLayout.Mode != AutoLayoutMode.None)
                        {
                            Reflow(child);
                        }
                    }
                }
            }
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

            var layout = frame.AutoLayout;

            switch (layout.Mode)
            {
                case AutoLayoutMode.Horizontal:
                    ReflowHorizontal(frame, visibleChildren, layout);
                    break;
                case AutoLayoutMode.Vertical:
                    ReflowVertical(frame, visibleChildren, layout);
                    break;
                case AutoLayoutMode.Wrap:
                    ReflowWrap(frame, visibleChildren, layout);
                    break;
                case AutoLayoutMode.Grid:
                    ReflowGrid(frame, visibleChildren, layout);
                    break;
            }
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
            double availWidth = Math.Max(100.0, frame.Width - layout.PaddingLeft - layout.PaddingRight);
            double curX = frame.X + layout.PaddingLeft;
            double curY = frame.Y + layout.PaddingTop;
            double rowHeight = 0.0;
            double maxRowW = 0.0;

            foreach (var child in children)
            {
                if (curX + child.Width > frame.X + layout.PaddingLeft + availWidth && curX > frame.X + layout.PaddingLeft)
                {
                    // Wrap to next line
                    curX = frame.X + layout.PaddingLeft;
                    curY += rowHeight + layout.Gap;
                    rowHeight = 0.0;
                }

                child.X = curX;
                child.Y = curY;

                rowHeight = Math.Max(rowHeight, child.Height);
                curX += child.Width + layout.Gap;
                maxRowW = Math.Max(maxRowW, curX - (frame.X + layout.PaddingLeft));
            }

            double totalH = curY + rowHeight - frame.Y + layout.PaddingBottom;

            if (layout.SizingH == SizingMode.Hug)
            {
                frame.Width = Math.Max(1.0, maxRowW + layout.PaddingLeft + layout.PaddingRight);
            }
            if (layout.SizingV == SizingMode.Hug)
            {
                frame.Height = Math.Max(1.0, totalH);
            }
        }

        private static void ReflowGrid(LayerNode frame, List<LayerNode> children, AutoLayoutData layout)
        {
            int cols = Math.Max(1, layout.GridColumns);
            double availWidth = Math.Max(100.0, frame.Width - layout.PaddingLeft - layout.PaddingRight);
            double colWidth = (availWidth - (cols - 1) * layout.Gap) / cols;

            double startX = frame.X + layout.PaddingLeft;
            double curY = frame.Y + layout.PaddingTop;
            double rowMaxH = 0.0;

            for (int i = 0; i < children.Count; i++)
            {
                int col = i % cols;
                if (col == 0 && i > 0)
                {
                    curY += rowMaxH + layout.Gap;
                    rowMaxH = 0.0;
                }

                var child = children[i];
                child.X = startX + col * (colWidth + layout.Gap);
                child.Y = curY;
                child.Width = colWidth;

                rowMaxH = Math.Max(rowMaxH, child.Height);
            }

            double totalH = curY + rowMaxH - frame.Y + layout.PaddingBottom;
            if (layout.SizingV == SizingMode.Hug)
            {
                frame.Height = Math.Max(1.0, totalH);
            }
        }

        public static LayerNode AutoAlignSelection(Page page, List<LayerNode> selectedLayers)
        {
            if (page == null || selectedLayers == null || selectedLayers.Count == 0) return null;

            // Filter out any layer whose ancestor is also selected
            var topLayers = selectedLayers.Where(l => !selectedLayers.Any(other => other != l && Page.IsDescendant(other, l))).ToList();
            if (topLayers.Count == 0) return null;

            // If a single frame is selected, toggle its AutoLayout mode (None -> Horizontal -> Vertical -> Horizontal)
            if (topLayers.Count == 1 && topLayers[0].Type == ElementType.Frame)
            {
                var singleFrame = topLayers[0];
                if (singleFrame.AutoLayout == null) singleFrame.AutoLayout = new AutoLayoutData();

                if (singleFrame.AutoLayout.Mode == AutoLayoutMode.None)
                {
                    singleFrame.AutoLayout.Mode = AutoLayoutMode.Horizontal;
                }
                else if (singleFrame.AutoLayout.Mode == AutoLayoutMode.Horizontal)
                {
                    singleFrame.AutoLayout.Mode = AutoLayoutMode.Vertical;
                }
                else
                {
                    singleFrame.AutoLayout.Mode = AutoLayoutMode.Horizontal;
                }

                Reflow(singleFrame);
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

                Reflow(singleWrapFrame);
                return singleWrapFrame;
            }

            // If multiple layers selected: wrap full bounding envelope into Auto Layout Frame
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var l in topLayers)
            {
                minX = Math.Min(minX, l.X);
                minY = Math.Min(minY, l.Y);
                maxX = Math.Max(maxX, l.X + Math.Max(1.0, l.Width));
                maxY = Math.Max(maxY, l.Y + Math.Max(1.0, l.Height));
            }

            double totalW = Math.Max(1.0, maxX - minX);
            double totalH = Math.Max(1.0, maxY - minY);
            var mode = (totalW >= totalH) ? AutoLayoutMode.Horizontal : AutoLayoutMode.Vertical;

            // Order layers spatially depending on mode
            var ordered = (mode == AutoLayoutMode.Horizontal)
                ? topLayers.OrderBy(l => l.X).ToList()
                : topLayers.OrderBy(l => l.Y).ToList();

            // Measure natural gap between items along primary axis to preserve exact outer span
            double measuredGap = 0.0;
            if (ordered.Count > 1)
            {
                if (mode == AutoLayoutMode.Horizontal)
                {
                    double sumW = ordered.Sum(l => l.Width);
                    measuredGap = Math.Max(0.0, (totalW - sumW) / (ordered.Count - 1));
                }
                else
                {
                    double sumH = ordered.Sum(l => l.Height);
                    measuredGap = Math.Max(0.0, (totalH - sumH) / (ordered.Count - 1));
                }
            }

            // Calculate exact padding envelope from outer bounds
            double padLeft = Math.Max(0.0, ordered.Min(l => l.X) - minX);
            double padTop = Math.Max(0.0, ordered.Min(l => l.Y) - minY);
            double padRight = Math.Max(0.0, maxX - ordered.Max(l => l.X + l.Width));
            double padBottom = Math.Max(0.0, maxY - ordered.Max(l => l.Y + l.Height));

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

            Reflow(frame);
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
                    newLayout.Mode = AutoLayoutMode.Horizontal;
                }
                else if (oldLayout.Mode == AutoLayoutMode.Horizontal)
                {
                    newLayout.Mode = AutoLayoutMode.Vertical;
                }
                else
                {
                    newLayout.Mode = AutoLayoutMode.Horizontal;
                }

                return new ToggleAutoLayoutCommand(singleFrame, oldLayout, newLayout, onRender);
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

            // Multiple layers
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var l in topLayers)
            {
                minX = Math.Min(minX, l.X);
                minY = Math.Min(minY, l.Y);
                maxX = Math.Max(maxX, l.X + Math.Max(1.0, l.Width));
                maxY = Math.Max(maxY, l.Y + Math.Max(1.0, l.Height));
            }

            double totalW = Math.Max(1.0, maxX - minX);
            double totalH = Math.Max(1.0, maxY - minY);
            var mode = (totalW >= totalH) ? AutoLayoutMode.Horizontal : AutoLayoutMode.Vertical;

            var ordered = (mode == AutoLayoutMode.Horizontal)
                ? topLayers.OrderBy(l => l.X).ToList()
                : topLayers.OrderBy(l => l.Y).ToList();

            double measuredGap = 0.0;
            if (ordered.Count > 1)
            {
                if (mode == AutoLayoutMode.Horizontal)
                {
                    double sumW = ordered.Sum(l => l.Width);
                    measuredGap = Math.Max(0.0, (totalW - sumW) / (ordered.Count - 1));
                }
                else
                {
                    double sumH = ordered.Sum(l => l.Height);
                    measuredGap = Math.Max(0.0, (totalH - sumH) / (ordered.Count - 1));
                }
            }

            double padLeft = Math.Max(0.0, ordered.Min(l => l.X) - minX);
            double padTop = Math.Max(0.0, ordered.Min(l => l.Y) - minY);
            double padRight = Math.Max(0.0, maxX - ordered.Max(l => l.X + l.Width));
            double padBottom = Math.Max(0.0, maxY - ordered.Max(l => l.Y + l.Height));

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
