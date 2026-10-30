using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using Kerf.Model;
using Kerf.Engine;

namespace Kerf.Tools
{
    public class BezierHitResult
    {
        public int NodeIndex { get; set; }
        public bool IsAnchor { get; set; }
        public bool IsInHandle { get; set; }
        public bool IsOutHandle { get; set; }

        public BezierHitResult()
        {
            NodeIndex = -1;
        }
    }

    public static class BezierVectorEngine
    {
        public static BezierHitResult HitTest(VectorGeometry geometry, Point point, double tolerance)
        {
            var res = new BezierHitResult();
            if (geometry == null || geometry.Nodes == null) return res;

            // Check handles first (they are smaller and on top of anchors)
            for (int i = 0; i < geometry.Nodes.Count; i++)
            {
                var node = geometry.Nodes[i];
                if (Math.Abs(point.X - node.InHandle.X) <= tolerance && Math.Abs(point.Y - node.InHandle.Y) <= tolerance)
                {
                    res.NodeIndex = i;
                    res.IsInHandle = true;
                    return res;
                }
                if (Math.Abs(point.X - node.OutHandle.X) <= tolerance && Math.Abs(point.Y - node.OutHandle.Y) <= tolerance)
                {
                    res.NodeIndex = i;
                    res.IsOutHandle = true;
                    return res;
                }
            }

            // Check anchors
            for (int i = 0; i < geometry.Nodes.Count; i++)
            {
                var node = geometry.Nodes[i];
                if (Math.Abs(point.X - node.Anchor.X) <= tolerance && Math.Abs(point.Y - node.Anchor.Y) <= tolerance)
                {
                    res.NodeIndex = i;
                    res.IsAnchor = true;
                    return res;
                }
            }

            return res;
        }

        /// <summary>
        /// Hit-tests segments of the vector geometry (Figma-style).
        /// If a segment between nodes is within tolerance, returns true with the segment index and closest point on curve.
        /// </summary>
        public static bool HitTestSegment(VectorGeometry geometry, Point point, double tolerance, out int segmentIndex, out Point closestPoint)
        {
            segmentIndex = -1;
            closestPoint = point;
            if (geometry == null || geometry.Nodes == null || geometry.Nodes.Count < 2) return false;

            double bestDistSq = tolerance * tolerance;
            int count = geometry.Nodes.Count;
            int segmentLimit = geometry.IsClosed ? count : count - 1;

            for (int i = 0; i < segmentLimit; i++)
            {
                var p0 = geometry.Nodes[i];
                var p1 = geometry.Nodes[(i + 1) % count];

                bool isCurve = (p0.OutHandle.X != p0.Anchor.X || p0.OutHandle.Y != p0.Anchor.Y ||
                                p1.InHandle.X != p1.Anchor.X || p1.InHandle.Y != p1.Anchor.Y);

                if (!isCurve)
                {
                    Point proj;
                    double dSq = DistanceSqToSegment(point, p0.Anchor, p1.Anchor, out proj);
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        segmentIndex = i;
                        closestPoint = proj;
                    }
                }
                else
                {
                    Point prevPt = p0.Anchor;
                    const int steps = 16;
                    for (int s = 1; s <= steps; s++)
                    {
                        double t = (double)s / steps;
                        Point curPt = SampleCubicBezier(p0.Anchor, p0.OutHandle, p1.InHandle, p1.Anchor, t);
                        Point proj;
                        double dSq = DistanceSqToSegment(point, prevPt, curPt, out proj);
                        if (dSq < bestDistSq)
                        {
                            bestDistSq = dSq;
                            segmentIndex = i;
                            closestPoint = proj;
                        }
                        prevPt = curPt;
                    }
                }
            }

            return segmentIndex >= 0;
        }

