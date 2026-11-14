using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Kerf.Model;

namespace Kerf.Engine
{
    /// <summary>
    /// Rive-style Rigging and Animation Constraints Evaluation Engine.
    /// Solves Transform, Distance, Translation, Scale, Rotation, IK, Follow Path, and Scroll constraints.
    /// Strictly adheres to C# 5.0 compatibility.
    /// </summary>
    public static class ConstraintsEngine
    {
        private sealed class ConstraintTransformSnapshot
        {
            public RenderTransform BaseTransform;
            public RenderTransform LastAppliedTransform;
        }

        private static readonly Dictionary<LayerNode, ConstraintTransformSnapshot> AppliedTransforms = new Dictionary<LayerNode, ConstraintTransformSnapshot>();

        public static void ForgetSolvedTransforms(IEnumerable<LayerNode> layers)
        {
            if (layers == null) return;
            foreach (LayerNode layer in layers)
            {
                if (layer != null) AppliedTransforms.Remove(layer);
            }
        }

        public static Point ClampPointerCenter(Page page, LayerNode node, Point desiredCenter)
        {
            if (page == null || node == null || node.Constraints == null) return desiredCenter;
            for (int i = 0; i < node.Constraints.Count; i++)
            {
                AnimationConstraintData constraint = node.Constraints[i];
                if (constraint == null || !constraint.Enabled || constraint.Type != AnimationConstraintType.Distance || string.IsNullOrEmpty(constraint.TargetLayerId)) continue;
                LayerNode target = page.FindLayerById(constraint.TargetLayerId);
                if (target == null || target == node) continue;

                double targetCenterX = target.X + GetTranslationX(target) + target.Width * 0.5;
                double targetCenterY = target.Y + GetTranslationY(target) + target.Height * 0.5;
                double dx = desiredCenter.X - targetCenterX;
                double dy = desiredCenter.Y - targetCenterY;
                double currentDistance = Math.Sqrt(dx * dx + dy * dy);
                double desiredDistance = Math.Max(0.0, constraint.Distance);
                if (double.IsNaN(desiredDistance) || double.IsInfinity(desiredDistance)) continue;

                bool clamp = false;
                if (constraint.DistanceMode == DistanceConstraintMode.Closer && currentDistance > desiredDistance) clamp = true;
                else if (constraint.DistanceMode == DistanceConstraintMode.Further && currentDistance < desiredDistance) clamp = true;
                else if (constraint.DistanceMode == DistanceConstraintMode.Exact && Math.Abs(currentDistance - desiredDistance) > 0.001) clamp = true;
                if (!clamp) continue;

                if (currentDistance < 0.0001)
                {
                    dx = 1.0;
                    dy = 0.0;
                    currentDistance = 1.0;
                }
                desiredCenter = new Point(
                    targetCenterX + dx / currentDistance * desiredDistance,
                    targetCenterY + dy / currentDistance * desiredDistance);
            }
            return desiredCenter;
        }

