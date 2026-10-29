using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Kerf.Engine;
using Kerf.Model;

namespace Kerf.Tools
{
    public enum GradientHandleType
    {
        None,
        Start,
        End,
        StopNode,
        Line
    }

    public class GradientHitResult
    {
        public GradientHandleType Type { get; set; }
        public int StopIndex { get; set; }
        public double ParameterT { get; set; }
        public Paint TargetPaint { get; set; }

        public GradientHitResult()
        {
            Type = GradientHandleType.None;
            StopIndex = -1;
            ParameterT = 0.0;
        }
    }

    /// <summary>
    /// Figma-style on-canvas gradient roller & handle engine.
    /// Provides direct manipulation of gradient angle, direction, and color stops on the canvas.
    /// </summary>
    public static class GradientRollerEngine
    {
        public static bool GetGradientPoints(
            LayerNode node,
            Paint paint,
            out Point startPt,
            out Point endPt,
            out List<Point> stopPoints)
        {
            startPt = new Point(0, 0);
            endPt = new Point(0, 0);
            stopPoints = new List<Point>();

            if (node == null || paint == null) return false;
            if (paint.Type != PaintType.LinearGradient && paint.Type != PaintType.RadialGradient) return false;

            double w = Math.Max(1.0, node.Width);
            double h = Math.Max(1.0, node.Height);
            Point center = new Point(node.X + w / 2.0, node.Y + h / 2.0);

            if (paint.Type == PaintType.LinearGradient)
            {
                double rad = (paint.GradientAngle * Math.PI) / 180.0;
                double x1 = 0.5 - 0.5 * Math.Cos(rad);
                double y1 = 0.5 - 0.5 * Math.Sin(rad);
                double x2 = 0.5 + 0.5 * Math.Cos(rad);
                double y2 = 0.5 + 0.5 * Math.Sin(rad);

                if (paint.GradientStart.HasValue)
                {
                    x1 = paint.GradientStart.Value.X;
                    y1 = paint.GradientStart.Value.Y;
                }
                if (paint.GradientEnd.HasValue)
                {
                    x2 = paint.GradientEnd.Value.X;
                    y2 = paint.GradientEnd.Value.Y;
                }

                Point p1 = new Point(node.X + w * x1, node.Y + h * y1);
                Point p2 = new Point(node.X + w * x2, node.Y + h * y2);

                if (Math.Abs(node.Rotation) > 0.001)
                {
                    p1 = RotatePoint(p1, center, node.Rotation);
                    p2 = RotatePoint(p2, center, node.Rotation);
                }

                startPt = p1;
                endPt = p2;

                if (paint.GradientStops != null && paint.GradientStops.Count > 0)
                {
                    foreach (var s in paint.GradientStops)
                    {
                        double t = Math.Max(0.0, Math.Min(1.0, s.Position));
                        double sx = p1.X + t * (p2.X - p1.X);
                        double sy = p1.Y + t * (p2.Y - p1.Y);
                        stopPoints.Add(new Point(sx, sy));
                    }
                }

                return true;
            }
            else // RadialGradient
            {
                Point cNorm = paint.GradientCenter;
                Point pCenter = new Point(node.X + w * cNorm.X, node.Y + h * cNorm.Y);
                double rDist = Math.Max(10.0, Math.Min(w, h) * paint.GradientRadius);
                Point pRadius = new Point(pCenter.X + rDist, pCenter.Y);

                if (Math.Abs(node.Rotation) > 0.001)
                {
                    pCenter = RotatePoint(pCenter, center, node.Rotation);
                    pRadius = RotatePoint(pRadius, center, node.Rotation);
                }

                startPt = pCenter;
                endPt = pRadius;

                if (paint.GradientStops != null && paint.GradientStops.Count > 0)
                {
                    foreach (var s in paint.GradientStops)
                    {
                        double t = Math.Max(0.0, Math.Min(1.0, s.Position));
                        double sx = pCenter.X + t * (pRadius.X - pCenter.X);
                        double sy = pCenter.Y + t * (pRadius.Y - pCenter.Y);
                        stopPoints.Add(new Point(sx, sy));
                    }
                }

                return true;
            }
        }

