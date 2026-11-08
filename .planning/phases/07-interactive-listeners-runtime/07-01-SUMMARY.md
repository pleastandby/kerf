---
phase: 07-interactive-listeners-runtime
plan: 01
subsystem: animation-interactive-listeners
tags: [listeners, event-dispatcher, command-history, right-inspector, csharp5]
requires:
  - phase: 06-state-machine-engine
    provides: StateMachineDefinition, MachineInput, ConditionGroups, StateMachineRuntime
provides:
  - InteractiveEventDispatcher with full gesture tracking (Hover, Down, Up, Click, Tap, Drag)
  - Extended PointerEventKind and ActionKind enums (Click, Tap, DragStart, Drag, DragEnd, ToggleBool, Reset, FireTrigger)
  - NodeListener with ListenerId GUID and Enabled toggle
  - AddListenerCommand, DeleteListenerCommand, ModifyListenerCommand in CommandHistory
  - Collapsible Right Inspector "Listeners" section for document objects in Animate mode
affects:
  - 07-02-PLAN.md (StateMachineRuntime transition conditions & blend crossfade)
  - 07-03-PLAN.md (Spacebar simulation gating & live test suite)
tech-stack:
  added: []
  patterns:
    - Centralized gesture lifecycle router (InteractiveEventDispatcher)
    - Atomic reversible listener snapshot commands (NodeListenerSnapshot)
    - In-place collapsible Right Property Inspector listener cards
key-files:
  created:
    - native-windows/src/Engine/InteractiveEventDispatcher.cs
  modified:
    - native-windows/src/Engine/Animation/StateMachineTypes.cs
    - native-windows/src/Engine/Animation/StateMachineRuntime.cs
    - native-windows/src/Engine/CommandHistory.cs
    - native-windows/src/Engine/RetainedSceneGraph.cs
    - native-windows/src/UI/StudioView.cs
    - native-windows/tests/TestRunner.cs
decisions:
  - "Extended PointerEventKind and ActionKind with explicit integer enums preserving backward compatibility."
  - "Gated listener execution in StateMachineRuntime.OnPointerEvent with listener.Enabled == true check."
  - "Implemented NodeListenerSnapshot deep-cloning listener actions to guarantee transactional undo/redo integrity."
  - "Added collapsible Listeners inspector section to right panel when a canvas object is selected in Animate mode."
metrics:
  duration: 15m
  completed_date: "2026-11-08"
status: complete
actuals:
  tokens: 11250
  tasks: 3
  commits: 3
  plan_head_before: 1780197c9cddbe1d803211ab9031d8e4b989ae1d
  plan_head_after: 6ed6d239c4f74a01c3d7ea349f42fa0e6b83f5c9
---

# Phase 07 Plan 01: Centralized Event Dispatcher, Extended Listener Model & Right Inspector Panel Summary

Delivered the centralized interactive event dispatcher, extended document object listener model, transactional undo/redo commands, and the Right Property Inspector "Listeners" authoring panel with strict C# 5.0 compatibility and zero test regressions.

## Key Deliverables

1. **Extended Listener Data Model (`StateMachineTypes.cs`):**
   - Extended `PointerEventKind` with `Click = 5`, `Tap = 6`, `DragStart = 7`, `Drag = 8`, `DragEnd = 9`, and aliases `PointerDown = 0`, `PointerUp = 1`, `PointerMove = 2`, `PointerEnter = 3`, `PointerExit = 4`.
   - Extended `ActionKind` with `ToggleBool = 7`, `Reset = 8`, and alias `FireTrigger = 2`.
   - Extended `NodeListener` with permanent string GUID `ListenerId` (`sm_lst_*`), `Enabled` boolean toggle (defaulting to `true`), and deep `Clone()` support.
   - Extended `ListenerAction` with `InputId` string targeting alongside `InputIndex` and `PropertyName`.

2. **Centralized Interactive Event Dispatcher (`InteractiveEventDispatcher.cs`):**
   - Created in namespace `Kerf.Engine`.
   - Manages hover lifecycle (`PointerEnter`, `PointerExit`, `PointerMove`), press lifecycle (`PointerDown`, `PointerUp`), click gestures (`Click`, `Tap`), and drag lifecycle (`DragStart`, `Drag`, `DragEnd` using a 4px Euclidean threshold).
   - Routes hit-tested target node IDs to `StateMachineRuntime.OnPointerEvent`.

