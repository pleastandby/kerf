using System;
using System.Collections.Generic;

namespace Kerf.Engine.Animation
{
    public enum ValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public class ValidationIssue
    {
        public string IssueCode { get; set; }
        public string Message { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string StateId { get; set; }
        public string TransitionId { get; set; }
        public string InputId { get; set; }

        public ValidationIssue()
        {
        }

        public ValidationIssue(string code, string message, ValidationSeverity severity, string stateId = null, string transitionId = null, string inputId = null)
        {
            IssueCode = code ?? "UNKNOWN";
            Message = message ?? string.Empty;
            Severity = severity;
            StateId = stateId;
            TransitionId = transitionId;
            InputId = inputId;
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1}: {2}", Severity, IssueCode, Message);
        }
    }

    public class StateMachineValidationResult
    {
        public List<ValidationIssue> Issues { get; set; }

        public StateMachineValidationResult()
        {
            Issues = new List<ValidationIssue>();
        }

        public bool HasErrors
        {
            get
            {
                if (Issues == null) return false;
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i] != null && Issues[i].Severity == ValidationSeverity.Error)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool HasWarnings
        {
            get
            {
                if (Issues == null) return false;
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i] != null && Issues[i].Severity == ValidationSeverity.Warning)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool IsValid
        {
            get { return !HasErrors; }
        }

        public bool HasCode(string code)
        {
            if (Issues == null || string.IsNullOrEmpty(code)) return false;
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i] != null && string.Equals(Issues[i].IssueCode, code, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public List<ValidationIssue> GetIssuesForState(string stateId)
        {
            List<ValidationIssue> list = new List<ValidationIssue>();
            if (Issues == null || string.IsNullOrEmpty(stateId)) return list;
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i] != null && string.Equals(Issues[i].StateId, stateId, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(Issues[i]);
                }
            }
            return list;
        }

