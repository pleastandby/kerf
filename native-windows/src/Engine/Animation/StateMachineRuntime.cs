using System;
using System.Collections.Generic;
using System.Linq;

namespace Kerf.Engine.Animation
{
    public class ActiveTransitionState
    {
        public StateTransition Definition { get; set; }
        public int ToState { get; set; }
        public ClipInstance ToTime { get; set; }
        public double Elapsed { get; set; }
        public double Duration { get; set; }
        public Dictionary<uint, Dictionary<ushort, double>> SnapshotPose { get; set; }

        public ActiveTransitionState(StateTransition def, int toState, double duration)
        {
            Definition = def;
            ToState = toState;
            ToTime = new ClipInstance(toState);
            Elapsed = 0.0;
            Duration = Math.Max(0.0, duration);
            SnapshotPose = null;
        }
    }

    public class LayerRuntimeState
    {
        public int CurrentState { get; set; }
        public ClipInstance CurTime { get; set; }
        public ActiveTransitionState ActiveTransition { get; set; }
        public bool IsFinished { get; set; }

        public LayerRuntimeState(int entryState)
        {
            CurrentState = entryState;
            CurTime = new ClipInstance(entryState);
            ActiveTransition = null;
            IsFinished = false;
        }
    }

    public class StateMachineRuntime
    {
        public StateMachineDefinition Definition { get; private set; }
        public StateMachineViewModel ViewModel { get; private set; }
        public List<Clip> Clips { get; private set; }
        public List<LayerRuntimeState> LayerRuntimes { get; private set; }
        public List<string> ReportedEvents { get; private set; }
        public Dictionary<uint, Dictionary<ushort, double>> RestPose { get; set; }

        public bool HasActiveTransitions()
        {
            if (LayerRuntimes == null) return false;
            for (int i = 0; i < LayerRuntimes.Count; i++)
            {
                if (LayerRuntimes[i] != null && LayerRuntimes[i].ActiveTransition != null)
                    return true;
            }
            return false;
        }

