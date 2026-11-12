using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Kerf.Model;

namespace Kerf.Engine
{
    /// <summary>
    /// Advanced vector stroke effects engine providing:
    /// 1. Exact Outside & Inside stroke alignment with pixel-perfect inverse interior clipping.
    /// 2. Trim Path evaluation (TrimStart, TrimEnd, TrimOffset) for write-on and laser trace animations.
    /// 3. Stroke Tapering (TaperStart, TaperEnd, TaperStartWidth, TaperEndWidth) for calligraphic and velocity line effects.
    /// Strictly adheres to C# 5.0 compatibility.
    /// </summary>
    public static class StrokeEffectsEngine
    {
        /// <summary>
        /// Trims a geometry along its arc length based on normalized Start (0..1), End (0..1), and Offset.
        /// Returns the trimmed PathGeometry, or the original geometry if no trim is applied.
        /// </summary>
        public static Geometry TrimGeometry(Geometry sourceGeo, double trimStart, double trimEnd, double trimOffset)
        {
            if (sourceGeo == null) return null;

            // Clamp and sanitize
            trimStart = Math.Max(0.0, Math.Min(1.0, trimStart));
            trimEnd = Math.Max(0.0, Math.Min(1.0, trimEnd));

            // Fast path: full path without offset
            if (trimStart <= 0.0 && trimEnd >= 1.0 && Math.Abs(trimOffset) < 0.0001)
            {
                return sourceGeo;
            }

            // If length is zero, return empty geometry
            if (Math.Abs(trimEnd - trimStart) < 0.0001)
            {
                var empty = new StreamGeometry();
                empty.Freeze();
                return empty;
            }

            PathGeometry flat;
            try
            {
                flat = sourceGeo.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
            }
            catch
            {
                return sourceGeo;
            }

            if (flat == null || flat.Figures == null || flat.Figures.Count == 0)
            {
                return sourceGeo;
            }

            var resultPath = new PathGeometry();

            // Normalize offset to [0, 1)
            double offsetNorm = trimOffset % 1.0;
            if (offsetNorm < 0.0) offsetNorm += 1.0;

            double normStart = (trimStart + offsetNorm) % 1.0;
            double trimLength = trimEnd - trimStart;
            if (trimLength <= 0.0)
            {
                var empty = new StreamGeometry();
                empty.Freeze();
                return empty;
            }

            double normEnd = normStart + trimLength;

            foreach (PathFigure fig in flat.Figures)
            {
                var points = ExtractFigurePoints(fig);
                if (points.Count < 2) continue;

                // Compute cumulative lengths
                var cumDist = new double[points.Count];
                cumDist[0] = 0.0;
                for (int i = 1; i < points.Count; i++)
                {
                    double dx = points[i].X - points[i - 1].X;
                    double dy = points[i].Y - points[i - 1].Y;
                    cumDist[i] = cumDist[i - 1] + Math.Sqrt(dx * dx + dy * dy);
                }

                double totalLen = cumDist[points.Count - 1];
                if (totalLen <= 0.001) continue;

                if (normEnd <= 1.0)
                {
                    // Single continuous sub-path
                    double s0 = normStart * totalLen;
                    double s1 = normEnd * totalLen;
                    AddSubFigure(resultPath, points, cumDist, totalLen, s0, s1);
                }
                else
                {
                    // Wraps around the end of the path: [normStart .. 1.0] and [0.0 .. (normEnd - 1.0)]
                    double s0 = normStart * totalLen;
                    double s1 = totalLen;
                    AddSubFigure(resultPath, points, cumDist, totalLen, s0, s1);

                    double s2 = 0.0;
                    double s3 = (normEnd - 1.0) * totalLen;
                    if (s3 > 0.001)
                    {
                        AddSubFigure(resultPath, points, cumDist, totalLen, s2, s3);
                    }
                }
            }

            resultPath.Freeze();
            return resultPath;
        }

