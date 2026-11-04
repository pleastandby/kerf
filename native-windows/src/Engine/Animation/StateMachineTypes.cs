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
        public string Name { get; set; }
        public InputKind Kind { get; set; }
        public double Value { get; set; }
        public double InitialValue { get; set; }
        public bool Fired { get; set; }

        public MachineInput() : this("Input", InputKind.Number, 0.0) { }

        public MachineInput(string name, InputKind kind, double initialVal = 0.0)
        {
            Name = name ?? "Input";
            Kind = kind;
            Value = initialVal;
            InitialValue = initialVal;
            Fired = false;
        }

        public static MachineInput Bool(string name, bool initial)
        {
            return new MachineInput(name, InputKind.Bool, initial ? 1.0 : 0.0);
        }

        public static MachineInput Number(string name, double initial)
        {
            return new MachineInput(name, InputKind.Number, initial);
        }

        public static MachineInput Trigger(string name)
        {
            return new MachineInput(name, InputKind.Trigger, 0.0);
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
        public int InputIndex { get; set; }
        public string PropertyName { get; set; }
        public string TextValue { get; set; }
        public ConditionOp Op { get; set; }
        public double Value { get; set; }

        public TransitionCondition() : this(0, ConditionOp.Eq, 0.0) { }

        public TransitionCondition(int inputIndex, ConditionOp op, double val)
        {
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
        }

        public bool IsTrue(IList<MachineInput> inputs)
        {
            if (inputs == null || InputIndex < 0 || InputIndex >= inputs.Count) return false;
            MachineInput input = inputs[InputIndex];
            if (input == null) return false;
            if (input.Kind == InputKind.Trigger)
            {
                return input.Fired;
            }

            double diff = input.Value - Value;
            const double EPSILON = 1e-5;

            switch (Op)
            {
                case ConditionOp.Eq:
                    return Math.Abs(diff) <= EPSILON;
                case ConditionOp.Ne:
                    return Math.Abs(diff) > EPSILON;
                case ConditionOp.Lt:
                    return input.Value < Value - EPSILON;
                case ConditionOp.Le:
                    return input.Value <= Value + EPSILON;
                case ConditionOp.Gt:
                    return input.Value > Value + EPSILON;
                case ConditionOp.Ge:
                    return input.Value >= Value - EPSILON;
                default:
                    return false;
            }
        }

        public bool IsTrue(IList<MachineInput> inputs, StateMachineViewModel viewModel)
        {
            if (!string.IsNullOrEmpty(PropertyName) && viewModel != null)
            {
                if (viewModel.IsText(PropertyName))
                {
                    bool equal = string.Equals(viewModel.GetText(PropertyName), TextValue ?? string.Empty, StringComparison.Ordinal);
                    return Op == ConditionOp.Ne ? !equal : (Op == ConditionOp.Eq && equal);
                }
                double value;
                bool fired;
                if (!viewModel.TryGetNumber(PropertyName, out value, out fired)) return false;
                if (viewModel.IsTrigger(PropertyName)) return fired;
                return Evaluate(value);
            }
            return IsTrue(inputs);
        }

        private bool Evaluate(double actual)
        {
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
        public int Id { get; set; }
        public string Name { get; set; }
        public StateKind Kind { get; set; }
        public int ClipIndex { get; set; }
        public int BlendInputIndex { get; set; }
        public double GraphX { get; set; }
        public double GraphY { get; set; }
        public List<Blend1DItem> Blend1DItems { get; set; }
        public List<BlendDirectItem> BlendDirectItems { get; set; }

        public MachineState() : this(0, "State", StateKind.Animation) { }

        public MachineState(int id, string name, StateKind kind, int clipIndex = -1)
        {
            Id = id;
            Name = name ?? "State";
            Kind = kind;
            ClipIndex = clipIndex;
            BlendInputIndex = -1;
            GraphX = -1.0;
            GraphY = -1.0;
            Blend1DItems = new List<Blend1DItem>();
            BlendDirectItems = new List<BlendDirectItem>();
        }
    }

    public class StateTransition
    {
        public int FromState { get; set; }
        public int ToState { get; set; }
        public List<TransitionCondition> Conditions { get; set; }
        public double Duration { get; set; } // seconds, 0 = instant
        public double? ExitTime { get; set; } // normalized [0, 1] of from-state duration
        public bool Interrupt { get; set; }
        public InterpMode Interpolation { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public StateTransition() : this(0, 0, 0.0) { }

        public StateTransition(int fromState, int toState, double duration = 0.0)
        {
            FromState = fromState;
            ToState = toState;
            Duration = Math.Max(0.0, duration);
            Conditions = new List<TransitionCondition>();
            ExitTime = null;
            Interrupt = true;
            Interpolation = InterpMode.Cubic;
            X1 = 0.25;
            Y1 = 0.1;
            X2 = 0.25;
            Y2 = 1.0;
        }

        public StateTransition WithCondition(TransitionCondition cond)
        {
            if (cond != null) Conditions.Add(cond);
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
            States.Add(state);
            return idx;
        }

        public void AddTransition(StateTransition trans)
        {
            if (trans != null) Transitions.Add(trans);
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
            Inputs.Add(input);
            return idx;
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

