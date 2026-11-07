---
phase: 06-interactive-state-machine-engine
plan: 01
subsystem: animation-state-machine
tags:
  - state-machine
  - validation-engine
  - condition-groups
  - stable-guids
  - automated-testing
dependency_graph:
  requires:
    - Phase 5: Animation Command History & Full Undo/Redo
  provides:
    - Stable string GUID data model for state machine entities (MachineState, StateTransition, MachineInput, ConditionGroup)
    - Compound condition group evaluation (OR of ANDs)
    - StateMachineValidator non-destructive diagnostics engine
    - Test #60 automated regression harness
  affects:
    - native-windows/src/Engine/Animation/StateMachineTypes.cs
    - native-windows/src/Engine/Animation/StateMachineRuntime.cs
    - native-windows/src/Engine/Animation/StateMachineValidator.cs
    - native-windows/tests/TestRunner.cs
tech_stack:
  added:
    - Kerf.Engine.Animation.StateMachineValidator
    - Kerf.Engine.Animation.ConditionGroup
  patterns:
    - Dual-mode integer accessors and index adapters for stable GUID entities
    - Compound boolean evaluation tree (OR between groups, AND within a group)
    - Non-destructive diagnostics validation pipeline
key_files:
  created:
    - native-windows/src/Engine/Animation/StateMachineValidator.cs
  modified:
    - native-windows/src/Engine/Animation/StateMachineTypes.cs
    - native-windows/src/Engine/Animation/StateMachineRuntime.cs
    - native-windows/tests/TestRunner.cs
decisions:
  - "D-02: Permanent stable string GUIDs (sm_node_*, sm_trans_*, sm_input_*, sm_cg_*) with dual-mode integer accessors and index adapters preserving 100% backward compatibility for existing serialized schemas and Test #46."
  - "D-03: Compound ConditionGroup model evaluating transitions where ANY group evaluates to true (OR between groups) and ALL conditions within a group evaluate to true (AND within a group), maintaining automatic trigger clearing after 1 update frame."
  - "D-05: Enhanced MachineState model supporting Entry, Animation (clip binding, speed, loop), Any, and Exit first-class node kinds with independent canvas coordinates."
  - "D-07: Non-destructive StateMachineValidator performing read-only graph integrity analysis and reporting diagnostic codes (MISSING_ENTRY, DANGLING_TRANSITION, INVALID_TARGET_ENTRY, INVALID_TARGET_ANY, UNREACHABLE_STATE, INVALID_CLIP_REF, TYPE_MISMATCH)."
metrics:
  duration: 12 min
  completed: 2026-11-07
status: complete
actuals:
  tokens: 11250
  tasks: 2
  commits: 2
  plan_head_before: a0d73bc8e680f4272dc3176f9e9737900f7fe05f
  plan_head_after: ccf4af7ee7988e160e550d396d05e5746131a5d8
---

# Phase 6 Plan 01: Core State Machine Graph Model, Compound Condition Groups, Validation Engine & Test #60 Scaffold Summary

Delivered the stable string GUID entity model, compound condition group evaluator (OR between groups, AND within each group), non-destructive StateMachineValidator diagnostics engine, and automated Test #60 harness scaffold with zero regressions across all 60 test fixtures.

## Key Changes Implemented

1. **Permanent Stable String GUID Data Model (D-02):**
   - Implemented formatted stable GUIDs across all state machine entities:
     - `MachineState.NodeId`: `sm_node_<guid>`
     - `StateTransition.TransitionId`: `sm_trans_<guid>`
     - `MachineInput.InputId`: `sm_input_<guid>`
     - `ConditionGroup.Id`: `sm_cg_<guid>`
   - Added dual-mode transparent index adapters (`FindStateById`, `FindInputById`, `GetStateIndex`, `GetInputIndex`, `GetStateNodeId`, `GetInputId`) across `MachineLayer` and `StateMachineDefinition` allowing bidirectional resolution by integer index or stable string GUID.
   - Enhanced `MachineState` with first-class node kinds (`Entry`, `Animation`, `Any`, `Exit`), `Speed` (default 1.0), `Loop` (default true), and independent `GraphX` and `GraphY` canvas coordinates (default -1.0) per D-05.
   - Guaranteed full backward compatibility with legacy schemas and Test #46 by preserving all existing integer ID properties and constructor overloads.

