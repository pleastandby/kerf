using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Kerf.Engine;
using Kerf.Model;

namespace Kerf.Tools
{
    public static class BooleanGeometryEngine
    {
        public static LayerNode Combine(
            IList<LayerNode> nodes,
            BooleanOp operation,
            GpuRenderer renderer)
        {
            if (nodes == null || nodes.Count < 2) return null;
            if (renderer == null) renderer = new GpuRenderer();

            // Primary node provides styles
            var primary = nodes[0];
            var baseGeo = renderer.BuildGeometry(primary);
            if (baseGeo == null) return null;

            GeometryCombineMode mode;
            switch (operation)
            {
                case BooleanOp.Union:
                    mode = GeometryCombineMode.Union;
                    break;
                case BooleanOp.Subtract:
                    mode = GeometryCombineMode.Exclude;
                    break;
                case BooleanOp.Intersect:
                    mode = GeometryCombineMode.Intersect;
                    break;
                case BooleanOp.Exclude:
                    mode = GeometryCombineMode.Xor;
                    break;
                default:
                    return null;
            }

            Geometry combined = baseGeo;

            for (int i = 1; i < nodes.Count; i++)
            {
                var nextGeo = renderer.BuildGeometry(nodes[i]);
                if (nextGeo != null)
                {
                    combined = new CombinedGeometry(mode, combined, nextGeo);
                }
            }

            var pathGeo = PathGeometry.CreateFromGeometry(combined);
            pathGeo.Freeze();

            var bounds = pathGeo.Bounds;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = new Rect(primary.X, primary.Y, primary.Width, primary.Height);
            }

            // Convert pathGeo to VectorGeometry
            var vectorGeo = ConvertPathGeometryToVector(pathGeo);

            var resultNode = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = string.Format("{0} ({1})", primary.Name, operation),
                Type = ElementType.Path,
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                Rotation = 0,
                ScaleX = 1.0,
                ScaleY = 1.0,
                CornerRadius = 0,
                Opacity = primary.Opacity,
                BlendMode = primary.BlendMode,
                Geometry = vectorGeo,
                BooleanOperation = operation,
                IsVisible = true,
                IsLocked = false
            };

            // Copy fills, strokes, effects from primary
            if (primary.Fills != null)
            {
                resultNode.Fills = new List<Paint>();
                foreach (var f in primary.Fills) resultNode.Fills.Add(f.Clone());
            }
            if (primary.Strokes != null)
            {
                resultNode.Strokes = new List<StrokeData>();
                foreach (var s in primary.Strokes) resultNode.Strokes.Add(s.Clone());
            }
            if (primary.Effects != null)
            {
                resultNode.Effects = new List<EffectData>();
                foreach (var e in primary.Effects) resultNode.Effects.Add(e.Clone());
            }

            return resultNode;
        }

        private static VectorGeometry ConvertPathGeometryToVector(PathGeometry pathGeo)
        {
            var vg = new VectorGeometry { IsClosed = true };
            if (pathGeo == null || pathGeo.Figures == null || pathGeo.Figures.Count == 0)
            {
                return vg;
            }

            foreach (PathFigure figure in pathGeo.Figures)
            {
                vg.IsClosed = figure.IsClosed;
                var startPt = figure.StartPoint;
                vg.Nodes.Add(new BezierNode(startPt.X, startPt.Y));

                foreach (PathSegment segment in figure.Segments)
                {
                    var lineSeg = segment as LineSegment;
                    if (lineSeg != null)
                    {
                        vg.Nodes.Add(new BezierNode(lineSeg.Point.X, lineSeg.Point.Y));
                        continue;
                    }

                    var bezSeg = segment as BezierSegment;
                    if (bezSeg != null)
                    {
                        if (vg.Nodes.Count > 0)
                        {
                            vg.Nodes[vg.Nodes.Count - 1].OutHandle = bezSeg.Point1;
                        }
                        var newNode = new BezierNode(bezSeg.Point3.X, bezSeg.Point3.Y)
                        {
                            InHandle = bezSeg.Point2,
                            Type = BezierNodeType.Smooth
                        };
                        vg.Nodes.Add(newNode);
                        continue;
                    }

                    var polyLine = segment as PolyLineSegment;
                    if (polyLine != null)
                    {
                        foreach (var pt in polyLine.Points)
                        {
                            vg.Nodes.Add(new BezierNode(pt.X, pt.Y));
                        }
                        continue;
                    }

                    var polyBez = segment as PolyBezierSegment;
                    if (polyBez != null && polyBez.Points.Count >= 3)
                    {
                        for (int k = 0; k < polyBez.Points.Count; k += 3)
                        {
                            if (vg.Nodes.Count > 0)
                            {
                                vg.Nodes[vg.Nodes.Count - 1].OutHandle = polyBez.Points[k];
                            }
                            var newNode = new BezierNode(polyBez.Points[k + 2].X, polyBez.Points[k + 2].Y)
                            {
                                InHandle = polyBez.Points[k + 1],
                                Type = BezierNodeType.Smooth
                            };
                            vg.Nodes.Add(newNode);
                        }
                    }
                }
            }

            vg.SvgPathData = pathGeo.ToString();
            return vg;
        }
    }
}
