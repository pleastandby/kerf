using System;
using System.Collections.Generic;
using Kerf.Model;

namespace Kerf.Engine.Animation
{
    public class AnimationTimelineController
    {
        public double CurrentTime { get; private set; }
        public double Duration { get; set; }
        public double WorkAreaStart
        {
            get { return _activeClip != null ? _activeClip.GetNormalizedWorkAreaStart() : 0.0; }
        }
        public double WorkAreaEnd
        {
            get { return _activeClip != null ? _activeClip.GetNormalizedWorkAreaEnd() : Duration; }
        }
        public double Fps { get; set; }
        private double _speed = 1.0;
        public double Speed
        {
            get { return _speed; }
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) _speed = 1.0;
                else _speed = Math.Max(0.01, Math.Min(100.0, value));
            }
        }
        public bool IsPlaying { get; private set; }
        public LoopMode LoopMode { get; set; }
        public bool UseStateMachine { get; set; }
        public bool SnapToFrames { get; set; }

        public double SnapTime(double time)
        {
            if (!SnapToFrames) return time;
            double fps = Math.Max(1.0, Fps);
            return Math.Round(time * fps) / fps;
        }

        private Clip _activeClip;
        public Clip ActiveClip
        {
            get { return _activeClip; }
            set
            {
                _activeClip = value;
                if (_activeClip != null)
                {
                    Duration = Math.Max(0.001, _activeClip.Duration);
                    CurrentTime = Math.Max(WorkAreaStart, Math.Min(WorkAreaEnd, CurrentTime));
                    LoopMode = _activeClip.LoopMode;
                }
            }
        }
        public StateMachineRuntime ActiveStateMachine { get; set; }

        public event EventHandler<Dictionary<uint, Dictionary<ushort, double>>> FrameUpdated;
        public event EventHandler<Dictionary<uint, VectorGeometry>> GeometryFrameUpdated;
        public event EventHandler PlaybackStopped;

        private readonly Dictionary<uint, Dictionary<ushort, double>> _currentPose;
        private readonly Dictionary<uint, VectorGeometry> _currentGeometryPose;

        public AnimationTimelineController(double duration = 1.0, double fps = 60.0)
        {
            CurrentTime = 0.0;
            Duration = Math.Max(0.001, duration);
            Fps = Math.Max(1.0, fps);
            Speed = 1.0;
            IsPlaying = false;
            LoopMode = LoopMode.OneShot;
            UseStateMachine = false;
            SnapToFrames = true;
            _currentPose = new Dictionary<uint, Dictionary<ushort, double>>();
            _currentGeometryPose = new Dictionary<uint, VectorGeometry>();
        }

        public void Play()
        {
            if (CurrentTime >= WorkAreaEnd || CurrentTime < WorkAreaStart)
            {
                CurrentTime = WorkAreaStart;
            }
            IsPlaying = true;
        }

        public void Pause()
        {
            IsPlaying = false;
        }

        public void Stop()
        {
            IsPlaying = false;
            if (ActiveStateMachine != null) ActiveStateMachine.Reset();
            Seek(WorkAreaStart);
            var handler = PlaybackStopped;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Seek(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)) time = 0.0;
            CurrentTime = Math.Max(WorkAreaStart, Math.Min(WorkAreaEnd, time));
            EvaluateAndNotify(0.0);
        }

        public void StepFrame(int deltaFrames)
        {
            double frameDuration = 1.0 / Math.Max(1.0, Fps);
            Seek(CurrentTime + deltaFrames * frameDuration);
        }

        public void Step(double dt)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt)) dt = 0.0;
            if (double.IsNaN(Speed) || double.IsInfinity(Speed)) Speed = 1.0;
            dt = Math.Max(0.0, dt);
            double stepDt = dt * Math.Abs(Speed);

            if (IsPlaying)
            {
                if (UseStateMachine)
                {
                    CurrentTime += stepDt * _playDirection;
                    if (ActiveStateMachine != null && ActiveStateMachine.IsFinished)
                    {
                        IsPlaying = false;
                    }
                }
                else
                {
                    double start = WorkAreaStart;
                    double end = WorkAreaEnd;
                    double rangeDuration = Math.Max(0.001, end - start);
                    double nextTime = CurrentTime + stepDt * _playDirection;

                    switch (LoopMode)
                    {
                        case LoopMode.OneShot:
                            if (nextTime >= end)
                            {
                                CurrentTime = end;
                                IsPlaying = false;
                            }
                            else if (nextTime <= start)
                            {
                                CurrentTime = start;
                                IsPlaying = false;
                            }
                            else CurrentTime = nextTime;
                            break;

                        case LoopMode.Loop:
                            CurrentTime = start + PositiveModulo(nextTime - start, rangeDuration);
                            _playDirection = 1;
                            break;

                        case LoopMode.PingPong:
                            double phase = PositiveModulo(nextTime - start, rangeDuration * 2.0);
                            CurrentTime = start + (phase <= rangeDuration ? phase : (rangeDuration * 2.0 - phase));
                            _playDirection = (phase <= rangeDuration ? 1 : -1) * _playDirection;
                            break;
                    }
                }
            }

            EvaluateAndNotify(stepDt);
        }

        private int _playDirection = 1;

        private static double PositiveModulo(double value, double modulus)
        {
            if (modulus <= 0.0) return 0.0;
            double result = value % modulus;
            return result < 0.0 ? result + modulus : result;
        }

        private void EvaluateAndNotify(double dt)
        {
            _currentPose.Clear();
            _currentGeometryPose.Clear();

            if (UseStateMachine && ActiveStateMachine != null)
            {
                double effectiveDt = IsPlaying ? Math.Abs(dt) : 0.0;
                ActiveStateMachine.Update(effectiveDt, _currentPose, _currentGeometryPose);
            }
            else if (ActiveClip != null)
            {
                ActiveClip.SampleToPose(CurrentTime, _currentPose, _currentGeometryPose);
            }

            var handler = FrameUpdated;
            if (handler != null)
            {
                handler(this, _currentPose);
            }

            var geometryHandler = GeometryFrameUpdated;
            if (geometryHandler != null) geometryHandler(this, _currentGeometryPose);
        }
    }
}

