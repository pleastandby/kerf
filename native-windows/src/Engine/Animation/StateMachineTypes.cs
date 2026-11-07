using System;
using System.Collections.Generic;

namespace Kerf.Engine.Animation
{
    public enum InputKind
    {
        Bool = 0,
        Number = 1,
        Trigger = 2
    }

    public class MachineInput
    {
        public string InputId { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public InputKind Kind { get; set; }
        public double Value { get; set; }
        public double InitialValue { get; set; }
        public bool Fired { get; set; }

        public MachineInput() : this("Input", InputKind.Number, 0.0, null) { }

        public MachineInput(string name, InputKind kind, double initialVal = 0.0) : this(name, kind, initialVal, null) { }

        public MachineInput(string name, InputKind kind, double initialVal, string inputId)
        {
            InputId = !string.IsNullOrEmpty(inputId) ? inputId : "sm_input_" + Guid.NewGuid().ToString("N");
            Id = 0;
            Name = name ?? "Input";
            Kind = kind;
            if (double.IsNaN(initialVal) || double.IsInfinity(initialVal)) initialVal = 0.0;
            Value = initialVal;
            InitialValue = initialVal;
            Fired = false;
        }

        public static MachineInput Bool(string name, bool initial, string inputId = null)
        {
            return new MachineInput(name, InputKind.Bool, initial ? 1.0 : 0.0, inputId);
        }

        public static MachineInput Number(string name, double initial, string inputId = null)
        {
            return new MachineInput(name, InputKind.Number, initial, inputId);
        }

        public static MachineInput Trigger(string name, string inputId = null)
        {
            return new MachineInput(name, InputKind.Trigger, 0.0, inputId);
        }
    }

    public enum ConditionOp
    {
        Eq = 0,
        Ne = 1,
        Lt = 2,
        Le = 3,
        Gt = 4,
        Ge = 5
    }

    public class TransitionCondition
    {
        public string InputId { get; set; }
        public int InputIndex { get; set; }
        public string PropertyName { get; set; }
        public string TextValue { get; set; }
        public ConditionOp Op { get; set; }
        public double Value { get; set; }

        public TransitionCondition() : this(0, ConditionOp.Eq, 0.0) { }

        public TransitionCondition(int inputIndex, ConditionOp op, double val)
        {
            InputId = null;
            InputIndex = inputIndex;
            PropertyName = null;
            TextValue = null;
            Op = op;
            Value = val;
        }

        public TransitionCondition(string propertyName, ConditionOp op, double val)
            : this(-1, op, val)
        {
            PropertyName = propertyName;
            if (!string.IsNullOrEmpty(propertyName) && propertyName.StartsWith("sm_input_"))
            {
                InputId = propertyName;
            }
        }

        public TransitionCondition(string inputId, ConditionOp op, double val, bool isInputId)
            : this(-1, op, val)
        {
            if (isInputId)
            {
                InputId = inputId;
                PropertyName = null;
            }
            else
            {
                PropertyName = inputId;
                if (!string.IsNullOrEmpty(inputId) && inputId.StartsWith("sm_input_"))
                {
                    InputId = inputId;
                }
            }
        }

        public bool IsTrue(IList<MachineInput> inputs)
        {
            return IsTrue(inputs, null, null);
        }

        public bool IsTrue(IList<MachineInput> inputs, StateMachineViewModel viewModel)
        {
            return IsTrue(inputs, viewModel, null);
        }

