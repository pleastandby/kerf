using System;
using System.Collections.Generic;
using Kerf.Model;

namespace Kerf.Engine.Animation
{
    public enum PropKey : ushort
    {
        X = 1,
        Y = 2,
        Width = 3,
        Height = 4,
        Rotation = 5,
        Opacity = 6,
        CornerRadius = 7,
        ColorR = 8,
        ColorG = 9,
        ColorB = 10,
        ColorA = 11,
        ScaleX = 12,
        ScaleY = 13,
        AnchorX = 14,
        AnchorY = 15,
        VisualWidth = 16,
        VisualHeight = 17,
        StrokeWidth = 18,
        StrokeTrimStart = 19,
        StrokeTrimEnd = 20,
        StrokeTrimOffset = 21,
        StrokeTaperStart = 22,
        StrokeTaperEnd = 23,
        Path = 24,
        PathNode = 25,
        Custom = 100
    }

    public enum InterpMode
    {
        Hold = 0,
        Linear = 1,
        Cubic = 2,
        Spring = 3
    }

    public enum EasingPreset
    {
        Linear = 0,
        Ease = 1,
        EaseIn = 2,
        EaseOut = 3,
        EaseInOut = 4,
        QuadIn = 5,
        QuadOut = 6,
        QuadInOut = 7,
        CubicIn = 8,
        CubicOut = 9,
        CubicInOut = 10,
        QuartIn = 11,
        QuartOut = 12,
        QuartInOut = 13,
        QuintIn = 14,
        QuintOut = 15,
        QuintInOut = 16,
        Back = 17,
        Elastic = 18,
        Bounce = 19,
        Smooth = 20,
        CustomBezier = 21
    }

    public enum LoopMode
    {
        OneShot = 0,
        Loop = 1,
        PingPong = 2
    }

    public class Keyframe
    {
        public string Id { get; set; }
        public double Time { get; set; }
        public double Value { get; set; }
        public InterpMode Interp { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public double Mass { get; set; }
        public double Stiffness { get; set; }
        public double Damping { get; set; }
        public double InitialVelocity { get; set; }
        public bool IsTangentBroken { get; set; }
        public EasingPreset Preset { get; set; }
        public VectorGeometry GeometryValue { get; set; }
        public BezierNode GeometryNodeValue { get; set; }

        public Keyframe() : this(0.0, 0.0, InterpMode.Linear) { }

        public Keyframe(double time, double val, InterpMode interp, double x1 = 0.0, double y1 = 0.0, double x2 = 1.0, double y2 = 1.0, string id = null)
        {
            Id = !string.IsNullOrEmpty(id) ? id : Guid.NewGuid().ToString("N");
            Time = time;
            Value = val;
            Interp = interp;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;

            Mass = 1.0;
            Stiffness = 100.0;
            Damping = 10.0;
            InitialVelocity = 0.0;
            IsTangentBroken = false;
            Preset = EasingPreset.Linear;
        }

        public Keyframe Clone(bool preserveId = false)
        {
            var kf = new Keyframe(Time, Value, Interp, X1, Y1, X2, Y2, preserveId ? Id : null);
            kf.Mass = Mass;
            kf.Stiffness = Stiffness;
            kf.Damping = Damping;
            kf.InitialVelocity = InitialVelocity;
            kf.IsTangentBroken = IsTangentBroken;
            kf.Preset = Preset;
            kf.GeometryValue = GeometryValue != null ? GeometryValue.Clone() : null;
            kf.GeometryNodeValue = GeometryNodeValue != null ? GeometryNodeValue.Clone() : null;
            return kf;
        }

        public static Keyframe Hold(double time, double val)
        {
            return new Keyframe(time, val, InterpMode.Hold);
        }

        public static Keyframe Linear(double time, double val)
        {
            return new Keyframe(time, val, InterpMode.Linear);
        }

        public static Keyframe Cubic(double time, double val, double x1, double y1, double x2, double y2)
        {
            return new Keyframe(time, val, InterpMode.Cubic, x1, y1, x2, y2);
        }

        public static Keyframe Spring(double time, double val, double mass = 1.0, double stiffness = 100.0, double damping = 10.0, double initialVelocity = 0.0)
        {
            var kf = new Keyframe(time, val, InterpMode.Spring);
            kf.Mass = mass;
            kf.Stiffness = stiffness;
            kf.Damping = damping;
            kf.InitialVelocity = initialVelocity;
            return kf;
        }

        public override bool Equals(object obj)
        {
            var other = obj as Keyframe;
            if (other == null) return false;
            return string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return Id != null ? Id.GetHashCode() : 0;
        }
    }

