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
        public List<Keyframe> Keys { get; private set; }

        public Track(uint nodeId, ushort prop)
        {
            NodeId = nodeId;
            Prop = prop;
            Keys = new List<Keyframe>();
        }

        public void AddKeyframe(Keyframe kf)
        {
            int idx = Keys.BinarySearch(kf, KeyframeComparer.Instance);
            if (idx < 0) idx = ~idx;
            Keys.Insert(idx, kf);
        }

        public double Sample(double t)
        {
            if (Keys.Count == 0) return 0.0;
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
        public List<Track> Tracks { get; private set; }

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
            if (track != null) Tracks.Add(track);
        }

        public void SampleToPose(double t, Dictionary<uint, Dictionary<ushort, double>> outPose)
        {
            if (outPose == null) return;
            for (int i = 0; i < Tracks.Count; i++)
            {
                Track tr = Tracks[i];
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
            if (duration <= 0.0)
            {
                Time = 0.0;
                return true;
            }

            Time += dt * Speed * Direction;
            bool completed = false;

            switch (loopMode)
            {
                case LoopMode.OneShot:
                    if (Direction >= 0 && Time >= duration)
                    {
                        Time = duration;
                        completed = true;
                    }
                    else if (Direction < 0 && Time <= 0.0)
                    {
                        Time = 0.0;
                        completed = true;
                    }
                    break;

                case LoopMode.Loop:
                    while (Time >= duration)
                    {
                        Time -= duration;
                        completed = true;
                    }
                    while (Time < 0.0)
                    {
                        Time += duration;
                        completed = true;
                    }
                    break;

                case LoopMode.PingPong:
                    while (Time > duration || Time < 0.0)
                    {
                        if (Time > duration)
                        {
                            Time = 2.0 * duration - Time;
                            Direction = -1;
                            completed = true;
                        }
                        if (Time < 0.0)
                        {
                            Time = -Time;
                            Direction = 1;
                            completed = true;
                        }
                    }
                    break;
            }

            return completed;
        }
    }
}