        public bool IsTrue(IList<MachineInput> inputs, StateMachineViewModel viewModel, StateMachineDefinition def)
        {
            if (!string.IsNullOrEmpty(PropertyName) && viewModel != null)
            {
                if (viewModel.IsText(PropertyName))
                {
                    bool equal = string.Equals(viewModel.GetText(PropertyName), TextValue ?? string.Empty, StringComparison.Ordinal);
                    return Op == ConditionOp.Ne ? !equal : (Op == ConditionOp.Eq && equal);
                }
                double vmVal;
                bool fired;
                if (viewModel.TryGetNumber(PropertyName, out vmVal, out fired))
                {
                    if (viewModel.IsTrigger(PropertyName)) return fired;
                    return Evaluate(vmVal);
                }
            }

            MachineInput targetInput = null;
            if (inputs != null)
            {
                if (!string.IsNullOrEmpty(InputId))
                {
                    for (int i = 0; i < inputs.Count; i++)
                    {
                        if (inputs[i] != null && string.Equals(inputs[i].InputId, InputId, StringComparison.OrdinalIgnoreCase))
                        {
                            targetInput = inputs[i];
                            break;
                        }
                    }
                }
                if (targetInput == null && !string.IsNullOrEmpty(PropertyName))
                {
                    for (int i = 0; i < inputs.Count; i++)
                    {
                        if (inputs[i] != null && (string.Equals(inputs[i].InputId, PropertyName, StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(inputs[i].Name, PropertyName, StringComparison.OrdinalIgnoreCase)))
                        {
                            targetInput = inputs[i];
                            break;
                        }
                    }
                }
                if (targetInput == null && InputIndex >= 0 && InputIndex < inputs.Count)
                {
                    targetInput = inputs[InputIndex];
                }
            }
            else if (def != null)
            {
                if (!string.IsNullOrEmpty(InputId)) targetInput = def.FindInputById(InputId);
                if (targetInput == null && !string.IsNullOrEmpty(PropertyName)) targetInput = def.FindInputById(PropertyName);
                if (targetInput == null && InputIndex >= 0) targetInput = def.FindInputById(InputIndex);
            }

            if (targetInput == null) return false;

            if (targetInput.Kind == InputKind.Trigger)
            {
                return targetInput.Fired;
            }

            return Evaluate(targetInput.Value);
        }

        private bool Evaluate(double actual)
        {
            if (double.IsNaN(actual) || double.IsInfinity(actual) || double.IsNaN(Value) || double.IsInfinity(Value))
            {
                return false;
            }

            double diff = actual - Value;
            const double EPSILON = 1e-5;
            switch (Op)
            {
                case ConditionOp.Eq: return Math.Abs(diff) <= EPSILON;
                case ConditionOp.Ne: return Math.Abs(diff) > EPSILON;
                case ConditionOp.Lt: return actual < Value - EPSILON;
                case ConditionOp.Le: return actual <= Value + EPSILON;
                case ConditionOp.Gt: return actual > Value + EPSILON;
                case ConditionOp.Ge: return actual >= Value - EPSILON;
                default: return false;
            }
        }
    }

    public class ConditionGroup
    {
        public string Id { get; set; }
        public List<TransitionCondition> Conditions { get; set; }

        public ConditionGroup(string id = null)
        {
            Id = !string.IsNullOrEmpty(id) ? id : "sm_cg_" + Guid.NewGuid().ToString("N");
            Conditions = new List<TransitionCondition>();
        }

        public ConditionGroup Clone()
        {
            ConditionGroup grp = new ConditionGroup(Id);
            if (Conditions != null)
            {
                for (int i = 0; i < Conditions.Count; i++)
                {
                    TransitionCondition c = Conditions[i];
                    if (c == null) continue;
                    grp.Conditions.Add(new TransitionCondition(c.InputIndex, c.Op, c.Value)
                    {
                        InputId = c.InputId,
                        PropertyName = c.PropertyName,
                        TextValue = c.TextValue
                    });
                }
            }
            return grp;
        }

        public bool Evaluate(IList<MachineInput> inputs, StateMachineViewModel viewModel, StateMachineDefinition def)
        {
            if (Conditions == null || Conditions.Count == 0) return true;
            for (int i = 0; i < Conditions.Count; i++)
            {
                TransitionCondition cond = Conditions[i];
                if (cond == null) continue;
                if (!cond.IsTrue(inputs, viewModel, def)) return false;
            }
            return true;
        }
    }

    public enum StateKind
    {
        Entry = 0,
        Exit = 1,
        Any = 2,
        Animation = 3,
        Blend1D = 4,
        BlendDirect = 5
    }

    public class Blend1DItem
    {
        public double Threshold { get; set; }
        public int ClipIndex { get; set; }

        public Blend1DItem() : this(0.0, -1) { }

        public Blend1DItem(double threshold, int clipIndex)
        {
            Threshold = threshold;
            ClipIndex = clipIndex;
        }
    }

    public class BlendDirectItem
    {
        public int WeightInputIndex { get; set; }
        public int ClipIndex { get; set; }

        public BlendDirectItem() : this(-1, -1) { }

        public BlendDirectItem(int weightInputIndex, int clipIndex)
        {
            WeightInputIndex = weightInputIndex;
            ClipIndex = clipIndex;
        }
    }