        public StateMachineRuntime(StateMachineDefinition def, List<Clip> clips)
        {
            Definition = def ?? new StateMachineDefinition("Empty");
            if (Definition.Inputs == null) Definition.Inputs = new List<MachineInput>();
            if (Definition.ViewModelProperties == null) Definition.ViewModelProperties = new List<ViewModelPropertyDefinition>();
            if (Definition.Layers == null) Definition.Layers = new List<MachineLayer>();
            if (Definition.Listeners == null) Definition.Listeners = new List<NodeListener>();
            ViewModel = new StateMachineViewModel(Definition.ViewModelProperties);
            Definition.Listeners.RemoveAll(listener => listener == null);
            Clips = clips ?? new List<Clip>();
            LayerRuntimes = new List<LayerRuntimeState>();
            ReportedEvents = new List<string>();

            for (int i = 0; i < Definition.Inputs.Count; i++)
            {
                if (Definition.Inputs[i] == null) continue;
                Definition.Inputs[i].Id = i;
                if (string.IsNullOrEmpty(Definition.Inputs[i].InputId))
                {
                    Definition.Inputs[i].InputId = "sm_input_" + Guid.NewGuid().ToString("N");
                }
            }

            for (int i = 0; i < Definition.Layers.Count; i++)
            {
                MachineLayer layer = Definition.Layers[i];
                if (layer == null)
                {
                    layer = new MachineLayer("Layer");
                    Definition.Layers[i] = layer;
                }
                if (layer.States == null) layer.States = new List<MachineState>();
                if (layer.Transitions == null) layer.Transitions = new List<StateTransition>();
                for (int s = 0; s < layer.States.Count; s++)
                {
                    if (layer.States[s] == null) layer.States[s] = new MachineState(s, "State", StateKind.Animation);
                    layer.States[s].Id = s;
                    if (string.IsNullOrEmpty(layer.States[s].NodeId))
                    {
                        layer.States[s].NodeId = "sm_node_" + Guid.NewGuid().ToString("N");
                    }
                    if (layer.States[s].Blend1DItems == null) layer.States[s].Blend1DItems = new List<Blend1DItem>();
                    if (layer.States[s].BlendDirectItems == null) layer.States[s].BlendDirectItems = new List<BlendDirectItem>();
                    layer.States[s].Blend1DItems = layer.States[s].Blend1DItems.OrderBy(item => item == null ? double.MaxValue : item.Threshold).ToList();
                    layer.States[s].Blend1DItems.RemoveAll(item => item == null);
                    layer.States[s].BlendDirectItems.RemoveAll(item => item == null);
                }
                for (int t = 0; t < layer.Transitions.Count; t++)
                {
                    if (layer.Transitions[t] == null) layer.Transitions[t] = new StateTransition(0, 0);
                    StateTransition trans = layer.Transitions[t];
                    if (string.IsNullOrEmpty(trans.TransitionId))
                    {
                        trans.TransitionId = "sm_trans_" + Guid.NewGuid().ToString("N");
                    }
                    if (trans.FromState < 0 && !string.IsNullOrEmpty(trans.FromStateId))
                    {
                        trans.FromState = layer.GetStateIndex(trans.FromStateId);
                    }
                    else if (string.IsNullOrEmpty(trans.FromStateId) && trans.FromState >= 0 && trans.FromState < layer.States.Count)
                    {
                        trans.FromStateId = layer.States[trans.FromState].NodeId;
                    }

                    if (trans.ToState < 0 && !string.IsNullOrEmpty(trans.ToStateId))
                    {
                        trans.ToState = layer.GetStateIndex(trans.ToStateId);
                    }
                    else if (string.IsNullOrEmpty(trans.ToStateId) && trans.ToState >= 0 && trans.ToState < layer.States.Count)
                    {
                        trans.ToStateId = layer.States[trans.ToState].NodeId;
                    }

                    if (trans.ConditionGroups == null) trans.ConditionGroups = new List<ConditionGroup>();
                    for (int g = 0; g < trans.ConditionGroups.Count; g++)
                    {
                        if (trans.ConditionGroups[g] == null) trans.ConditionGroups[g] = new ConditionGroup();
                        if (string.IsNullOrEmpty(trans.ConditionGroups[g].Id))
                        {
                            trans.ConditionGroups[g].Id = "sm_cg_" + Guid.NewGuid().ToString("N");
                        }
                        if (trans.ConditionGroups[g].Conditions == null) trans.ConditionGroups[g].Conditions = new List<TransitionCondition>();
                        trans.ConditionGroups[g].Conditions.RemoveAll(condition => condition == null);
                    }
                }
                int entryIdx = 0;
                for (int s = 0; s < layer.States.Count; s++)
                {
                    if (layer.States[s].Kind == StateKind.Entry)
                    {
                        entryIdx = s;
                        break;
                    }
                }
                LayerRuntimes.Add(new LayerRuntimeState(entryIdx));
            }
            Reset();
        }

        public void Reset()
        {
            if (ViewModel != null) ViewModel.Reset();
            for (int i = 0; i < Definition.Inputs.Count; i++)
            {
                MachineInput input = Definition.Inputs[i];
                if (input == null) continue;
                input.Value = input.InitialValue;
                input.Fired = false;
            }
            for (int i = 0; i < Definition.Layers.Count && i < LayerRuntimes.Count; i++)
            {
                MachineLayer layer = Definition.Layers[i];
                int entryIdx = 0;
                for (int s = 0; s < layer.States.Count; s++)
                {
                    if (layer.States[s].Kind == StateKind.Entry)
                    {
                        entryIdx = s;
                        break;
                    }
                }
                LayerRuntimes[i].CurrentState = entryIdx;
                LayerRuntimes[i].CurTime = new ClipInstance(entryIdx);
                LayerRuntimes[i].ActiveTransition = null;
                LayerRuntimes[i].IsFinished = false;
            }
        }