        public static GradientHitResult HitTest(
            Point canvasPt,
            LayerNode node,
            Paint paint,
            double zoomScale)
        {
            var result = new GradientHitResult { TargetPaint = paint };
            if (node == null || paint == null) return result;

            Point startPt, endPt;
            List<Point> stopPts;
            if (!GetGradientPoints(node, paint, out startPt, out endPt, out stopPts))
            {
                return result;
            }

            double handleRadius = 12.0 / Math.Max(0.01, zoomScale);

            // 1. Check End Handle (direction / angle controller with cyan ring)
            if (Distance(canvasPt, endPt) <= handleRadius + 2.0 / zoomScale)
            {
                result.Type = GradientHandleType.End;
                result.StopIndex = (paint.GradientStops != null && paint.GradientStops.Count > 0) ? paint.GradientStops.Count - 1 : 1;
                result.ParameterT = 1.0;
                return result;
            }

            // 2. Check Start Handle (origin handle)
            if (Distance(canvasPt, startPt) <= handleRadius + 2.0 / zoomScale)
            {
                result.Type = GradientHandleType.Start;
                result.StopIndex = 0;
                result.ParameterT = 0.0;
                return result;
            }

            // 3. Check intermediate stop nodes strictly away from start and end handles
            if (stopPts != null && paint.GradientStops != null)
            {
                for (int i = 0; i < stopPts.Count; i++)
                {
                    // Don't intercept start or end handles
                    if (Distance(stopPts[i], startPt) < handleRadius || Distance(stopPts[i], endPt) < handleRadius)
                        continue;

                    if (Distance(canvasPt, stopPts[i]) <= handleRadius)
                    {
                        result.Type = GradientHandleType.StopNode;
                        result.StopIndex = i;
                        result.ParameterT = paint.GradientStops[i].Position;
                        return result;
                    }
                }
            }

            // 4. Check on-canvas roller line (clicking line adds a stop like in Figma)
            double lineDist = DistanceToSegment(canvasPt, startPt, endPt);
            if (lineDist <= 8.0 / Math.Max(0.01, zoomScale))
            {
                result.Type = GradientHandleType.Line;
                result.ParameterT = ProjectOntoSegment(canvasPt, startPt, endPt);
                return result;
            }

            return result;
        }

        /// <summary>
        /// Freely drags the Start handle on canvas. The End handle remains anchored in its position,
        /// allowing free stretching, resizing, and arbitrary positioning of the gradient line in 2D space.
        /// </summary>
        public static void UpdateStartHandleFromDrag(
            LayerNode node,
            Paint paint,
            Point currentCanvasPt,
            bool snapToAngles = false)
        {
            if (node == null || paint == null) return;
            double w = Math.Max(1.0, node.Width);
            double h = Math.Max(1.0, node.Height);
            Point center = new Point(node.X + w / 2.0, node.Y + h / 2.0);

            Point localMouse = currentCanvasPt;
            if (Math.Abs(node.Rotation) > 0.001)
            {
                localMouse = RotatePoint(localMouse, center, -node.Rotation);
            }

            if (paint.Type == PaintType.LinearGradient)
            {
                double u = (localMouse.X - node.X) / w;
                double v = (localMouse.Y - node.Y) / h;

                // Determine existing End handle position in UV space (or default from GradientAngle)
                Point endNorm;
                if (paint.GradientEnd.HasValue)
                {
                    endNorm = paint.GradientEnd.Value;
                }
                else
                {
                    double rad = (paint.GradientAngle * Math.PI) / 180.0;
                    endNorm = new Point(Math.Round(0.5 + 0.5 * Math.Cos(rad), 4), Math.Round(0.5 + 0.5 * Math.Sin(rad), 4));
                }

                // Vector from moving Start handle to fixed End handle in pixel space
                double dx = (endNorm.X - u) * w;
                double dy = (endNorm.Y - v) * h;

                if (snapToAngles && (dx * dx + dy * dy > 0.0001))
                {
                    double deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    if (deg < 0) deg += 360.0;
                    double snapped = Math.Round(deg / 45.0) * 45.0;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double snapRad = (snapped * Math.PI) / 180.0;
                    u = endNorm.X - (dist * Math.Cos(snapRad)) / w;
                    v = endNorm.Y - (dist * Math.Sin(snapRad)) / h;
                    dx = (endNorm.X - u) * w;
                    dy = (endNorm.Y - v) * h;
                }

                if (dx * dx + dy * dy > 0.0001)
                {
                    double deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    if (deg < 0) deg += 360.0;
                    paint.GradientAngle = Math.Round(deg, 1);
                }

                // Start handle moves freely; End handle stays fixed
                paint.GradientStart = new Point(Math.Round(u, 4), Math.Round(v, 4));
                paint.GradientEnd = endNorm;
            }
            else // RadialGradient
            {
                double u = (localMouse.X - node.X) / w;
                double v = (localMouse.Y - node.Y) / h;
                paint.GradientCenter = new Point(Math.Round(u, 4), Math.Round(v, 4));
            }
        }

