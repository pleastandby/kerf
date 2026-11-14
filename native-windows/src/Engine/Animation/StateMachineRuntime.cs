using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Kerf.Model;

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
        public Dictionary<uint, VectorGeometry> SnapshotGeometryPose { get; set; }

        public ActiveTransitionState(StateTransition def, int toState, double duration)
        {
            Definition = def;
            ToState = toState;
            ToTime = new ClipInstance(toState);
            Elapsed = 0.0;
            Duration = Math.Max(0.0, duration);
            SnapshotPose = null;
            SnapshotGeometryPose = null;
        }
    }

    public class LayerRuntimeState
    {
        public int CurrentState { get; set; }
        public int PreviousState { get; set; }
        public double TimeInCurrentState { get; set; }
        public int InputVersionAtStateEntry { get; set; }
        public ClipInstance CurTime { get; set; }
        public ActiveTransitionState ActiveTransition { get; set; }
        public bool IsFinished { get; set; }

        public LayerRuntimeState(int entryState)
        {
            CurrentState = entryState;
            PreviousState = -1;
            TimeInCurrentState = 0.0;
            InputVersionAtStateEntry = 1;
            CurTime = new ClipInstance(entryState);
            ActiveTransition = null;
            IsFinished = false;
        }
    }

    public class StateMachineRuntime
    {
        private sealed class ListenerActionInvocation
        {
            public NodeListener Listener;
            public ListenerAction Action;

            public ListenerActionInvocation(NodeListener listener, ListenerAction action)
            {
                Listener = listener;
                Action = action;
            }
        }

        public StateMachineDefinition Definition { get; private set; }
        public StateMachineViewModel ViewModel { get; private set; }
        public List<Clip> Clips { get; private set; }
        public List<LayerRuntimeState> LayerRuntimes { get; private set; }
        public List<string> ReportedEvents { get; private set; }
        public Dictionary<uint, Point> PointerAlignmentTargets { get; private set; }
        public uint FocusedNodeId { get; private set; }
        public Dictionary<uint, Dictionary<ushort, double>> RestPose { get; set; }

        public bool IsFinished
        {
            get
            {
                if (LayerRuntimes == null || LayerRuntimes.Count == 0) return false;
                for (int i = 0; i < LayerRuntimes.Count; i++)
                {
                    if (LayerRuntimes[i] != null && !LayerRuntimes[i].IsFinished) return false;
                }
                return true;
            }
        }

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
            PointerAlignmentTargets = new Dictionary<uint, Point>();

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

        private int _inputVersion = 1;

        public int InputVersion
        {
            get { return _inputVersion; }
        }

        public void NotifyInputChanged()
        {
            _inputVersion++;
        }

        public void Reset()
        {
            _inputVersion++;
            if (PointerAlignmentTargets != null) PointerAlignmentTargets.Clear();
            FocusedNodeId = 0u;
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
                LayerRuntimes[i].PreviousState = -1;
                LayerRuntimes[i].CurrentState = entryIdx;
                LayerRuntimes[i].CurTime = new ClipInstance(entryIdx);
                LayerRuntimes[i].ActiveTransition = null;
                LayerRuntimes[i].IsFinished = false;
                LayerRuntimes[i].TimeInCurrentState = 0.0;
                LayerRuntimes[i].InputVersionAtStateEntry = _inputVersion;
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
                _inputVersion++;
                Definition.Inputs[inputIdx].Value = val ? 1.0 : 0.0;
                if (ViewModel != null && !string.IsNullOrEmpty(Definition.Inputs[inputIdx].Name))
                {
                    ViewModel.SetBool(Definition.Inputs[inputIdx].Name, val);
                }
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
                _inputVersion++;
                Definition.Inputs[inputIdx].Value = val;
                if (ViewModel != null && !string.IsNullOrEmpty(Definition.Inputs[inputIdx].Name))
                {
                    ViewModel.SetNumber(Definition.Inputs[inputIdx].Name, val);
                }
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
                _inputVersion++;
                Definition.Inputs[inputIdx].Fired = true;
                if (ViewModel != null && !string.IsNullOrEmpty(Definition.Inputs[inputIdx].Name))
                {
                    ViewModel.FireTrigger(Definition.Inputs[inputIdx].Name);
                }
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
            _inputVersion++;
            return ViewModel != null && ViewModel.SetBool(name, value);
        }

        public bool SetViewModelNumber(string name, double value)
        {
            _inputVersion++;
            return ViewModel != null && ViewModel.SetNumber(name, value);
        }

        public bool SetViewModelText(string name, string value)
        {
            _inputVersion++;
            return ViewModel != null && ViewModel.SetText(name, value);
        }

        public bool FireViewModelTrigger(string name)
        {
            _inputVersion++;
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
            OnPointerEvent(new List<uint> { targetNodeId }, kind, null, null, null);
        }

        public void OnPointerEvent(IList<uint> targetNodeIds, PointerEventKind kind)
        {
            OnPointerEvent(targetNodeIds, kind, null, null, null);
        }

        public void OnPointerEvent(IList<uint> targetNodeIds, PointerEventKind kind, Point? canvasPt, Rect? targetBounds, string keyOrText)
        {
            if (Definition == null || Definition.Listeners == null) return;
            List<ListenerActionInvocation> actionsToRun = new List<ListenerActionInvocation>();
            for (int i = 0; i < Definition.Listeners.Count; i++)
            {
                NodeListener listener = Definition.Listeners[i];
                if (listener != null && listener.Enabled && listener.IsActive && listener.Kind == kind && listener.Actions != null)
                {
                    bool targetMatches = targetNodeIds != null && targetNodeIds.Contains(listener.TargetNodeId);
                    if (targetMatches)
                    {
                        for (int a = 0; a < listener.Actions.Count; a++)
                        {
                            if (listener.Actions[a] != null)
                                actionsToRun.Add(new ListenerActionInvocation(listener, listener.Actions[a]));
                        }
                    }
                }
            }

            // Pointer alignment is active only while its pointer listener is receiving movement.
            // Exiting the listener target returns the object to its authored/animated pose.
            if (kind == PointerEventKind.Exit || kind == PointerEventKind.PointerExit)
            {
                for (int i = 0; i < Definition.Listeners.Count; i++)
                {
                    NodeListener listener = Definition.Listeners[i];
                    if (listener == null || !listener.Enabled || !listener.IsActive ||
                        (listener.Kind != PointerEventKind.Move && listener.Kind != PointerEventKind.PointerDrag) ||
                        listener.Actions == null)
                        continue;
                    bool targetMatches = targetNodeIds != null && targetNodeIds.Contains(listener.TargetNodeId);
                    if (!targetMatches) continue;
                    for (int a = 0; a < listener.Actions.Count; a++)
                    {
                        ListenerAction moveAction = listener.Actions[a];
                        if (moveAction == null || moveAction.Kind != ActionKind.AlignObjectToPointer) continue;
                        uint alignedNodeId = moveAction.TargetNodeId != 0u ? moveAction.TargetNodeId : listener.TargetNodeId;
                        if (alignedNodeId != 0u) PointerAlignmentTargets.Remove(alignedNodeId);
                    }
                }
            }

            // Auto-unhover for PointerExit: if an object has a PointerEnter listener setting a bool input to True,
            // and no explicit PointerExit listener for that input is registered, automatically reset it to False.
            if (kind == PointerEventKind.Exit && targetNodeIds != null && Definition.Listeners != null)
            {
                for (int i = 0; i < Definition.Listeners.Count; i++)
                {
                    NodeListener listener = Definition.Listeners[i];
                    if (listener != null && listener.Enabled && listener.IsActive && (listener.Kind == PointerEventKind.Enter || listener.Kind == PointerEventKind.PointerEnter) && listener.Actions != null)
                    {
                        bool targetMatches = targetNodeIds.Contains(listener.TargetNodeId);
                        if (targetMatches)
                        {
                            for (int a = 0; a < listener.Actions.Count; a++)
                            {
                                ListenerAction enterAction = listener.Actions[a];
                                if (enterAction != null && enterAction.Kind == ActionKind.SetBool && enterAction.Parameter > 0.5)
                                {
                                    bool hasExplicitExit = false;
                                    for (int ex = 0; ex < actionsToRun.Count; ex++)
                                    {
                                        ListenerAction exitAction = actionsToRun[ex].Action;
                                        if (exitAction != null &&
                                            (exitAction.InputIndex == enterAction.InputIndex ||
                                             (!string.IsNullOrEmpty(enterAction.InputId) && exitAction.InputId == enterAction.InputId) ||
                                             (!string.IsNullOrEmpty(enterAction.PropertyName) && exitAction.PropertyName == enterAction.PropertyName)))
                                        {
                                            hasExplicitExit = true;
                                            break;
                                        }
                                    }
                                    if (!hasExplicitExit)
                                    {
                                        actionsToRun.Add(new ListenerActionInvocation(listener, new ListenerAction(ActionKind.SetBool, enterAction.InputIndex, 0.0)
                                            {
                                                InputId = enterAction.InputId,
                                                PropertyName = enterAction.PropertyName
                                            }));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            for (int a = 0; a < actionsToRun.Count; a++)
            {
                ListenerActionInvocation invocation = actionsToRun[a];
                ListenerAction action = invocation != null ? invocation.Action : null;
                if (action == null) continue;

                // If Keyboard listener with specific key filter, check match
                if ((kind == PointerEventKind.Keyboard || kind == PointerEventKind.Gamepad ||
                     kind == PointerEventKind.TextInput || kind == PointerEventKind.SemanticAction) && !string.IsNullOrEmpty(keyOrText))
                {
                    string filter = invocation.Listener != null ? invocation.Listener.Filter : null;
                    if (string.IsNullOrWhiteSpace(filter) && invocation.Listener != null &&
                        !string.IsNullOrEmpty(action.InputId) && !string.IsNullOrEmpty(action.PropertyName))
                    {
                        var boundInput = Definition != null ? Definition.FindInputById(action.InputId) : null;
                        if (boundInput != null && !string.Equals(action.PropertyName, boundInput.Name, StringComparison.OrdinalIgnoreCase))
                            filter = action.PropertyName; // Older files stored the key filter here.
                    }
                    if (!string.IsNullOrEmpty(filter) && !string.Equals(filter, keyOrText, StringComparison.OrdinalIgnoreCase) && !string.Equals(filter, "Any", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                int inputIdx = action.InputIndex;
                if (inputIdx < 0 && !string.IsNullOrEmpty(action.InputId) && Definition != null)
                {
                    inputIdx = Definition.GetInputIndex(action.InputId);
                }

                switch (action.Kind)
                {
                    case ActionKind.SetBool:
                        if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count)
                            SetBool(inputIdx, action.Parameter > 0.5);
                        else if (!string.IsNullOrEmpty(action.PropertyName))
                            SetBool(action.PropertyName, action.Parameter > 0.5);
                        break;
                    case ActionKind.ToggleBool:
                        if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count && Definition.Inputs[inputIdx] != null && Definition.Inputs[inputIdx].Kind == InputKind.Bool)
                        {
                            SetBool(inputIdx, Definition.Inputs[inputIdx].Value <= 0.5);
                        }
                        else if (!string.IsNullOrEmpty(action.PropertyName) && Definition != null)
                        {
                            var inp = Definition.FindInputById(action.PropertyName);
                            if (inp != null && inp.Kind == InputKind.Bool)
                            {
                                SetBool(inp.InputId, inp.Value <= 0.5);
                            }
                        }
                        break;
                    case ActionKind.SetNumber:
                        if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count)
                            SetNumber(inputIdx, action.Parameter);
                        else if (!string.IsNullOrEmpty(action.PropertyName))
                            SetNumber(action.PropertyName, action.Parameter);
                        break;
                    case ActionKind.SetPointerX:
                        {
                            double px = canvasPt.HasValue ? canvasPt.Value.X : 0.0;
                            if (targetBounds.HasValue) px -= targetBounds.Value.X;
                            if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count) SetNumber(inputIdx, px);
                            else if (!string.IsNullOrEmpty(action.PropertyName)) SetNumber(action.PropertyName, px);
                        }
                        break;
                    case ActionKind.SetPointerY:
                        {
                            double py = canvasPt.HasValue ? canvasPt.Value.Y : 0.0;
                            if (targetBounds.HasValue) py -= targetBounds.Value.Y;
                            if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count) SetNumber(inputIdx, py);
                            else if (!string.IsNullOrEmpty(action.PropertyName)) SetNumber(action.PropertyName, py);
                        }
                        break;
                    case ActionKind.SetPointerNormalizedX:
                        {
                            double normX = 50.0;
                            if (canvasPt.HasValue && targetBounds.HasValue && targetBounds.Value.Width > 0.0)
                            {
                                normX = Math.Max(0.0, Math.Min(100.0, (canvasPt.Value.X - targetBounds.Value.X) / targetBounds.Value.Width * 100.0));
                            }
                            if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count) SetNumber(inputIdx, normX);
                            else if (!string.IsNullOrEmpty(action.PropertyName)) SetNumber(action.PropertyName, normX);
                        }
                        break;
                    case ActionKind.SetPointerNormalizedY:
                        {
                            double normY = 50.0;
                            if (canvasPt.HasValue && targetBounds.HasValue && targetBounds.Value.Height > 0.0)
                            {
                                normY = Math.Max(0.0, Math.Min(100.0, (canvasPt.Value.Y - targetBounds.Value.Y) / targetBounds.Value.Height * 100.0));
                            }
                            if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count) SetNumber(inputIdx, normY);
                            else if (!string.IsNullOrEmpty(action.PropertyName)) SetNumber(action.PropertyName, normY);
                        }
                        break;
                    case ActionKind.AlignObjectToPointer:
                        {
                            uint alignedNodeId = action.TargetNodeId != 0u ? action.TargetNodeId : (invocation.Listener != null ? invocation.Listener.TargetNodeId : 0u);
                            if (alignedNodeId != 0u && canvasPt.HasValue)
                            {
                                PointerAlignmentTargets[alignedNodeId] = canvasPt.Value;
                            }
                            else if (alignedNodeId != 0u)
                            {
                                PointerAlignmentTargets.Remove(alignedNodeId);
                            }
                        }
                        break;
                    case ActionKind.FocusTarget:
                        {
                            uint focusNodeId = action.TargetNodeId != 0u ? action.TargetNodeId :
                                (invocation.Listener != null ? invocation.Listener.TargetNodeId : 0u);
                            if (focusNodeId != 0u) OnFocus(focusNodeId);
                        }
                        break;
                    case ActionKind.Fire: // ActionKind.FireTrigger is an alias (= 2)
                        if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count)
                            FireTrigger(inputIdx);
                        else if (!string.IsNullOrEmpty(action.PropertyName))
                            FireTrigger(action.PropertyName);
                        break;
                    case ActionKind.Reset:
                        if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count && Definition.Inputs[inputIdx] != null)
                        {
                            var inp = Definition.Inputs[inputIdx];
                            inp.Value = inp.InitialValue;
                            inp.Fired = false;
                        }
                        else if (!string.IsNullOrEmpty(action.PropertyName) && Definition != null)
                        {
                            var inp = Definition.FindInputById(action.PropertyName);
                            if (inp != null)
                            {
                                inp.Value = inp.InitialValue;
                                inp.Fired = false;
                            }
                        }
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
                    case ActionKind.ReportEvent:
                        if (!string.IsNullOrWhiteSpace(action.TextValue))
                        {
                            ReportedEvents.Add(action.TextValue);
                            if (ReportedEvents.Count > 512) ReportedEvents.RemoveAt(0);
                        }
                        break;
                    case ActionKind.Blank:
                        // An explicitly blank listener action is a valid no-op.
                        break;
                }
            }
        }

        public void OnKeyDown(string keyName)
        {
            var targets = new List<uint>();
            if (FocusedNodeId != 0u) targets.Add(FocusedNodeId);
            targets.Add(0u);
            OnPointerEvent(targets, PointerEventKind.Keyboard, null, null, keyName);
        }

        public void OnTextInput(string text)
        {
            var targets = new List<uint>();
            if (FocusedNodeId != 0u) targets.Add(FocusedNodeId);
            targets.Add(0u);
            OnPointerEvent(targets, PointerEventKind.TextInput, null, null, text);
        }

        public void OnFocus(uint targetId)
        {
            if (targetId == 0u || FocusedNodeId == targetId) return;
            uint previousTarget = FocusedNodeId;
            FocusedNodeId = targetId;
            if (previousTarget != 0u) OnPointerEvent(new List<uint> { previousTarget }, PointerEventKind.Blur, null, null, null);
            OnPointerEvent(new List<uint> { targetId }, PointerEventKind.Focus, null, null, null);
        }

        public void OnBlur(uint targetId)
        {
            if (FocusedNodeId == targetId) FocusedNodeId = 0u;
            OnPointerEvent(new List<uint> { targetId }, PointerEventKind.Blur, null, null, null);
        }

        public void OnGamepad(int buttonIndex)
        {
            OnGamepad(buttonIndex.ToString());
        }

        public void OnGamepad(string buttonName)
        {
            OnPointerEvent(new List<uint> { 0u }, PointerEventKind.Gamepad, null, null, buttonName);
        }

        public void OnSemanticAction(string actionName, uint targetId = 0u)
        {
            OnPointerEvent(new List<uint> { targetId }, PointerEventKind.SemanticAction, null, null, actionName);
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

        private StateTransition FindValidTransition(MachineLayer layer, int currentState, double curTime, int activeTransitionToState, double? progressOverride = null, LayerRuntimeState layerRt = null)
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

            // Ordered candidate search per D-05:
            // 1. Outgoing transitions from current state in declaration order
            // 2. Outgoing transitions from Any state in declaration order (fallback)
            List<StateTransition> candidates = new List<StateTransition>();
            for (int t = 0; t < layer.Transitions.Count; t++)
            {
                StateTransition trans = layer.Transitions[t];
                if (trans == null) continue;
                int fromIdx = trans.FromState;
                if (fromIdx < 0 && !string.IsNullOrEmpty(trans.FromStateId))
                {
                    fromIdx = layer.GetStateIndex(trans.FromStateId);
                }
                if (fromIdx == currentState)
                {
                    candidates.Add(trans);
                }
            }
            if (anyStateIdx >= 0)
            {
                for (int t = 0; t < layer.Transitions.Count; t++)
                {
                    StateTransition trans = layer.Transitions[t];
                    if (trans == null) continue;
                    int fromIdx = trans.FromState;
                    if (fromIdx < 0 && !string.IsNullOrEmpty(trans.FromStateId))
                    {
                        fromIdx = layer.GetStateIndex(trans.FromStateId);
                    }
                    if (fromIdx == anyStateIdx)
                    {
                        candidates.Add(trans);
                    }
                }
            }

            double progress = progressOverride.HasValue
                ? progressOverride.Value
                : GetNormalizedProgress(layer, currentState, curTime);

            for (int i = 0; i < candidates.Count; i++)
            {
                StateTransition trans = candidates[i];
                if (trans == null) continue;

                int toIdx = trans.ToState;
                if (toIdx < 0 && !string.IsNullOrEmpty(trans.ToStateId))
                {
                    toIdx = layer.GetStateIndex(trans.ToStateId);
                }
                if (toIdx < 0 || toIdx >= layer.States.Count) continue;

                int fromIdx = trans.FromState;
                if (fromIdx < 0 && !string.IsNullOrEmpty(trans.FromStateId))
                {
                    fromIdx = layer.GetStateIndex(trans.FromStateId);
                }

                // Avoid self-restart loop if from Any to current
                if (anyStateIdx >= 0 && fromIdx == anyStateIdx && toIdx == currentState)
                {
                    continue;
                }

                // If currently transitioning, do not interrupt with the same target state
                if (activeTransitionToState >= 0 && toIdx == activeTransitionToState)
                {
                    continue;
                }

                // Cycle oscillation guard:
                // Prevent bouncing back to PreviousState immediately if inputs haven't changed and exit time not reached
                if (layerRt != null &&
                    toIdx == layerRt.PreviousState &&
                    layerRt.TimeInCurrentState < 0.1 &&
                    _inputVersion == layerRt.InputVersionAtStateEntry &&
                    !trans.ExitTime.HasValue)
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

        public bool Update(double dt, Dictionary<uint, Dictionary<ushort, double>> outPose, Dictionary<uint, VectorGeometry> outGeometryPose = null)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0.0) dt = 0.0;
            if (outGeometryPose != null) outGeometryPose.Clear();
            bool needsRedraw = false;

            for (int i = 0; i < Definition.Layers.Count; i++)
            {
                MachineLayer layerDef = Definition.Layers[i];
                LayerRuntimeState layerRt = LayerRuntimes[i];

                if (layerRt.IsFinished) continue;

                layerRt.TimeInCurrentState += dt;

                double playbackSpeed = 1.0;
                if (!string.IsNullOrEmpty(Definition.PlaybackSpeedPropertyName))
                    playbackSpeed = Math.Max(0.0, Math.Min(8.0, GetViewModelNumber(Definition.PlaybackSpeedPropertyName)));
                int speedInput = Definition.PlaybackSpeedInputIndex;
                if (string.IsNullOrEmpty(Definition.PlaybackSpeedPropertyName) && speedInput >= 0 && speedInput < Definition.Inputs.Count && Definition.Inputs[speedInput] != null && Definition.Inputs[speedInput].Kind == InputKind.Number)
                    playbackSpeed = Math.Max(0.0, Math.Min(8.0, Definition.Inputs[speedInput].Value));
                if (layerRt.CurTime != null) layerRt.CurTime.Speed = playbackSpeed;

                // 1. Advance current state clip
                bool pauseSource = layerRt.ActiveTransition != null && layerRt.ActiveTransition.Definition != null && layerRt.ActiveTransition.Definition.PauseSourceWhenExiting;
                bool currentStateCrossedPlaybackBoundary = false;
                if (!pauseSource && layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
                {
                    MachineState curState = layerDef.States[layerRt.CurrentState];
                    if (curState.Kind == StateKind.Animation && curState.ClipIndex >= 0 && curState.ClipIndex < Clips.Count)
                    {
                        Clip clip = Clips[curState.ClipIndex];
                        double stateSpeed = (Math.Abs(curState.Speed) < 0.0001) ? 1.0 : curState.Speed;
                        // Playback belongs to the state. Do not let the referenced clip's
                        // global loop mode turn a looping state into a one-shot (or vice versa).
                        LoopMode loopMode = curState.LoopMode;
                        double rawNextTime = layerRt.CurTime.Time + dt * stateSpeed * layerRt.CurTime.Speed * layerRt.CurTime.Direction;
                        int rawMotionDirection = (dt * stateSpeed * layerRt.CurTime.Speed * layerRt.CurTime.Direction) < 0.0 ? -1 : 1;
                        bool crossedBoundary = layerRt.CurTime.Advance(dt * stateSpeed, clip.Duration, loopMode);
                        currentStateCrossedPlaybackBoundary = loopMode == LoopMode.Loop
                            ? crossedBoundary
                            : (loopMode == LoopMode.PingPong && rawMotionDirection > 0 && rawNextTime >= clip.Duration);
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
                            double toStateSpeed = (Math.Abs(toState.Speed) < 0.0001) ? 1.0 : toState.Speed;
                            LoopMode toLoopMode = toState.LoopMode;
                            layerRt.ActiveTransition.ToTime.Advance(dt * toStateSpeed, clip.Duration, toLoopMode);
                        }
                    }
                }

                // 3. Resolve transitions with zero-duration chain resolution & cycle guard (max 100 steps)
                const int MAX_STEPS = 100;
                for (int step = 0; step < MAX_STEPS; step++)
                {
                    if (layerRt.ActiveTransition != null && layerRt.ActiveTransition.Definition != null && !layerRt.ActiveTransition.Definition.Interrupt)
                    {
                        // Current transition cannot be interrupted mid-blend
                        break;
                    }

                    int activeToState = (layerRt.ActiveTransition != null) ? layerRt.ActiveTransition.ToState : -1;
                    double? progressOverride = currentStateCrossedPlaybackBoundary ? (double?)1.0 : null;
                    StateTransition nextTrans = FindValidTransition(layerDef, layerRt.CurrentState, layerRt.CurTime.Time, activeToState, progressOverride, layerRt);
                    if (nextTrans != null)
                    {
                        // Do not interrupt if already blending to the exact same target state
                        if (layerRt.ActiveTransition != null && layerRt.ActiveTransition.ToState == nextTrans.ToState)
                        {
                            break;
                        }

                        // Snapshot current pose if interrupting an active transition to prevent visual pops per D-04
                        Dictionary<uint, Dictionary<ushort, double>> snapshot = null;
                        Dictionary<uint, VectorGeometry> geometrySnapshot = null;
                        if (layerRt.ActiveTransition != null)
                        {
                            var fromPose = new Dictionary<uint, Dictionary<ushort, double>>();
                            var toPose = new Dictionary<uint, Dictionary<ushort, double>>();
                            var fromGeometryPose = new Dictionary<uint, VectorGeometry>();
                            var toGeometryPose = new Dictionary<uint, VectorGeometry>();
                            if (layerRt.ActiveTransition.SnapshotPose != null)
                            {
                                fromPose = layerRt.ActiveTransition.SnapshotPose;
                                if (layerRt.ActiveTransition.SnapshotGeometryPose != null)
                                    fromGeometryPose = layerRt.ActiveTransition.SnapshotGeometryPose;
                            }
                            else if (layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
                            {
                                SampleState(layerDef.States[layerRt.CurrentState], layerRt.CurTime.Time, fromPose, fromGeometryPose);
                            }

                            if (layerRt.ActiveTransition.ToState >= 0 && layerRt.ActiveTransition.ToState < layerDef.States.Count)
                            {
                                SampleState(layerDef.States[layerRt.ActiveTransition.ToState], layerRt.ActiveTransition.ToTime.Time, toPose, toGeometryPose);
                            }
                            double progress = Math.Max(0.0, Math.Min(1.0, layerRt.ActiveTransition.Elapsed / Math.Max(0.001, layerRt.ActiveTransition.Duration)));
                            double mix = layerRt.ActiveTransition.Definition != null ? layerRt.ActiveTransition.Definition.EvaluateInterpolation(progress) : progress;
                            snapshot = new Dictionary<uint, Dictionary<ushort, double>>();
                            geometrySnapshot = new Dictionary<uint, VectorGeometry>();
                            BlendPoses(fromPose, toPose, mix, snapshot, RestPose);
                            GeometryAnimation.BlendPoses(fromGeometryPose, toGeometryPose, mix, geometrySnapshot);
                        }

                        int toState = nextTrans.ToState;
                        double duration = nextTrans.Duration;
                        layerRt.ActiveTransition = new ActiveTransitionState(nextTrans, toState, duration);
                        layerRt.ActiveTransition.SnapshotPose = snapshot;
                        layerRt.ActiveTransition.SnapshotGeometryPose = geometrySnapshot;
                        needsRedraw = true;

                        if (duration <= 0.0)
                        {
                            // Instant zero-duration transition: complete immediately and allow chain resolution
                            layerRt.PreviousState = layerRt.CurrentState;
                            layerRt.CurrentState = toState;
                            layerRt.CurTime = new ClipInstance(toState);
                            layerRt.ActiveTransition = null;
                            layerRt.TimeInCurrentState = 0.0;
                            layerRt.InputVersionAtStateEntry = _inputVersion;
                            currentStateCrossedPlaybackBoundary = false;

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
                var layerGeometryPose = new Dictionary<uint, VectorGeometry>();
                if (layerRt.ActiveTransition != null)
                {
                    var fromPose = new Dictionary<uint, Dictionary<ushort, double>>();
                    var fromGeometryPose = new Dictionary<uint, VectorGeometry>();
                    if (layerRt.ActiveTransition.SnapshotPose != null)
                    {
                        fromPose = layerRt.ActiveTransition.SnapshotPose;
                        if (layerRt.ActiveTransition.SnapshotGeometryPose != null)
                            fromGeometryPose = layerRt.ActiveTransition.SnapshotGeometryPose;
                    }
                    else if (layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
                    {
                        SampleState(layerDef.States[layerRt.CurrentState], layerRt.CurTime.Time, fromPose, fromGeometryPose);
                    }

                    var toPose = new Dictionary<uint, Dictionary<ushort, double>>();
                    var toGeometryPose = new Dictionary<uint, VectorGeometry>();
                    if (layerRt.ActiveTransition.ToState >= 0 && layerRt.ActiveTransition.ToState < layerDef.States.Count)
                    {
                        SampleState(layerDef.States[layerRt.ActiveTransition.ToState], layerRt.ActiveTransition.ToTime.Time, toPose, toGeometryPose);
                    }

                    double u = Math.Max(0.0, Math.Min(1.0, layerRt.ActiveTransition.Elapsed / Math.Max(0.001, layerRt.ActiveTransition.Duration)));
                    double mix = layerRt.ActiveTransition.Definition != null ? layerRt.ActiveTransition.Definition.EvaluateInterpolation(u) : u;
                    BlendPoses(fromPose, toPose, mix, layerPose, RestPose);
                    GeometryAnimation.BlendPoses(fromGeometryPose, toGeometryPose, mix, layerGeometryPose);

                    // Commit to target state when transition duration has completed
                    if (layerRt.ActiveTransition.Elapsed >= layerRt.ActiveTransition.Duration)
                    {
                        int toState = layerRt.ActiveTransition.ToState;
                        ClipInstance toTime = layerRt.ActiveTransition.ToTime;
                        layerRt.PreviousState = layerRt.CurrentState;
                        layerRt.CurrentState = toState;
                        layerRt.CurTime = toTime;
                        layerRt.ActiveTransition = null;
                        layerRt.TimeInCurrentState = 0.0;
                        layerRt.InputVersionAtStateEntry = _inputVersion;

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
                    SampleState(layerDef.States[layerRt.CurrentState], layerRt.CurTime.Time, layerPose, layerGeometryPose);
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
                if (outGeometryPose != null)
                {
                    foreach (var geometry in layerGeometryPose)
                        if (geometry.Value != null) outGeometryPose[geometry.Key] = geometry.Value.Clone();
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

        private void SampleState(MachineState state, double time, Dictionary<uint, Dictionary<ushort, double>> outPose, Dictionary<uint, VectorGeometry> outGeometryPose = null)
        {
            if (state == null) return;

            switch (state.Kind)
            {
                case StateKind.Animation:
                    if (state.ClipIndex >= 0 && state.ClipIndex < Clips.Count)
                    {
                        Clips[state.ClipIndex].SampleToPose(time, outPose, outGeometryPose);
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
                        if (cIdx >= 0 && cIdx < Clips.Count) Clips[cIdx].SampleToPose(time, outPose, outGeometryPose);
                        return;
                    }
                    if (val >= state.Blend1DItems[state.Blend1DItems.Count - 1].Threshold)
                    {
                        int cIdx = state.Blend1DItems[state.Blend1DItems.Count - 1].ClipIndex;
                        if (cIdx >= 0 && cIdx < Clips.Count) Clips[cIdx].SampleToPose(time, outPose, outGeometryPose);
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
                            var geometryA = new Dictionary<uint, VectorGeometry>();
                            var geometryB = new Dictionary<uint, VectorGeometry>();
                            if (a.ClipIndex >= 0 && a.ClipIndex < Clips.Count) Clips[a.ClipIndex].SampleToPose(time, poseA, geometryA);
                            if (b.ClipIndex >= 0 && b.ClipIndex < Clips.Count) Clips[b.ClipIndex].SampleToPose(time, poseB, geometryB);
                            BlendPoses(poseA, poseB, w, outPose);
                            if (outGeometryPose != null) GeometryAnimation.BlendPoses(geometryA, geometryB, w, outGeometryPose);
                            return;
                        }
                    }
                    break;

                case StateKind.BlendDirect:
                    var geometryWeights = new Dictionary<uint, double>();
                    for (int i = 0; i < state.BlendDirectItems.Count; i++)
                    {
                        BlendDirectItem item = state.BlendDirectItems[i];
                        double w = (item.WeightInputIndex >= 0 && item.WeightInputIndex < Definition.Inputs.Count)
                            ? Math.Max(0.0, Math.Min(1.0, Definition.Inputs[item.WeightInputIndex].Value))
                            : 0.0;

                        if (w > 0.001 && item.ClipIndex >= 0 && item.ClipIndex < Clips.Count)
                        {
                            var temp = new Dictionary<uint, Dictionary<ushort, double>>();
                            var tempGeometryPose = new Dictionary<uint, VectorGeometry>();
                            Clips[item.ClipIndex].SampleToPose(time, temp, tempGeometryPose);
                            MergeWeightedGeometryPoses(outGeometryPose, geometryWeights, tempGeometryPose, w);
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

        private static void MergeWeightedGeometryPoses(
            Dictionary<uint, VectorGeometry> target,
            Dictionary<uint, double> accumulatedWeights,
            Dictionary<uint, VectorGeometry> source,
            double weight)
        {
            if (target == null || source == null || weight <= 0.0) return;
            foreach (var item in source)
            {
                if (item.Value == null) continue;
                VectorGeometry current;
                double previousWeight;
                if (!target.TryGetValue(item.Key, out current) || current == null)
                {
                    target[item.Key] = item.Value.Clone();
                    accumulatedWeights[item.Key] = weight;
                    continue;
                }
                accumulatedWeights.TryGetValue(item.Key, out previousWeight);
                double mix = weight / Math.Max(0.000001, previousWeight + weight);
                target[item.Key] = GeometryAnimation.Interpolate(current, item.Value, mix);
                accumulatedWeights[item.Key] = previousWeight + weight;
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
