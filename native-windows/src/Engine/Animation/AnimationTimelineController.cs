using System;
using System.Collections.Generic;

namespace Kerf.Engine.Animation
{
    public class AnimationTimelineController
    {
        public double CurrentTime { get; private set; }
        public double Duration { get; set; }
        public double Fps { get; set; }
        public double Speed { get; set; }
        public bool IsPlaying { get; private set; }
        public LoopMode LoopMode { get; set; }

        public Clip ActiveClip { get; set; }
        public StateMachineRuntime ActiveStateMachine { get; set; }

        public event EventHandler<Dictionary<uint, Dictionary<ushort, double>>> FrameUpdated;

        private readonly Dictionary<uint, Dictionary<ushort, double>> _currentPose;

        public AnimationTimelineController(double duration = 1.0, double fps = 60.0)
        {
            CurrentTime = 0.0;
            Duration = Math.Max(0.001, duration);
            Fps = Math.Max(1.0, fps);
            Speed = 1.0;
            IsPlaying = false;
            LoopMode = LoopMode.Loop;
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
            Seek(0.0);
        }

        public void Seek(double time)
        {
            CurrentTime = Math.Max(0.0, Math.Min(Duration, time));
            EvaluateAndNotify(0.0);
        }

        public void Step(double dt)
        {
            double stepDt = dt * Speed;
            CurrentTime += stepDt;

            switch (LoopMode)
            {
                case LoopMode.OneShot:
                    if (CurrentTime >= Duration)
                    {
                        CurrentTime = Duration;
                        IsPlaying = false;
                    }
                    else if (CurrentTime < 0.0)
                    {
                        CurrentTime = 0.0;
                        IsPlaying = false;
                    }
                    break;

                case LoopMode.Loop:
                    while (CurrentTime >= Duration) CurrentTime -= Duration;
                    while (CurrentTime < 0.0) CurrentTime += Duration;
                    break;

                case LoopMode.PingPong:
                    while (CurrentTime > Duration || CurrentTime < 0.0)
                    {
                        if (CurrentTime > Duration)
                        {
                            CurrentTime = 2.0 * Duration - CurrentTime;
                            Speed = -Math.Abs(Speed);
                        }
                        if (CurrentTime < 0.0)
                        {
                            CurrentTime = -CurrentTime;
                            Speed = Math.Abs(Speed);
                        }
                    }
                    break;
            }

            EvaluateAndNotify(dt);
        }

        private void EvaluateAndNotify(double dt)
        {
            _currentPose.Clear();

            if (ActiveStateMachine != null)
            {
                ActiveStateMachine.Update(dt, _currentPose);
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
