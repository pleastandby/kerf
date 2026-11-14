using System;
using System.Collections.Generic;
using Kerf.Model;

namespace Kerf.Engine.Animation
{
    public class ClipboardKeyframeItem
    {
        public uint SourceNodeId { get; set; }
        public ushort PropKey { get; set; }
        public int GeometryNodeIndex { get; set; }
        public double RelativeTime { get; set; }
        public double Value { get; set; }
        public InterpMode Interp { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public VectorGeometry GeometryValue { get; set; }
        public BezierNode GeometryNodeValue { get; set; }

        public ClipboardKeyframeItem()
        {
            X1 = 0.0;
            Y1 = 0.0;
            X2 = 1.0;
            Y2 = 1.0;
        }
    }

    public static class KeyframeClipboard
    {
        private static readonly List<ClipboardKeyframeItem> _buffer = new List<ClipboardKeyframeItem>();

        public static bool HasData
        {
            get { return _buffer.Count > 0; }
        }

        public static int Count
        {
            get { return _buffer.Count; }
        }

        public static void Clear()
        {
            _buffer.Clear();
        }

        public static void Copy(IEnumerable<Tuple<Track, Keyframe>> selection)
        {
            _buffer.Clear();
            if (selection == null) return;

            double minTime = double.MaxValue;
            var list = new List<Tuple<Track, Keyframe>>();
            foreach (var item in selection)
            {
                if (item != null && item.Item1 != null && item.Item2 != null)
                {
                    list.Add(item);
                    if (item.Item2.Time < minTime)
                    {
                        minTime = item.Item2.Time;
                    }
                }
            }

            if (list.Count == 0 || minTime == double.MaxValue) return;

            foreach (var item in list)
            {
                var tr = item.Item1;
                var kf = item.Item2;
                _buffer.Add(new ClipboardKeyframeItem
                {
                    SourceNodeId = tr.NodeId,
                    PropKey = tr.Prop,
                    GeometryNodeIndex = tr.GeometryNodeIndex,
                    RelativeTime = Math.Max(0.0, kf.Time - minTime),
                    Value = kf.Value,
                    Interp = kf.Interp,
                    X1 = kf.X1,
                    Y1 = kf.Y1,
                    X2 = kf.X2,
                    Y2 = kf.Y2,
                    GeometryValue = kf.GeometryValue != null ? kf.GeometryValue.Clone() : null,
                    GeometryNodeValue = kf.GeometryNodeValue != null ? kf.GeometryNodeValue.Clone() : null
                });
            }
        }

        public static List<Keyframe> Paste(Clip targetClip, uint targetNodeId, double playheadTime)
        {
            var pastedKeys = new List<Keyframe>();
            if (targetClip == null || _buffer.Count == 0) return pastedKeys;

            double baseTime = Math.Max(0.0, playheadTime);

            foreach (var item in _buffer)
            {
                uint nodeId = (targetNodeId != 0) ? targetNodeId : item.SourceNodeId;
                Track track;
                if (item.PropKey == (ushort)PropKey.PathNode)
                {
                    track = targetClip.GetGeometryNodeTrack(nodeId, item.GeometryNodeIndex);
                    if (track == null)
                    {
                        track = new Track(nodeId, item.PropKey, null,
                            "Node " + (item.GeometryNodeIndex + 1).ToString(), item.GeometryNodeIndex);
                        targetClip.AddTrack(track);
                    }
                }
                else
                {
                    track = targetClip.GetOrCreateTrack(nodeId, item.PropKey);
                }
                double kfTime = baseTime + item.RelativeTime;

                var newKf = new Keyframe(kfTime, item.Value, item.Interp, item.X1, item.Y1, item.X2, item.Y2);
                newKf.GeometryValue = item.GeometryValue != null ? item.GeometryValue.Clone() : null;
                newKf.GeometryNodeValue = item.GeometryNodeValue != null ? item.GeometryNodeValue.Clone() : null;
                track.AddKeyframe(newKf);
                pastedKeys.Add(newKf);
            }

            return pastedKeys;
        }

        public static List<Keyframe> Duplicate(Clip targetClip, IEnumerable<Tuple<Track, Keyframe>> selection, double timeOffset = 0.1)
        {
            var duplicatedKeys = new List<Keyframe>();
            if (targetClip == null || selection == null) return duplicatedKeys;

            foreach (var item in selection)
            {
                if (item != null && item.Item1 != null && item.Item2 != null)
                {
                    var track = item.Item1;
                    var origKf = item.Item2;
                    double newTime = origKf.Time + Math.Max(0.001, timeOffset);

                    var newKf = new Keyframe(newTime, origKf.Value, origKf.Interp, origKf.X1, origKf.Y1, origKf.X2, origKf.Y2);
                    newKf.GeometryValue = origKf.GeometryValue != null ? origKf.GeometryValue.Clone() : null;
                    newKf.GeometryNodeValue = origKf.GeometryNodeValue != null ? origKf.GeometryNodeValue.Clone() : null;
                    track.AddKeyframe(newKf);
                    duplicatedKeys.Add(newKf);
                }
            }

            return duplicatedKeys;
        }
    }
}