        public void RebuildViewModel()
        {
            ViewModel = new StateMachineViewModel(Definition != null ? Definition.ViewModelProperties : null);
        }

        public void SetBool(int inputIdx, bool val)
        {
            if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count && Definition.Inputs[inputIdx] != null && Definition.Inputs[inputIdx].Kind == InputKind.Bool)
            {
                Definition.Inputs[inputIdx].Value = val ? 1.0 : 0.0;
            }
        }

        public void SetBool(string inputId, bool val)
        {
            int idx = Definition.GetInputIndex(inputId);
            if (idx >= 0) SetBool(idx, val);
        }

        public void SetNumber(int inputIdx, double val)
        {
            if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count && Definition.Inputs[inputIdx] != null && Definition.Inputs[inputIdx].Kind == InputKind.Number && !double.IsNaN(val) && !double.IsInfinity(val))
            {
                Definition.Inputs[inputIdx].Value = val;
            }
        }

        public void SetNumber(string inputId, double val)
        {
            int idx = Definition.GetInputIndex(inputId);
            if (idx >= 0) SetNumber(idx, val);
        }

        public void FireTrigger(int inputIdx)
        {
            if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count && Definition.Inputs[inputIdx] != null && Definition.Inputs[inputIdx].Kind == InputKind.Trigger)
            {
                Definition.Inputs[inputIdx].Fired = true;
            }
        }

        public void FireTrigger(string inputId)
        {
            int idx = Definition.GetInputIndex(inputId);
            if (idx >= 0) FireTrigger(idx);
        }

        public double GetInputValue(int inputIdx)
        {
            if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count && Definition.Inputs[inputIdx] != null)
            {
                return Definition.Inputs[inputIdx].Value;
            }
            return 0.0;
        }

        public double GetInputValue(string inputId)
        {
            int idx = Definition.GetInputIndex(inputId);
            return idx >= 0 ? GetInputValue(idx) : 0.0;
        }

        public bool SetViewModelBool(string name, bool value)
        {
            return ViewModel != null && ViewModel.SetBool(name, value);
        }

        public bool SetViewModelNumber(string name, double value)
        {
            return ViewModel != null && ViewModel.SetNumber(name, value);
        }

        public bool SetViewModelText(string name, string value)
        {
            return ViewModel != null && ViewModel.SetText(name, value);
        }

        public bool FireViewModelTrigger(string name)
        {
            return ViewModel != null && ViewModel.FireTrigger(name);
        }

        public double GetViewModelNumber(string name)
        {
            double value;
            bool fired;
            return ViewModel != null && ViewModel.TryGetNumber(name, out value, out fired) ? value : 0.0;
        }

        public void OnPointerEvent(uint targetNodeId, PointerEventKind kind)
        {
            if (Definition.Listeners == null) return;
            List<ListenerAction> actionsToRun = new List<ListenerAction>();
            for (int i = 0; i < Definition.Listeners.Count; i++)
            {
                NodeListener listener = Definition.Listeners[i];
                if (listener != null && (listener.TargetNodeId == targetNodeId || listener.TargetNodeId == 0) && listener.Kind == kind && listener.Actions != null)
                {
                    actionsToRun.AddRange(listener.Actions);
                }
            }

            for (int a = 0; a < actionsToRun.Count; a++)
            {
                ListenerAction action = actionsToRun[a];
                if (action == null) continue;
                switch (action.Kind)
                {
                    case ActionKind.SetBool:
                        if (action.InputIndex >= 0 && action.InputIndex < Definition.Inputs.Count)
                            SetBool(action.InputIndex, action.Parameter > 0.5);
                        else if (!string.IsNullOrEmpty(action.PropertyName))
                            SetBool(action.PropertyName, action.Parameter > 0.5);
                        break;
                    case ActionKind.SetNumber:
                        if (action.InputIndex >= 0 && action.InputIndex < Definition.Inputs.Count)
                            SetNumber(action.InputIndex, action.Parameter);
                        else if (!string.IsNullOrEmpty(action.PropertyName))
                            SetNumber(action.PropertyName, action.Parameter);
                        break;
                    case ActionKind.Fire:
                        if (action.InputIndex >= 0 && action.InputIndex < Definition.Inputs.Count)
                            FireTrigger(action.InputIndex);
                        else if (!string.IsNullOrEmpty(action.PropertyName))
                            FireTrigger(action.PropertyName);
                        break;
                    case ActionKind.SetViewModelBool:
                        SetViewModelBool(action.PropertyName, action.Parameter > 0.5);
                        break;
                    case ActionKind.SetViewModelNumber:
                        SetViewModelNumber(action.PropertyName, action.Parameter);
                        break;
                    case ActionKind.FireViewModelTrigger:
                        FireViewModelTrigger(action.PropertyName);
                        break;
                    case ActionKind.SetViewModelText:
                        SetViewModelText(action.PropertyName, action.TextValue);
                        break;
                }
            }
        }

        private double GetNormalizedProgress(MachineLayer layer, int currentStateIdx, double curTime)
        {
            if (currentStateIdx < 0 || currentStateIdx >= layer.States.Count) return 1.0;
            MachineState state = layer.States[currentStateIdx];
            if (state.Kind == StateKind.Animation && state.ClipIndex >= 0 && state.ClipIndex < Clips.Count)
            {
                double d = Clips[state.ClipIndex].Duration;
                return (d > 0.0) ? Math.Max(0.0, curTime / d) : 1.0;
            }
            return 1.0;
        }

        public bool CanTransition(StateTransition trans, double progress)
        {
            if (trans == null) return false;

            // Check exit time
            if (trans.ExitTime.HasValue && progress < trans.ExitTime.Value)
            {
                return false;
            }

            // Evaluate condition groups (OR of ANDs)
            return trans.EvaluateConditions(Definition.Inputs, ViewModel, Definition);
        }

        private StateTransition FindValidTransition(MachineLayer layer, int currentState, double curTime, int activeTransitionToState)
        {
            int anyStateIdx = -1;
            for (int s = 0; s < layer.States.Count; s++)
            {
                if (layer.States[s].Kind == StateKind.Any)
                {
                    anyStateIdx = s;
                    break;
                }
            }

            // Ordered candidate search: transitions from current state first, then Any state
            List<StateTransition> candidates = new List<StateTransition>();
            for (int t = 0; t < layer.Transitions.Count; t++)
            {
                if (layer.Transitions[t].FromState == currentState)
                {
                    candidates.Add(layer.Transitions[t]);
                }
            }
            if (anyStateIdx >= 0)
            {
                for (int t = 0; t < layer.Transitions.Count; t++)
                {
                    if (layer.Transitions[t].FromState == anyStateIdx)
                    {
                        candidates.Add(layer.Transitions[t]);
                    }
                }
            }

            double progress = GetNormalizedProgress(layer, currentState, curTime);

            for (int i = 0; i < candidates.Count; i++)
            {
                StateTransition trans = candidates[i];
                if (trans.ToState < 0 || trans.ToState >= layer.States.Count) continue;

                // Avoid self-restart loop if from Any to current
                if (anyStateIdx >= 0 && trans.FromState == anyStateIdx && trans.ToState == currentState)
                {
                    continue;
                }

                // If currently transitioning, do not interrupt with the same target state
                if (activeTransitionToState >= 0 && trans.ToState == activeTransitionToState)
                {
                    continue;
                }

                // Check transition feasibility via CanTransition
                if (CanTransition(trans, progress))
                {
                    return trans;
                }
            }

            return null;
        }

        public bool Update(double dt, Dictionary<uint, Dictionary<ushort, double>> outPose)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0.0) dt = 0.0;
            bool needsRedraw = false;

            for (int i = 0; i < Definition.Layers.Count; i++)
            {
                MachineLayer layerDef = Definition.Layers[i];
                LayerRuntimeState layerRt = LayerRuntimes[i];

                if (layerRt.IsFinished) continue;

                double playbackSpeed = 1.0;
                if (!string.IsNullOrEmpty(Definition.PlaybackSpeedPropertyName))
                    playbackSpeed = Math.Max(0.0, Math.Min(8.0, GetViewModelNumber(Definition.PlaybackSpeedPropertyName)));
                int speedInput = Definition.PlaybackSpeedInputIndex;
                if (string.IsNullOrEmpty(Definition.PlaybackSpeedPropertyName) && speedInput >= 0 && speedInput < Definition.Inputs.Count && Definition.Inputs[speedInput] != null && Definition.Inputs[speedInput].Kind == InputKind.Number)
                    playbackSpeed = Math.Max(0.0, Math.Min(8.0, Definition.Inputs[speedInput].Value));
                if (layerRt.CurTime != null) layerRt.CurTime.Speed = playbackSpeed;

                // 1. Advance current state clip
                bool pauseSource = layerRt.ActiveTransition != null && layerRt.ActiveTransition.Definition != null && layerRt.ActiveTransition.Definition.PauseSourceWhenExiting;
                if (!pauseSource && layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
                {
                    MachineState curState = layerDef.States[layerRt.CurrentState];
                    if (curState.Kind == StateKind.Animation && curState.ClipIndex >= 0 && curState.ClipIndex < Clips.Count)
                    {
                        Clip clip = Clips[curState.ClipIndex];
                        double stateSpeed = (curState.Speed > 0.0 || curState.Speed < 0.0) ? curState.Speed : 1.0;
                        layerRt.CurTime.Advance(dt * stateSpeed, clip.Duration, curState.Loop ? clip.LoopMode : LoopMode.OneShot);
                        needsRedraw = true;
                    }
                }

                // 2. Advance transition clip if active
                if (layerRt.ActiveTransition != null)
                {
                    layerRt.ActiveTransition.ToTime.Speed = playbackSpeed;
                    layerRt.ActiveTransition.Elapsed += dt;
                    needsRedraw = true;

                    if (layerRt.ActiveTransition.ToState >= 0 && layerRt.ActiveTransition.ToState < layerDef.States.Count)
                    {
                        MachineState toState = layerDef.States[layerRt.ActiveTransition.ToState];
                        if (toState.Kind == StateKind.Animation && toState.ClipIndex >= 0 && toState.ClipIndex < Clips.Count)
                        {
                            Clip clip = Clips[toState.ClipIndex];
                            double toStateSpeed = (toState.Speed > 0.0 || toState.Speed < 0.0) ? toState.Speed : 1.0;
                            layerRt.ActiveTransition.ToTime.Advance(dt * toStateSpeed, clip.Duration, toState.Loop ? clip.LoopMode : LoopMode.OneShot);
                        }
                    }
                }

                // 3. Resolve transitions with zero-duration chain resolution & cycle guard (max 100 steps)
                const int MAX_STEPS = 100;
                for (int step = 0; step < MAX_STEPS; step++)
                {
                    if (layerRt.ActiveTransition != null && !layerRt.ActiveTransition.Definition.Interrupt)
                    {
                        // Current transition cannot be interrupted mid-blend
                        break;
                    }

                    int activeToState = (layerRt.ActiveTransition != null) ? layerRt.ActiveTransition.ToState : -1;
                    StateTransition nextTrans = FindValidTransition(layerDef, layerRt.CurrentState, layerRt.CurTime.Time, activeToState);
                    if (nextTrans != null)
                    {
                        // Do not interrupt if already blending to the exact same target state
                        if (layerRt.ActiveTransition != null && layerRt.ActiveTransition.ToState == nextTrans.ToState)
                        {
                            break;
                        }

                        // Snapshot current pose if interrupting an active transition to prevent visual pops
                        Dictionary<uint, Dictionary<ushort, double>> snapshot = null;
                        if (layerRt.ActiveTransition != null)
                        {
                            var fromPose = new Dictionary<uint, Dictionary<ushort, double>>();
                            var toPose = new Dictionary<uint, Dictionary<ushort, double>>();
                            if (layerRt.ActiveTransition.SnapshotPose != null)
                            {
                                fromPose = layerRt.ActiveTransition.SnapshotPose;
                            }
                            else if (layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
                            {
                                SampleState(layerDef.States[layerRt.CurrentState], layerRt.CurTime.Time, fromPose);
                            }

                            if (layerRt.ActiveTransition.ToState >= 0 && layerRt.ActiveTransition.ToState < layerDef.States.Count)
                            {
                                SampleState(layerDef.States[layerRt.ActiveTransition.ToState], layerRt.ActiveTransition.ToTime.Time, toPose);
                            }
                            double progress = Math.Max(0.0, Math.Min(1.0, layerRt.ActiveTransition.Elapsed / Math.Max(0.001, layerRt.ActiveTransition.Duration)));
                            double mix = layerRt.ActiveTransition.Definition.EvaluateInterpolation(progress);
                            snapshot = new Dictionary<uint, Dictionary<ushort, double>>();
                            BlendPoses(fromPose, toPose, mix, snapshot, RestPose);
                        }

                        int toState = nextTrans.ToState;
                        double duration = nextTrans.Duration;
                        layerRt.ActiveTransition = new ActiveTransitionState(nextTrans, toState, duration);
                        layerRt.ActiveTransition.SnapshotPose = snapshot;
                        needsRedraw = true;

                        if (duration <= 0.0)
                        {
                            // Instant zero-duration transition: complete immediately and allow chain resolution
                            layerRt.CurrentState = toState;
                            layerRt.CurTime = new ClipInstance(toState);
                            layerRt.ActiveTransition = null;

                            if (layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
                            {
                                if (layerDef.States[layerRt.CurrentState].Kind == StateKind.Exit)
                                {
                                    layerRt.IsFinished = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                // 4. Sample poses and blend
                var layerPose = new Dictionary<uint, Dictionary<ushort, double>>();
                if (layerRt.ActiveTransition != null)
                {
                    var fromPose = new Dictionary<uint, Dictionary<ushort, double>>();
                    if (layerRt.ActiveTransition.SnapshotPose != null)
                    {
                        fromPose = layerRt.ActiveTransition.SnapshotPose;
                    }
                    else if (layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
                    {
                        SampleState(layerDef.States[layerRt.CurrentState], layerRt.CurTime.Time, fromPose);
                    }

                    var toPose = new Dictionary<uint, Dictionary<ushort, double>>();
                    if (layerRt.ActiveTransition.ToState >= 0 && layerRt.ActiveTransition.ToState < layerDef.States.Count)
                    {
                        SampleState(layerDef.States[layerRt.ActiveTransition.ToState], layerRt.ActiveTransition.ToTime.Time, toPose);
                    }

                    double u = Math.Max(0.0, Math.Min(1.0, layerRt.ActiveTransition.Elapsed / Math.Max(0.001, layerRt.ActiveTransition.Duration)));
                    double mix = layerRt.ActiveTransition.Definition.EvaluateInterpolation(u);
                    BlendPoses(fromPose, toPose, mix, layerPose, RestPose);

                    // Commit to target state when transition duration has completed
                    if (layerRt.ActiveTransition.Elapsed >= layerRt.ActiveTransition.Duration)
                    {
                        int toState = layerRt.ActiveTransition.ToState;
                        ClipInstance toTime = layerRt.ActiveTransition.ToTime;
                        layerRt.CurrentState = toState;
                        layerRt.CurTime = toTime;
                        layerRt.ActiveTransition = null;

                        if (layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
                        {
                            if (layerDef.States[layerRt.CurrentState].Kind == StateKind.Exit)
                            {
                                layerRt.IsFinished = true;
                            }
                        }
                    }
                }
                else if (layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
                {
                    SampleState(layerDef.States[layerRt.CurrentState], layerRt.CurTime.Time, layerPose);
                }

                // Merge into final output pose (later layers override earlier layers)
                if (outPose != null)
                {
                    foreach (var nodeKvp in layerPose)
                    {
                        Dictionary<ushort, double> targetProps;
                        if (!outPose.TryGetValue(nodeKvp.Key, out targetProps))
                        {
                            targetProps = new Dictionary<ushort, double>();
                            outPose[nodeKvp.Key] = targetProps;
                        }
                        foreach (var propKvp in nodeKvp.Value)
                        {
                            targetProps[propKvp.Key] = propKvp.Value;
                        }
                    }
                }
            }

            // 6. Clear triggers: triggers last exactly one update frame!
            for (int i = 0; i < Definition.Inputs.Count; i++)
            {
                if (Definition.Inputs[i] != null && Definition.Inputs[i].Kind == InputKind.Trigger)
                {
                    Definition.Inputs[i].Fired = false;
                }
            }
            if (ViewModel != null) ViewModel.ClearTriggers();

            return needsRedraw;
        }

        private void SampleState(MachineState state, double time, Dictionary<uint, Dictionary<ushort, double>> outPose)
        {
            if (state == null || outPose == null) return;

            switch (state.Kind)
            {
                case StateKind.Animation:
                    if (state.ClipIndex >= 0 && state.ClipIndex < Clips.Count)
                    {
                        Clips[state.ClipIndex].SampleToPose(time, outPose);
                    }
                    break;

                case StateKind.Blend1D:
                    if (state.Blend1DItems.Count == 0) return;
                    double val = (state.BlendInputIndex >= 0 && state.BlendInputIndex < Definition.Inputs.Count)
                        ? Definition.Inputs[state.BlendInputIndex].Value
                        : 0.0;

                    if (val <= state.Blend1DItems[0].Threshold)
                    {
                        int cIdx = state.Blend1DItems[0].ClipIndex;
                        if (cIdx >= 0 && cIdx < Clips.Count) Clips[cIdx].SampleToPose(time, outPose);
                        return;
                    }
                    if (val >= state.Blend1DItems[state.Blend1DItems.Count - 1].Threshold)
                    {
                        int cIdx = state.Blend1DItems[state.Blend1DItems.Count - 1].ClipIndex;
                        if (cIdx >= 0 && cIdx < Clips.Count) Clips[cIdx].SampleToPose(time, outPose);
                        return;
                    }

                    for (int i = 0; i < state.Blend1DItems.Count - 1; i++)
                    {
                        Blend1DItem a = state.Blend1DItems[i];
                        Blend1DItem b = state.Blend1DItems[i + 1];
                        if (val >= a.Threshold && val <= b.Threshold)
                        {
                            double span = b.Threshold - a.Threshold;
                            double w = (span > 1e-5) ? (val - a.Threshold) / span : 0.0;
                            var poseA = new Dictionary<uint, Dictionary<ushort, double>>();
                            var poseB = new Dictionary<uint, Dictionary<ushort, double>>();
                            if (a.ClipIndex >= 0 && a.ClipIndex < Clips.Count) Clips[a.ClipIndex].SampleToPose(time, poseA);
                            if (b.ClipIndex >= 0 && b.ClipIndex < Clips.Count) Clips[b.ClipIndex].SampleToPose(time, poseB);
                            BlendPoses(poseA, poseB, w, outPose);
                            return;
                        }
                    }
                    break;

                case StateKind.BlendDirect:
                    for (int i = 0; i < state.BlendDirectItems.Count; i++)
                    {
                        BlendDirectItem item = state.BlendDirectItems[i];
                        double w = (item.WeightInputIndex >= 0 && item.WeightInputIndex < Definition.Inputs.Count)
                            ? Math.Max(0.0, Math.Min(1.0, Definition.Inputs[item.WeightInputIndex].Value))
                            : 0.0;

                        if (w > 0.001 && item.ClipIndex >= 0 && item.ClipIndex < Clips.Count)
                        {
                            var temp = new Dictionary<uint, Dictionary<ushort, double>>();
                            Clips[item.ClipIndex].SampleToPose(time, temp);
                            foreach (var nKvp in temp)
                            {
                                Dictionary<ushort, double> nodeProps;
                                if (!outPose.TryGetValue(nKvp.Key, out nodeProps))
                                {
                                    nodeProps = new Dictionary<ushort, double>();
                                    outPose[nKvp.Key] = nodeProps;
                                }
                                foreach (var pKvp in nKvp.Value)
                                {
                                    double cur = nodeProps.ContainsKey(pKvp.Key) ? nodeProps[pKvp.Key] : 0.0;
                                    nodeProps[pKvp.Key] = cur + pKvp.Value * w;
                                }
                            }
                        }
                    }
                    break;
            }
        }

        private static double GetDefaultPropValue(ushort prop, uint nodeId, Dictionary<uint, Dictionary<ushort, double>> restPose)
        {
            if (restPose != null)
            {
                Dictionary<ushort, double> nodeRest;
                if (restPose.TryGetValue(nodeId, out nodeRest) && nodeRest != null)
                {
                    double rVal;
                    if (nodeRest.TryGetValue(prop, out rVal))
                    {
                        return rVal;
                    }
                }
            }

            if (prop == (ushort)PropKey.ScaleX || prop == (ushort)PropKey.ScaleY) return 1.0;
            if (prop == (ushort)PropKey.Opacity) return 1.0;
            if (prop == (ushort)PropKey.AnchorX || prop == (ushort)PropKey.AnchorY) return 0.5;
            return 0.0;
        }

        public static void BlendPoses(
            Dictionary<uint, Dictionary<ushort, double>> basePose,
            Dictionary<uint, Dictionary<ushort, double>> targetPose,
            double mix,
            Dictionary<uint, Dictionary<ushort, double>> outPose)
        {
            BlendPoses(basePose, targetPose, mix, outPose, null);
        }

        public static void BlendPoses(
            Dictionary<uint, Dictionary<ushort, double>> basePose,
            Dictionary<uint, Dictionary<ushort, double>> targetPose,
            double mix,
            Dictionary<uint, Dictionary<ushort, double>> outPose,
            Dictionary<uint, Dictionary<ushort, double>> restPose)
        {
            if (outPose == null) return;
            mix = Math.Max(0.0, Math.Min(1.0, mix));

            var allNodeIds = new HashSet<uint>();
            if (basePose != null)
            {
                foreach (var id in basePose.Keys) allNodeIds.Add(id);
            }
            if (targetPose != null)
            {
                foreach (var id in targetPose.Keys) allNodeIds.Add(id);
            }

            foreach (var nodeId in allNodeIds)
            {
                Dictionary<ushort, double> baseProps = null;
                if (basePose != null) basePose.TryGetValue(nodeId, out baseProps);

                Dictionary<ushort, double> targetProps = null;
                if (targetPose != null) targetPose.TryGetValue(nodeId, out targetProps);

                Dictionary<ushort, double> outProps;
                if (!outPose.TryGetValue(nodeId, out outProps))
                {
                    outProps = new Dictionary<ushort, double>();
                    outPose[nodeId] = outProps;
                }

                var allProps = new HashSet<ushort>();
                if (baseProps != null)
                {
                    foreach (var p in baseProps.Keys) allProps.Add(p);
                }
                if (targetProps != null)
                {
                    foreach (var p in targetProps.Keys) allProps.Add(p);
                }

                foreach (var prop in allProps)
                {
                    double defVal = GetDefaultPropValue(prop, nodeId, restPose);
                    double bVal = (baseProps != null && baseProps.ContainsKey(prop)) ? baseProps[prop] : defVal;
                    double tVal = (targetProps != null && targetProps.ContainsKey(prop)) ? targetProps[prop] : defVal;
                    outProps[prop] = bVal + (tVal - bVal) * mix;
                }
            }
        }
    }
}