        public List<ValidationIssue> GetIssuesForTransition(string transitionId)
        {
            List<ValidationIssue> list = new List<ValidationIssue>();
            if (Issues == null || string.IsNullOrEmpty(transitionId)) return list;
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i] != null && string.Equals(Issues[i].TransitionId, transitionId, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(Issues[i]);
                }
            }
            return list;
        }
    }

    public static class StateMachineValidator
    {
        public const string MISSING_ENTRY = "MISSING_ENTRY";
        public const string DANGLING_TRANSITION = "DANGLING_TRANSITION";
        public const string INVALID_TARGET_ENTRY = "INVALID_TARGET_ENTRY";
        public const string INVALID_TARGET_ANY = "INVALID_TARGET_ANY";
        public const string UNREACHABLE_STATE = "UNREACHABLE_STATE";
        public const string INVALID_CLIP_REF = "INVALID_CLIP_REF";
        public const string TYPE_MISMATCH = "TYPE_MISMATCH";
        public const string DUPLICATE_ID = "DUPLICATE_ID";

        public static StateMachineValidationResult Validate(StateMachineDefinition def, IList<Clip> clips)
        {
            StateMachineValidationResult result = new StateMachineValidationResult();
            if (def == null)
            {
                result.Issues.Add(new ValidationIssue("MISSING_DEFINITION", "State machine definition is null.", ValidationSeverity.Error));
                return result;
            }

            // 1. Check duplicate input IDs or names
            if (def.Inputs != null)
            {
                HashSet<string> seenInputIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> seenInputNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < def.Inputs.Count; i++)
                {
                    MachineInput input = def.Inputs[i];
                    if (input == null) continue;
                    if (!string.IsNullOrEmpty(input.InputId))
                    {
                        if (seenInputIds.Contains(input.InputId))
                        {
                            result.Issues.Add(new ValidationIssue(DUPLICATE_ID, "Duplicate input ID detected: " + input.InputId, ValidationSeverity.Error, null, null, input.InputId));
                        }
                        else
                        {
                            seenInputIds.Add(input.InputId);
                        }
                    }
                    if (!string.IsNullOrEmpty(input.Name))
                    {
                        if (seenInputNames.Contains(input.Name))
                        {
                            result.Issues.Add(new ValidationIssue(DUPLICATE_ID, "Duplicate input name detected: " + input.Name, ValidationSeverity.Warning, null, null, input.InputId));
                        }
                        else
                        {
                            seenInputNames.Add(input.Name);
                        }
                    }
                }
            }

            // 2. Validate Layers
            if (def.Layers == null || def.Layers.Count == 0)
            {
                result.Issues.Add(new ValidationIssue("MISSING_LAYERS", "State machine contains no layers.", ValidationSeverity.Warning));
                return result;
            }

            for (int l = 0; l < def.Layers.Count; l++)
            {
                MachineLayer layer = def.Layers[l];
                if (layer == null) continue;

                // Check duplicate state node IDs
                if (layer.States != null)
                {
                    HashSet<string> seenNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int s = 0; s < layer.States.Count; s++)
                    {
                        MachineState st = layer.States[s];
                        if (st == null) continue;
                        if (!string.IsNullOrEmpty(st.NodeId))
                        {
                            if (seenNodeIds.Contains(st.NodeId))
                            {
                                result.Issues.Add(new ValidationIssue(DUPLICATE_ID, "Duplicate state node ID in layer '" + layer.Name + "': " + st.NodeId, ValidationSeverity.Error, st.NodeId));
                            }
                            else
                            {
                                seenNodeIds.Add(st.NodeId);
                            }
                        }
                    }
                }

                // Check MISSING_ENTRY: Layer must have at least one Entry state
                bool hasEntry = false;
                if (layer.States != null)
                {
                    for (int s = 0; s < layer.States.Count; s++)
                    {
                        if (layer.States[s] != null && layer.States[s].Kind == StateKind.Entry)
                        {
                            hasEntry = true;
                            break;
                        }
                    }
                }
                if (!hasEntry)
                {
                    result.Issues.Add(new ValidationIssue(MISSING_ENTRY, "Layer '" + layer.Name + "' contains no Entry state.", ValidationSeverity.Error));
                }

                // Check INVALID_CLIP_REF: Animation states must reference valid clips
                if (layer.States != null)
                {
                    for (int s = 0; s < layer.States.Count; s++)
                    {
                        MachineState st = layer.States[s];
                        if (st == null) continue;

                        if (st.Kind == StateKind.Animation)
                        {
                            if (clips == null || st.ClipIndex < 0 || st.ClipIndex >= clips.Count)
                            {
                                result.Issues.Add(new ValidationIssue(INVALID_CLIP_REF,
                                    string.Format("Animation state '{0}' references invalid or missing clip index {1}.", st.Name, st.ClipIndex),
                                    ValidationSeverity.Error, st.NodeId));
                            }
                        }
                    }
                }

                // Check Transitions
                if (layer.Transitions != null)
                {
                    for (int t = 0; t < layer.Transitions.Count; t++)
                    {
                        StateTransition trans = layer.Transitions[t];
                        if (trans == null) continue;

                        // Resolve FromState
                        MachineState fromState = null;
                        if (!string.IsNullOrEmpty(trans.FromStateId))
                        {
                            fromState = layer.FindStateById(trans.FromStateId);
                        }
                        if (fromState == null && trans.FromState >= 0 && layer.States != null && trans.FromState < layer.States.Count)
                        {
                            fromState = layer.States[trans.FromState];
                        }

                        // Resolve ToState
                        MachineState toState = null;
                        if (!string.IsNullOrEmpty(trans.ToStateId))
                        {
                            toState = layer.FindStateById(trans.ToStateId);
                        }
                        if (toState == null && trans.ToState >= 0 && layer.States != null && trans.ToState < layer.States.Count)
                        {
                            toState = layer.States[trans.ToState];
                        }

                        // Check DANGLING_TRANSITION
                        if (fromState == null)
                        {
                            result.Issues.Add(new ValidationIssue(DANGLING_TRANSITION,
                                string.Format("Transition '{0}' references missing or non-existent source state (Id={1}, NodeId='{2}').",
                                trans.TransitionId, trans.FromState, trans.FromStateId),
                                ValidationSeverity.Error, trans.FromStateId, trans.TransitionId));
                        }
                        if (toState == null)
                        {
                            result.Issues.Add(new ValidationIssue(DANGLING_TRANSITION,
                                string.Format("Transition '{0}' references missing or non-existent target state (Id={1}, NodeId='{2}').",
                                trans.TransitionId, trans.ToState, trans.ToStateId),
                                ValidationSeverity.Error, trans.ToStateId, trans.TransitionId));
                        }

                        // Check INVALID_TARGET_ENTRY
                        if (toState != null && toState.Kind == StateKind.Entry)
                        {
                            result.Issues.Add(new ValidationIssue(INVALID_TARGET_ENTRY,
                                string.Format("Transition '{0}' cannot target Entry state '{1}'. Entry states can only have outgoing transitions.",
                                trans.TransitionId, toState.Name),
                                ValidationSeverity.Error, toState.NodeId, trans.TransitionId));
                        }

                        // Check INVALID_TARGET_ANY
                        if (toState != null && toState.Kind == StateKind.Any)
                        {
                            result.Issues.Add(new ValidationIssue(INVALID_TARGET_ANY,
                                string.Format("Transition '{0}' cannot target Any state '{1}'. Any states can only have outgoing transitions.",
                                trans.TransitionId, toState.Name),
                                ValidationSeverity.Error, toState.NodeId, trans.TransitionId));
                        }

                        // Check TYPE_MISMATCH on conditions
                        List<TransitionCondition> conditionsToCheck = new List<TransitionCondition>();
                        if (trans.ConditionGroups != null && trans.ConditionGroups.Count > 0)
                        {
                            for (int g = 0; g < trans.ConditionGroups.Count; g++)
                            {
                                ConditionGroup grp = trans.ConditionGroups[g];
                                if (grp != null && grp.Conditions != null)
                                {
                                    conditionsToCheck.AddRange(grp.Conditions);
                                }
                            }
                        }
                        else if (trans.Conditions != null)
                        {
                            conditionsToCheck.AddRange(trans.Conditions);
                        }

                        for (int c = 0; c < conditionsToCheck.Count; c++)
                        {
                            TransitionCondition cond = conditionsToCheck[c];
                            if (cond == null) continue;

                            MachineInput condInput = null;
                            if (def.Inputs != null)
                            {
                                if (!string.IsNullOrEmpty(cond.InputId))
                                {
                                    condInput = def.FindInputById(cond.InputId);
                                }
                                if (condInput == null && !string.IsNullOrEmpty(cond.PropertyName))
                                {
                                    condInput = def.FindInputById(cond.PropertyName);
                                }
                                if (condInput == null && cond.InputIndex >= 0 && cond.InputIndex < def.Inputs.Count)
                                {
                                    condInput = def.Inputs[cond.InputIndex];
                                }
                            }

                            if (condInput != null && condInput.Kind == InputKind.Bool)
                            {
                                if (cond.Op == ConditionOp.Lt || cond.Op == ConditionOp.Le ||
                                    cond.Op == ConditionOp.Gt || cond.Op == ConditionOp.Ge)
                                {
                                    result.Issues.Add(new ValidationIssue(TYPE_MISMATCH,
                                        string.Format("Transition '{0}' condition on input '{1}' uses relational operator '{2}', which is invalid for Boolean inputs (only Eq and Ne are supported).",
                                        trans.TransitionId, condInput.Name, cond.Op),
                                        ValidationSeverity.Error, null, trans.TransitionId, condInput.InputId));
                                }
                            }
                        }
                    }
                }

                // Check UNREACHABLE_STATE via BFS from Entry and Any states
                if (layer.States != null && layer.States.Count > 0)
                {
                    HashSet<int> reachableIndices = new HashSet<int>();
                    Queue<int> queue = new Queue<int>();

                    for (int s = 0; s < layer.States.Count; s++)
                    {
                        MachineState st = layer.States[s];
                        if (st == null) continue;
                        if (st.Kind == StateKind.Entry || st.Kind == StateKind.Any)
                        {
                            reachableIndices.Add(s);
                            queue.Enqueue(s);
                        }
                    }

                    while (queue.Count > 0)
                    {
                        int currentIdx = queue.Dequeue();
                        if (layer.Transitions == null) continue;

                        for (int t = 0; t < layer.Transitions.Count; t++)
                        {
                            StateTransition trans = layer.Transitions[t];
                            if (trans == null) continue;

                            int fromIdx = trans.FromState;
                            if (fromIdx < 0 && !string.IsNullOrEmpty(trans.FromStateId))
                            {
                                fromIdx = layer.GetStateIndex(trans.FromStateId);
                            }

                            if (fromIdx == currentIdx)
                            {
                                int toIdx = trans.ToState;
                                if (toIdx < 0 && !string.IsNullOrEmpty(trans.ToStateId))
                                {
                                    toIdx = layer.GetStateIndex(trans.ToStateId);
                                }

                                if (toIdx >= 0 && toIdx < layer.States.Count && !reachableIndices.Contains(toIdx))
                                {
                                    reachableIndices.Add(toIdx);
                                    queue.Enqueue(toIdx);
                                }
                            }
                        }
                    }

                    for (int s = 0; s < layer.States.Count; s++)
                    {
                        MachineState st = layer.States[s];
                        if (st == null) continue;
                        if (!reachableIndices.Contains(s))
                        {
                            result.Issues.Add(new ValidationIssue(UNREACHABLE_STATE,
                                string.Format("State '{0}' (Kind={1}) cannot be reached from Entry or Any states.", st.Name, st.Kind),
                                ValidationSeverity.Warning, st.NodeId));
                        }
                    }
                }
            }

            return result;
        }
    }
}