    public class Track
    {
        public string Id { get; set; }
        public bool IsActive { get; set; }
        public uint NodeId { get; set; }
        public string TargetNodeId { get; set; }
        public ushort Prop { get; set; }
        public string PropertyPath { get; set; }
        public int GeometryNodeIndex { get; set; }
        public List<Keyframe> Keys { get; set; }

        public Track() : this(0, 0) { }

        public Track(uint nodeId, ushort prop, string targetNodeId = null, string propPath = null, int geometryNodeIndex = -1)
        {
            Id = Guid.NewGuid().ToString("N");
            IsActive = true;
            NodeId = nodeId;
            TargetNodeId = targetNodeId ?? string.Empty;
            Prop = prop;
            PropertyPath = propPath ?? ((PropKey)prop).ToString();
            GeometryNodeIndex = geometryNodeIndex;
            Keys = new List<Keyframe>();
        }

        public void AddKeyframe(Keyframe kf)
        {
            if (kf == null) return;
            if (Keys == null) Keys = new List<Keyframe>();
            for (int i = 0; i < Keys.Count; i++)
            {
                if (Math.Abs(Keys[i].Time - kf.Time) < 1e-6)
                {
                    // Deterministic duplicate resolution: update existing keyframe's values
                    Keys[i].Value = kf.Value;
                    Keys[i].Interp = kf.Interp;
                    Keys[i].X1 = kf.X1;
                    Keys[i].Y1 = kf.Y1;
                    Keys[i].X2 = kf.X2;
                    Keys[i].Y2 = kf.Y2;
                    Keys[i].Mass = kf.Mass;
                    Keys[i].Stiffness = kf.Stiffness;
                    Keys[i].Damping = kf.Damping;
                    Keys[i].InitialVelocity = kf.InitialVelocity;
                    Keys[i].IsTangentBroken = kf.IsTangentBroken;
                    Keys[i].Preset = kf.Preset;
                    Keys[i].GeometryValue = kf.GeometryValue != null ? kf.GeometryValue.Clone() : null;
                    Keys[i].GeometryNodeValue = kf.GeometryNodeValue != null ? kf.GeometryNodeValue.Clone() : null;
                    return;
                }
            }
            int idx = Keys.BinarySearch(kf, KeyframeComparer.Instance);
            if (idx < 0) idx = ~idx;
            Keys.Insert(idx, kf);
        }

