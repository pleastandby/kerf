---
status: complete
phase: 06-interactive-state-machine-engine
source:
  - 06-01-SUMMARY.md
  - 06-02-SUMMARY.md
  - 06-03-SUMMARY.md
started: "2026-11-07T09:42:00Z"
updated: "2026-11-07T09:45:00Z"
---

## Current Test

number: 8
name: Complete
expected: All tests verified.
awaiting: none

## Tests

### 1. 3-Column Split Dock Layout & Canvas Navigation
expected: Open the State Machine bottom dock tab in `KerfStudio.exe`. The editor displays 3 distinct columns: Left Inputs panel (220px), Center Graph Canvas (Star-stretching), and Right State/Transition Inspector panel (280px). The top design canvas remains visible above for live animation preview. Panning (Middle-mouse drag or Space + Left-drag) and centered zooming (Ctrl + MouseWheel or toolbar -, 100%, +, Fit) work smoothly between 25% and 250% scale.
result: pass

### 2. Left Inputs Panel: Add, Modify, Live Toggle & Delete with Undo/Redo
expected: In the Left Inputs panel, click "+ Add" to create Bool, Number, and Trigger variables. Double-click or edit their names, scrub numeric values, and click Bool badges to toggle values or Trigger badges to fire impulses without polluting CommandHistory. Press Delete or click the trash icon to remove an input; pressing `Ctrl+Z` restores the input and any dependent transition conditions without loss.
result: pass

### 3. Center Graph Canvas: Port Drag-to-Target Wire Drawing & Validation Guards
expected: Drag from the right circular port of any state node towards another node. A live dashed mint Bézier curve tracks the mouse pointer at 60 FPS. Releasing the mouse over a valid target node creates a directional transition with a directional polygon arrow aligned with the tangent vector at t = 0.5 and a centered duration pill. Attempting to connect to an Entry node or from an Exit node is blocked cleanly.
result: pass

### 4. Empty-Canvas Marquee Multi-Selection & Node Drag Batching
expected: Click and drag on an empty area of the canvas to display a dashed rectangular marquee bounding box. Releasing selects all enclosed state nodes. Dragging the selected nodes moves them smoothly across the canvas at 60 FPS without pushing history entries. Pointer release commits a single atomic `MoveStateNodesCommand`. Pressing `Ctrl+Z` reverts all selected nodes to their pre-drag coordinates in one step.
result: pass

### 5. Context-Sensitive State & Transition Inspector Panels
expected: Clicking a state node opens the State Inspector showing kind badges (Entry, Animation, Any, Exit), editable name, clip selector dropdown, playback speed scrub field, and loop toggle. Clicking a transition wire or duration pill switches the Right Inspector to the Transition Inspector showing From -> To header, duration, easing mode picker (Hold, Linear, Cubic), exit-time slider, and interruption toggle.
result: pass

### 6. Compound Condition Group Builder (OR of ANDs)
expected: In the Transition Inspector, click "+ Add Condition Group" to create condition cards separated by `— OR —` badges. Within each card, click "+ Add Condition" to create conditions separated by `AND` badges. Select input variables from the dropdown, choose comparison operators (==, !=, <, <=, >, >=), and set comparison values. Transition triggers at runtime if ANY condition group evaluates to true and within that group ALL conditions are met. All changes participate in undo/redo via `ModifyConditionGroupsCommand`.
result: pass

### 7. Non-Destructive StateMachineValidator Live Diagnostics Badge
expected: The toolbar displays a real-time status badge ("✓ Valid", "⚠ X Warnings", or "⚠ X Errors"). If an invalid state graph is constructed (e.g. missing Entry state, dangling transition, unreachable state, or relational operator on Boolean input), the badge updates immediately to amber/red with descriptive hover tooltips, all without discarding or mutating user graph data.
result: pass

### 8. Cascading Deletion & Symmetrical Topological Restoration (Ctrl+Z / Ctrl+Y)
expected: Select a state node that has multiple incoming and outgoing transitions. Press Delete or click "Delete State" in the inspector: the state and all connected transitions are removed simultaneously. Press `Ctrl+Z`: the state node and all incident transitions are restored to their exact original positions with stable GUID bindings intact. Press `Ctrl+Y`: the state and transitions are re-deleted cleanly.
result: pass

## Summary

total: 8
passed: 8
issues: 0
pending: 0
skipped: 0

## Gaps

[none]