        public static void Solve(Page page)
        {
            if (page == null || page.Layers == null || page.Layers.Count == 0) return;

            var allLayers = page.GetAllLayersFlat();
            var lookup = new Dictionary<string, LayerNode>(StringComparer.OrdinalIgnoreCase);
            var constrainedNodes = new List<LayerNode>();
            var baseTransforms = new Dictionary<LayerNode, RenderTransform>();

            foreach (var l in allLayers)
            {
                if (l != null && !string.IsNullOrEmpty(l.Id))
                {
                    lookup[l.Id] = l;
                    ConstraintTransformSnapshot previous;
                    if (AppliedTransforms.TryGetValue(l, out previous) && SameTransform(l.RenderTransform, previous.LastAppliedTransform))
                    {
                        l.RenderTransform = previous.BaseTransform != null ? previous.BaseTransform.Clone() : null;
                    }
                    baseTransforms[l] = l.RenderTransform != null ? l.RenderTransform.Clone() : null;
                    if (l.Constraints != null && l.Constraints.Count > 0)
                    {
                        constrainedNodes.Add(l);
                    }
                }
            }

            foreach (var node in constrainedNodes)
            {
                SolveNodeConstraints(node, lookup);
            }

            var currentLayers = new HashSet<LayerNode>(allLayers);
            var tracked = new List<LayerNode>(AppliedTransforms.Keys);
            for (int i = 0; i < tracked.Count; i++)
            {
                if (!currentLayers.Contains(tracked[i]))
                {
                    ConstraintTransformSnapshot previous;
                    if (AppliedTransforms.TryGetValue(tracked[i], out previous) &&
                        SameTransform(tracked[i].RenderTransform, previous.LastAppliedTransform))
                    {
                        tracked[i].RenderTransform = previous.BaseTransform != null ? previous.BaseTransform.Clone() : null;
                    }
                    AppliedTransforms.Remove(tracked[i]);
                }
            }

            foreach (var node in allLayers)
            {
                if (node == null) continue;
                bool hasActiveConstraint = false;
                for (int i = 0; node.Constraints != null && i < node.Constraints.Count; i++)
                {
                    AnimationConstraintData constraint = node.Constraints[i];
                    if (constraint != null && constraint.Enabled && !string.IsNullOrEmpty(constraint.TargetLayerId) && lookup.ContainsKey(constraint.TargetLayerId))
                    {
                        hasActiveConstraint = true;
                        break;
                    }
                }
                if (hasActiveConstraint)
                {
                    RenderTransform baseTransform;
                    baseTransforms.TryGetValue(node, out baseTransform);
                    AppliedTransforms[node] = new ConstraintTransformSnapshot
                    {
                        BaseTransform = baseTransform != null ? baseTransform.Clone() : null,
                        LastAppliedTransform = node.RenderTransform != null ? node.RenderTransform.Clone() : null
                    };
                }
                else
                {
                    AppliedTransforms.Remove(node);
                }
            }
        }

        private static bool SameTransform(RenderTransform a, RenderTransform b)
        {
            if (a == null && b == null) return true;
            if (a == null) a = new RenderTransform();
            if (b == null) b = new RenderTransform();
            const double epsilon = 0.000001;
            return Math.Abs(a.TranslationX - b.TranslationX) <= epsilon &&
                   Math.Abs(a.TranslationY - b.TranslationY) <= epsilon &&
                   Math.Abs(a.ScaleX - b.ScaleX) <= epsilon &&
                   Math.Abs(a.ScaleY - b.ScaleY) <= epsilon &&
                   Math.Abs(a.Rotation - b.Rotation) <= epsilon &&
                   Math.Abs(a.SkewX - b.SkewX) <= epsilon &&
                   Math.Abs(a.SkewY - b.SkewY) <= epsilon &&
                   Math.Abs(a.OriginX - b.OriginX) <= epsilon &&
                   Math.Abs(a.OriginY - b.OriginY) <= epsilon &&
                   Math.Abs(a.VisualWidth - b.VisualWidth) <= epsilon &&
                   Math.Abs(a.VisualHeight - b.VisualHeight) <= epsilon;
        }

        public static void SolveNodeConstraints(LayerNode node, Dictionary<string, LayerNode> lookup)
        {
            if (node == null || node.Constraints == null || node.Constraints.Count == 0 || lookup == null) return;

            for (int i = 0; i < node.Constraints.Count; i++)
            {
                var c = node.Constraints[i];
                if (c == null || !c.Enabled) continue;
                if (string.IsNullOrEmpty(c.TargetLayerId)) continue;

                LayerNode target = null;
                if (!lookup.TryGetValue(c.TargetLayerId, out target) || target == null || target == node) continue;

                double strength = Math.Max(0.0, Math.Min(1.0, c.Strength));
                if (strength <= 0.0) continue;

                switch (c.Type)
                {
                    case AnimationConstraintType.Translation:
                        ApplyTranslationConstraint(node, target, c, strength);
                        break;
                    case AnimationConstraintType.Scale:
                        ApplyScaleConstraint(node, target, c, strength);
                        break;
                    case AnimationConstraintType.Rotation:
                        ApplyRotationConstraint(node, target, c, strength);
                        break;
                    case AnimationConstraintType.Transform:
                        ApplyTransformConstraint(node, target, c, strength);
                        break;
                    case AnimationConstraintType.Distance:
                        ApplyDistanceConstraint(node, target, c, strength);
                        break;
                    case AnimationConstraintType.IK:
                        ApplyIKConstraint(node, target, c, strength);
                        break;
                    case AnimationConstraintType.FollowPath:
                        ApplyFollowPathConstraint(node, target, c, strength);
                        break;
                    case AnimationConstraintType.Scroll:
                        ApplyScrollConstraint(node, target, c, strength);
                        break;
                }
            }
        }

