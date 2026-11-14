using System.Collections.Generic;
using Kerf.Engine;
using Kerf.Engine.Animation;
using Kerf.Model;

namespace Kerf.IO
{
    /// <summary>
    /// Applies a clip sample to a page for frame-based exports, including animated vector geometry.
    /// </summary>
    internal static class AnimationFrameSampler
    {
        internal static Dictionary<LayerNode, VectorGeometry> CaptureRestGeometry(Page page)
        {
            var restGeometry = new Dictionary<LayerNode, VectorGeometry>();
            if (page == null) return restGeometry;

            foreach (LayerNode layer in page.GetAllLayersFlat())
            {
                if (layer != null && layer.Type == ElementType.Path)
                    restGeometry[layer] = layer.Geometry != null ? layer.Geometry.Clone() : null;
            }
            return restGeometry;
        }

        internal static void ApplyPose(Page page, Clip clip, double time, Dictionary<LayerNode, VectorGeometry> restGeometry)
        {
            if (page == null || clip == null) return;

            var pose = new Dictionary<uint, Dictionary<ushort, double>>();
            var geometryPose = new Dictionary<uint, VectorGeometry>();
            clip.SampleToPose(time, pose, geometryPose);

            foreach (LayerNode layer in page.GetAllLayersFlat())
            {
                if (layer == null || layer.Type != ElementType.Path) continue;

                VectorGeometry rest;
                if (restGeometry != null && restGeometry.TryGetValue(layer, out rest))
                    layer.Geometry = rest != null ? rest.Clone() : null;

                VectorGeometry geometry;
                if (geometryPose.TryGetValue(PropertyBinder.GetNumericNodeId(layer.Id), out geometry) && geometry != null)
                    layer.Geometry = geometry.Clone();
            }

            PropertyBinder.ApplyPoseToLayers(page.Layers, pose);
        }

        internal static void RestoreRestGeometry(Dictionary<LayerNode, VectorGeometry> restGeometry)
        {
            if (restGeometry == null) return;
            foreach (var item in restGeometry)
            {
                if (item.Key != null)
                    item.Key.Geometry = item.Value != null ? item.Value.Clone() : null;
            }
        }
    }
}