        public bool RemoveKeyframe(string keyframeId)
        {
            if (Keys == null || string.IsNullOrEmpty(keyframeId)) return false;
            for (int i = 0; i < Keys.Count; i++)
            {
                if (Keys[i].Id == keyframeId)
                {
                    Keys.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public bool RemoveKeyframeAt(double time, double tolerance = 1e-6)
        {
            if (Keys == null) return false;
            for (int i = 0; i < Keys.Count; i++)
            {
                if (Math.Abs(Keys[i].Time - time) <= tolerance)
                {
                    Keys.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public Keyframe FindKeyframeAt(double time, double tolerance = 1e-6)
        {
            if (Keys == null) return null;
            for (int i = 0; i < Keys.Count; i++)
            {
                if (Math.Abs(Keys[i].Time - time) <= tolerance)
                {
                    return Keys[i];
                }
            }
            return null;
        }

        public double? GetPreviousKeyframeTime(double currentTime)
        {
            if (Keys == null || Keys.Count == 0) return null;
            double? prev = null;
            for (int i = 0; i < Keys.Count; i++)
            {
                if (Keys[i].Time < currentTime - 1e-4)
                {
                    prev = Keys[i].Time;
                }
                else
                {
                    break;
                }
            }
            return prev;
        }

        public double? GetNextKeyframeTime(double currentTime)
        {
            if (Keys == null || Keys.Count == 0) return null;
            for (int i = 0; i < Keys.Count; i++)
            {
                if (Keys[i].Time > currentTime + 1e-4)
                {
                    return Keys[i].Time;
                }
            }
            return null;
        }

        public void SortKeys()
        {
            if (Keys != null && Keys.Count > 1)
            {
                Keys.Sort(KeyframeComparer.Instance);
            }
        }

        public double Sample(double t)
        {
            if (Keys == null || Keys.Count == 0) return 0.0;
            if (t <= Keys[0].Time) return Keys[0].Value;
            if (t >= Keys[Keys.Count - 1].Time) return Keys[Keys.Count - 1].Value;

            int low = 0;
            int high = Keys.Count - 1;
            int idx = 0;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (Keys[mid].Time <= t)
                {
                    idx = mid;
                    low = mid + 1;
                }
                else
                {
                    if (mid == 0) break;
                    high = mid - 1;
                }
            }

            if (idx >= Keys.Count - 1) return Keys[Keys.Count - 1].Value;

            Keyframe a = Keys[idx];
            Keyframe b = Keys[idx + 1];
            double span = b.Time - a.Time;
            if (span <= 1e-6) return a.Value;

            double u = (t - a.Time) / span;
            switch (a.Interp)
            {
                case InterpMode.Hold:
                    return a.Value;
                case InterpMode.Linear:
                    return a.Value + (b.Value - a.Value) * u;
                case InterpMode.Cubic:
                    double eased = CubicBezierEase.Evaluate(u, a.X1, a.Y1, a.X2, a.Y2);
                    if (double.IsNaN(eased) || double.IsInfinity(eased)) eased = u;
                    return a.Value + (b.Value - a.Value) * eased;
                case InterpMode.Spring:
                    double springProgress = EasingEngine.EvaluateSpring(t - a.Time, a.Mass, a.Stiffness, a.Damping, a.InitialVelocity);
                    if (double.IsNaN(springProgress) || double.IsInfinity(springProgress)) springProgress = 1.0;
                    return a.Value + (b.Value - a.Value) * springProgress;
                default:
                    return a.Value;
            }
        }

        public VectorGeometry SampleGeometry(double t)
        {
            return GeometryAnimation.Sample(Keys, t);
        }

        public BezierNode SampleGeometryNode(double t)
        {
            return GeometryAnimation.SampleNode(Keys, t);
        }

        private class KeyframeComparer : IComparer<Keyframe>
        {
            public static readonly KeyframeComparer Instance = new KeyframeComparer();
            public int Compare(Keyframe x, Keyframe y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                return x.Time.CompareTo(y.Time);
            }
        }
    }

    public static class GeometryAnimation
    {
        public static VectorGeometry Sample(IList<Keyframe> keys, double time)
        {
            if (keys == null || keys.Count == 0) return null;
            var valid = new List<Keyframe>();
            for (int i = 0; i < keys.Count; i++)
                if (keys[i] != null && keys[i].GeometryValue != null) valid.Add(keys[i]);
            if (valid.Count == 0) return null;
            if (time <= valid[0].Time) return valid[0].GeometryValue.Clone();
            if (time >= valid[valid.Count - 1].Time) return valid[valid.Count - 1].GeometryValue.Clone();

            for (int i = 0; i < valid.Count - 1; i++)
            {
                Keyframe from = valid[i];
                Keyframe to = valid[i + 1];
                if (time < from.Time || time > to.Time) continue;
                double span = to.Time - from.Time;
                double progress = span <= 1e-6 ? 1.0 : Math.Max(0.0, Math.Min(1.0, (time - from.Time) / span));
                if (from.Interp == InterpMode.Hold) return from.GeometryValue.Clone();
                if (from.Interp == InterpMode.Cubic)
                {
                    progress = CubicBezierEase.Evaluate(progress, from.X1, from.Y1, from.X2, from.Y2);
                    if (double.IsNaN(progress) || double.IsInfinity(progress)) progress = 0.0;
                }
                else if (from.Interp == InterpMode.Spring)
                {
                    progress = EasingEngine.EvaluateSpring(time - from.Time, from.Mass, from.Stiffness, from.Damping, from.InitialVelocity);
                    if (double.IsNaN(progress) || double.IsInfinity(progress)) progress = 0.0;
                }
                return Interpolate(from.GeometryValue, to.GeometryValue, Math.Max(0.0, Math.Min(1.0, progress)));
            }
            return valid[valid.Count - 1].GeometryValue.Clone();
        }

        public static BezierNode SampleNode(IList<Keyframe> keys, double time)
        {
            if (keys == null || keys.Count == 0) return null;
            var valid = new List<Keyframe>();
            for (int i = 0; i < keys.Count; i++)
                if (keys[i] != null && keys[i].GeometryNodeValue != null) valid.Add(keys[i]);
            if (valid.Count == 0) return null;
            if (time <= valid[0].Time) return valid[0].GeometryNodeValue.Clone();
            if (time >= valid[valid.Count - 1].Time) return valid[valid.Count - 1].GeometryNodeValue.Clone();

            for (int i = 0; i < valid.Count - 1; i++)
            {
                Keyframe from = valid[i];
                Keyframe to = valid[i + 1];
                if (time < from.Time || time > to.Time) continue;
                double span = to.Time - from.Time;
                double progress = span <= 1e-6 ? 1.0 : (time - from.Time) / span;
                if (from.Interp == InterpMode.Hold) return from.GeometryNodeValue.Clone();
                if (from.Interp == InterpMode.Cubic)
                    progress = CubicBezierEase.Evaluate(progress, from.X1, from.Y1, from.X2, from.Y2);
                else if (from.Interp == InterpMode.Spring)
                    progress = EasingEngine.EvaluateSpring(time - from.Time, from.Mass, from.Stiffness, from.Damping, from.InitialVelocity);
                if (double.IsNaN(progress) || double.IsInfinity(progress)) progress = 0.0;

                BezierNode a = from.GeometryNodeValue;
                BezierNode b = to.GeometryNodeValue;
                return new BezierNode
                {
                    Anchor = Interpolate(a.Anchor, b.Anchor, progress),
                    InHandle = Interpolate(a.InHandle, b.InHandle, progress),
                    OutHandle = Interpolate(a.OutHandle, b.OutHandle, progress),
                    Type = progress < 0.5 ? a.Type : b.Type
                };
            }
            return valid[valid.Count - 1].GeometryNodeValue.Clone();
        }

        public static VectorGeometry Interpolate(VectorGeometry from, VectorGeometry to, double amount)
        {
            if (from == null) return to != null ? to.Clone() : null;
            if (to == null) return from.Clone();
            amount = Math.Max(0.0, Math.Min(1.0, amount));
            if (amount <= 0.0) return from.Clone();
            if (amount >= 1.0) return to.Clone();

            bool canMorph = from.Nodes != null && to.Nodes != null && from.Nodes.Count == to.Nodes.Count &&
                from.Nodes.Count > 0 && string.IsNullOrEmpty(from.SvgPathData) && string.IsNullOrEmpty(to.SvgPathData);
            if (!canMorph) return from.Clone();

            var result = new VectorGeometry
            {
                IsClosed = amount < 0.5 ? from.IsClosed : to.IsClosed,
                SvgPathData = null,
                Nodes = new List<BezierNode>(from.Nodes.Count)
            };
            for (int i = 0; i < from.Nodes.Count; i++)
            {
                BezierNode a = from.Nodes[i];
                BezierNode b = to.Nodes[i];
                if (a == null || b == null)
                {
                    result.Nodes.Add((amount < 0.5 ? a : b) != null ? (amount < 0.5 ? a : b).Clone() : new BezierNode());
                    continue;
                }
                result.Nodes.Add(new BezierNode
                {
                    Anchor = Interpolate(a.Anchor, b.Anchor, amount),
                    InHandle = Interpolate(a.InHandle, b.InHandle, amount),
                    OutHandle = Interpolate(a.OutHandle, b.OutHandle, amount),
                    Type = amount < 0.5 ? a.Type : b.Type
                });
            }
            return result;
        }

        private static System.Windows.Point Interpolate(System.Windows.Point a, System.Windows.Point b, double amount)
        {
            return new System.Windows.Point(a.X + (b.X - a.X) * amount, a.Y + (b.Y - a.Y) * amount);
        }

        public static void BlendPoses(Dictionary<uint, VectorGeometry> from, Dictionary<uint, VectorGeometry> to, double amount, Dictionary<uint, VectorGeometry> result)
        {
            if (result == null) return;
            amount = Math.Max(0.0, Math.Min(1.0, amount));
            var nodeIds = new HashSet<uint>();
            if (from != null) foreach (uint id in from.Keys) nodeIds.Add(id);
            if (to != null) foreach (uint id in to.Keys) nodeIds.Add(id);
            foreach (uint id in nodeIds)
            {
                VectorGeometry a = null;
                VectorGeometry b = null;
                if (from != null) from.TryGetValue(id, out a);
                if (to != null) to.TryGetValue(id, out b);
                VectorGeometry chosen = a == null ? b : b == null ? a : Interpolate(a, b, amount);
                if (chosen != null) result[id] = chosen.Clone();
            }
        }
    }

    public class Clip
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public double Duration { get; set; }
        public double WorkAreaStart { get; set; }
        public double WorkAreaEnd { get; set; }
        public double Fps { get; set; }
        public LoopMode LoopMode { get; set; }
        public List<Track> Tracks { get; set; }

        public Clip() : this(0, "Clip", 1.0) { }

        public Clip(uint id, string name, double duration, double fps = 60.0, LoopMode loopMode = LoopMode.OneShot)
        {
            Id = id;
            Name = name ?? "Clip";
            Duration = Math.Max(0.001, duration);
            WorkAreaStart = 0.0;
            WorkAreaEnd = Duration;
            Fps = Math.Max(1.0, fps);
            LoopMode = loopMode;
            Tracks = new List<Track>();
        }

        public void AddTrack(Track track)
        {
            if (Tracks == null) Tracks = new List<Track>();
            if (track != null) Tracks.Add(track);
        }

        public bool RemoveTrack(Track track)
        {
            if (Tracks == null || track == null) return false;
            return Tracks.Remove(track);
        }

        public void SampleToPose(double t, Dictionary<uint, Dictionary<ushort, double>> outPose)
        {
            SampleToPose(t, outPose, null);
        }

        public void SampleToPose(double t, Dictionary<uint, Dictionary<ushort, double>> outPose, Dictionary<uint, VectorGeometry> geometryPose)
        {
            if (Tracks == null) return;
            // Sample complete paths first so per-node tracks can overlay them regardless of
            // track creation order.
            for (int i = 0; i < Tracks.Count; i++)
            {
                Track tr = Tracks[i];
                if (tr == null || !tr.IsActive) continue;
                if (tr.Prop == (ushort)PropKey.Path)
                {
                    VectorGeometry geometry = tr.SampleGeometry(t);
                    if (geometry != null && geometryPose != null) geometryPose[tr.NodeId] = geometry;
                    continue;
                }
                if (tr.Prop == (ushort)PropKey.PathNode) continue;
                if (outPose == null) continue;
                double val = tr.Sample(t);

                Dictionary<ushort, double> nodeProps;
                if (!outPose.TryGetValue(tr.NodeId, out nodeProps))
                {
                    nodeProps = new Dictionary<ushort, double>();
                    outPose[tr.NodeId] = nodeProps;
                }
                nodeProps[tr.Prop] = val;
            }

            if (geometryPose != null)
            {
                for (int i = 0; i < Tracks.Count; i++)
                {
                    Track tr = Tracks[i];
                    if (tr == null || !tr.IsActive || tr.Prop != (ushort)PropKey.PathNode || tr.GeometryNodeIndex < 0) continue;
                    VectorGeometry geometry;
                    if (!geometryPose.TryGetValue(tr.NodeId, out geometry) || geometry == null || geometry.Nodes == null || tr.GeometryNodeIndex >= geometry.Nodes.Count) continue;
                    BezierNode node = tr.SampleGeometryNode(t);
                    if (node != null) geometry.Nodes[tr.GeometryNodeIndex] = node;
                }
            }
        }

        public double GetNormalizedWorkAreaStart()
        {
            if (double.IsNaN(WorkAreaStart) || double.IsInfinity(WorkAreaStart)) return 0.0;
            return Math.Max(0.0, Math.Min(Math.Max(0.0, Duration - 0.001), WorkAreaStart));
        }

        public double GetNormalizedWorkAreaEnd()
        {
            double start = GetNormalizedWorkAreaStart();
            if (double.IsNaN(WorkAreaEnd) || double.IsInfinity(WorkAreaEnd) || WorkAreaEnd <= start)
                return Math.Max(start + 0.001, Duration);
            return Math.Max(start + 0.001, Math.Min(Duration, WorkAreaEnd));
        }

        public Dictionary<uint, Dictionary<ushort, double>> EvaluateAt(double t)
        {
            var pose = new Dictionary<uint, Dictionary<ushort, double>>();
            SampleToPose(t, pose);
            return pose;
        }

        public Track GetTrack(uint nodeId, ushort prop)
        {
            if (Tracks == null) return null;
            for (int i = 0; i < Tracks.Count; i++)
            {
                if (Tracks[i].NodeId == nodeId && Tracks[i].Prop == prop)
                {
                    return Tracks[i];
                }
            }
            return null;
        }

        public Track GetOrCreateTrack(uint nodeId, ushort prop, string targetNodeId = null, string propPath = null)
        {
            var tr = GetTrack(nodeId, prop);
            if (tr == null)
            {
                tr = new Track(nodeId, prop, targetNodeId, propPath);
                AddTrack(tr);
            }
            return tr;
        }

        public Track GetGeometryNodeTrack(uint nodeId, int geometryNodeIndex)
        {
            if (Tracks == null) return null;
            for (int i = 0; i < Tracks.Count; i++)
            {
                Track track = Tracks[i];
                if (track != null && track.NodeId == nodeId && track.Prop == (ushort)PropKey.PathNode &&
                    track.GeometryNodeIndex == geometryNodeIndex) return track;
            }
            return null;
        }

        public List<Track> GetTracksForNode(uint nodeId)
        {
            var list = new List<Track>();
            if (Tracks == null) return list;
            for (int i = 0; i < Tracks.Count; i++)
            {
                if (Tracks[i].NodeId == nodeId) list.Add(Tracks[i]);
            }
            return list;
        }
    }

    public class ClipInstance
    {
        public int ClipIndex { get; set; }
        public double Time { get; set; }
        public double Speed { get; set; }
        public int Direction { get; set; } // +1 or -1

        public ClipInstance(int clipIndex)
        {
            ClipIndex = clipIndex;
            Time = 0.0;
            Speed = 1.0;
            Direction = 1;
        }

        public bool Advance(double dt, double duration, LoopMode loopMode)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt)) dt = 0.0;
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0.0)
            {
                Time = 0.0;
                return true;
            }

            return Advance(dt, 0.0, duration, loopMode);
        }

