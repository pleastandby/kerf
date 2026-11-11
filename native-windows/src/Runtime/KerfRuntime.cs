using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kerf.Engine.Animation;
using Kerf.IO;
using Kerf.Model;

namespace Kerf.Runtime
{
    public class KerfRuntime
    {
        private KerfDocument _document;
        private StateMachineRuntime _stateMachineRuntime;
        private Clip _activeClip;
        private ClipInstance _clipInstance;
        private double _currentTime;
        private int _lastReportedState = -1;

        public event EventHandler<string> StateChanged;

        public KerfDocument Document
        {
            get { return _document; }
        }

        public StateMachineRuntime StateMachine
        {
            get { return _stateMachineRuntime; }
        }

        public double CurrentTime
        {
            get { return _currentTime; }
        }

        public Clip ActiveClip
        {
            get { return _activeClip; }
        }

        public KerfRuntime()
        {
            _currentTime = 0.0;
        }

        public void LoadPackage(string kfxFilePath)
        {
            if (string.IsNullOrEmpty(kfxFilePath))
            {
                throw new ArgumentNullException("kfxFilePath");
            }
            if (!File.Exists(kfxFilePath))
            {
                throw new FileNotFoundException("KFX package not found: " + kfxFilePath);
            }

            var doc = KfxPackage.ImportPackage(kfxFilePath);
            LoadDocument(doc);
        }

        public void LoadDocument(KerfDocument doc)
        {
            if (doc == null)
            {
                throw new ArgumentNullException("doc");
            }

            _document = doc;
            _currentTime = 0.0;
            _lastReportedState = -1;

            if (_document.Animation != null)
            {
                var clips = _document.Animation.Clips != null
                    ? _document.Animation.Clips
                    : new List<Clip>();

                if (_document.Animation.StateMachine != null &&
                    _document.Animation.StateMachine.Layers != null &&
                    _document.Animation.StateMachine.Layers.Count > 0)
                {
                    _stateMachineRuntime = new StateMachineRuntime(_document.Animation.StateMachine, clips);
                }
                else
                {
                    _stateMachineRuntime = null;
                }

                if (clips.Count > 0)
                {
                    _activeClip = clips.FirstOrDefault(c => c.Id == _document.Animation.ActiveClipId);
                    if (_activeClip == null) _activeClip = clips[0];
                    _clipInstance = new ClipInstance(0);
                }
                else
                {
                    _activeClip = null;
                    _clipInstance = null;
                }
            }
            else
            {
                _stateMachineRuntime = null;
                _activeClip = null;
                _clipInstance = null;
            }
        }

        public void SetActiveClip(uint clipId)
        {
            if (_document != null && _document.Animation != null && _document.Animation.Clips != null)
            {
                var clip = _document.Animation.Clips.FirstOrDefault(c => c.Id == clipId);
                if (clip != null)
                {
                    _activeClip = clip;
                    _clipInstance = new ClipInstance(0);
                }
            }
        }

        public void SetActiveClip(string clipName)
        {
            if (_document != null && _document.Animation != null && _document.Animation.Clips != null)
            {
                var clip = _document.Animation.Clips.FirstOrDefault(
                    c => string.Equals(c.Name, clipName, StringComparison.OrdinalIgnoreCase));
                if (clip != null)
                {
                    _activeClip = clip;
                    _clipInstance = new ClipInstance(0);
                }
            }
        }

        public void Update(double dt, RenderPose outPose)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0.0) dt = 0.0;
            _currentTime += dt;

            if (outPose == null) return;
            outPose.Clear();
            outPose.Timestamp = _currentTime;

            var poseMap = new Dictionary<uint, Dictionary<ushort, double>>();

            // 1. Evaluate StateMachine if configured
            if (_stateMachineRuntime != null &&
                _stateMachineRuntime.Definition != null &&
                _stateMachineRuntime.Definition.Layers != null &&
                _stateMachineRuntime.Definition.Layers.Count > 0)
            {
                _stateMachineRuntime.Update(dt, poseMap);

                // Check for state changes to fire StateChanged event
                if (_stateMachineRuntime.LayerRuntimes != null && _stateMachineRuntime.LayerRuntimes.Count > 0)
                {
                    int curState = _stateMachineRuntime.LayerRuntimes[0].CurrentState;
                    if (curState != _lastReportedState && curState >= 0)
                    {
                        _lastReportedState = curState;
                        var layerDef = _stateMachineRuntime.Definition.Layers[0];
                        if (layerDef.States != null && curState < layerDef.States.Count && layerDef.States[curState] != null)
                        {
                            string stateName = layerDef.States[curState].Name;
                            if (StateChanged != null)
                            {
                                StateChanged(this, stateName);
                            }
                        }
                    }
                }
            }
            // 2. Otherwise evaluate active Clip if present
            else if (_activeClip != null && _clipInstance != null)
            {
                _clipInstance.Advance(dt, _activeClip.Duration, _activeClip.LoopMode);
                _activeClip.SampleToPose(_clipInstance.Time, poseMap);
            }

            // 3. Apply evaluated poseMap to layers
            if (_document != null && poseMap.Count > 0)
            {
                var page = _document.GetActivePage();
                if (page != null && page.Layers != null)
                {
                    PropertyBinder.ApplyPoseToLayers(page.GetAllLayersFlat(), poseMap);
                }
            }