        private static List<Point> ExtractFigurePoints(PathFigure fig)
        {
            var points = new List<Point>();
            points.Add(fig.StartPoint);

            if (fig.Segments != null)
            {
                foreach (PathSegment seg in fig.Segments)
                {
                    var lineSeg = seg as LineSegment;
                    if (lineSeg != null)
                    {
                        points.Add(lineSeg.Point);
                        continue;
                    }

                    var polyLine = seg as PolyLineSegment;
                    if (polyLine != null && polyLine.Points != null)
                    {
                        for (int i = 0; i < polyLine.Points.Count; i++)
                        {
                            points.Add(polyLine.Points[i]);
                        }
                        continue;
                    }
                }
            }

            if (fig.IsClosed && points.Count > 1)
            {
                var first = points[0];
                var last = points[points.Count - 1];
                if (Math.Abs(first.X - last.X) > 0.001 || Math.Abs(first.Y - last.Y) > 0.001)
                {
                    points.Add(first);
                }
            }

            return points;
        }

        private static void AddSubFigure(PathGeometry target, List<Point> points, double[] cumDist, double totalLen, double s0, double s1)
        {
            if (s1 <= s0 || s1 - s0 < 0.001) return;

            Point pStart = SamplePointAtArcLength(points, cumDist, s0);
            Point pEnd = SamplePointAtArcLength(points, cumDist, s1);

            var fig = new PathFigure
            {
                StartPoint = pStart,
                IsClosed = false,
                IsFilled = true
            };

            // Collect all original intermediate vertices that fall between s0 and s1
            for (int i = 0; i < points.Count; i++)
            {
                if (cumDist[i] > s0 + 0.001 && cumDist[i] < s1 - 0.001)
                {
                    fig.Segments.Add(new LineSegment(points[i], true));
                }
            }

            fig.Segments.Add(new LineSegment(pEnd, true));
            target.Figures.Add(fig);
        }

        private static Point SamplePointAtArcLength(List<Point> points, double[] cumDist, double s)
        {
            if (s <= 0.0) return points[0];
            if (s >= cumDist[points.Count - 1]) return points[points.Count - 1];

            for (int i = 1; i < points.Count; i++)
            {
                if (s <= cumDist[i])
                {
                    double segLen = cumDist[i] - cumDist[i - 1];
                    if (segLen < 0.0001) return points[i];
                    double u = (s - cumDist[i - 1]) / segLen;
                    return new Point(
                        points[i - 1].X + (points[i].X - points[i - 1].X) * u,
                        points[i - 1].Y + (points[i].Y - points[i - 1].Y) * u
                    );
                }
            }

            return points[points.Count - 1];
        }

        public static bool IsClockwisePolygon(List<Point> points)
        {
            if (points == null || points.Count < 3) return true;
            double sum = 0.0;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                sum += (points[i].X * points[next].Y - points[next].X * points[i].Y);
            }
            return sum >= 0.0;
        }

        /// <summary>
        /// Backward-compatible overload for tapered stroke mesh rendering.
        /// </summary>
        public static bool RenderTaperedStroke(
            DrawingContext dc,
            Geometry pathGeo,
            Brush strokeBrush,
            double baseWidth,
            double taperStart,
            double taperEnd,
            double startWidthFrac,
            double endWidthFrac,
            StrokeCap cap)
        {
            if (taperStart <= 0.0001 && taperEnd <= 0.0001) return false;
            return RenderContourMesh(dc, pathGeo, strokeBrush, baseWidth, StrokeAlignment.Center, true, taperStart, taperEnd, startWidthFrac, endWidthFrac, cap);
        }