        /// <summary>
        /// Freely drags the End handle on canvas. The Start handle remains anchored in its position,
        /// allowing free stretching, resizing, and arbitrary positioning of the gradient line in 2D space.
        /// </summary>
        public static void UpdateEndHandleFromDrag(
            LayerNode node,
            Paint paint,
            Point currentCanvasPt,
            bool snapToAngles = false)
        {
            if (node == null || paint == null) return;
            double w = Math.Max(1.0, node.Width);
            double h = Math.Max(1.0, node.Height);
            Point center = new Point(node.X + w / 2.0, node.Y + h / 2.0);

            Point localMouse = currentCanvasPt;
            if (Math.Abs(node.Rotation) > 0.001)
            {
                localMouse = RotatePoint(localMouse, center, -node.Rotation);
            }

            if (paint.Type == PaintType.LinearGradient)
            {
                double u = (localMouse.X - node.X) / w;
                double v = (localMouse.Y - node.Y) / h;

                // Determine existing Start handle position in UV space (or default from GradientAngle)
                Point startNorm;
                if (paint.GradientStart.HasValue)
                {
                    startNorm = paint.GradientStart.Value;
                }
                else
                {
                    double rad = (paint.GradientAngle * Math.PI) / 180.0;
                    startNorm = new Point(Math.Round(0.5 - 0.5 * Math.Cos(rad), 4), Math.Round(0.5 - 0.5 * Math.Sin(rad), 4));
                }

                // Vector from fixed Start handle to moving End handle in pixel space
                double dx = (u - startNorm.X) * w;
                double dy = (v - startNorm.Y) * h;

                if (snapToAngles && (dx * dx + dy * dy > 0.0001))
                {
                    double deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    if (deg < 0) deg += 360.0;
                    double snapped = Math.Round(deg / 45.0) * 45.0;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double snapRad = (snapped * Math.PI) / 180.0;
                    u = startNorm.X + (dist * Math.Cos(snapRad)) / w;
                    v = startNorm.Y + (dist * Math.Sin(snapRad)) / h;
                    dx = (u - startNorm.X) * w;
                    dy = (v - startNorm.Y) * h;
                }

                if (dx * dx + dy * dy > 0.0001)
                {
                    double deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    if (deg < 0) deg += 360.0;
                    paint.GradientAngle = Math.Round(deg, 1);
                }

                // End handle moves freely; Start handle stays fixed
                paint.GradientEnd = new Point(Math.Round(u, 4), Math.Round(v, 4));
                paint.GradientStart = startNorm;
            }
            else // RadialGradient
            {
                Point cNorm = paint.GradientCenter;
                Point pCenter = new Point(node.X + w * cNorm.X, node.Y + h * cNorm.Y);
                double dist = Distance(localMouse, pCenter);
                double r = dist / Math.Max(10.0, Math.Min(w, h));
                paint.GradientRadius = Math.Max(0.05, Math.Round(r, 3));
            }
        }

