using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Kerf.Model;

namespace Kerf.Tools
{
    public enum ToolType
    {
        Select,
        Marquee,
        Hand,
        Frame,
        Rect,
        Ellipse,
        Star,
        Polygon,
        Pen,
        Text,
        Line,
        Arrow
    }

    public enum HandleType
    {
        None,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
        Rotate,
        CornerRadius
    }

    public class TransformResult
    {
        public Rect Bounds { get; set; }
        public double Rotation { get; set; }

        public TransformResult(Rect bounds, double rotation)
        {
            Bounds = bounds;
            Rotation = rotation;
        }
    }

    public class TransformEngine
    {
        public const double HandleSize = 8.0;
        public const double RotateOffset = 24.0;
        public const double HitTolerance = 6.0;

        public static Dictionary<HandleType, Point> GetHandlePositions(Rect bounds)
        {
            var dict = new Dictionary<HandleType, Point>();
            double x = bounds.X;
            double y = bounds.Y;
            double w = bounds.Width;
            double h = bounds.Height;
            double mx = x + w / 2.0;
            double my = y + h / 2.0;

            dict[HandleType.TopLeft] = new Point(x, y);
            dict[HandleType.TopCenter] = new Point(mx, y);
            dict[HandleType.TopRight] = new Point(x + w, y);
            dict[HandleType.MiddleLeft] = new Point(x, my);
            dict[HandleType.MiddleRight] = new Point(x + w, my);
            dict[HandleType.BottomLeft] = new Point(x, y + h);
            dict[HandleType.BottomCenter] = new Point(mx, y + h);
            dict[HandleType.BottomRight] = new Point(x + w, y + h);
            dict[HandleType.Rotate] = new Point(mx, y - RotateOffset);

            return dict;
        }

        public static HandleType HitTestHandle(Point pt, Rect bounds, double rotation, double zoomScale)
        {
            double tolerance = (HitTolerance + HandleSize / 2.0) / Math.Max(0.1, zoomScale);
            var center = new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0);

            // Unrotate mouse point to evaluate in local axis-aligned space
            Point localPt = pt;
            if (Math.Abs(rotation) > 0.001)
            {
                var mat = Matrix.Identity;
                mat.RotateAt(-rotation, center.X, center.Y);
                localPt = mat.Transform(pt);
            }

            var handles = GetHandlePositions(bounds);
            foreach (var kvp in handles)
            {
                var hPt = kvp.Value;
                if (Math.Abs(localPt.X - hPt.X) <= tolerance && Math.Abs(localPt.Y - hPt.Y) <= tolerance)
                {
                    return kvp.Key;
                }
            }

            return HandleType.None;
        }

