---
phase: 06-interactive-state-machine-engine
plan: 03
subsystem: ui-state-machine
tags:
  - state-machine
  - visual-graph-editor
  - 3-column-dock
  - port-drag-to-connect
  - canvas-marquee
  - node-drag-batching
  - condition-group-builder
  - test-runner
dependency_graph:
  requires:
    - 06-01
    - 06-02
  provides:
    - 3-column split view dock for state machine authoring
    - Addressable inputs sidebar with real-time editing and impulse preview
    - Pan/zoom canvas with centered wheel zoom and middle/space panning
    - Port drag-to-connect UX with live dashed Bézier wires and target snapping
    - Empty-canvas marquee multi-selection
    - Batched node dragging committing a single MoveStateNodesCommand on release
    - Mid-wire directional arrows aligned with tangent vectors and duration pills
    - Context-sensitive State inspector (name, kind badge, clip dropdown, speed, loop, delete)
    - Context-sensitive Transition inspector (duration, easing, exit time slider, interruption, delete)
    - Compound Condition Group Builder with OR between groups and AND within each group
    - Live StateMachineValidator diagnostics badge with tooltip summaries
    - Automated Test #60 end-to-end authoring and undo/redo suite
  affects:
    - native-windows/src/UI/StudioView.cs
    - native-windows/tests/TestRunner.cs
tech_stack:
  added:
    - 3-column expandable dock (Left Inputs 220px, Center Canvas 1* Star, Right Inspector 280px)
    - Port drag-to-connect dashed Bézier interactive wire preview
    - Cubic Bézier mid-wire tangent arrow and duration pill
    - Interactive compound condition group builder UI
  patterns:
    - Star-stretching bottom dock preserving top canvas live preview
    - 60 FPS pointer drag preview with atomic command commit on pointer release
    - Context-sensitive inspector routing for states, transitions, and layers
    - Deep-clone condition group command dispatching via CommandHistory
key_files:
  created: []
  modified:
    - native-windows/src/UI/StudioView.cs
    - native-windows/tests/TestRunner.cs
decisions:
  - "D-01: 3-column bottom dock layout hosting Left Inputs (220px), Center Graph Canvas (1* Star), and Right Inspector (280px) preserving top canvas visibility for animation preview."
  - "D-03: Compound Condition Group Builder UI supporting distinct visual cards with centered OR badges between groups and AND badges within groups, wired to ModifyConditionGroupsCommand."
  - "D-04: Port drag-to-connect drawing live dashed Bézier curve snapping to target nodes, empty-canvas marquee multi-selection, and mid-wire directional polygon arrows aligned with tangent vectors at t=0.5."
  - "D-05: Context-sensitive State node inspector displaying kind badges, editable names, clip selectors, speed scrubbing, and loop toggling."
  - "D-06: Node drag batching committing a single MoveStateNodesCommand on pointer release without flooding the undo stack."
  - "D-07: Non-destructive validator status badge in canvas toolbar displaying error and warning issue tooltips."
metrics:
  duration: 25 min
  completed: 2026-11-07
status: complete
actuals:
  tokens: 16500
  tasks: 3
  commits: 3
  plan_head_before: e1ac82b3d688a44b150937a4c7e6c518ca47c0bc
  plan_head_after: ebe75a8d29b0fcebe5ecda468bc73fc1498b31a5
---

# Phase 6 Plan 03: Interactive 3-Column Visual Graph Editor, Port Drag-to-Connect, Canvas Marquee & Inspector Panels Summary

Delivered a complete, professional, Rive-inspired 3-column visual graph editor and authoring environment for the Interactive State Machine Engine in `StudioView.cs`, verified through full end-to-end authoring automation in Test #60 with all 60 tests passing and zero regressions.

## Key Changes Implemented

1. **3-Column Bottom Dock Layout & Addressable Inputs Sidebar Panel (D-01, STATE-03):**
   - Reconstructed `CreateStateMachineControlsPanel()` into an expandable 3-column split view dock:
     - **Column 0 (Width: 220px, Left):** Addressable Inputs list sidebar panel with "+ Add Input" menu for Bool, Number, and Trigger variables.
     - **Column 1 (Width: 1* Star, Center):** Graph Canvas & Toolbar.
     - **Column 2 (Width: 280px, Right):** Context-sensitive State, Transition, and Layer Inspector.
   - Connected `_stateMachineScrollContainer = _stateMachineContainer` directly into Row 1 of `panelBorder` in `CreateTimelinePanel()` to enable 100% Star-height stretching while preserving top canvas visibility for live animation preview per D-01.
   - Designed distinct variable card styling with badges (purple ⚡ for Trigger, emerald ⏻ for Bool, blue 🎚 for Number).
   - Wired input creation to `CreateInputCommand`, name/value modifications to `ModifyInputCommand`, and deletion to `DeleteInputCommand` through `CommandHistory`.
   - Enabled direct impulse firing for triggers and live boolean value toggling without polluting `CommandHistory`, ensuring runtime transport isolation per D-01.
   - Included search / filter box (`FilterStateMachineItems`) for rapid variable lookup.