        private static double DistanceSqToSegment(Point pt, Point a, Point b, out Point proj)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-6)
            {
                proj = a;
                double px = pt.X - a.X;
                double py = pt.Y - a.Y;
                return px * px + py * py;
            }

            double t = Math.Max(0.0, Math.Min(1.0, ((pt.X - a.X) * dx + (pt.Y - a.Y) * dy) / lenSq));
            proj = new Point(a.X + t * dx, a.Y + t * dy);
            double rx = pt.X - proj.X;
            double ry = pt.Y - proj.Y;
            return rx * rx + ry * ry;
        }

        private static Point SampleCubicBezier(Point p0, Point p1, Point p2, Point p3, double t)
        {
            double u = 1.0 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            double x = uuu * p0.X + 3.0 * uu * t * p1.X + 3.0 * u * tt * p2.X + ttt * p3.X;
            double y = uuu * p0.Y + 3.0 * uu * t * p1.Y + 3.0 * u * tt * p2.Y + ttt * p3.Y;
            return new Point(x, y);
        }

        /// <summary>
        /// Bends a segment between two nodes towards the mouse position (Figma Bend Tool behavior).
        /// </summary>
        public static void BendSegment(VectorGeometry geometry, int segmentIndex, Point mousePos)
        {
            if (geometry == null || geometry.Nodes == null || geometry.Nodes.Count < 2) return;
            int count = geometry.Nodes.Count;
            if (segmentIndex < 0 || segmentIndex >= count) return;

            var p0 = geometry.Nodes[segmentIndex];
            var p1 = geometry.Nodes[(segmentIndex + 1) % count];

            Point mid = new Point((p0.Anchor.X + p1.Anchor.X) * 0.5, (p0.Anchor.Y + p1.Anchor.Y) * 0.5);
            double dx = mousePos.X - mid.X;
            double dy = mousePos.Y - mid.Y;

            p0.Type = BezierNodeType.Smooth;
            p1.Type = BezierNodeType.Smooth;

            double segDx = p1.Anchor.X - p0.Anchor.X;
            double segDy = p1.Anchor.Y - p0.Anchor.Y;

            p0.OutHandle = new Point(p0.Anchor.X + segDx * 0.33 + dx * 0.7, p0.Anchor.Y + segDy * 0.33 + dy * 0.7);
            p1.InHandle = new Point(p1.Anchor.X - segDx * 0.33 + dx * 0.7, p1.Anchor.Y - segDy * 0.33 + dy * 0.7);

            if (p0.Type == BezierNodeType.Smooth || p0.Type == BezierNodeType.Symmetric)
            {
                double hdx = p0.OutHandle.X - p0.Anchor.X;
                double hdy = p0.OutHandle.Y - p0.Anchor.Y;
                p0.InHandle = new Point(p0.Anchor.X - hdx, p0.Anchor.Y - hdy);
            }
            if (p1.Type == BezierNodeType.Smooth || p1.Type == BezierNodeType.Symmetric)
            {
                double hdx = p1.InHandle.X - p1.Anchor.X;
                double hdy = p1.InHandle.Y - p1.Anchor.Y;
                p1.OutHandle = new Point(p1.Anchor.X - hdx, p1.Anchor.Y - hdy);
            }
        }

        public static void MoveAnchor(VectorGeometry geometry, int index, Point newAnchor)
        {
            if (geometry == null || index < 0 || index >= geometry.Nodes.Count) return;

            var node = geometry.Nodes[index];
            double dx = newAnchor.X - node.Anchor.X;
            double dy = newAnchor.Y - node.Anchor.Y;

            node.Anchor = newAnchor;
            node.InHandle = new Point(node.InHandle.X + dx, node.InHandle.Y + dy);
            node.OutHandle = new Point(node.OutHandle.X + dx, node.OutHandle.Y + dy);
        }

        public static void MoveHandle(VectorGeometry geometry, int index, bool isOutHandle, Point newHandlePos)
        {
            if (geometry == null || index < 0 || index >= geometry.Nodes.Count) return;

            var node = geometry.Nodes[index];

            if (isOutHandle)
            {
                node.OutHandle = newHandlePos;
                if (node.Type == BezierNodeType.Symmetric)
                {
                    double dx = newHandlePos.X - node.Anchor.X;
                    double dy = newHandlePos.Y - node.Anchor.Y;
                    node.InHandle = new Point(node.Anchor.X - dx, node.Anchor.Y - dy);
                }
                else if (node.Type == BezierNodeType.Smooth)
                {
                    double dx = newHandlePos.X - node.Anchor.X;
                    double dy = newHandlePos.Y - node.Anchor.Y;
                    double lenOut = Math.Sqrt(dx * dx + dy * dy);
                    if (lenOut > 0.001)
                    {
                        double inDx = node.InHandle.X - node.Anchor.X;
                        double inDy = node.InHandle.Y - node.Anchor.Y;
                        double lenIn = Math.Sqrt(inDx * inDx + inDy * inDy);
                        if (lenIn < 0.001) lenIn = lenOut;

                        double ux = dx / lenOut;
                        double uy = dy / lenOut;
                        node.InHandle = new Point(node.Anchor.X - ux * lenIn, node.Anchor.Y - uy * lenIn);
                    }
                }
            }
            else
            {
                node.InHandle = newHandlePos;
                if (node.Type == BezierNodeType.Symmetric)
                {
                    double dx = newHandlePos.X - node.Anchor.X;
                    double dy = newHandlePos.Y - node.Anchor.Y;
                    node.OutHandle = new Point(node.Anchor.X - dx, node.Anchor.Y - dy);
                }
                else if (node.Type == BezierNodeType.Smooth)
                {
                    double dx = newHandlePos.X - node.Anchor.X;
                    double dy = newHandlePos.Y - node.Anchor.Y;
                    double lenIn = Math.Sqrt(dx * dx + dy * dy);
                    if (lenIn > 0.001)
                    {
                        double outDx = node.OutHandle.X - node.Anchor.X;
                        double outDy = node.OutHandle.Y - node.Anchor.Y;
                        double lenOut = Math.Sqrt(outDx * outDx + outDy * outDy);
                        if (lenOut < 0.001) lenOut = lenIn;

                        double ux = dx / lenIn;
                        double uy = dy / lenIn;
                        node.OutHandle = new Point(node.Anchor.X - ux * lenOut, node.Anchor.Y - uy * lenOut);
                    }
                }
            }
        }

        public static void SetNodeType(VectorGeometry geometry, int index, BezierNodeType type)
        {
            if (geometry == null || index < 0 || index >= geometry.Nodes.Count) return;

            var node = geometry.Nodes[index];
            node.Type = type;

            if (type == BezierNodeType.Corner)
            {
                // Pure sharp corner: retract handles to anchor
                node.InHandle = node.Anchor;
                node.OutHandle = node.Anchor;
            }
            else if (type == BezierNodeType.Split)
            {
                // Split handles: independent angle and length.
                // If handles were previously retracted (e.g. from Corner), extend them at an angle
                double inDist = Math.Sqrt(Math.Pow(node.InHandle.X - node.Anchor.X, 2) + Math.Pow(node.InHandle.Y - node.Anchor.Y, 2));
                double outDist = Math.Sqrt(Math.Pow(node.OutHandle.X - node.Anchor.X, 2) + Math.Pow(node.OutHandle.Y - node.Anchor.Y, 2));
                if (inDist < 0.001 && outDist < 0.001)
                {
                    node.InHandle = new Point(node.Anchor.X - 20, node.Anchor.Y - 15);
                    node.OutHandle = new Point(node.Anchor.X + 20, node.Anchor.Y - 15);
                }
            }
            else if (type == BezierNodeType.Smooth || type == BezierNodeType.Symmetric)
            {
                // Align handles
                double dx = node.OutHandle.X - node.Anchor.X;
                double dy = node.OutHandle.Y - node.Anchor.Y;
                if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                {
                    // Default horizontal handles
                    node.OutHandle = new Point(node.Anchor.X + 20, node.Anchor.Y);
                    node.InHandle = new Point(node.Anchor.X - 20, node.Anchor.Y);
                }
                else
                {
                    if (type == BezierNodeType.Symmetric)
                    {
                        node.InHandle = new Point(node.Anchor.X - dx, node.Anchor.Y - dy);
                    }
                    else
                    {
                        MoveHandle(geometry, index, true, node.OutHandle);
                    }
                }
            }
        }

        /// <summary>
        /// Splits a path at the given node index (cuts open loop or duplicates anchor for separate branches).
        /// </summary>
        public static void SplitPathAtNode(VectorGeometry geometry, int index)
        {
            if (geometry == null || index < 0 || index >= geometry.Nodes.Count) return;

            if (geometry.IsClosed)
            {
                // Reorder nodes starting at index and open path loop
                var reordered = new List<BezierNode>();
                for (int i = 0; i < geometry.Nodes.Count; i++)
                {
                    reordered.Add(geometry.Nodes[(index + i) % geometry.Nodes.Count]);
                }
                var endNode = reordered[0].Clone();
                reordered.Add(endNode);
                geometry.Nodes = reordered;
                geometry.IsClosed = false;
            }
            else if (index > 0 && index < geometry.Nodes.Count - 1)
            {
                // Duplicate node at split index with slight offset so it can be dragged separately
                var dup = geometry.Nodes[index].Clone();
                dup.Anchor = new Point(dup.Anchor.X + 10, dup.Anchor.Y + 10);
                dup.InHandle = new Point(dup.InHandle.X + 10, dup.InHandle.Y + 10);
                dup.OutHandle = new Point(dup.OutHandle.X + 10, dup.OutHandle.Y + 10);
                geometry.Nodes.Insert(index + 1, dup);
            }
        }

        public static void AddNode(VectorGeometry geometry, Point point)
        {
            if (geometry == null) return;
            var node = new BezierNode(point.X, point.Y)
            {
                Type = BezierNodeType.Corner
            };
            geometry.Nodes.Add(node);
        }

        public static void InsertNode(VectorGeometry geometry, int index, Point point)
        {
            if (geometry == null) return;
            double ux = 1.0, uy = 0.0;
            if (geometry.Nodes != null && geometry.Nodes.Count >= 2)
            {
                int count = geometry.Nodes.Count;
                int prevIdx = (index > 0) ? index - 1 : (geometry.IsClosed ? count - 1 : 0);
                int nextIdx = (index < count) ? index : (geometry.IsClosed ? 0 : count - 1);
                var pPrev = geometry.Nodes[prevIdx];
                var pNext = geometry.Nodes[nextIdx];
                double dx = pNext.Anchor.X - pPrev.Anchor.X;
                double dy = pNext.Anchor.Y - pPrev.Anchor.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.001)
                {
                    ux = dx / len;
                    uy = dy / len;
                }
            }

            var node = new BezierNode(point.X, point.Y)
            {
                Type = BezierNodeType.Smooth,
                InHandle = new Point(point.X - ux * 16, point.Y - uy * 16),
                OutHandle = new Point(point.X + ux * 16, point.Y + uy * 16)
            };
            geometry.Nodes.Insert(Math.Max(0, Math.Min(geometry.Nodes.Count, index)), node);
        }

        public static void RemoveNode(VectorGeometry geometry, int index)
        {
            if (geometry == null || index < 0 || index >= geometry.Nodes.Count) return;
            geometry.Nodes.RemoveAt(index);
        }

        public static string ToSvgPathData(VectorGeometry geometry)
        {
            if (geometry == null || geometry.Nodes == null || geometry.Nodes.Count == 0) return "";

            var sb = new StringBuilder();
            var nodes = geometry.Nodes;
            sb.AppendFormat(CultureInfo.InvariantCulture, "M {0:F2} {1:F2}", nodes[0].Anchor.X, nodes[0].Anchor.Y);

            for (int i = 1; i < nodes.Count; i++)
            {
                var prev = nodes[i - 1];
                var cur = nodes[i];

                bool prevHasHandle = (prev.OutHandle.X != prev.Anchor.X || prev.OutHandle.Y != prev.Anchor.Y);
                bool curHasHandle = (cur.InHandle.X != cur.Anchor.X || cur.InHandle.Y != cur.Anchor.Y);

                if (prevHasHandle || curHasHandle)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, " C {0:F2} {1:F2}, {2:F2} {3:F2}, {4:F2} {5:F2}",
                        prev.OutHandle.X, prev.OutHandle.Y,
                        cur.InHandle.X, cur.InHandle.Y,
                        cur.Anchor.X, cur.Anchor.Y);
                }
                else
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, " L {0:F2} {1:F2}", cur.Anchor.X, cur.Anchor.Y);
                }
            }

            if (geometry.IsClosed)
            {
                sb.Append(" Z");
            }

            return sb.ToString();
        }

        public static VectorGeometry FromPoints(IList<Point> points, bool isClosed)
        {
            var vg = new VectorGeometry { IsClosed = isClosed };
            if (points == null || points.Count == 0) return vg;

            foreach (var pt in points)
            {
                vg.Nodes.Add(new BezierNode(pt.X, pt.Y));
            }

            return vg;
        }

        public static void OffsetGeometry(VectorGeometry geo, double dx, double dy)
        {
            if (geo == null || geo.Nodes == null) return;
            foreach (var node in geo.Nodes)
            {
                node.Anchor = new Point(node.Anchor.X + dx, node.Anchor.Y + dy);
                node.InHandle = new Point(node.InHandle.X + dx, node.InHandle.Y + dy);
                node.OutHandle = new Point(node.OutHandle.X + dx, node.OutHandle.Y + dy);
            }
        }

        public static void ScaleGeometry(VectorGeometry geo, Rect oldBounds, Rect newBounds)
        {
            if (geo == null || geo.Nodes == null || oldBounds.Width < 0.001 || oldBounds.Height < 0.001) return;
            double sx = newBounds.Width / oldBounds.Width;
            double sy = newBounds.Height / oldBounds.Height;
            foreach (var node in geo.Nodes)
            {
                node.Anchor = new Point(newBounds.X + (node.Anchor.X - oldBounds.X) * sx, newBounds.Y + (node.Anchor.Y - oldBounds.Y) * sy);
                node.InHandle = new Point(newBounds.X + (node.InHandle.X - oldBounds.X) * sx, newBounds.Y + (node.InHandle.Y - oldBounds.Y) * sy);
                node.OutHandle = new Point(newBounds.X + (node.OutHandle.X - oldBounds.X) * sx, newBounds.Y + (node.OutHandle.Y - oldBounds.Y) * sy);
            }
        }

        public static Rect CalculateBounds(VectorGeometry geo)
        {
            if (geo == null || geo.Nodes == null || geo.Nodes.Count == 0) return Rect.Empty;

            // Use accelerated native Rust engine exact cubic bounds when available!
            Rect rustBounds;
            if (RustEngineInterop.TryComputePathBounds(geo, out rustBounds))
            {
                return rustBounds;
            }

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var node in geo.Nodes)
            {
                minX = Math.Min(minX, Math.Min(node.Anchor.X, Math.Min(node.InHandle.X, node.OutHandle.X)));
                minY = Math.Min(minY, Math.Min(node.Anchor.Y, Math.Min(node.InHandle.Y, node.OutHandle.Y)));
                maxX = Math.Max(maxX, Math.Max(node.Anchor.X, Math.Max(node.InHandle.X, node.OutHandle.X)));
                maxY = Math.Max(maxY, Math.Max(node.Anchor.Y, Math.Max(node.InHandle.Y, node.OutHandle.Y)));
            }
            return new Rect(minX, minY, Math.Max(1.0, maxX - minX), Math.Max(1.0, maxY - minY));
        }

        public static void SmoothAllNodes(VectorGeometry geo)
        {
            if (geo == null || geo.Nodes == null || geo.Nodes.Count < 2) return;
            int count = geo.Nodes.Count;
            for (int i = 0; i < count; i++)
            {
                var prev = geo.Nodes[(i - 1 + count) % count];
                var cur = geo.Nodes[i];
                var next = geo.Nodes[(i + 1) % count];

                double dx = (next.Anchor.X - prev.Anchor.X) * 0.25;
                double dy = (next.Anchor.Y - prev.Anchor.Y) * 0.25;

                cur.Type = BezierNodeType.Smooth;
                cur.OutHandle = new Point(cur.Anchor.X + dx, cur.Anchor.Y + dy);
                cur.InHandle = new Point(cur.Anchor.X - dx, cur.Anchor.Y - dy);
            }
        }

        public static void SharpenAllNodes(VectorGeometry geo)
        {
            if (geo == null || geo.Nodes == null) return;
            foreach (var n in geo.Nodes)
            {
                n.Type = BezierNodeType.Corner;
                n.InHandle = n.Anchor;
                n.OutHandle = n.Anchor;
            }
        }

        public static void ToggleClosed(VectorGeometry geo)
        {
            if (geo == null) return;
            geo.IsClosed = !geo.IsClosed;
        }
    }
}