        /// <summary>
        /// Updates gradient angle based on dragging around layer center (symmetric angle control).
        /// </summary>
        public static void UpdateAngleFromDrag(
            LayerNode node,
            Paint paint,
            Point currentCanvasPt,
            bool isStartHandle)
        {
            if (node == null || paint == null) return;
            double w = Math.Max(1.0, node.Width);
            double h = Math.Max(1.0, node.Height);
            Point center = new Point(node.X + w / 2.0, node.Y + h / 2.0);

            Point localMouse = currentCanvasPt;
            if (Math.Abs(node.Rotation) > 0.001)
            {
                localMouse = RotatePoint(localMouse, center, -node.Rotation);
            }

            if (paint.Type == PaintType.LinearGradient)
            {
                double u = (localMouse.X - node.X) / w;
                double v = (localMouse.Y - node.Y) / h;

                double du = isStartHandle ? (0.5 - u) : (u - 0.5);
                double dv = isStartHandle ? (0.5 - v) : (v - 0.5);
                if (Math.Abs(du) > 0.0001 || Math.Abs(dv) > 0.0001)
                {
                    double deg = Math.Atan2(dv, du) * 180.0 / Math.PI;
                    if (deg < 0) deg += 360.0;
                    paint.GradientAngle = Math.Round(deg, 1);
                }

                if (isStartHandle)
                {
                    paint.GradientStart = new Point(Math.Round(u, 4), Math.Round(v, 4));
                    paint.GradientEnd = new Point(Math.Round(0.5 + du, 4), Math.Round(0.5 + dv, 4));
                }
                else
                {
                    paint.GradientEnd = new Point(Math.Round(u, 4), Math.Round(v, 4));
                    paint.GradientStart = new Point(Math.Round(0.5 - du, 4), Math.Round(0.5 - dv, 4));
                }
            }
            else // RadialGradient
            {
                if (isStartHandle)
                {
                    double u = (localMouse.X - node.X) / w;
                    double v = (localMouse.Y - node.Y) / h;
                    paint.GradientCenter = new Point(Math.Round(u, 4), Math.Round(v, 4));
                }
                else
                {
                    Point cNorm = paint.GradientCenter;
                    Point pCenter = new Point(node.X + w * cNorm.X, node.Y + h * cNorm.Y);
                    double dist = Distance(localMouse, pCenter);
                    double r = dist / Math.Max(10.0, Math.Min(w, h));
                    paint.GradientRadius = Math.Max(0.05, Math.Round(r, 3));
                }
            }
        }

        public static double ProjectOntoSegment(Point pt, Point p1, Point p2)
        {
            Vector v = p2 - p1;
            double lenSq = v.X * v.X + v.Y * v.Y;
            if (lenSq < 0.001) return 0.0;

            Vector w = pt - p1;
            double t = (w.X * v.X + w.Y * v.Y) / lenSq;
            return Math.Max(0.0, Math.Min(1.0, t));
        }

