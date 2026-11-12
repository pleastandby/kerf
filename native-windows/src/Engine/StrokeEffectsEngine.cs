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

        /// <summary>
        /// Renders a tapered stroke along a path geometry.
        /// Returns true if a tapered mesh was rendered; false if taper is inactive (taperStart == 0 and taperEnd == 0).
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
            if (dc == null || pathGeo == null || strokeBrush == null) return false;
            if (baseWidth <= 0.0) return false;

            // If no taper is active, return false so normal pen rendering is used
            if (taperStart <= 0.0001 && taperEnd <= 0.0001)
            {
                return false;
            }

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

                // Cumulative distances
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

                // Compute normals and width for each vertex
                var leftPoints = new Point[n];
                var rightPoints = new Point[n];

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

                    double halfW = Math.Max(0.25, (baseWidth * factor) * 0.5);

                    // Tangent vector
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

                    // Normal vector is perpendicular: (-ty, tx)
                    double nx = -ty;
                    double ny = tx;

                    leftPoints[i] = new Point(points[i].X + nx * halfW, points[i].Y + ny * halfW);
                    rightPoints[i] = new Point(points[i].X - nx * halfW, points[i].Y - ny * halfW);
                }

                // Construct tapered polygon mesh
                var streamGeo = new StreamGeometry();
                using (var ctx = streamGeo.Open())
                {
                    ctx.BeginFigure(leftPoints[0], true, true);

                    for (int i = 1; i < n; i++)
                    {
                        ctx.LineTo(leftPoints[i], true, false);
                    }

                    // End cap
                    if (cap == StrokeCap.Round)
                    {
                        ctx.ArcTo(rightPoints[n - 1], new Size(baseWidth * 0.5, baseWidth * 0.5), 0, false, SweepDirection.Clockwise, true, false);
                    }
                    else
                    {
                        ctx.LineTo(rightPoints[n - 1], true, false);
                    }

                    for (int i = n - 2; i >= 0; i--)
                    {
                        ctx.LineTo(rightPoints[i], true, false);
                    }

                    // Start cap
                    if (cap == StrokeCap.Round)
                    {
                        ctx.ArcTo(leftPoints[0], new Size(baseWidth * 0.5, baseWidth * 0.5), 0, false, SweepDirection.Clockwise, true, false);
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

            bool hasTaper = stroke.TaperStart > 0.0001 || stroke.TaperEnd > 0.0001;

            if (stroke.Alignment == StrokeAlignment.Inside)
            {
                // Inside alignment: clip strictly to the shape interior
                dc.PushClip(geo);
                if (hasTaper)
                {
                    RenderTaperedStroke(dc, activeGeo, strokeBrush, stroke.Width * 2.0, stroke.TaperStart, stroke.TaperEnd, stroke.TaperStartWidth, stroke.TaperEndWidth, stroke.Cap);
                }
                else
                {
                    var insidePen = strokePen.Clone();
                    insidePen.Thickness = stroke.Width * 2.0;
                    insidePen.Freeze();
                    dc.DrawGeometry(null, insidePen, activeGeo);
                }
                dc.Pop();
            }
            else if (stroke.Alignment == StrokeAlignment.Outside)
            {
                // Outside alignment: clip OUT the interior of the shape completely so stroke never bleeds inside!
                var bounds = geo.GetRenderBounds(strokePen);
                bounds.Inflate(Math.Max(50.0, stroke.Width * 4.0), Math.Max(50.0, stroke.Width * 4.0));
                var bigBox = new RectangleGeometry(bounds);
                var outsideClip = new CombinedGeometry(GeometryCombineMode.Exclude, bigBox, geo);
                outsideClip.Freeze();

                dc.PushClip(outsideClip);
                if (hasTaper)
                {
                    RenderTaperedStroke(dc, activeGeo, strokeBrush, stroke.Width * 2.0, stroke.TaperStart, stroke.TaperEnd, stroke.TaperStartWidth, stroke.TaperEndWidth, stroke.Cap);
                }
                else
                {
                    var outsidePen = strokePen.Clone();
                    outsidePen.Thickness = stroke.Width * 2.0;
                    outsidePen.Freeze();
                    dc.DrawGeometry(null, outsidePen, activeGeo);
                }
                dc.Pop();
            }
            else
            {
                // Center alignment
                if (hasTaper)
                {
                    RenderTaperedStroke(dc, activeGeo, strokeBrush, stroke.Width, stroke.TaperStart, stroke.TaperEnd, stroke.TaperStartWidth, stroke.TaperEndWidth, stroke.Cap);
                }
                else
                {
                    dc.DrawGeometry(null, strokePen, activeGeo);
                }
            }
        }
    }
}
