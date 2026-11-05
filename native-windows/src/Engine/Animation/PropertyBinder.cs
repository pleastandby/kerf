using System;
using System.Collections.Generic;
using Kerf.Model;

namespace Kerf.Engine.Animation
{
    public static class PropertyBinder
    {
        public static uint GetNumericNodeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < id.Length; i++)
                {
                    hash ^= id[i];
                    hash *= 16777619;
                }
                return hash == 0 ? 1u : hash;
            }
        }

        public static void ApplyPoseToLayer(
            LayerNode layer,
            uint numericNodeId,
            Dictionary<uint, Dictionary<ushort, double>> pose)
        {
            if (layer == null || pose == null) return;

            Dictionary<ushort, double> props;
            if (!pose.TryGetValue(numericNodeId, out props)) return;

            if (layer.RenderTransform == null)
            {
                layer.RenderTransform = new RenderTransform();
            }

            double val;
            // 1. Non-destructive X, Y translation offsets
            if (props.TryGetValue((ushort)PropKey.X, out val))
            {
                layer.RenderTransform.TranslationX = val - layer.X;
            }
            if (props.TryGetValue((ushort)PropKey.Y, out val))
            {
                layer.RenderTransform.TranslationY = val - layer.Y;
            }

            // 2. Non-destructive ScaleX, ScaleY transforms
            if (props.TryGetValue((ushort)PropKey.ScaleX, out val))
            {
                layer.RenderTransform.ScaleX = val;
            }
            else if (props.TryGetValue((ushort)PropKey.Width, out val))
            {
                layer.RenderTransform.ScaleX = val / Math.Max(0.001, layer.Width);
            }

            if (props.TryGetValue((ushort)PropKey.ScaleY, out val))
            {
                layer.RenderTransform.ScaleY = val;
            }
            else if (props.TryGetValue((ushort)PropKey.Height, out val))
            {
                layer.RenderTransform.ScaleY = val / Math.Max(0.001, layer.Height);
            }

            // 3. Non-destructive Rotation
            if (props.TryGetValue((ushort)PropKey.Rotation, out val))
            {
                layer.RenderTransform.Rotation = val - layer.Rotation;
            }

            // 4. Visual Opacity (non-destructive to geometry)
            if (props.TryGetValue((ushort)PropKey.Opacity, out val))
            {
                layer.Opacity = Math.Max(0.0, Math.Min(1.0, val));
            }

            // 5. CornerRadius
            if (props.TryGetValue((ushort)PropKey.CornerRadius, out val))
            {
                layer.CornerRadius = Math.Max(0.0, val);
                layer.CornerRadiusTopLeft = layer.CornerRadius;
                layer.CornerRadiusTopRight = layer.CornerRadius;
                layer.CornerRadiusBottomRight = layer.CornerRadius;
                layer.CornerRadiusBottomLeft = layer.CornerRadius;
            }
        }

        public static void ApplyPoseToLayers(
            IEnumerable<LayerNode> layers,
            Dictionary<uint, Dictionary<ushort, double>> pose)
        {
            if (layers == null || pose == null) return;
            foreach (LayerNode l in layers)
            {
                if (l != null)
                {
                    uint numId = GetNumericNodeId(l.Id);
                    ApplyPoseToLayer(l, numId, pose);
                }
            }
        }

        public static void ApplyPoseToTarget(
            uint nodeId,
            Dictionary<uint, Dictionary<ushort, double>> pose,
            Action<ushort, double> setter)
        {
            if (pose == null || setter == null) return;
            Dictionary<ushort, double> props;
            if (pose.TryGetValue(nodeId, out props))
            {
                foreach (var kvp in props)
                {
                    setter(kvp.Key, kvp.Value);
                }
            }
        }
    }
}

