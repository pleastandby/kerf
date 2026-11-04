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
        Custom = 100
    }

    public enum InterpMode
    {
        Hold = 0,
        Linear = 1,
        Cubic = 2
    }

    public enum LoopMode
    {
        OneShot = 0,
        Loop = 1,
        PingPong = 2
    }

    public class Keyframe
    {
        public double Time { get; set; }
        public double Value { get; set; }
        public InterpMode Interp { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public Keyframe() : this(0.0, 0.0, InterpMode.Linear) { }

        public Keyframe(double time, double val, InterpMode interp, double x1 = 0.0, double y1 = 0.0, double x2 = 1.0, double y2 = 1.0)
        {
            Time = time;
            Value = val;
            Interp = interp;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
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
    }

    public class Track
    {
        public uint NodeId { get; set; }
        public ushort Prop { get; set; }
        public List<Keyframe> Keys { get; set; }

        public Track() : this(0, 0) { }

        public Track(uint nodeId, ushort prop)
        {
            NodeId = nodeId;
            Prop = prop;
            Keys = new List<Keyframe>();
        }

        public void AddKeyframe(Keyframe kf)
        {
            if (kf == null) return;
            if (Keys == null) Keys = new List<Keyframe>();
            for (int i = 0; i < Keys.Count; i++)
            {
                if (Math.Abs(Keys[i].Time - kf.Time) < 1e-9)
                {
                    Keys[i] = kf;
                    return;
                }
            }
            int idx = Keys.BinarySearch(kf, KeyframeComparer.Instance);
            if (idx < 0) idx = ~idx;
            Keys.Insert(idx, kf);
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