2. **Pan/Zoom Canvas, Port Drag-to-Connect Wires, Marquee Multi-Selection & Node Drag Batching (D-04, D-06, T-06-05, T-06-06):**
   - Implemented smooth centered mouse-wheel zoom (Ctrl + Wheel) anchored at pointer position and toolbar zoom buttons (-, 100%, +, Fit) using `ScaleTransform` clamped between 25% and 250% per T-06-05.
   - Implemented canvas panning via Middle-Mouse drag or Space + Left-Mouse drag updating `ScrollViewer.ScrollToHorizontalOffset` and `ScrollToVerticalOffset`.
   - Built port drag-to-connect UX: dragging from a node's output port draws a live dashed Bézier curve (`KerfBrand.MintBrush`, `StrokeDashArray = { 3, 2 }`) tracking cursor at 60 FPS; releasing over a valid target node validates connections (preventing duplicate transitions, transitions to Entry, and transitions from Exit per T-06-06) and executes `CreateTransitionCommand` through `CommandHistory`.
   - Implemented empty-canvas marquee multi-selection: dragging on empty canvas renders a dashed bounding box (`_stateGraphMarqueeVisual`) and populates `_selectedGraphStateIndices`; clicking empty canvas clears selection.
   - Implemented continuous node drag batching: dragging selected nodes updates canvas coordinates smoothly without pushing history entries; pointer release (`HandleStateGraphMouseUp`) batches all displacements into a single atomic `MoveStateNodesCommand` into `CommandHistory`.
   - Updated connection wire rendering with directional cubic Bézier curves, mid-wire polygon arrows oriented with the tangent vector at $t = 0.5$, and centered duration pills.
   - Added live `StateMachineValidator` diagnostics status badge (`_stateMachineValidationBadge`) in the toolbar with detailed issue tooltips per D-07.

3. **Context-Sensitive Inspector & Compound Condition Group Builder (D-01, D-03, D-05, D-06):**
   - **State Node Inspector:** Context-sensitive card showing kind badge (`Entry`, `Animation`, `Any`, `Exit`), editable name text box wired to `RenameStateNodeCommand`, Animation clip dropdown populated from `Document.Animation.Clips` and `sm.Clips`, speed scrub field (0.1x to 10.0x), looping checkbox toggle, and delete state button wired to `DeleteStateNodesCommand`.
   - **Transition Inspector:** Displays From ➔ To header, blend duration scrub field, easing mode picker (`Hold`, `Linear`, `Cubic`), exit time toggle and interactive slider (0.00 to 1.00), interruption checkbox toggle, and delete transition button wired to `DeleteTransitionsCommand`.
   - **Compound Condition Group Builder:** Full UI supporting OR between groups and AND within groups per D-03:
     - Group cards separated by visual `— OR —` badges.
     - Within each group: conditions separated by centered `AND` badges.
     - Input dropdown selector mapped to stable unique GUIDs (`sm_input_...`).
     - Comparison operator combo (`==`, `!=`, `<`, `<=`, `>`, `>=`).
     - Typed comparison value editors: `Fires` badge for Trigger variables, `true`/`false` dropdown for Bool variables, and numeric text box for Number variables.
     - `+ Add Condition` button inside each group (AND logic).
     - `+ Add Condition Group (OR)` button at the bottom of the transition inspector.
     - Remove buttons (`×`) on each condition and condition group.
     - All mutations committed atomically through `ModifyConditionGroupsCommand` via `CommandHistory` with deep clone state isolation.

4. **Automated Test #60 Finalization in TestRunner.cs:**
   - Completed Section 7 covering end-to-end authoring integration:
     - 7a: Verified 3-column UI model binding across all 4 state node kinds and all 3 input variable types.
     - 7b: Verified port drag-to-connect transition creation, validation guards (rejecting transition to Entry and from Exit), and command execution/undo/redo.
     - 7c: Verified Compound Condition Group Builder execution with `ModifyConditionGroupsCommand`, evaluating truth table combinations under AND-within-group and OR-between-group logic.
     - 7d: Verified full undo/redo stack integrity across a mixed authoring pipeline (`CreateStateNodeCommand`, `MoveStateNodesCommand`, `RenameStateNodeCommand`, `ModifyConditionGroupsCommand`, `CreateTransitionCommand`), performing step-by-step undo and redo and asserting exact graph state.
   - Full suite execution: **60 Passed, 0 Failed**.

## Deviations from Plan

None - plan executed exactly as written.

## Threat Model Verification

- **T-06-05 (Denial of Service - Canvas Zoom/Coordinates):** Mitigated. Zoom levels are strictly clamped between 0.25 (25%) and 2.50 (250%), and canvas panning offsets are clamped to non-negative values.
- **T-06-06 (Elevation of Privilege / Tampering - Connection Validation):** Mitigated. Port drag release checks destination and source kinds, rejecting transitions connecting to Entry as destination or connecting from Exit as source, as well as rejecting duplicate connections.
- **T-06-SC (Supply Chain / External Dependencies):** Mitigated. Built strictly with native .NET Framework 4.8 and existing assemblies; 0 external package manager dependencies.

## Self-Check: PASSED
- `native-windows/src/UI/StudioView.cs` exists and compiles cleanly under C# 5.0.
- `native-windows/tests/TestRunner.cs` exists and all 60 tests pass.
- Commits `c55e688`, `caeed12`, and `ebe75a8` are in master branch history.