3. **Runtime Action Execution (`StateMachineRuntime.cs`):**
   - Updated `OnPointerEvent` to guard with `listener.Enabled == true`.
   - Added support for `ActionKind.ToggleBool` (toggling booleans `<= 0.5 ? 1.0 : 0.0`).
   - Added support for `ActionKind.Reset` (restoring `InitialValue` and clearing `Fired = false`).
   - Handled `ActionKind.Fire` and `ActionKind.FireTrigger` trigger execution.

4. **Transactional Undo/Redo Commands (`CommandHistory.cs`):**
   - Implemented `NodeListenerSnapshot` for deep-cloning listener target, event kind, enabled state, and action lists.
   - Implemented `AddListenerCommand : ICommand` with insert index tracking and inspector refresh callback.
   - Implemented `DeleteListenerCommand : ICommand` capturing index upon deletion and restoring exact position upon `Unexecute()`.
   - Implemented `ModifyListenerCommand : ICommand` providing full before/after snapshot reversal.

5. **Right Property Inspector Listeners Panel (`StudioView.cs`):**
   - Added `BuildListenersInspectorSection(Layer selectedLayer)` in `StudioView.cs`.
   - Displays a collapsible card container with title, listener count badge, and `+ Add` button.
   - Each listener card renders:
     * Header row: Event kind ComboBox, `Enabled` CheckBox, and Delete `✕` button.
     * Action row: `ActionKind` ComboBox (`SetBool`, `SetNumber`, `ToggleBool`, `FireTrigger`, `Reset`).
     * Input row: Bound `MachineInput` ComboBox listing definition inputs.
     * Parameter row: Boolean checkbox for `SetBool` or numeric TextBox for `SetNumber`.
   - All user additions, edits, and deletions dispatch through `CommandHistory.Execute` with canvas and inspector refresh callbacks.

6. **Automated Verification (`TestRunner.cs`):**
   - Added automated test #66 `Interactive Listeners & Centralized Event Dispatcher`.
   - Verified enum assignments, `NodeListener` defaults and cloning, runtime action execution (`ToggleBool`, `Reset`, `FireTrigger`, `Enabled` gating), `InteractiveEventDispatcher` hover/drag/click routing, and complete undo/redo symmetry for all three listener commands.
   - 66/66 tests passed with 0 failures.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] RetainedSceneGraph.HitTest empty WorldBounds fallback**
- **Found during:** Task 1 test verification
- **Issue:** When running in headless test scenarios without prior viewport rendering, `SceneVisualNode.WorldBounds` was `Rect.Empty`, causing `HitTest` to return `null`.
- **Fix:** Initialized `WorldBounds` in `SceneVisualNode` constructor from `modelNode.GetBounds()` and added fallback check in `HitTestSubtree`.
- **Files modified:** `native-windows/src/Engine/RetainedSceneGraph.cs`
- **Commit:** `f3d01c3`

**2. [Rule 1 - Bug] Duplicate switch case for Fire and FireTrigger enum alias**
- **Found during:** Task 1 compilation
- **Issue:** Because `ActionKind.FireTrigger = 2` is an alias for `ActionKind.Fire = 2`, having both in the same switch statement caused C# compiler error `CS0152`.
- **Fix:** Handled both under `case ActionKind.Fire:` with explanatory comment.
- **Files modified:** `native-windows/src/Engine/Animation/StateMachineRuntime.cs`
- **Commit:** `f3d01c3`

## Verification Results

Command:
```cmd
cmd.exe /c "cd /d native-windows && build.bat --test"
```
Output:
```
=====================================================================
 Tests Completed: 66 Passed, 0 Failed
=====================================================================
```

## Self-Check: PASSED
- Created file `native-windows/src/Engine/InteractiveEventDispatcher.cs`: FOUND
- Commit `f3d01c3`: FOUND
- Commit `1de6023`: FOUND
- Commit `6ed6d23`: FOUND