        public bool Advance(double dt, double startTime, double endTime, LoopMode loopMode)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt)) dt = 0.0;
            if (double.IsNaN(startTime) || double.IsInfinity(startTime)) startTime = 0.0;
            if (double.IsNaN(endTime) || double.IsInfinity(endTime)) endTime = startTime + 0.001;
            startTime = Math.Max(0.0, startTime);
            if (endTime <= startTime) endTime = startTime + 0.001;

            double range = endTime - startTime;
            if (range <= 0.0)
            {
                Time = startTime;
                return true;
            }

            if (double.IsNaN(Speed) || double.IsInfinity(Speed)) Speed = 1.0;
            if (Direction == 0) Direction = 1;
            if (double.IsNaN(Time) || double.IsInfinity(Time) || Time < startTime || Time > endTime)
                Time = startTime;
            double previousTime = Time;
            double delta = dt * Speed * Direction;
            double nextTime = previousTime + delta;
            bool completed = false;

            switch (loopMode)
            {
                case LoopMode.OneShot:
                    if (nextTime >= endTime)
                    {
                        Time = endTime;
                        completed = delta >= 0.0;
                    }
                    else if (nextTime <= startTime)
                    {
                        Time = startTime;
                        completed = delta <= 0.0;
                    }
                    else Time = nextTime;
                    break;

                case LoopMode.Loop:
                    double loopPhase = (nextTime - startTime) % range;
                    if (loopPhase < 0.0) loopPhase += range;
                    Time = startTime + loopPhase;
                    completed = nextTime < startTime || nextTime >= endTime;
                    break;

                case LoopMode.PingPong:
                    double phase = (nextTime - startTime) % (range * 2.0);
                    if (phase < 0.0) phase += range * 2.0;
                    Time = startTime + (phase <= range ? phase : (range * 2.0 - phase));
                    Direction = (phase <= range ? 1 : -1) * (delta < 0.0 ? -1 : 1);
                    completed = nextTime < startTime || nextTime > endTime;
                    break;
            }

            return completed;
        }
    }

    public class AnimationProjectData
    {
        public List<Clip> Clips { get; set; }
        public StateMachineDefinition StateMachine { get; set; }
        public uint ActiveClipId { get; set; }

        public AnimationProjectData()
        {
            Clips = new List<Clip>();
            StateMachine = new StateMachineDefinition();
            ActiveClipId = 0;
        }
    }
}

