using System;
using System.Collections.Generic;

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
            Preset = (interp == InterpMode.Linear) ? EasingPreset.Linear : EasingPreset.CustomBezier;
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
        public uint NodeId { get; set; }
        public string TargetNodeId { get; set; }
        public ushort Prop { get; set; }
        public string PropertyPath { get; set; }
        public List<Keyframe> Keys { get; set; }

        public Track() : this(0, 0) { }

        public Track(uint nodeId, ushort prop, string targetNodeId = null, string propPath = null)
        {
            NodeId = nodeId;
            TargetNodeId = targetNodeId ?? string.Empty;
            Prop = prop;
            PropertyPath = propPath ?? ((PropKey)prop).ToString();
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
                    return a.Value + (b.Value - a.Value) * eased;
                case InterpMode.Spring:
                    double springProgress = EasingEngine.EvaluateSpring(t - a.Time, a.Mass, a.Stiffness, a.Damping, a.InitialVelocity);
                    return a.Value + (b.Value - a.Value) * springProgress;
                default:
                    return a.Value;
            }
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

    public class Clip
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public double Duration { get; set; }
        public double Fps { get; set; }
        public LoopMode LoopMode { get; set; }
        public List<Track> Tracks { get; set; }

        public Clip() : this(0, "Clip", 1.0) { }

        public Clip(uint id, string name, double duration, double fps = 60.0, LoopMode loopMode = LoopMode.OneShot)
        {
            Id = id;
            Name = name ?? "Clip";
            Duration = Math.Max(0.001, duration);
            Fps = Math.Max(1.0, fps);
            LoopMode = loopMode;
            Tracks = new List<Track>();
        }

        public void AddTrack(Track track)
        {
            if (Tracks == null) Tracks = new List<Track>();
            if (track != null) Tracks.Add(track);
        }

        public void SampleToPose(double t, Dictionary<uint, Dictionary<ushort, double>> outPose)
        {
            if (outPose == null || Tracks == null) return;
            for (int i = 0; i < Tracks.Count; i++)
            {
                Track tr = Tracks[i];
                if (tr == null) continue;
                double val = tr.Sample(t);

                Dictionary<ushort, double> nodeProps;
                if (!outPose.TryGetValue(tr.NodeId, out nodeProps))
                {
                    nodeProps = new Dictionary<ushort, double>();
                    outPose[tr.NodeId] = nodeProps;
                }
                nodeProps[tr.Prop] = val;
            }
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

            if (double.IsNaN(Speed) || double.IsInfinity(Speed)) Speed = 1.0;
            if (Direction == 0) Direction = 1;
            double previousTime = Time;
            double delta = dt * Speed * Direction;
            double nextTime = previousTime + delta;
            bool completed = false;

            switch (loopMode)
            {
                case LoopMode.OneShot:
                    if (nextTime >= duration)
                    {
                        Time = duration;
                        completed = delta >= 0.0;
                    }
                    else if (nextTime <= 0.0)
                    {
                        Time = 0.0;
                        completed = delta <= 0.0;
                    }
                    else Time = nextTime;
                    break;

                case LoopMode.Loop:
                    Time = nextTime % duration;
                    if (Time < 0.0) Time += duration;
                    completed = nextTime < 0.0 || nextTime >= duration;
                    break;

                case LoopMode.PingPong:
                    double phase = nextTime % (duration * 2.0);
                    if (phase < 0.0) phase += duration * 2.0;
                    Time = phase <= duration ? phase : (duration * 2.0 - phase);
                    Direction = (phase <= duration ? 1 : -1) * (delta < 0.0 ? -1 : 1);
                    completed = nextTime < 0.0 || nextTime > duration;
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
            StateMachine = null;
            ActiveClipId = 0;
        }
    }
}

