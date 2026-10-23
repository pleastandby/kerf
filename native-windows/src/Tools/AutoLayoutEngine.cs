using System;
using System.Collections.Generic;
using System.Linq;
using Kerf.Model;

namespace Kerf.Tools
{
    public static class AutoLayoutEngine
    {
        public static void Reflow(LayerNode frame)
        {
            if (frame == null || frame.AutoLayout == null) return;
            if (frame.AutoLayout.Mode == AutoLayoutMode.None) return;
            if (frame.Children == null || frame.Children.Count == 0) return;

            var visibleChildren = frame.Children.Where(c => c.IsVisible).ToList();
            if (visibleChildren.Count == 0) return;

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
            double curX = frame.X + layout.PaddingLeft;
            double maxHeight = 0.0;
            double totalWidth = 0.0;

            // First pass: measure
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                maxHeight = Math.Max(maxHeight, child.Height);
                totalWidth += child.Width;
                if (i > 0) totalWidth += layout.Gap;
            }

            // Available width for justify
            double availWidth = frame.Width - layout.PaddingLeft - layout.PaddingRight;
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
            else if (layout.JustifyContent == JustifyContent.SpaceBetween && children.Count > 1)
            {
                gap += extraSpace / (children.Count - 1);
            }

            curX = startX;

            // Second pass: position
            foreach (var child in children)
            {
                child.X = curX;

                // Vertical alignment
                double innerY = frame.Y + layout.PaddingTop;
                double availInnerH = frame.Height - layout.PaddingTop - layout.PaddingBottom;

                switch (layout.AlignItems)
                {
                    case AlignItems.Center:
                        child.Y = innerY + (availInnerH - child.Height) / 2.0;
                        break;
                    case AlignItems.End:
                        child.Y = innerY + (availInnerH - child.Height);
                        break;
                    case AlignItems.Start:
                    default:
                        child.Y = innerY;
                        break;
                }

                curX += child.Width + gap;
            }

            // Hug sizing on parent
            if (layout.SizingH == SizingMode.Hug)
            {
                frame.Width = Math.Max(40.0, totalWidth + layout.PaddingLeft + layout.PaddingRight);
            }
            if (layout.SizingV == SizingMode.Hug)
            {
                frame.Height = Math.Max(40.0, maxHeight + layout.PaddingTop + layout.PaddingBottom);
            }
        }

        private static void ReflowVertical(LayerNode frame, List<LayerNode> children, AutoLayoutData layout)
        {
            double curY = frame.Y + layout.PaddingTop;
            double maxWidth = 0.0;
            double totalHeight = 0.0;

            // First pass: measure
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                maxWidth = Math.Max(maxWidth, child.Width);
                totalHeight += child.Height;
                if (i > 0) totalHeight += layout.Gap;
            }

            double availHeight = frame.Height - layout.PaddingTop - layout.PaddingBottom;
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
            else if (layout.JustifyContent == JustifyContent.SpaceBetween && children.Count > 1)
            {
                gap += extraSpace / (children.Count - 1);
            }

            curY = startY;

            // Second pass: position
            foreach (var child in children)
            {
                child.Y = curY;

                // Horizontal alignment
                double innerX = frame.X + layout.PaddingLeft;
                double availInnerW = frame.Width - layout.PaddingLeft - layout.PaddingRight;

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

            // Hug sizing on parent
            if (layout.SizingH == SizingMode.Hug)
            {
                frame.Width = Math.Max(40.0, maxWidth + layout.PaddingLeft + layout.PaddingRight);
            }
            if (layout.SizingV == SizingMode.Hug)
            {
                frame.Height = Math.Max(40.0, totalHeight + layout.PaddingTop + layout.PaddingBottom);
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
                frame.Width = Math.Max(40.0, maxRowW + layout.PaddingLeft + layout.PaddingRight);
            }
            if (layout.SizingV == SizingMode.Hug)
            {
                frame.Height = Math.Max(40.0, totalH);
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
                frame.Height = Math.Max(40.0, totalH);
            }
        }
    }
}
