using System;
using System.Collections.Generic;
using Kerf.Model;

namespace Kerf.Engine.Animation
{
    public static class SmartAnimateCompiler
    {
        public static Clip CompileTransitionClip(
            uint clipId,
            string clipName,
            double duration,
            Dictionary<uint, Dictionary<ushort, double>> frameAProps,
            Dictionary<uint, Dictionary<ushort, double>> frameBProps,
            double easeX1 = 0.25, double easeY1 = 0.1, double easeX2 = 0.25, double easeY2 = 1.0)
        {
            Clip clip = new Clip(clipId, clipName, duration, 60.0, LoopMode.OneShot);
            if (frameAProps == null && frameBProps == null) return clip;

            HashSet<uint> allNodes = new HashSet<uint>();
            if (frameAProps != null)
            {
                foreach (uint n in frameAProps.Keys) allNodes.Add(n);
            }
            if (frameBProps != null)
            {
                foreach (uint n in frameBProps.Keys) allNodes.Add(n);
            }

            foreach (uint nodeId in allNodes)
            {
                Dictionary<ushort, double> propsA = null;
                if (frameAProps != null) frameAProps.TryGetValue(nodeId, out propsA);

                Dictionary<ushort, double> propsB = null;
                if (frameBProps != null) frameBProps.TryGetValue(nodeId, out propsB);

                HashSet<ushort> allProps = new HashSet<ushort>();
                if (propsA != null)
                {
                    foreach (ushort p in propsA.Keys) allProps.Add(p);
                }
                if (propsB != null)
                {
                    foreach (ushort p in propsB.Keys) allProps.Add(p);
                }

                foreach (ushort propKey in allProps)
                {
                    double valA = (propsA != null && propsA.ContainsKey(propKey)) ? propsA[propKey] : 0.0;
                    double valB = (propsB != null && propsB.ContainsKey(propKey)) ? propsB[propKey] : valA;

                    // Fade in if only in B
                    if (propsA == null || !propsA.ContainsKey(propKey))
                    {
                        if (propKey == (ushort)PropKey.Opacity)
                        {
                            valA = 0.0;
                        }
                    }

                    // Fade out if only in A
                    if (propsB == null || !propsB.ContainsKey(propKey))
                    {
                        if (propKey == (ushort)PropKey.Opacity)
                        {
                            valB = 0.0;
                        }
                    }

                    if (Math.Abs(valA - valB) > 0.001)
                    {
                        Track tr = new Track(nodeId, propKey);
                        tr.AddKeyframe(Keyframe.Cubic(0.0, valA, easeX1, easeY1, easeX2, easeY2));
                        tr.AddKeyframe(Keyframe.Linear(duration, valB));
                        clip.AddTrack(tr);
                    }
                }
            }

            return clip;
        }

        public static Dictionary<uint, Dictionary<ushort, double>> ExtractFrameProperties(LayerNode frame)
        {
            var result = new Dictionary<uint, Dictionary<ushort, double>>();
            if (frame == null) return result;

            List<LayerNode> all = new List<LayerNode>();
            CollectLayers(frame, all);

            for (int i = 0; i < all.Count; i++)
            {
                LayerNode l = all[i];
                uint numericId = PropertyBinder.GetNumericNodeId(l.Id);

                var props = new Dictionary<ushort, double>();
                props[(ushort)PropKey.X] = l.X;
                props[(ushort)PropKey.Y] = l.Y;
                props[(ushort)PropKey.Width] = l.Width;
                props[(ushort)PropKey.Height] = l.Height;
                props[(ushort)PropKey.Rotation] = l.Rotation;
                props[(ushort)PropKey.Opacity] = l.Opacity;
                props[(ushort)PropKey.CornerRadius] = l.CornerRadius;

                result[numericId] = props;
            }

            return result;
        }

        private static void CollectLayers(LayerNode root, List<LayerNode> outList)
        {
            if (root == null) return;
            outList.Add(root);
            if (root.Children != null)
            {
                for (int i = 0; i < root.Children.Count; i++)
                {
                    CollectLayers(root.Children[i], outList);
                }
            }
        }
    }
}