        private static void ApplyTranslationConstraint(LayerNode node, LayerNode target, AnimationConstraintData c, double strength)
        {
            EnsureRenderTransform(node);
            double currentX = node.X + node.RenderTransform.TranslationX;
            double currentY = node.Y + node.RenderTransform.TranslationY;
            double targetX = target.X + GetTranslationX(target) + c.OffsetX;
            double targetY = target.Y + GetTranslationY(target) + c.OffsetY;
            if (c.ConstrainX)
            {
                if (c.MinLimitEnabled) targetX = Math.Max(c.MinLimit, targetX);
                if (c.MaxLimitEnabled) targetX = Math.Min(c.MaxLimit, targetX);
                node.RenderTransform.TranslationX = node.RenderTransform.TranslationX + (targetX - currentX) * strength;
            }
            if (c.ConstrainY)
            {
                if (c.MinLimitEnabled) targetY = Math.Max(c.MinLimit, targetY);
                if (c.MaxLimitEnabled) targetY = Math.Min(c.MaxLimit, targetY);
                node.RenderTransform.TranslationY = node.RenderTransform.TranslationY + (targetY - currentY) * strength;
            }
        }

        private static void ApplyScaleConstraint(LayerNode node, LayerNode target, AnimationConstraintData c, double strength)
        {
            EnsureRenderTransform(node);
            double nodeScaleX = node.ScaleX * node.RenderTransform.ScaleX;
            double nodeScaleY = node.ScaleY * node.RenderTransform.ScaleY;
            double targetScaleX = target.ScaleX * GetRenderScaleX(target);
            double targetScaleY = target.ScaleY * GetRenderScaleY(target);
            if (c.ConstrainX)
            {
                if (c.MinLimitEnabled) targetScaleX = Math.Max(c.MinLimit, targetScaleX);
                if (c.MaxLimitEnabled) targetScaleX = Math.Min(c.MaxLimit, targetScaleX);
                double desired = nodeScaleX + (targetScaleX - nodeScaleX) * strength;
                node.RenderTransform.ScaleX = desired / Math.Max(0.0001, node.ScaleX);
            }
            if (c.ConstrainY)
            {
                if (c.MinLimitEnabled) targetScaleY = Math.Max(c.MinLimit, targetScaleY);
                if (c.MaxLimitEnabled) targetScaleY = Math.Min(c.MaxLimit, targetScaleY);
                double desired = nodeScaleY + (targetScaleY - nodeScaleY) * strength;
                node.RenderTransform.ScaleY = desired / Math.Max(0.0001, node.ScaleY);
            }
        }

        private static void ApplyRotationConstraint(LayerNode node, LayerNode target, AnimationConstraintData c, double strength)
        {
            EnsureRenderTransform(node);
            double currentRotation = node.Rotation + node.RenderTransform.Rotation;
            double targetRot = target.Rotation + GetRenderRotation(target) + c.OffsetRotation;
            if (c.MinLimitEnabled) targetRot = Math.Max(c.MinLimit, targetRot);
            if (c.MaxLimitEnabled) targetRot = Math.Min(c.MaxLimit, targetRot);

            // Interpolate shortest rotational path
            double diff = (targetRot - currentRotation) % 360.0;
            if (diff > 180.0) diff -= 360.0;
            if (diff < -180.0) diff += 360.0;
            node.RenderTransform.Rotation += diff * strength;
        }

        private static void ApplyTransformConstraint(LayerNode node, LayerNode target, AnimationConstraintData c, double strength)
        {
            ApplyTranslationConstraint(node, target, c, strength);
            ApplyScaleConstraint(node, target, c, strength);
            ApplyRotationConstraint(node, target, c, strength);
        }

