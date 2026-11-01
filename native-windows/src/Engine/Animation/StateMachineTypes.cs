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
        public bool Fired { get; set; }

        public MachineInput(string name, InputKind kind, double initialVal = 0.0)
        {
            Name = name ?? "Input";
            Kind = kind;
            Value = initialVal;
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
        public ConditionOp Op { get; set; }
        public double Value { get; set; }

        public TransitionCondition(int inputIndex, ConditionOp op, double val)
        {
            InputIndex = inputIndex;
            Op = op;
            Value = val;
        }

        public bool IsTrue(IList<MachineInput> inputs)
        {
            if (inputs == null || InputIndex < 0 || InputIndex >= inputs.Count) return false;
            MachineInput input = inputs[InputIndex];
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
        public List<Blend1DItem> Blend1DItems { get; private set; }
        public List<BlendDirectItem> BlendDirectItems { get; private set; }

        public MachineState(int id, string name, StateKind kind, int clipIndex = -1)
        {
            Id = id;
            Name = name ?? "State";
            Kind = kind;
            ClipIndex = clipIndex;
            BlendInputIndex = -1;
            Blend1DItems = new List<Blend1DItem>();
            BlendDirectItems = new List<BlendDirectItem>();
        }
    }

    public class StateTransition
    {
        public int FromState { get; set; }
        public int ToState { get; set; }
        public List<TransitionCondition> Conditions { get; private set; }
        public double Duration { get; set; } // seconds, 0 = instant
        public double? ExitTime { get; set; } // normalized [0, 1] of from-state duration
        public bool Interrupt { get; set; }

        public StateTransition(int fromState, int toState, double duration = 0.0)
        {
            FromState = fromState;
            ToState = toState;
            Duration = Math.Max(0.0, duration);
            Conditions = new List<TransitionCondition>();
            ExitTime = null;
            Interrupt = true;
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
    }

    public class MachineLayer
    {
        public string Name { get; set; }
        public List<MachineState> States { get; private set; }
        public List<StateTransition> Transitions { get; private set; }

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
        Fire = 2
    }

    public class ListenerAction
    {
        public ActionKind Kind { get; set; }
        public int InputIndex { get; set; }
        public double Parameter { get; set; }

        public ListenerAction(ActionKind kind, int inputIndex, double param = 0.0)
        {
            Kind = kind;
            InputIndex = inputIndex;
            Parameter = param;
        }
    }

    public class NodeListener
    {
        public uint TargetNodeId { get; set; }
        public PointerEventKind Kind { get; set; }
        public List<ListenerAction> Actions { get; private set; }

        public NodeListener(uint targetNodeId, PointerEventKind kind)
        {
            TargetNodeId = targetNodeId;
            Kind = kind;
            Actions = new List<ListenerAction>();
        }
    }

    public class StateMachineDefinition
    {
        public string Name { get; set; }
        public List<MachineInput> Inputs { get; private set; }
        public List<MachineLayer> Layers { get; private set; }
        public List<NodeListener> Listeners { get; private set; }

        public StateMachineDefinition(string name)
        {
            Name = name ?? "StateMachine";
            Inputs = new List<MachineInput>();
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