            // 4. Hierarchical matrix composition
            if (_document != null)
            {
                var page = _document.GetActivePage();
                if (page != null && page.Layers != null)
                {
                    for (int i = 0; i < page.Layers.Count; i++)
                    {
                        ComputeWorldMatricesRecursive(page.Layers[i], Matrix3x2.Identity, 1.0, outPose);
                    }
                }
            }
        }

        private void ComputeWorldMatricesRecursive(
            LayerNode layer,
            Matrix3x2 parentWorldMatrix,
            double parentOpacity,
            RenderPose outPose)
        {
            if (layer == null) return;

            Matrix3x2 localMatrix = CalculateLocalMatrix(layer);
            Matrix3x2 worldMatrix = Matrix3x2.Multiply(localMatrix, parentWorldMatrix);
            double worldOpacity = parentOpacity * Math.Max(0.0, Math.Min(1.0, layer.Opacity));

            string layerId = !string.IsNullOrEmpty(layer.Id) ? layer.Id : Guid.NewGuid().ToString("N");
            outPose.LayerWorldMatrices[layerId] = worldMatrix;
            outPose.LayerOpacities[layerId] = worldOpacity;
            if (layer.RenderTransform != null)
            {
                outPose.LocalTransforms[layerId] = layer.RenderTransform.Clone();
            }

            if (layer.Children != null && layer.Children.Count > 0)
            {
                for (int i = 0; i < layer.Children.Count; i++)
                {
                    ComputeWorldMatricesRecursive(layer.Children[i], worldMatrix, worldOpacity, outPose);
                }
            }
        }

        public static Matrix3x2 CalculateLocalMatrix(LayerNode layer)
        {
            if (layer == null) return Matrix3x2.Identity;

            double lx = (double.IsNaN(layer.X) || double.IsInfinity(layer.X)) ? 0.0 : layer.X;
            double ly = (double.IsNaN(layer.Y) || double.IsInfinity(layer.Y)) ? 0.0 : layer.Y;
            double lw = Math.Max(1.0, (double.IsNaN(layer.Width) || double.IsInfinity(layer.Width)) ? 1.0 : layer.Width);
            double lh = Math.Max(1.0, (double.IsNaN(layer.Height) || double.IsInfinity(layer.Height)) ? 1.0 : layer.Height);

            var mat = Matrix3x2.CreateTranslation(lx, ly);

            if (Math.Abs(layer.Rotation) > 1e-6)
            {
                mat = Matrix3x2.Multiply(mat, Matrix3x2.CreateRotationDegrees(layer.Rotation, lx + lw * 0.5, ly + lh * 0.5));
            }
            if (Math.Abs(layer.ScaleX - 1.0) > 1e-6 || Math.Abs(layer.ScaleY - 1.0) > 1e-6)
            {
                mat = Matrix3x2.Multiply(mat, Matrix3x2.CreateScale(layer.ScaleX, layer.ScaleY, lx + lw * 0.5, ly + lh * 0.5));
            }

            if (layer.RenderTransform != null && !layer.RenderTransform.IsIdentity)
            {
                var rt = layer.RenderTransform;
                double ox = (double.IsNaN(rt.OriginX) || double.IsInfinity(rt.OriginX)) ? 0.5 : rt.OriginX;
                double oy = (double.IsNaN(rt.OriginY) || double.IsInfinity(rt.OriginY)) ? 0.5 : rt.OriginY;
                double pivotX = lx + ox * lw;
                double pivotY = ly + oy * lh;

                var rtMat = Matrix3x2.CreateTranslation(-pivotX, -pivotY);

                if (Math.Abs(rt.SkewX) > 1e-6 || Math.Abs(rt.SkewY) > 1e-6)
                {
                    double tanX = Math.Tan(rt.SkewX * Math.PI / 180.0);
                    double tanY = Math.Tan(rt.SkewY * Math.PI / 180.0);
                    rtMat = Matrix3x2.Multiply(rtMat, new Matrix3x2(1.0, tanY, tanX, 1.0, 0.0, 0.0));
                }

                double sx = (double.IsNaN(rt.ScaleX) || double.IsInfinity(rt.ScaleX)) ? 1.0 : rt.ScaleX;
                double sy = (double.IsNaN(rt.ScaleY) || double.IsInfinity(rt.ScaleY)) ? 1.0 : rt.ScaleY;
                if (Math.Abs(sx - 1.0) > 1e-6 || Math.Abs(sy - 1.0) > 1e-6)
                {
                    rtMat = Matrix3x2.Multiply(rtMat, Matrix3x2.CreateScale(sx, sy));
                }

                if (Math.Abs(rt.Rotation) > 1e-6)
                {
                    rtMat = Matrix3x2.Multiply(rtMat, Matrix3x2.CreateRotationDegrees(rt.Rotation));
                }

                double tx = (double.IsNaN(rt.TranslationX) || double.IsInfinity(rt.TranslationX)) ? 0.0 : rt.TranslationX;
                double ty = (double.IsNaN(rt.TranslationY) || double.IsInfinity(rt.TranslationY)) ? 0.0 : rt.TranslationY;
                rtMat = Matrix3x2.Multiply(rtMat, Matrix3x2.CreateTranslation(pivotX + tx, pivotY + ty));

                mat = Matrix3x2.Multiply(mat, rtMat);
            }

            return mat;
        }
    }
}
