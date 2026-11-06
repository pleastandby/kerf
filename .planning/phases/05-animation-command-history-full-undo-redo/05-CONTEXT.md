# Phase 5: Animation Command History & Full Undo/Redo - Context

**Gathered:** 2026-11-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Integrate all animation authoring operations into the centralized `CommandHistory` architecture to provide unified, transactional undo/redo across both Design Mode and Animate Mode without creating competing undo stacks or corrupting document state.

**In Scope:**
- Unified history architecture using the existing `CommandHistory` (`ICommand`, `CompositeCommand`, `BeginBatch`, `EndBatch`) for both Design Mode and Animate Mode.
- Reversible animation commands:
  - Keyframe operations: `AddKeyframeCommand`, `RemoveKeyframeCommand`, `ModifyKeyframeValueCommand`, `MoveKeyframesCommand`, `DuplicateKeyframesCommand`.
  - Curve & easing operations: `ChangeKeyframeEasingCommand`, `ModifyBezierHandlesCommand`, `ModifySpringParamsCommand`.
  - Track & clip operations: `AddTrackCommand`, `RemoveTrackCommand`, `AddClipCommand`, `RemoveClipCommand`.
  - Animation properties: duration changes, loop mode changes, and clip timing adjustments.
- Continuous interaction transaction batching:
  - Dragging keyframes across timeline batches into a single atomic command upon pointer release.
  - Dragging Bézier tangent handles in the graph editor commits on pointer release.
  - Scrubbing property values in the inspector live-updates the canvas and commits a single command on pointer release or input blur/Enter.
- Multi-keyframe / multi-track atomic operations:
  - Shifting, duplicating, or deleting multiple keyframes across multiple tracks forms one unified `CompositeCommand`.
- Cross-mode undo/redo coordination:
  - Undo/redo applies seamlessly across mode boundaries; Design edits undone in Animate Mode update base entities in the background without forcing an abrupt UI tab switch.
  - Redo stack invalidation on new edits in either mode.
- Complete isolation of runtime operations:
  - Timeline scrubbing, playback clock ticks, playhead seeking, preview evaluations, and state machine evaluations NEVER produce history entries.
- UI synchronization on undo/redo:
  - Canvas scene graph, timeline tracks, keyframe positions, and graph editor tangent handles immediately refresh after undo or redo.
- Preparation for Phase 6 state machine operations (interface compatibility for node, transition, and trigger commands).
- Automated regression tests:
  - Unit tests for every animation command, undo/redo sequences, multi-selection batching, handle adjustments, and mode-switching history integrity.

**Out of Scope (Deferred to future phases):**
- State machine node graph authoring, transition wiring, and runtime triggers (Phase 6).
- Legacy animation project file migration and obsolete PropertyBinder deprecation (Phase 7).
</domain>

<decisions>
## Implementation Decisions

### Cross-Mode Undo & Scope Boundary
- **D-01: In-Place Entity Undo Across Modes**
  If an undo or redo operation targets a Design Mode modification while the user is actively working in Animate Mode (or vice versa), the command updates the base entity properties in the background without forcing an abrupt UI tab switch or disrupting the current view. The animation rest pose updates seamlessly to reflect the restored base entity geometry or style.
  — **Reversibility:** reversible

### Playhead & Selection Handling
- **D-02: Silent Data Restore on Undo/Redo**
  Executing `Undo()` or `Redo()` cleanly restores document and track/keyframe data without forcibly moving the current playhead time or deselecting the user's active keyframe or layer selection. The view refreshes its visual keyframe diamonds and curves in-place.
  — **Reversibility:** reversible

### Continuous Interaction & Scrubbing Batching
- **D-03: Pointer Release & Blur Commit**
  Continuous parameter interactions (scrubbing inspector fields, dragging keyframes along the time ruler, or sculpting Bézier tangent handles) provide real-time preview on the canvas and curve editor without pushing incremental history steps. A single atomic command recording `(oldValue, newValue)` is committed to `CommandHistory` upon `MouseLeftButtonUp` / drag completion, or when a numeric field loses focus (`LostFocus`) or receives `Enter` (`KeyDown`).
  — **Reversibility:** reversible

### Multi-Keyframe & Multi-Track Operations
- **D-04: Atomic Composite Command for Multi-Selection**
  Shifting, duplicating, copying/pasting, or deleting multiple keyframes across one or more tracks is packaged into a single `CompositeCommand`. A single `Ctrl+Z` reverses the entirety of the multi-keyframe modification atomically.
  — **Reversibility:** reversible

### the agent's Discretion
- Concrete command class names and internal field layouts matching existing `native-windows/src/Engine/CommandHistory.cs` conventions.
- Exact event hooks in `StudioView.cs` (`OnHistoryChanged`, `Undo()`, `Redo()`) to refresh timeline tracks and universal graph editor curves alongside `_sceneGraph.Synchronize`.
- Command stack depth (defaults to 100 entries, matching existing `CommandHistory` capacity).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Core References
- `native-windows/src/Engine/CommandHistory.cs` — Centralized `ICommand`, `CompositeCommand`, `CommandHistory` undo/redo stack, and existing design commands.
- `native-windows/src/Engine/Animation/AnimationTypes.cs` — `Keyframe`, `Track`, `Clip`, `InterpMode`, `CubicBezierEase`, `SpringParams`.
- `native-windows/src/Engine/Animation/EasingEngine.cs` — Closed-form Bézier solver, analytical spring solver, and preset registry.
- `native-windows/src/Engine/Animation/AnimationTimelineController.cs` — Playhead transport clock, scrubbing, playback loops, and track evaluation.
- `native-windows/src/UI/StudioView.cs` — Timeline panel, inspector scrub controls, universal graph editor, and `Undo()`/`Redo()` shortcuts.
- `native-windows/src/Model/CanonicalDocument.cs` — `Document`, `Page`, `LayerNode`, `AnimationDocument`.
- `native-windows/tests/TestRunner.cs` — Existing 58 automated tests.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CommandHistory.cs`: Proven stack architecture with `undoStack`, `redoStack`, `maxHistoryDepth`, `BeginBatch`, `EndBatch`, and `CompositeCommand`. Needs new animation commands implementing `ICommand`.
- `StudioView.Undo()` / `Redo()`: Already wired to keyboard shortcuts (`Ctrl+Z`, `Ctrl+Y`, `Ctrl+Shift+Z`). Needs to trigger timeline track and graph editor refresh when in Animate Mode.
- `Track.AddKeyframe(double time, double val, ...)`: Already handles sorting and duplicate timestamp replacement deterministically.

### Established Patterns
- Strict C# 5.0 compatibility (`csc.exe` v4.0.30319) with zero external runtime dependencies.
- Atomic commit on mouse release: dragging operations track `initialValue` / `initialTime` at drag start, and only instantiate and execute the command when the drag finishes.
- Safe undo execution: commands store exact previous state and restore it cleanly without side-effects on unrelated objects.
</code_context>