    public class MachineState
    {
        public string NodeId { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public StateKind Kind { get; set; }
        public int ClipIndex { get; set; }
        public string ClipName { get; set; }
        public int BlendInputIndex { get; set; }
        public double GraphX { get; set; }
        public double GraphY { get; set; }
        public double Speed { get; set; }
        public bool Loop { get; set; }
        public List<Blend1DItem> Blend1DItems { get; set; }
        public List<BlendDirectItem> BlendDirectItems { get; set; }

        public MachineState() : this(0, "State", StateKind.Animation, -1, null) { }

        public MachineState(int id, string name, StateKind kind, int clipIndex = -1)
            : this(id, name, kind, clipIndex, null) { }

        public MachineState(int id, string name, StateKind kind, int clipIndex, string nodeId)
        {
            NodeId = !string.IsNullOrEmpty(nodeId) ? nodeId : "sm_node_" + Guid.NewGuid().ToString("N");
            Id = id;
            Name = name ?? "State";
            Kind = kind;
            ClipIndex = clipIndex;
            ClipName = null;
            BlendInputIndex = -1;
            GraphX = -1.0;
            GraphY = -1.0;
            Speed = 1.0;
            Loop = true;
            Blend1DItems = new List<Blend1DItem>();
            BlendDirectItems = new List<BlendDirectItem>();
        }
    }

    public class StateTransition
    {
        public string TransitionId { get; set; }
        public int FromState { get; set; }
        public int ToState { get; set; }
        public string FromStateId { get; set; }
        public string ToStateId { get; set; }
        public List<ConditionGroup> ConditionGroups { get; set; }

        public List<TransitionCondition> Conditions
        {
            get
            {
                if (ConditionGroups == null)
                {
                    ConditionGroups = new List<ConditionGroup>();
                }
                if (ConditionGroups.Count == 0)
                {
                    ConditionGroups.Add(new ConditionGroup());
                }
                return ConditionGroups[0].Conditions;
            }
            set
            {
                if (ConditionGroups == null)
                {
                    ConditionGroups = new List<ConditionGroup>();
                }
                if (ConditionGroups.Count == 0)
                {
                    ConditionGroups.Add(new ConditionGroup());
                }
                ConditionGroups[0].Conditions = (value != null) ? value : new List<TransitionCondition>();
            }
        }