2. **Compound Condition Groups & Runtime Evaluator (D-03):**
   - Created `ConditionGroup` class holding `Id` and `List<TransitionCondition> Conditions`.
   - Updated `StateTransition` with `List<ConditionGroup> ConditionGroups` and a backward-compatible `Conditions` adapter property that automatically synchronizes with `ConditionGroups[0]`.
   - Enhanced `StateTransition.EvaluateConditions()` and `StateMachineRuntime.CanTransition()` to evaluate compound logic: a transition fires if ANY group evaluates to true (OR between groups), where within each group ALL individual conditions must evaluate to true (AND within a group).
   - Preserved automatic clearing of trigger inputs (`input.Fired = false`) at the end of each `StateMachineRuntime.Update()` tick.
   - Implemented NaN/Infinity sanitization on numeric inputs and transition condition comparisons per T-06-02.

3. **Non-Destructive StateMachineValidator Engine (D-07):**
   - Created `StateMachineValidator` in `native-windows/src/Engine/Animation/StateMachineValidator.cs` returning `StateMachineValidationResult` containing structured `ValidationIssue` reports (`Error`, `Warning`, `Info`).
   - Implemented non-destructive diagnostic rules that evaluate definitions in read-only mode without mutating or discarding user data:
     - `MISSING_ENTRY`: Detects layers lacking an Entry state.
     - `DANGLING_TRANSITION`: Catches transitions pointing to non-existent source or target IDs/indices.
     - `INVALID_TARGET_ENTRY`: Detects transitions targeting an Entry state.
     - `INVALID_TARGET_ANY`: Detects transitions targeting an Any state.
     - `UNREACHABLE_STATE`: BFS graph traversal from Entry and Any states identifying isolated/unreachable state nodes.
     - `INVALID_CLIP_REF`: Flags animation states referencing missing or out-of-range clip indices.
     - `TYPE_MISMATCH`: Flags relational comparison operators (`<`, `>`, `<=`, `>=`) configured against Boolean inputs.
     - `DUPLICATE_ID`: Detects duplicate input or node IDs.

4. **Automated Test #60 Harness Scaffold:**
   - Implemented `TestInteractiveStateMachineEngine` in `native-windows/tests/TestRunner.cs` and registered it in `Main()`.
   - Verified stable GUID generation and index adapter resolution.
   - Verified compound condition groups where Group 1 fails and Group 2 succeeds (evaluating true via OR) and where an individual condition inside a group fails (evaluating false via AND).
   - Verified trigger auto-clearing after 1 update frame.
   - Verified that `StateMachineValidator` accurately detects `MISSING_ENTRY`, `DANGLING_TRANSITION`, `INVALID_TARGET_ENTRY`, `INVALID_TARGET_ANY`, `UNREACHABLE_STATE`, and `TYPE_MISMATCH` while strictly maintaining definition model immutability.
   - Full test suite execution: **60 Passed, 0 Failed**.

## Deviations from Plan

None - plan executed exactly as written.

## Self-Check: PASSED

- [x] `native-windows/src/Engine/Animation/StateMachineTypes.cs` exists and contains stable GUIDs, ConditionGroup, and index adapters.
- [x] `native-windows/src/Engine/Animation/StateMachineRuntime.cs` exists and implements ConditionGroup evaluation and trigger clearing.
- [x] `native-windows/src/Engine/Animation/StateMachineValidator.cs` exists and implements all D-07 diagnostic rules.
- [x] `native-windows/tests/TestRunner.cs` includes Test #60 and reports 60 passed, 0 failed.
- [x] Commit `7442d6f` exists: feat(06-01): implement stable GUID data model, condition groups and runtime tracer
- [x] Commit `ccf4af7` exists: feat(06-01): implement non-destructive state machine validator and Test #60 scaffold