        public static bool HitTestCornerRadiusHandle(Point pt, Rect bounds, double rotation, double currentRadius, double zoomScale)
        {
            if (bounds.Width < 20 || bounds.Height < 20) return false;
            double tolerance = (HitTolerance + HandleSize / 2.0) / Math.Max(0.1, zoomScale);
            var center = new Point(bounds.X + bounds.Width / 2.0, bounds.Y + bounds.Height / 2.0);

            Point localPt = pt;
            if (Math.Abs(rotation) > 0.001)
            {
                var mat = Matrix.Identity;
                mat.RotateAt(-rotation, center.X, center.Y);
                localPt = mat.Transform(pt);
            }

            double maxRadius = Math.Min(bounds.Width, bounds.Height) / 2.0;
            double cr = Math.Min(currentRadius, maxRadius);
            double offset = Math.Max(10.0, Math.Min(14.0 + cr * 0.4, maxRadius * 0.7));

            Point[] crPoints = new Point[]
            {
                new Point(bounds.X + offset, bounds.Y + offset),
                new Point(bounds.X + bounds.Width - offset, bounds.Y + offset),
                new Point(bounds.X + bounds.Width - offset, bounds.Y + bounds.Height - offset),
                new Point(bounds.X + offset, bounds.Y + bounds.Height - offset)
            };

            foreach (var p in crPoints)
            {
                if (Math.Abs(localPt.X - p.X) <= tolerance && Math.Abs(localPt.Y - p.Y) <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        public static TransformResult ApplyTransform(
            HandleType handle,
            Point currentPoint,
            Point startPoint,
            Rect origBounds,
            double origRotation,
            bool keepAspectRatio,
            bool fromCenter,
            double snapGridSize)
        {
            if (handle == HandleType.None)
            {
                return new TransformResult(origBounds, origRotation);
            }

            var center = new Point(origBounds.X + origBounds.Width / 2.0, origBounds.Y + origBounds.Height / 2.0);

            // Rotation handle
            if (handle == HandleType.Rotate)
            {
                double angleStart = Math.Atan2(startPoint.Y - center.Y, startPoint.X - center.X) * (180.0 / Math.PI);
                double angleCurrent = Math.Atan2(currentPoint.Y - center.Y, currentPoint.X - center.X) * (180.0 / Math.PI);
                double delta = angleCurrent - angleStart;
                double newRot = origRotation + delta;

                // Snap to 15 degree increments if Shift is held (keepAspectRatio parameter can act as angle snap)
                if (keepAspectRatio)
                {
                    newRot = Math.Round(newRot / 15.0) * 15.0;
                }

                while (newRot < 0) newRot += 360.0;
                while (newRot >= 360.0) newRot -= 360.0;

                return new TransformResult(origBounds, newRot);
            }

            // Unrotate current and start points around center to compute delta in element local space
            Point curLocal = currentPoint;
            Point startLocal = startPoint;
            if (Math.Abs(origRotation) > 0.001)
            {
                var mat = Matrix.Identity;
                mat.RotateAt(-origRotation, center.X, center.Y);
                curLocal = mat.Transform(currentPoint);
                startLocal = mat.Transform(startPoint);
            }

            double dx = curLocal.X - startLocal.X;
            double dy = curLocal.Y - startLocal.Y;

            // Optional grid snap
            if (snapGridSize > 1.0)
            {
                dx = Math.Round(dx / snapGridSize) * snapGridSize;
                dy = Math.Round(dy / snapGridSize) * snapGridSize;
            }

            double x = origBounds.X;
            double y = origBounds.Y;
            double w = origBounds.Width;
            double h = origBounds.Height;
            double origRatio = w / Math.Max(1.0, h);

            switch (handle)
            {
                case HandleType.TopLeft:
                    if (fromCenter) { x += dx; y += dy; w -= 2 * dx; h -= 2 * dy; }
                    else { x += dx; y += dy; w -= dx; h -= dy; }
                    if (keepAspectRatio)
                    {
                        double targetH = w / origRatio;
                        y += (h - targetH);
                        h = targetH;
                    }
                    break;

                case HandleType.TopCenter:
                    if (fromCenter) { y += dy; h -= 2 * dy; }
                    else { y += dy; h -= dy; }
                    break;

                case HandleType.TopRight:
                    if (fromCenter) { x -= dx; y += dy; w += 2 * dx; h -= 2 * dy; }
                    else { y += dy; w += dx; h -= dy; }
                    if (keepAspectRatio)
                    {
                        double targetH = w / origRatio;
                        y += (h - targetH);
                        h = targetH;
                    }
                    break;

                case HandleType.MiddleLeft:
                    if (fromCenter) { x += dx; w -= 2 * dx; }
                    else { x += dx; w -= dx; }
                    break;

                case HandleType.MiddleRight:
                    if (fromCenter) { x -= dx; w += 2 * dx; }
                    else { w += dx; }
                    break;

                case HandleType.BottomLeft:
                    if (fromCenter) { x += dx; y -= dy; w -= 2 * dx; h += 2 * dy; }
                    else { x += dx; w -= dx; h += dy; }
                    if (keepAspectRatio)
                    {
                        h = w / origRatio;
                    }
                    break;

                case HandleType.BottomCenter:
                    if (fromCenter) { y -= dy; h += 2 * dy; }
                    else { h += dy; }
                    break;

                case HandleType.BottomRight:
                    if (fromCenter) { x -= dx; y -= dy; w += 2 * dx; h += 2 * dy; }
                    else { w += dx; h += dy; }
                    if (keepAspectRatio)
                    {
                        h = w / origRatio;
                    }
                    break;
            }

            // Prevent negative or zero dimensions
            if (w < 4.0) w = 4.0;
            if (h < 4.0) h = 4.0;

            return new TransformResult(new Rect(x, y, w, h), origRotation);
        }

        public static Rect UnionBounds(IEnumerable<LayerNode> nodes)
        {
            Rect result = Rect.Empty;
            if (nodes == null) return result;

            foreach (var n in nodes)
            {
                if (!n.IsVisible) continue;
                var b = n.GetBounds();
                result = result.IsEmpty ? b : Rect.Union(result, b);
            }

            return result;
        }
    }
}