        public double Duration { get; set; } // seconds, 0 = instant
        public double? ExitTime { get; set; } // normalized [0, 1] of from-state duration
        public bool Interrupt { get; set; }
        public InterpMode Interpolation { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public StateTransition() : this(0, 0, 0.0, null) { }

        public StateTransition(int fromState, int toState, double duration = 0.0)
            : this(fromState, toState, duration, null) { }

        public StateTransition(int fromState, int toState, double duration, string transitionId)
        {
            TransitionId = !string.IsNullOrEmpty(transitionId) ? transitionId : "sm_trans_" + Guid.NewGuid().ToString("N");
            FromState = fromState;
            ToState = toState;
            FromStateId = null;
            ToStateId = null;
            Duration = Math.Max(0.0, duration);
            ConditionGroups = new List<ConditionGroup>();
            ExitTime = null;
            Interrupt = true;
            Interpolation = InterpMode.Cubic;
            X1 = 0.25;
            Y1 = 0.1;
            X2 = 0.25;
            Y2 = 1.0;
        }

        public StateTransition(string fromStateId, string toStateId, double duration = 0.0, string transitionId = null)
            : this(-1, -1, duration, transitionId)
        {
            FromStateId = fromStateId;
            ToStateId = toStateId;
        }

        public StateTransition WithCondition(TransitionCondition cond)
        {
            if (cond != null) Conditions.Add(cond);
            return this;
        }

        public StateTransition WithConditionGroup(ConditionGroup grp)
        {
            if (grp != null)
            {
                if (ConditionGroups == null) ConditionGroups = new List<ConditionGroup>();
                ConditionGroups.Add(grp);
            }
            return this;
        }

        public StateTransition WithExitTime(double exitTime)
        {
            ExitTime = exitTime;
            return this;
        }

        public StateTransition WithInterrupt(bool interrupt)
        {
            Interrupt = interrupt;
            return this;
        }

        public double EvaluateInterpolation(double progress)
        {
            progress = Math.Max(0.0, Math.Min(1.0, progress));
            if (Interpolation == InterpMode.Hold) return progress >= 1.0 ? 1.0 : 0.0;
            if (Interpolation == InterpMode.Linear) return progress;
            return CubicBezierEase.Evaluate(progress, X1, Y1, X2, Y2);
        }

        public bool EvaluateConditions(IList<MachineInput> inputs, StateMachineViewModel viewModel, StateMachineDefinition def)
        {
            if (ConditionGroups == null || ConditionGroups.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < ConditionGroups.Count; i++)
            {
                ConditionGroup grp = ConditionGroups[i];
                if (grp == null) continue;
                if (grp.Evaluate(inputs, viewModel, def))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class MachineLayer
    {
        public string Name { get; set; }
        public List<MachineState> States { get; set; }
        public List<StateTransition> Transitions { get; set; }

        public MachineLayer() : this("Layer") { }

        public MachineLayer(string name)
        {
            Name = name ?? "Layer";
            States = new List<MachineState>();
            Transitions = new List<StateTransition>();
        }

        public int AddState(MachineState state)
        {
            if (state == null) return -1;
            int idx = States.Count;
            state.Id = idx;
            if (string.IsNullOrEmpty(state.NodeId))
            {
                state.NodeId = "sm_node_" + Guid.NewGuid().ToString("N");
            }
            States.Add(state);
            return idx;
        }

        public void AddTransition(StateTransition trans)
        {
            if (trans == null) return;
            if (string.IsNullOrEmpty(trans.TransitionId))
            {
                trans.TransitionId = "sm_trans_" + Guid.NewGuid().ToString("N");
            }
            if (string.IsNullOrEmpty(trans.FromStateId) && trans.FromState >= 0 && trans.FromState < States.Count && States[trans.FromState] != null)
            {
                trans.FromStateId = States[trans.FromState].NodeId;
            }
            if (string.IsNullOrEmpty(trans.ToStateId) && trans.ToState >= 0 && trans.ToState < States.Count && States[trans.ToState] != null)
            {
                trans.ToStateId = States[trans.ToState].NodeId;
            }
            Transitions.Add(trans);
        }

        public MachineState FindStateById(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            for (int i = 0; i < States.Count; i++)
            {
                if (States[i] != null && string.Equals(States[i].NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    return States[i];
                }
            }
            int parsedId;
            if (int.TryParse(nodeId, out parsedId))
            {
                return FindStateById(parsedId);
            }
            return null;
        }

        public MachineState FindStateById(int id)
        {
            for (int i = 0; i < States.Count; i++)
            {
                if (States[i] != null && States[i].Id == id)
                {
                    return States[i];
                }
            }
            if (id >= 0 && id < States.Count)
            {
                return States[id];
            }
            return null;
        }

        public int GetStateIndex(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return -1;
            for (int i = 0; i < States.Count; i++)
            {
                if (States[i] != null && string.Equals(States[i].NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            int parsedId;
            if (int.TryParse(nodeId, out parsedId))
            {
                for (int i = 0; i < States.Count; i++)
                {
                    if (States[i] != null && States[i].Id == parsedId)
                    {
                        return i;
                    }
                }
                if (parsedId >= 0 && parsedId < States.Count)
                {
                    return parsedId;
                }
            }
            return -1;
        }

        public int GetStateIndex(MachineState state)
        {
            if (state == null) return -1;
            return States.IndexOf(state);
        }

        public string GetStateNodeId(int index)
        {
            if (index >= 0 && index < States.Count && States[index] != null)
            {
                return States[index].NodeId;
            }
            return null;
        }
    }

    public enum PointerEventKind
    {
        Down = 0,
        Up = 1,
        Move = 2,
        Enter = 3,
        Exit = 4
    }

    public enum ActionKind
    {
        SetBool = 0,
        SetNumber = 1,
        Fire = 2,
        SetViewModelBool = 3,
        SetViewModelNumber = 4,
        FireViewModelTrigger = 5,
        SetViewModelText = 6
    }

    public class ListenerAction
    {
        public ActionKind Kind { get; set; }
        public int InputIndex { get; set; }
        public string PropertyName { get; set; }
        public string TextValue { get; set; }
        public double Parameter { get; set; }

        public ListenerAction() : this(ActionKind.Fire, -1, 0.0) { }

        public ListenerAction(ActionKind kind, int inputIndex, double param = 0.0)
        {
            Kind = kind;
            InputIndex = inputIndex;
            PropertyName = null;
            TextValue = null;
            Parameter = param;
        }

        public ListenerAction(ActionKind kind, string propertyName, double param = 0.0)
            : this(kind, -1, param)
        {
            PropertyName = propertyName;
        }
    }

    public class NodeListener
    {
        public uint TargetNodeId { get; set; }
        public PointerEventKind Kind { get; set; }
        public List<ListenerAction> Actions { get; set; }

        public NodeListener() : this(0, PointerEventKind.Down) { }

        public NodeListener(uint targetNodeId, PointerEventKind kind)
        {
            TargetNodeId = targetNodeId;
            Kind = kind;
            Actions = new List<ListenerAction>();
        }
    }

    public enum ViewModelPropertyKind
    {
        Boolean = 0,
        Number = 1,
        Trigger = 2,
        Text = 3
    }

    public class ViewModelPropertyDefinition
    {
        public string Name { get; set; }
        public ViewModelPropertyKind Kind { get; set; }
        public double InitialNumber { get; set; }
        public bool InitialBoolean { get; set; }
        public string InitialText { get; set; }

        public ViewModelPropertyDefinition() : this("Property", ViewModelPropertyKind.Boolean) { }

        public ViewModelPropertyDefinition(string name, ViewModelPropertyKind kind)
        {
            Name = name ?? "Property";
            Kind = kind;
            InitialNumber = 0.0;
            InitialBoolean = false;
            InitialText = string.Empty;
        }
    }

    public class StateMachineViewModel
    {
        private sealed class ValueSlot
        {
            public ViewModelPropertyKind Kind;
            public double Number;
            public bool Boolean;
            public bool Fired;
            public string Text;
        }

        private readonly Dictionary<string, ValueSlot> _values = new Dictionary<string, ValueSlot>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ViewModelPropertyDefinition> _definitions;

        public StateMachineViewModel(IEnumerable<ViewModelPropertyDefinition> definitions)
        {
            _definitions = definitions != null ? new List<ViewModelPropertyDefinition>(definitions) : new List<ViewModelPropertyDefinition>();
            Reset();
        }

        public void Reset()
        {
            _values.Clear();
            foreach (var definition in _definitions)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Name)) continue;
                _values[definition.Name] = new ValueSlot
                {
                    Kind = definition.Kind,
                    Number = definition.InitialNumber,
                    Boolean = definition.InitialBoolean,
                    Fired = false,
                    Text = definition.InitialText ?? string.Empty
                };
            }
        }

        public bool SetBool(string name, bool value)
        {
            ValueSlot slot;
            if (!_values.TryGetValue(name ?? string.Empty, out slot) || slot.Kind != ViewModelPropertyKind.Boolean) return false;
            slot.Boolean = value;
            slot.Number = value ? 1.0 : 0.0;
            return true;
        }

        public bool SetNumber(string name, double value)
        {
            ValueSlot slot;
            if (double.IsNaN(value) || double.IsInfinity(value) || !_values.TryGetValue(name ?? string.Empty, out slot) || slot.Kind != ViewModelPropertyKind.Number) return false;
            slot.Number = value;
            return true;
        }

        public bool SetText(string name, string value)
        {
            ValueSlot slot;
            if (!_values.TryGetValue(name ?? string.Empty, out slot) || slot.Kind != ViewModelPropertyKind.Text) return false;
            slot.Text = value ?? string.Empty;
            return true;
        }

        public bool FireTrigger(string name)
        {
            ValueSlot slot;
            if (!_values.TryGetValue(name ?? string.Empty, out slot) || slot.Kind != ViewModelPropertyKind.Trigger) return false;
            slot.Fired = true;
            return true;
        }

        public bool GetBool(string name)
        {
            ValueSlot slot;
            return _values.TryGetValue(name ?? string.Empty, out slot) && slot.Kind == ViewModelPropertyKind.Boolean && slot.Boolean;
        }

        public bool IsTrigger(string name)
        {
            ValueSlot slot;
            return _values.TryGetValue(name ?? string.Empty, out slot) && slot.Kind == ViewModelPropertyKind.Trigger;
        }

        public bool IsText(string name)
        {
            ValueSlot slot;
            return _values.TryGetValue(name ?? string.Empty, out slot) && slot.Kind == ViewModelPropertyKind.Text;
        }

        public string GetText(string name)
        {
            ValueSlot slot;
            return _values.TryGetValue(name ?? string.Empty, out slot) && slot.Kind == ViewModelPropertyKind.Text ? (slot.Text ?? string.Empty) : string.Empty;
        }

        public bool TryGetNumber(string name, out double value, out bool fired)
        {
            ValueSlot slot;
            if (_values.TryGetValue(name ?? string.Empty, out slot))
            {
                value = slot.Kind == ViewModelPropertyKind.Boolean ? (slot.Boolean ? 1.0 : 0.0) : slot.Number;
                fired = slot.Fired;
                return slot.Kind != ViewModelPropertyKind.Text;
            }
            value = 0.0;
            fired = false;
            return false;
        }

        public void ClearTriggers()
        {
            foreach (var slot in _values.Values)
                if (slot.Kind == ViewModelPropertyKind.Trigger) slot.Fired = false;
        }
    }

    public class StateMachineDefinition
    {
        public string Name { get; set; }
        public int PlaybackSpeedInputIndex { get; set; }
        public string PlaybackSpeedPropertyName { get; set; }
        public List<MachineInput> Inputs { get; set; }
        public List<ViewModelPropertyDefinition> ViewModelProperties { get; set; }
        public List<MachineLayer> Layers { get; set; }
        public List<NodeListener> Listeners { get; set; }

        public StateMachineDefinition() : this("StateMachine") { }

        public StateMachineDefinition(string name)
        {
            Name = name ?? "StateMachine";
            PlaybackSpeedInputIndex = -1;
            PlaybackSpeedPropertyName = null;
            Inputs = new List<MachineInput>();
            ViewModelProperties = new List<ViewModelPropertyDefinition>();
            Layers = new List<MachineLayer>();
            Listeners = new List<NodeListener>();
        }

        public int AddInput(MachineInput input)
        {
            if (input == null) return -1;
            int idx = Inputs.Count;
            input.Id = idx;
            if (string.IsNullOrEmpty(input.InputId))
            {
                input.InputId = "sm_input_" + Guid.NewGuid().ToString("N");
            }
            Inputs.Add(input);
            return idx;
        }

        public MachineInput FindInputById(string inputId)
        {
            if (string.IsNullOrEmpty(inputId)) return null;
            for (int i = 0; i < Inputs.Count; i++)
            {
                if (Inputs[i] != null && string.Equals(Inputs[i].InputId, inputId, StringComparison.OrdinalIgnoreCase))
                {
                    return Inputs[i];
                }
            }
            for (int i = 0; i < Inputs.Count; i++)
            {
                if (Inputs[i] != null && string.Equals(Inputs[i].Name, inputId, StringComparison.OrdinalIgnoreCase))
                {
                    return Inputs[i];
                }
            }
            int parsedId;
            if (int.TryParse(inputId, out parsedId))
            {
                return FindInputById(parsedId);
            }
            return null;
        }

        public MachineInput FindInputById(int index)
        {
            if (index >= 0 && index < Inputs.Count)
            {
                return Inputs[index];
            }
            return null;
        }

        public int GetInputIndex(string inputId)
        {
            if (string.IsNullOrEmpty(inputId)) return -1;
            for (int i = 0; i < Inputs.Count; i++)
            {
                if (Inputs[i] != null && (string.Equals(Inputs[i].InputId, inputId, StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(Inputs[i].Name, inputId, StringComparison.OrdinalIgnoreCase)))
                {
                    return i;
                }
            }
            int parsedId;
            if (int.TryParse(inputId, out parsedId))
            {
                if (parsedId >= 0 && parsedId < Inputs.Count)
                {
                    return parsedId;
                }
            }
            return -1;
        }

        public int GetInputIndex(MachineInput input)
        {
            if (input == null) return -1;
            return Inputs.IndexOf(input);
        }

        public string GetInputId(int index)
        {
            if (index >= 0 && index < Inputs.Count && Inputs[index] != null)
            {
                return Inputs[index].InputId;
            }
            return null;
        }

        public MachineState FindStateById(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            for (int l = 0; l < Layers.Count; l++)
            {
                if (Layers[l] != null)
                {
                    MachineState st = Layers[l].FindStateById(nodeId);
                    if (st != null) return st;
                }
            }
            return null;
        }

        public MachineState FindStateById(int id)
        {
            for (int l = 0; l < Layers.Count; l++)
            {
                if (Layers[l] != null)
                {
                    MachineState st = Layers[l].FindStateById(id);
                    if (st != null) return st;
                }
            }
            return null;
        }

        public void AddViewModelProperty(ViewModelPropertyDefinition property)
        {
            if (property == null) return;
            if (ViewModelProperties == null) ViewModelProperties = new List<ViewModelPropertyDefinition>();
            ViewModelProperties.Add(property);
        }

        public int AddLayer(MachineLayer layer)
        {
            if (layer == null) return -1;
            int idx = Layers.Count;
            Layers.Add(layer);
            return idx;
        }

        public void AddListener(NodeListener listener)
        {
            if (listener != null) Listeners.Add(listener);
        }
    }
}