        private static void ApplyDistanceConstraint(LayerNode node, LayerNode target, AnimationConstraintData c, double strength)
        {
            EnsureRenderTransform(node);
            double nodeX = node.X + node.RenderTransform.TranslationX;
            double nodeY = node.Y + node.RenderTransform.TranslationY;
            double nodeCenterX = nodeX + node.Width * 0.5;
            double nodeCenterY = nodeY + node.Height * 0.5;
            double targetCenterX = target.X + GetTranslationX(target) + target.Width * 0.5;
            double targetCenterY = target.Y + GetTranslationY(target) + target.Height * 0.5;

            double dx = nodeCenterX - targetCenterX;
            double dy = nodeCenterY - targetCenterY;
            double currentDist = Math.Sqrt(dx * dx + dy * dy);
            if (currentDist < 0.0001)
            {
                dx = 1.0;
                dy = 0.0;
                currentDist = 1.0;
            }

            double desiredDist = c.Distance;
            bool apply = false;
            double finalDist = currentDist;

            switch (c.DistanceMode)
            {
                case DistanceConstraintMode.Exact:
                    finalDist = desiredDist;
                    apply = true;
                    break;
                case DistanceConstraintMode.Closer:
                    if (currentDist > desiredDist)
                    {
                        finalDist = desiredDist;
                        apply = true;
                    }
                    break;
                case DistanceConstraintMode.Further:
                    if (currentDist < desiredDist)
                    {
                        finalDist = desiredDist;
                        apply = true;
                    }
                    break;
            }

            if (apply)
            {
                double uX = dx / currentDist;
                double uY = dy / currentDist;
                double idealCenterX = targetCenterX + uX * finalDist;
                double idealCenterY = targetCenterY + uY * finalDist;

                double idealX = idealCenterX - node.Width * 0.5;
                double idealY = idealCenterY - node.Height * 0.5;

                node.RenderTransform.TranslationX += (idealX - nodeX) * strength;
                node.RenderTransform.TranslationY += (idealY - nodeY) * strength;
            }
        }

        private static void ApplyIKConstraint(LayerNode node, LayerNode target, AnimationConstraintData c, double strength)
        {
            EnsureRenderTransform(node);
            double baseX = node.X + node.RenderTransform.TranslationX;
            double baseY = node.Y + node.RenderTransform.TranslationY;
            double targetX = target.X + GetTranslationX(target) + target.Width * 0.5;
            double targetY = target.Y + GetTranslationY(target) + target.Height * 0.5;

            double dx = targetX - baseX;
            double dy = targetY - baseY;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 0.0001) return;

            double l1 = c.BoneLength1 > 0.0 ? c.BoneLength1 : Math.Max(20.0, node.Height * 0.5);
            double l2 = c.BoneLength2 > 0.0 ? c.BoneLength2 : Math.Max(20.0, node.Height * 0.5);

            // Clamp reach
            double maxReach = l1 + l2 - 0.001;
            double minReach = Math.Abs(l1 - l2) + 0.001;
            dist = Math.Max(minReach, Math.Min(maxReach, dist));

            // Law of cosines for shoulder angle alpha
            double cosAlpha = (l1 * l1 + dist * dist - l2 * l2) / (2.0 * l1 * dist);
            cosAlpha = Math.Max(-1.0, Math.Min(1.0, cosAlpha));
            double alpha = Math.Acos(cosAlpha) * 180.0 / Math.PI;

            double baseAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double finalAngle = c.InvertBend ? (baseAngle - alpha) : (baseAngle + alpha);

            double diff = (finalAngle - node.Rotation - node.RenderTransform.Rotation) % 360.0;
            if (diff > 180.0) diff -= 360.0;
            if (diff < -180.0) diff += 360.0;
            node.RenderTransform.Rotation += diff * strength;
        }