        public static void RenderOverlay(
            DrawingContext dc,
            LayerNode node,
            Paint paint,
            double zoomScale,
            int selectedStopIndex)
        {
            if (node == null || paint == null || dc == null) return;

            Point startPt, endPt;
            List<Point> stopPts;
            if (!GetGradientPoints(node, paint, out startPt, out endPt, out stopPts))
            {
                return;
            }

            double invZoom = 1.0 / Math.Max(0.01, zoomScale);

            // 1. High-contrast gradient roller line connecting start to end
            var shadowPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), 3.2 * invZoom);
            shadowPen.Freeze();
            var linePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 1.5 * invZoom);
            linePen.Freeze();

            dc.DrawLine(shadowPen, startPt, endPt);
            dc.DrawLine(linePen, startPt, endPt);

            // 2. Draw Start Handle (circular roller handle)
            double startRadius = 6.5 * invZoom;
            Color startColor = Color.FromRgb(255, 255, 255);
            if (paint.GradientStops != null && paint.GradientStops.Count > 0)
                startColor = GpuRenderer.ParseHexColor(paint.GradientStops[0].ColorHex, 1.0);

            var startFill = new SolidColorBrush(startColor);
            startFill.Freeze();
            var handleBorder = new Pen(Brushes.White, 2.0 * invZoom);
            handleBorder.Freeze();
            var handleShadow = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 3.5 * invZoom);
            handleShadow.Freeze();

            dc.DrawEllipse(null, handleShadow, startPt, startRadius, startRadius);
            dc.DrawEllipse(startFill, handleBorder, startPt, startRadius, startRadius);

            // 3. Draw End Handle (arrow/direction circle)
            double endRadius = 6.5 * invZoom;
            Color endColor = Color.FromRgb(255, 255, 255);
            if (paint.GradientStops != null && paint.GradientStops.Count > 1)
                endColor = GpuRenderer.ParseHexColor(paint.GradientStops[paint.GradientStops.Count - 1].ColorHex, 1.0);

            var endFill = new SolidColorBrush(endColor);
            endFill.Freeze();

            dc.DrawEllipse(null, handleShadow, endPt, endRadius, endRadius);
            dc.DrawEllipse(endFill, handleBorder, endPt, endRadius, endRadius);

            // Outer directional ring on end handle (Figma style)
            var arrowRing = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.5 * invZoom);
            arrowRing.Freeze();
            dc.DrawEllipse(null, arrowRing, endPt, endRadius + 3.0 * invZoom, endRadius + 3.0 * invZoom);

            // 4. Draw intermediate stop handles along the line
            if (stopPts != null && paint.GradientStops != null)
            {
                double stopRadius = 5.0 * invZoom;
                for (int i = 0; i < stopPts.Count; i++)
                {
                    // Skip if close to start or end handle to avoid overlapping visual clutter
                    if (Distance(stopPts[i], startPt) < 3.0 * invZoom || Distance(stopPts[i], endPt) < 3.0 * invZoom)
                        continue;

                    var sData = paint.GradientStops[i];
                    bool isSelected = (i == selectedStopIndex);

                    var sBrush = new SolidColorBrush(GpuRenderer.ParseHexColor(sData.ColorHex, 1.0));
                    sBrush.Freeze();

                    var sPen = isSelected
                        ? new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 2.5 * invZoom)
                        : new Pen(Brushes.White, 1.8 * invZoom);
                    sPen.Freeze();

                    dc.DrawEllipse(null, handleShadow, stopPts[i], stopRadius, stopRadius);
                    dc.DrawEllipse(sBrush, sPen, stopPts[i], stopRadius, stopRadius);

                    if (isSelected)
                    {
                        var selHalo = new Pen(new SolidColorBrush(Color.FromArgb(180, 56, 189, 248)), 1.5 * invZoom);
                        selHalo.Freeze();
                        dc.DrawEllipse(null, selHalo, stopPts[i], stopRadius + 3.5 * invZoom, stopRadius + 3.5 * invZoom);
                    }
                }
            }
        }

        private static Point RotatePoint(Point pt, Point center, double angleDegrees)
        {
            double rad = (angleDegrees * Math.PI) / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double dx = pt.X - center.X;
            double dy = pt.Y - center.Y;
            return new Point(center.X + (dx * cos - dy * sin), center.Y + (dx * sin + dy * cos));
        }

        private static double Distance(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double DistanceToSegment(Point pt, Point p1, Point p2)
        {
            double t = ProjectOntoSegment(pt, p1, p2);
            Point proj = new Point(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
            return Distance(pt, proj);
        }
    }
}
