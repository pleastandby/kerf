---
phase: 05-animation-command-history-full-undo-redo
plan: 02
subsystem: animation
tags: [command-history, undo-redo, scrub-batching, graph-editor, tangent-handles, cross-mode-undo, silent-restore, runtime-isolation]
status: complete

requires:
  - phase: 05-animation-command-history-full-undo-redo
    provides: 11 reversible animation commands, CompositeCommand, CommandHistory

provides:
  - Continuous numeric inspector scrub batching with onDragComplete pointer release and blur commit (D-03, HIST-04)
  - Aspect ratio locked paired dimensions batching into CompositeCommand (D-04)
  - GraphEditorCanvas tangent handle drag commit on pointer release (D-03, HIST-02)
  - Preset picker popover commit via ChangeKeyframeEasingCommand
  - Tangent mode toggle (Smooth vs Broken) commit via ModifyBezierHandlesCommand
  - Timeline multi-keyframe operations (Move, Delete, Duplicate) commit atomically via CommandHistory
  - In-place entity undo across modes without UI tab switching (D-01)
  - Silent data restore preserving playhead time and keyframe selection (D-02)
  - Strict runtime transport isolation (ticks, seeks, scrubs never enter history)
  - Comprehensive Test #59 verifying continuous interaction batching, cross-mode synchronization, and runtime isolation

affects: []

actuals:
  tokens: 32000
  tasks: 3
  commits: 3
  plan_head_before: 21ec21a
  plan_head_after: 1e17ac4

tech-stack:
  added: []
  patterns:
    - "Pointer release & blur transaction commit lifecycle (onDragComplete)"
    - "60 FPS live canvas evaluation during continuous scrub without history pollution"
    - "Symmetric in-place rest pose update and playhead seek on cross-mode undo/redo"
    - "Non-destructive selection set pruning and playhead timestamp preservation"

key-files:
  created: []
  modified:
    - native-windows/src/Engine/CommandHistory.cs
    - native-windows/src/UI/ScrubControls.cs
    - native-windows/src/UI/GraphEditorCanvas.cs
    - native-windows/src/UI/StudioView.cs
    - native-windows/tests/TestRunner.cs

key-decisions:
  - "D-01: In-Place Entity Undo across modes synchronizes _animationRestPose and evaluates pose without forced tab switching."
  - "D-02: Silent data restore preserves playhead CurrentTime and active keyframe selection across undo/redo."
  - "D-03: Continuous UI interactions update live previews at 60 FPS and commit atomic commands only upon pointer release or focus loss."
  - "D-04: Multi-selection operations and linked property changes batch atomically into a single CompositeCommand."

patterns-established:
  - "ScrubControls and GraphEditorCanvas capture drag start snapshots, update live preview on move, and dispatch commit events on mouse up."
  - "StudioView.Undo() and Redo() store preserved playhead time, execute history, capture rest pose in Animate mode, seek back to preserved time, and prune deleted keys."
  - "Strict runtime transport isolation guarantees playback ticks and playhead seeking never push history entries."

requirements-completed:
  - HIST-01
  - HIST-02
  - HIST-03
  - HIST-04
---

# Phase 5 Plan 02: Continuous UI Transaction Batching, Cross-Mode In-Place Undo & Runtime Isolation Summary

Inspector scrubbing and field editing transactionally commit on pointer release and blur; Graph Editor tangent handle dragging commits on pointer release; in-place entity undo across modes updates base entities and rest poses without tab switching; silent data restore preserves playhead position and active selections on undo/redo; and runtime transport operations remain strictly isolated from CommandHistory.

## Key Changes Implemented

### Task 1: Inspector Numeric Scrub & Blur Transaction Batching
- **`ScrubControls.cs`**: Implemented `onDragComplete` lifecycle across numeric text box fields and sliders. On `MouseLeftButtonUp`, `LostMouseCapture`, `LostFocus`, and `KeyDown(Enter)`, `onDragComplete` is triggered to commit changes after continuous interaction concludes.
- **`StudioView.cs`**:
  - Implemented `AutoKeyPropertyLivePreview` providing 60 FPS canvas updates during continuous dragging without pushing history commands.
  - Implemented `CommitKeyframeValueChange` creating `ModifyKeyframeValueCommand` or `AddKeyframeCommand` only upon drag completion or focus loss.
  - Wired X, Y, Width, Height, Rotation, CornerRadius, ScaleX, ScaleY, AnchorX, AnchorY, and Opacity inspector property fields to live preview during drag and commit on release.
  - Linked aspect-ratio locked properties (Width & Height) to batch atomically into a `CompositeCommand` via `History.BeginBatch` / `EndBatch` (D-04).
- **`CommandHistory.cs`**: Added `ModifyBoundsCommand`, `ModifyRotationCommand`, and `ModifyOpacityCommand` for reversible Design Mode transformations, and exposed `IsBatching`.
- **`TestRunner.cs` Test #59**: Added verification asserting that 50 continuous scrub events create 0 history commands, pointer release creates exactly 1 command, single `Undo()` restores initial value, and aspect-ratio locked edits undo in 1 step.

