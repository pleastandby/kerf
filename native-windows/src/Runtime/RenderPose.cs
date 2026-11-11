using System;
using System.Collections.Generic;
using Kerf.Model;

namespace Kerf.Runtime
{
    public class RenderPose
    {
        public double Timestamp { get; set; }
        public Dictionary<string, Matrix3x2> LayerWorldMatrices { get; private set; }
        public Dictionary<string, double> LayerOpacities { get; private set; }
        public Dictionary<string, RenderTransform> LocalTransforms { get; private set; }

        public RenderPose()
        {
            Timestamp = 0.0;
            LayerWorldMatrices = new Dictionary<string, Matrix3x2>(StringComparer.Ordinal);
            LayerOpacities = new Dictionary<string, double>(StringComparer.Ordinal);
            LocalTransforms = new Dictionary<string, RenderTransform>(StringComparer.Ordinal);
        }

        public void Clear()
        {
            Timestamp = 0.0;
            LayerWorldMatrices.Clear();
            LayerOpacities.Clear();
            LocalTransforms.Clear();
        }
    }
}