        /// <summary>
        /// Renders an analytical contour mesh along a path geometry.
        /// Handles Outside, Inside, and Center alignments natively without destructive clipping.
        /// Generates mathematically exact convex caps (round, flat, square) avoiding concave cutout artifacts.
        /// </summary>
        public static bool RenderContourMesh(
            DrawingContext dc,
            Geometry pathGeo,
            Brush strokeBrush,
            double baseWidth,
            StrokeAlignment alignment,
            bool isClockwise,
            double taperStart,
            double taperEnd,
            double startWidthFrac,
            double endWidthFrac,
            StrokeCap cap)
        {
            if (dc == null || pathGeo == null || strokeBrush == null) return false;
            if (baseWidth <= 0.0) return false;

            taperStart = Math.Max(0.0, Math.Min(1.0, taperStart));
            taperEnd = Math.Max(0.0, Math.Min(1.0, taperEnd));
            startWidthFrac = Math.Max(0.0, Math.Min(1.0, startWidthFrac));
            endWidthFrac = Math.Max(0.0, Math.Min(1.0, endWidthFrac));

            PathGeometry flat;
            try
            {
                flat = pathGeo.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
            }
            catch
            {
                return false;
            }

            if (flat == null || flat.Figures == null || flat.Figures.Count == 0)
            {
                return false;
            }

            foreach (PathFigure fig in flat.Figures)
            {
                var points = ExtractFigurePoints(fig);
                if (points.Count < 2) continue;

                int n = points.Count;
                var cumDist = new double[n];
                cumDist[0] = 0.0;
                for (int i = 1; i < n; i++)
                {
                    double dx = points[i].X - points[i - 1].X;
                    double dy = points[i].Y - points[i - 1].Y;
                    cumDist[i] = cumDist[i - 1] + Math.Sqrt(dx * dx + dy * dy);
                }

                double totalLen = cumDist[n - 1];
                if (totalLen <= 0.001) continue;

                var outerPoints = new Point[n];
                var innerPoints = new Point[n];
                var tangents = new Vector[n];
                var outNormals = new Vector[n];

                for (int i = 0; i < n; i++)
                {
                    double u = cumDist[i] / totalLen;

                    // Compute width factor at u
                    double factor = 1.0;
                    if (taperStart > 0.0001 && u < taperStart)
                    {
                        double t = u / taperStart;
                        factor = startWidthFrac + (1.0 - startWidthFrac) * t;
                    }
                    if (taperEnd > 0.0001 && (1.0 - u) < taperEnd)
                    {
                        double t = (1.0 - u) / taperEnd;
                        double endFactor = endWidthFrac + (1.0 - endWidthFrac) * t;
                        factor = Math.Min(factor, endFactor);
                    }

                    double curWidth = Math.Max(0.25, baseWidth * factor);

                    // Compute tangent vector (tx, ty)
                    double tx = 0.0, ty = 0.0;
                    if (i == 0)
                    {
                        tx = points[1].X - points[0].X;
                        ty = points[1].Y - points[0].Y;
                    }
                    else if (i == n - 1)
                    {
                        tx = points[n - 1].X - points[n - 2].X;
                        ty = points[n - 1].Y - points[n - 2].Y;
                    }
                    else
                    {
                        double t1x = points[i].X - points[i - 1].X;
                        double t1y = points[i].Y - points[i - 1].Y;
                        double l1 = Math.Sqrt(t1x * t1x + t1y * t1y);
                        if (l1 > 0.0001) { t1x /= l1; t1y /= l1; }

                        double t2x = points[i + 1].X - points[i].X;
                        double t2y = points[i + 1].Y - points[i].Y;
                        double l2 = Math.Sqrt(t2x * t2x + t2y * t2y);
                        if (l2 > 0.0001) { t2x /= l2; t2y /= l2; }

                        tx = t1x + t2x;
                        ty = t1y + t2y;
                    }

                    double tLen = Math.Sqrt(tx * tx + ty * ty);
                    if (tLen < 0.0001)
                    {
                        tx = 1.0;
                        ty = 0.0;
                    }
                    else
                    {
                        tx /= tLen;
                        ty /= tLen;
                    }
                    tangents[i] = new Vector(tx, ty);

                    // Determine unit outward normal
                    // In screen coordinates: Clockwise tangent (tx, ty) has outward normal (ty, -tx)
                    double nOutX = isClockwise ? ty : -ty;
                    double nOutY = isClockwise ? -tx : tx;
                    outNormals[i] = new Vector(nOutX, nOutY);

                    // Position outer and inner points based on alignment
                    if (alignment == StrokeAlignment.Outside)
                    {
                        outerPoints[i] = new Point(points[i].X + nOutX * curWidth, points[i].Y + nOutY * curWidth);
                        innerPoints[i] = new Point(points[i].X, points[i].Y);
                    }
                    else if (alignment == StrokeAlignment.Inside)
                    {
                        outerPoints[i] = new Point(points[i].X, points[i].Y);
                        innerPoints[i] = new Point(points[i].X - nOutX * curWidth, points[i].Y - nOutY * curWidth);
                    }
                    else
                    {
                        double halfW = curWidth * 0.5;
                        outerPoints[i] = new Point(points[i].X + nOutX * halfW, points[i].Y + nOutY * halfW);
                        innerPoints[i] = new Point(points[i].X - nOutX * halfW, points[i].Y - nOutY * halfW);
                    }
                }

                // Check if figure is closed
                bool isClosedFigure = fig.IsClosed;

                var streamGeo = new StreamGeometry();
                streamGeo.FillRule = FillRule.EvenOdd;
                using (var ctx = streamGeo.Open())
                {
                    if (isClosedFigure)
                    {
                        // Outer loop
                        ctx.BeginFigure(outerPoints[0], true, true);
                        for (int i = 1; i < n; i++)
                        {
                            ctx.LineTo(outerPoints[i], true, false);
                        }

                        // Inner loop (hole)
                        ctx.BeginFigure(innerPoints[0], true, true);
                        for (int i = 1; i < n; i++)
                        {
                            ctx.LineTo(innerPoints[i], true, false);
                        }
                    }
                    else
                    {
                        ctx.BeginFigure(outerPoints[0], true, true);

                        for (int i = 1; i < n; i++)
                        {
                            ctx.LineTo(outerPoints[i], true, false);
                        }
                        // Open path: End Cap from outerPoints[n-1] to innerPoints[n-1]
                        Point pOutEnd = outerPoints[n - 1];
                        Point pInEnd = innerPoints[n - 1];
                        double capCenterX = (pOutEnd.X + pInEnd.X) * 0.5;
                        double capCenterY = (pOutEnd.Y + pInEnd.Y) * 0.5;
                        double capRadius = Math.Sqrt((pOutEnd.X - pInEnd.X) * (pOutEnd.X - pInEnd.X) +
                                                     (pOutEnd.Y - pInEnd.Y) * (pOutEnd.Y - pInEnd.Y)) * 0.5;

                        Vector tEnd = tangents[n - 1];
                        Vector nOutEnd = outNormals[n - 1];
                        double uOutX = (capRadius > 0.001) ? (pOutEnd.X - capCenterX) / capRadius : nOutEnd.X;
                        double uOutY = (capRadius > 0.001) ? (pOutEnd.Y - capCenterY) / capRadius : nOutEnd.Y;

                        if (cap == StrokeCap.Round && capRadius > 0.5)
                        {
                            const int numSteps = 12;
                            for (int k = 1; k < numSteps; k++)
                            {
                                double alpha = (double)k / numSteps * Math.PI;
                                double cosA = Math.Cos(alpha);
                                double sinA = Math.Sin(alpha);
                                double px = capCenterX + uOutX * (capRadius * cosA) + tEnd.X * (capRadius * sinA);
                                double py = capCenterY + uOutY * (capRadius * cosA) + tEnd.Y * (capRadius * sinA);
                                ctx.LineTo(new Point(px, py), true, false);
                            }
                            ctx.LineTo(pInEnd, true, false);
                        }
                        else if (cap == StrokeCap.Square && capRadius > 0.5)
                        {
                            ctx.LineTo(new Point(pOutEnd.X + tEnd.X * capRadius, pOutEnd.Y + tEnd.Y * capRadius), true, false);
                            ctx.LineTo(new Point(pInEnd.X + tEnd.X * capRadius, pInEnd.Y + tEnd.Y * capRadius), true, false);
                            ctx.LineTo(pInEnd, true, false);
                        }
                        else
                        {
                            ctx.LineTo(pInEnd, true, false);
                        }

                        // Inner boundary
                        for (int i = n - 2; i >= 0; i--)
                        {
                            ctx.LineTo(innerPoints[i], true, false);
                        }

                        // Start Cap from innerPoints[0] to outerPoints[0]
                        Point pOutStart = outerPoints[0];
                        Point pInStart = innerPoints[0];
                        double capStartCenterX = (pOutStart.X + pInStart.X) * 0.5;
                        double capStartCenterY = (pOutStart.Y + pInStart.Y) * 0.5;
                        double capStartRadius = Math.Sqrt((pOutStart.X - pInStart.X) * (pOutStart.X - pInStart.X) +
                                                          (pOutStart.Y - pInStart.Y) * (pOutStart.Y - pInStart.Y)) * 0.5;

                        Vector tStart = tangents[0];
                        Vector nOutStart = outNormals[0];
                        double uOutStartX = (capStartRadius > 0.001) ? (pOutStart.X - capStartCenterX) / capStartRadius : nOutStart.X;
                        double uOutStartY = (capStartRadius > 0.001) ? (pOutStart.Y - capStartCenterY) / capStartRadius : nOutStart.Y;

                        if (cap == StrokeCap.Round && capStartRadius > 0.5)
                        {
                            const int numSteps = 12;
                            for (int k = 1; k < numSteps; k++)
                            {
                                double beta = (double)k / numSteps * Math.PI;
                                double cosB = Math.Cos(beta);
                                double sinB = Math.Sin(beta);
                                double px = capStartCenterX - uOutStartX * (capStartRadius * cosB) - tStart.X * (capStartRadius * sinB);
                                double py = capStartCenterY - uOutStartY * (capStartRadius * cosB) - tStart.Y * (capStartRadius * sinB);
                                ctx.LineTo(new Point(px, py), true, false);
                            }
                            ctx.LineTo(pOutStart, true, false);
                        }
                        else if (cap == StrokeCap.Square && capStartRadius > 0.5)
                        {
                            ctx.LineTo(new Point(pInStart.X - tStart.X * capStartRadius, pInStart.Y - tStart.Y * capStartRadius), true, false);
                            ctx.LineTo(new Point(pOutStart.X - tStart.X * capStartRadius, pOutStart.Y - tStart.Y * capStartRadius), true, false);
                            ctx.LineTo(pOutStart, true, false);
                        }
                        else
                        {
                            ctx.LineTo(pOutStart, true, false);
                        }
                    }
                }

                streamGeo.Freeze();
                dc.DrawGeometry(strokeBrush, null, streamGeo);
            }

            return true;
        }

