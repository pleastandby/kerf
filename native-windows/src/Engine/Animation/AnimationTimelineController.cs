using System;
using System.Collections.Generic;

namespace Kerf.Engine.Animation
{
    public class AnimationTimelineController
    {
        public double CurrentTime { get; private set; }
        public double Duration { get; set; }
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
                    CurrentTime = Math.Max(0.0, Math.Min(Duration, CurrentTime));
                    LoopMode = _activeClip.LoopMode;
                }
            }
        }
        public StateMachineRuntime ActiveStateMachine { get; set; }

        public event EventHandler<Dictionary<uint, Dictionary<ushort, double>>> FrameUpdated;
        public event EventHandler PlaybackStopped;

        private readonly Dictionary<uint, Dictionary<ushort, double>> _currentPose;

        public AnimationTimelineController(double duration = 1.0, double fps = 60.0)
        {
            CurrentTime = 0.0;
            Duration = Math.Max(0.001, duration);
            Fps = Math.Max(1.0, fps);
            Speed = 1.0;
            IsPlaying = false;
            LoopMode = LoopMode.Loop;
            UseStateMachine = false;
            _currentPose = new Dictionary<uint, Dictionary<ushort, double>>();
        }

        public void Play()
        {
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
            Seek(0.0);
            var handler = PlaybackStopped;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Seek(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)) time = 0.0;
            CurrentTime = Math.Max(0.0, Math.Min(Duration, time));
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
            double nextTime = CurrentTime + stepDt * _playDirection;

            switch (LoopMode)
            {
                case LoopMode.OneShot:
                    if (nextTime >= Duration)
                    {
                        CurrentTime = Duration;
                        IsPlaying = false;
                    }
                    else if (nextTime <= 0.0)
                    {
                        CurrentTime = 0.0;
                        IsPlaying = false;
                    }
                    else CurrentTime = nextTime;
                    break;

                case LoopMode.Loop:
                    CurrentTime = PositiveModulo(nextTime, Duration);
                    _playDirection = 1;
                    break;

                case LoopMode.PingPong:
                    double phase = PositiveModulo(nextTime, Duration * 2.0);
                    CurrentTime = phase <= Duration ? phase : (Duration * 2.0 - phase);
                    _playDirection = (phase <= Duration ? 1 : -1) * _playDirection;
                    break;
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

            if (UseStateMachine && ActiveStateMachine != null)
            {
                ActiveStateMachine.Update(Math.Abs(dt), _currentPose);
            }
            else if (ActiveClip != null)
            {
                ActiveClip.SampleToPose(CurrentTime, _currentPose);
            }

            var handler = FrameUpdated;
            if (handler != null)
            {
                handler(this, _currentPose);
            }
        }
    }
}

