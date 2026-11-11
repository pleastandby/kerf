using System;
using Kerf.Engine.Animation;
using Kerf.Model;
using Kerf.Runtime.Backends;

namespace Kerf.Runtime
{
    public class KerfPlayer
    {
        private readonly KerfRuntime _runtime;
        private readonly IRenderBackend _backend;
        private readonly RenderPose _currentPose;
        private bool _isPlaying;

        public event EventHandler<string> StateChanged;

        public bool IsPlaying
        {
            get { return _isPlaying; }
        }

        public RenderPose CurrentPose
        {
            get { return _currentPose; }
        }

        public IRenderBackend Backend
        {
            get { return _backend; }
        }

        public KerfRuntime Runtime
        {
            get { return _runtime; }
        }

        public KerfPlayer(IRenderBackend backend = null)
        {
            _backend = backend != null ? backend : new NullRenderBackend();
            _runtime = new KerfRuntime();
            _currentPose = new RenderPose();
            _isPlaying = false;

            _runtime.StateChanged += OnRuntimeStateChanged;
            _backend.Initialize(1920, 1080);
        }

        private void OnRuntimeStateChanged(object sender, string stateName)
        {
            if (StateChanged != null)
            {
                StateChanged(this, stateName);
            }
        }

        public void Load(string kfxFilePath)
        {
            _runtime.LoadPackage(kfxFilePath);
            InitializeBackendFromDocument();
        }

        public void Load(KerfDocument doc)
        {
            _runtime.LoadDocument(doc);
            InitializeBackendFromDocument();
        }

        private void InitializeBackendFromDocument()
        {
            if (_runtime.Document != null)
            {
                var page = _runtime.Document.GetActivePage();
                int w = 1920;
                int h = 1080;
                if (page != null && page.Layers != null && page.Layers.Count > 0)
                {
                    for (int i = 0; i < page.Layers.Count; i++)
                    {
                        var layer = page.Layers[i];
                        if (layer != null && layer.Type == ElementType.Frame && layer.Width > 0 && layer.Height > 0)
                        {
                            w = (int)Math.Max(1, Math.Round(layer.Width));
                            h = (int)Math.Max(1, Math.Round(layer.Height));
                            break;
                        }
                    }
                }
                _backend.Initialize(w, h);
            }
        }

        public void Play()
        {
            _isPlaying = true;
        }

        public void Pause()
        {
            _isPlaying = false;
        }

        public void Stop()
        {
            _isPlaying = false;
            if (_runtime.Document != null)
            {
                _runtime.LoadDocument(_runtime.Document);
            }
        }

        public void Destroy()
        {
            _isPlaying = false;
            _runtime.StateChanged -= OnRuntimeStateChanged;
            _backend.Destroy();
        }

        public void Advance(double dt)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0.0) dt = 0.0;

            if (_isPlaying)
            {
                _runtime.Update(dt, _currentPose);
                _backend.BeginFrame();
                _backend.RenderPose(_currentPose);
                _backend.EndFrame();
            }
        }

        public void SetInput(string name, double value)
        {
            if (_runtime.StateMachine != null)
            {
                int idx = _runtime.StateMachine.Definition.GetInputIndex(name);
                if (idx >= 0 && idx < _runtime.StateMachine.Definition.Inputs.Count && _runtime.StateMachine.Definition.Inputs[idx] != null)
                {
                    var input = _runtime.StateMachine.Definition.Inputs[idx];
                    if (input.Kind == InputKind.Bool)
                    {
                        _runtime.StateMachine.SetBool(idx, value > 0.5);
                    }
                    else if (input.Kind == InputKind.Trigger)
                    {
                        if (value > 0.5) _runtime.StateMachine.FireTrigger(idx);
                    }
                    else
                    {
                        _runtime.StateMachine.SetNumber(idx, value);
                    }
                }
                else
                {
                    _runtime.StateMachine.SetNumber(name, value);
                }
            }
        }

        public void SetBool(string name, bool value)
        {
            if (_runtime.StateMachine != null)
            {
                _runtime.StateMachine.SetBool(name, value);
            }
        }

        public void FireTrigger(string name)
        {
            if (_runtime.StateMachine != null)
            {
                _runtime.StateMachine.FireTrigger(name);
            }
        }

        public void DispatchPointerEvent(string layerId, PointerEventKind kind)
        {
            if (_runtime.StateMachine != null && !string.IsNullOrEmpty(layerId))
            {
                uint numericId = PropertyBinder.GetNumericNodeId(layerId);
                _runtime.StateMachine.OnPointerEvent(numericId, kind);
            }
        }
    }
}
