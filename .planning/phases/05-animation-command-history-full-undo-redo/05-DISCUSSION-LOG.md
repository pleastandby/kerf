# Phase 5: Animation Command History & Full Undo/Redo - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-06
**Phase:** 05-animation-command-history-full-undo-redo
**Areas discussed:** Cross-Mode Undo, Playhead & Selection Handling, Continuous Scrubbing Batching, Multi-Keyframe Operations

---

## Cross-Mode Undo Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| In-Place Entity Undo | Apply design undos to base entities in background without tab switch; seamless rest pose sync | ✓ |
| Strict Tab Switching | Automatically switch back to Design Mode tab whenever an undo action crosses mode boundary | |
| Mode-Isolated Stacks | Maintain two separate undo stacks for Design and Animate modes (explicitly rejected by architecture) | |

**User's choice:** In-Place Entity Undo
**Notes:** Never create competing undo stacks. Keep a single unified `CommandHistory`. If an undo reaches a Design edit while in Animate Mode, mutate the base layer directly and refresh the canvas and rest pose without forcing a mode tab switch.

---

## Playhead & Active Selection Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Silent Data Restore | Restore keyframe & track values cleanly without moving playhead or altering active selection | ✓ |
| Jump to Modified Time | Move playhead to the timestamp of the undone/redone keyframe | |
| Full State Reset | Reset selection and playhead to time 0.0s | |

**User's choice:** Silent Data Restore
**Notes:** Playhead position and active keyframe selection remain steady and undisturbed during undo/redo; data and curve geometry restore cleanly in-place.

---

## Continuous Scrubbing & Dragging Batching

| Option | Description | Selected |
|--------|-------------|----------|
| Pointer Release & Blur Commit | Preview updates live; commit single atomic command on mouse release or field blur/Enter | ✓ |
| Time-Debounced Batching | Accumulate edits over a 300ms window | |
| Explicit Keyframe Push Only | Only record history when the user clicks the keyframe diamond | |

**User's choice:** Pointer Release & Blur Commit
**Notes:** Scrubbing fields or dragging timeline keyframes/tangents previews in real-time, then pushes a single discrete `ICommand` on pointer release (`MouseLeftButtonUp`) or blur/Enter.

---

## Multi-Keyframe & Multi-Track Operations

| Option | Description | Selected |
|--------|-------------|----------|
| Atomic Composite Command | Group multi-keyframe/multi-track shifts, pastes, or deletes into one CompositeCommand | ✓ |
| Sequential Individual Commands | Push one command per keyframe modified | |

**User's choice:** Atomic Composite Command
**Notes:** Moving, copying, or deleting 5 keyframes across 2 tracks results in a single `Ctrl+Z` undo step.

---

## The Agent's Discretion

- Concrete command naming and class hierarchy (`AddKeyframeCommand`, `ModifyKeyframeValueCommand`, `MoveKeyframesCommand`, `DeleteKeyframesCommand`, `ChangeKeyframeEasingCommand`, `ModifyBezierHandlesCommand`, `ModifySpringParamsCommand`, `AddTrackCommand`, `RemoveTrackCommand`, `AddClipCommand`, `RemoveClipCommand`).
- Event dispatch wiring in `StudioView.cs` (`Undo()`, `Redo()`, `OnHistoryChanged`) to refresh timeline tracks and graph editor along with design canvas.

## Deferred Ideas

- State machine nodes, transitions, and triggers (deferred to Phase 6).
- Legacy animation file migration and deprecated binder cleanup (deferred to Phase 7).