        private static void ApplyFollowPathConstraint(LayerNode node, LayerNode target, AnimationConstraintData c, double strength)
        {
            // Build geometry of target. Editable vector nodes and SVG path data are already
            // stored in canvas coordinates (the renderer draws them without adding node X/Y).
            // The fallback rectangle below is local to the target layer.
            Geometry targetGeo = null;
            bool pathUsesCanvasCoordinates = false;
            if (target.Geometry != null && target.Geometry.Nodes != null && target.Geometry.Nodes.Count > 0)
            {
                pathUsesCanvasCoordinates = true;
                try
                {
                    var path = new PathGeometry();
                    var nodes = target.Geometry.Nodes;
                    var fig = new PathFigure { StartPoint = nodes[0].Anchor, IsClosed = target.Geometry.IsClosed };
                    for (int i = 1; i < nodes.Count; i++)
                    {
                        var previous = nodes[i - 1];
                        var current = nodes[i];
                        bool hasOutHandle = previous.OutHandle.X != previous.Anchor.X || previous.OutHandle.Y != previous.Anchor.Y;
                        bool hasInHandle = current.InHandle.X != current.Anchor.X || current.InHandle.Y != current.Anchor.Y;
                        if (hasOutHandle || hasInHandle)
                            fig.Segments.Add(new BezierSegment(previous.OutHandle, current.InHandle, current.Anchor, true));
                        else
                            fig.Segments.Add(new LineSegment(current.Anchor, true));
                    }

                    if (target.Geometry.IsClosed && nodes.Count > 2)
                    {
                        var last = nodes[nodes.Count - 1];
                        var first = nodes[0];
                        bool hasOutHandle = last.OutHandle.X != last.Anchor.X || last.OutHandle.Y != last.Anchor.Y;
                        bool hasInHandle = first.InHandle.X != first.Anchor.X || first.InHandle.Y != first.Anchor.Y;
                        if (hasOutHandle || hasInHandle)
                            fig.Segments.Add(new BezierSegment(last.OutHandle, first.InHandle, first.Anchor, true));
                    }
                    path.Figures.Add(fig);
                    targetGeo = path;
                }
                catch { }
            }
            if (targetGeo == null && target.Geometry != null && !string.IsNullOrEmpty(target.Geometry.SvgPathData))
            {
                try
                {
                    targetGeo = Geometry.Parse(target.Geometry.SvgPathData);
                    pathUsesCanvasCoordinates = true;
                }
                catch { }
            }
            if (targetGeo == null)
            {
                // Fallback to bounding rectangle geometry
                targetGeo = new RectangleGeometry(new Rect(0, 0, Math.Max(1, target.Width), Math.Max(1, target.Height)), target.CornerRadius, target.CornerRadius);
            }

            PathGeometry flat = null;
            try { flat = targetGeo.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute); } catch { }
            if (flat == null || flat.Figures == null || flat.Figures.Count == 0) return;

            var points = new List<Point>();
            bool isClosedPath = false;
            foreach (PathFigure fig in flat.Figures)
            {
                points.Add(fig.StartPoint);
                if (fig.Segments != null)
                {
                    foreach (PathSegment seg in fig.Segments)
                    {
                        var ls = seg as LineSegment;
                        if (ls != null) { points.Add(ls.Point); continue; }
                        var pls = seg as PolyLineSegment;
                        if (pls != null && pls.Points != null)
                        {
                            for (int p = 0; p < pls.Points.Count; p++) points.Add(pls.Points[p]);
                        }
                    }
                }
                if (fig.IsClosed && points.Count > 1)
                {
                    points.Add(fig.StartPoint);
                    isClosedPath = true;
                }
            }

            if (points.Count < 2) return;

            double[] cumDist = new double[points.Count];
            cumDist[0] = 0.0;
            for (int p = 1; p < points.Count; p++)
            {
                double segDx = points[p].X - points[p - 1].X;
                double segDy = points[p].Y - points[p - 1].Y;
                cumDist[p] = cumDist[p - 1] + Math.Sqrt(segDx * segDx + segDy * segDy);
            }
            double totalLen = cumDist[points.Count - 1];
            if (totalLen <= 0.001) return;

            double targetArcLen = (c.PathDistance + c.PathOffset) * totalLen / 100.0;
            if (isClosedPath)
            {
                targetArcLen %= totalLen;
                if (targetArcLen < 0.0) targetArcLen += totalLen;
            }

            Point pathPt = points[0];
            Vector tangent = new Vector(1, 0);