        /// <summary>
        /// Main entry point to render a stroke with Trim Path, Tapering, and exact Inside/Outside/Center alignment.
        /// </summary>
        public static void RenderStroke(
            DrawingContext dc,
            Geometry geo,
            StrokeData stroke,
            Brush strokeBrush,
            Pen strokePen)
        {
            if (dc == null || geo == null || stroke == null || strokeBrush == null || strokePen == null) return;
            if (stroke.Width <= 0.0 || !stroke.Enabled) return;

            // 1. Evaluate Trim Path
            Geometry activeGeo = TrimGeometry(geo, stroke.TrimStart, stroke.TrimEnd, stroke.TrimOffset);
            if (activeGeo == null) return;

            bool isTrimmed = stroke.TrimStart > 0.0001 || stroke.TrimEnd < 0.9999 || Math.Abs(stroke.TrimOffset) > 0.0001;
            bool hasTaper = stroke.TaperStart > 0.0001 || stroke.TaperEnd > 0.0001;
            bool needsContourMesh = hasTaper || isTrimmed || stroke.Alignment != StrokeAlignment.Center;

            if (needsContourMesh)
            {
                // Detect orientation of parent geometry to ensure outward normals point exterior to the shape
                bool isClockwise = true;
                PathGeometry flatParent;
                try { flatParent = geo.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute); }
                catch { flatParent = null; }
                if (flatParent != null && flatParent.Figures != null && flatParent.Figures.Count > 0)
                {
                    var parentPoints = ExtractFigurePoints(flatParent.Figures[0]);
                    isClockwise = IsClockwisePolygon(parentPoints);
                }

                // If inside alignment without taper/trim on closed shape, push clip to guarantee no outer bleed
                if (stroke.Alignment == StrokeAlignment.Inside)
                {
                    dc.PushClip(geo);
                }

                bool rendered = RenderContourMesh(
                    dc,
                    activeGeo,
                    strokeBrush,
                    stroke.Width,
                    stroke.Alignment,
                    isClockwise,
                    stroke.TaperStart,
                    stroke.TaperEnd,
                    stroke.TaperStartWidth,
                    stroke.TaperEndWidth,
                    stroke.Cap);

                if (stroke.Alignment == StrokeAlignment.Inside)
                {
                    dc.Pop();
                }

                if (rendered) return;
            }

            // Fallback for standard untrimmed center stroke
            dc.DrawGeometry(null, strokePen, activeGeo);
        }
    }
}