### Task 2: Graph Editor Tangent Handle Drag Commit & Multi-Keyframe Pointer Release
- **`GraphEditorCanvas.cs`**:
  - Captured initial Bézier handle coordinates in `KeyframeBezierSnapshot` upon `OnMouseLeftButtonDown` / `StartHandleDrag`.
  - Allowed live curve sculpting at 60 FPS during `OnMouseMove` / `MoveHandleDrag` without creating history commands.
  - On `OnMouseLeftButtonUp` / `CompleteHandleDrag`, compared snapshots and fired `TangentHandleDragCommitted` / `TangentHandlesDragCommitted` only if handles changed.
- **`StudioView.cs`**:
  - Wired `GraphEditor.TangentHandleDragCommitted` to execute `ModifyBezierHandlesCommand` via `History.Execute()`.
  - Replaced direct preset application in the 21-preset picker popup with `History.Execute(new ChangeKeyframeEasingCommand(...))`.
  - Updated `SetKeyframeTangentMode(bool smooth)` to execute `ModifyBezierHandlesCommand` with before and after handle snapshots.
  - Enhanced `HandleInterpolationGraphMouseDown` and `MouseUp` to capture initial handle snapshot on down, live-update without dirtying history on move, and execute `ModifyBezierHandlesCommand` on up.
  - Updated `DuplicateSelectedTimelineKeyframes` to execute `DuplicateKeyframesCommand` with owners and chronological sorting.
- **`TestRunner.cs` Test #59**: Added automated integration tests verifying:
  - 20 move events during tangent handle drag produce 0 commands; pointer release produces exactly 1 `ModifyBezierHandlesCommand`; single `Undo()` restores original handles.
  - Easing preset application via `ChangeKeyframeEasingCommand` reverses in 1 step to restore prior preset and control points.
  - Multi-keyframe deletion across 2 tracks with 4 keyframes reverses in 1 step to restore all 4 keyframes to their respective tracks.

### Task 3: In-Place Cross-Mode Undo, Silent Data Restore, Runtime Isolation & Verification
- **`StudioView.cs`**:
  - Enhanced `Undo()` and `Redo()` with `PostHistoryUndoRedo`:
    - **In-Place Entity Undo across modes (D-01)**: When in Animate Mode, undoing a Design Mode modification updates `_animationRestPose` in-place for all document layers, re-evaluates the active clip at the current playhead time, synchronizes the scene graph, and refreshes the canvas, timeline, and graph editor without tab switching or altering the active mode.
    - **Silent Data Restore (D-02)**: Preserves `CurrentTime` across Undo/Redo without resetting playhead to 0.0s, and calls `PruneSelectedTimelineKeyframes()` to clean deleted keyframes while preserving persisting selection sets.
  - Verified **Runtime Transport Isolation**: Playback clock ticks (`_animationTimer.Tick`), timeline seeking (`Seek`), timeline ruler scrubbing (`ScrubTimelineAtPoint`), and preview evaluations never push commands or touch the undo stack.
  - Verified **Strict Mode Isolation**: Design Mode operations mutate `LayerNode` and page models directly and never create animation tracks or keyframes.
- **`TestRunner.cs` Test #59**: Added comprehensive assertions verifying:
  - D-01: Layer moved to X=200 in Design Mode -> switch to Animate Mode -> `studio.Undo()` -> asserts `layer.X == 100`, `restPose.X == 100`, and `CurrentStudioMode == StudioMode.Animate`.
  - D-02: Playhead set to 1.45s with selected keyframe -> execute `studio.Undo()` -> asserts `CurrentTime == 1.45s` and active keyframe selection is intact.
  - Runtime Isolation: 100 clock ticks, 50 playhead seeks, and 20 timeline scrubbing updates produce `CanUndo == false` and `undoStack.Count == 0`.
  - Strict Mode Isolation: 10 design property modifications on `LayerNode` result in `defaultClip.Tracks.Count == 0`.
  - Redo Invalidation: Executing command C after undoing command B clears the redo stack (`CanRedo == false`).

## Verification Results

- Automated test harness: `cmd /c "cd native-windows && build.bat --test"`
- **59 of 59 tests passing (0 failures)**.
- Test #59 ("Animation Command History, Full Undo/Redo & Runtime Isolation") verifies all 11 animation commands, continuous interaction batching (inspector & graph editor), composite batching, in-place cross-mode undo (D-01), silent data restore (D-02), and runtime isolation.

## Self-Check: PASSED

- All 5 modified files exist on disk:
  - `native-windows/src/Engine/CommandHistory.cs`: FOUND
  - `native-windows/src/UI/ScrubControls.cs`: FOUND
  - `native-windows/src/UI/GraphEditorCanvas.cs`: FOUND
  - `native-windows/src/UI/StudioView.cs`: FOUND
  - `native-windows/tests/TestRunner.cs`: FOUND
- All 3 atomic task commits exist in git history:
  - `d35bfa5`: FOUND (Task 1)
  - `6c768fd`: FOUND (Task 2)
  - `1e17ac4`: FOUND (Task 3)