            if (targetArcLen < 0.0)
            {
                tangent = points[1] - points[0];
                if (tangent.Length > 0.0001)
                {
                    tangent.Normalize();
                    pathPt = points[0] + tangent * targetArcLen;
                }
            }
            else if (targetArcLen > totalLen)
            {
                tangent = points[points.Count - 1] - points[points.Count - 2];
                if (tangent.Length > 0.0001)
                {
                    tangent.Normalize();
                    pathPt = points[points.Count - 1] + tangent * (targetArcLen - totalLen);
                }
            }
            else for (int p = 1; p < points.Count; p++)
            {
                if (targetArcLen <= cumDist[p])
                {
                    double segLen = cumDist[p] - cumDist[p - 1];
                    double u = (segLen > 0.0001) ? (targetArcLen - cumDist[p - 1]) / segLen : 0.0;
                    pathPt = new Point(
                        points[p - 1].X + (points[p].X - points[p - 1].X) * u,
                        points[p - 1].Y + (points[p].Y - points[p - 1].Y) * u
                    );
                    tangent = new Vector(points[p].X - points[p - 1].X, points[p].Y - points[p - 1].Y);
                    break;
                }
            }

            // Editable paths are stored in canvas coordinates, while the fallback rectangle
            // is local to the target layer.
            Point targetPoint = pathUsesCanvasCoordinates
                ? pathPt
                : new Point(target.X + pathPt.X, target.Y + pathPt.Y);
            Matrix targetStaticTransform = target.GetStaticTransformMatrix();
            if (!targetStaticTransform.IsIdentity) targetPoint = targetStaticTransform.Transform(targetPoint);
            Matrix targetRenderTransform = target.GetRenderTransformMatrix();
            if (!targetRenderTransform.IsIdentity) targetPoint = targetRenderTransform.Transform(targetPoint);
            double idealX = targetPoint.X - node.Width * 0.5;
            double idealY = targetPoint.Y - node.Height * 0.5;

            EnsureRenderTransform(node);
            double currentX = node.X + node.RenderTransform.TranslationX;
            double currentY = node.Y + node.RenderTransform.TranslationY;
            node.RenderTransform.TranslationX += (idealX - currentX) * strength;
            node.RenderTransform.TranslationY += (idealY - currentY) * strength;

            if (c.OrientToPath && tangent.Length > 0.0001)
            {
                double angle = Math.Atan2(tangent.Y, tangent.X) * 180.0 / Math.PI;
                double diff = (angle - node.Rotation - node.RenderTransform.Rotation) % 360.0;
                if (diff > 180.0) diff -= 360.0;
                if (diff < -180.0) diff += 360.0;
                node.RenderTransform.Rotation += diff * strength;
            }
        }

        private static void ApplyScrollConstraint(LayerNode node, LayerNode target, AnimationConstraintData c, double strength)
        {
            EnsureRenderTransform(node);
            double offset = c.ScrollOffset;
            offset = Math.Max(c.MinScrollOffset, Math.Min(c.MaxScrollOffset, offset));

            if (c.ScrollDirection == ScrollConstraintDirection.Vertical || c.ScrollDirection == ScrollConstraintDirection.Both)
            {
                double targetY = target.Y + GetTranslationY(target) + offset;
                double currentY = node.Y + node.RenderTransform.TranslationY;
                node.RenderTransform.TranslationY += (targetY - currentY) * strength;
            }
            if (c.ScrollDirection == ScrollConstraintDirection.Horizontal || c.ScrollDirection == ScrollConstraintDirection.Both)
            {
                double targetX = target.X + GetTranslationX(target) + offset;
                double currentX = node.X + node.RenderTransform.TranslationX;
                node.RenderTransform.TranslationX += (targetX - currentX) * strength;
            }
        }

        private static void EnsureRenderTransform(LayerNode node)
        {
            if (node != null && node.RenderTransform == null) node.RenderTransform = new RenderTransform();
        }

        private static double GetTranslationX(LayerNode node)
        {
            return node != null && node.RenderTransform != null ? node.RenderTransform.TranslationX : 0.0;
        }

        private static double GetTranslationY(LayerNode node)
        {
            return node != null && node.RenderTransform != null ? node.RenderTransform.TranslationY : 0.0;
        }

        private static double GetRenderScaleX(LayerNode node)
        {
            return node != null && node.RenderTransform != null ? node.RenderTransform.ScaleX : 1.0;
        }

        private static double GetRenderScaleY(LayerNode node)
        {
            return node != null && node.RenderTransform != null ? node.RenderTransform.ScaleY : 1.0;
        }

        private static double GetRenderRotation(LayerNode node)
        {
            return node != null && node.RenderTransform != null ? node.RenderTransform.Rotation : 0.0;
        }
    }
}

