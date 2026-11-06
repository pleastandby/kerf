---
status: complete
phase: 05-animation-command-history-full-undo-redo
source:
  - 05-01-SUMMARY.md
  - 05-02-SUMMARY.md
started: "2026-11-06T18:31:30Z"
updated: "2026-11-06T18:32:00Z"
---

## Current Test

number: 7
name: Complete
expected: All tests verified.
awaiting: none

## Tests

### 1. Keyframe Creation, Value Editing & Overwrite Restoration (Ctrl+Z / Ctrl+Y)
expected: In Animate Mode, edit a property or click on the timeline to create a keyframe. Move the value. Press `Ctrl+Z` to undo: the value and visual diamond revert immediately. Press `Ctrl+Y` to redo: the modification re-applies cleanly. Creating a keyframe at an existing timestamp replaces it, and pressing `Ctrl+Z` restores the original keyframe without leaving ghost keys.
result: pass

### 2. Continuous Numeric Inspector Scrubbing & Pointer Release Batching
expected: In Animate Mode, click and continuously scrub any inspector numeric field (X, Y, W, H, Rotation, CornerRadius, ScaleX, ScaleY, Opacity). The canvas updates live at 60 FPS without generating intermediate undo entries. Releasing the mouse button or pressing Enter commits exactly one atomic undoable command. Pressing `Ctrl+Z` restores the initial pre-drag value in a single step.
result: pass

### 3. Graph Editor Tangent Handle Dragging & Atomic Commit
expected: Select an animated property in the timeline and open the Graph Editor. Click a keyframe to reveal tangent handles. Drag a tangent handle: the curve sculpts live at 60 FPS without creating history records. Releasing the pointer commits a single `ModifyBezierHandlesCommand`. Pressing `Ctrl+Z` reverts the curve handles directly to their pre-drag coordinates in one step.
result: pass

### 4. Multi-Keyframe Operations & Atomic Composite Undo
expected: Select multiple keyframes across multiple tracks using marquee or Shift-click. Move them horizontally along the timeline or press Delete. All selected keyframes update or delete simultaneously. Pressing `Ctrl+Z` restores all keyframes across all tracks to their exact original timestamps in a single undo step (CompositeCommand).
result: pass

### 5. In-Place Cross-Mode Undo (Design vs Animate Isolation)
expected: In Design Mode, select a layer and change its position or dimensions. Switch to Animate Mode without moving the playhead. Press `Ctrl+Z`. The layer's base properties and the animation rest pose update seamlessly in the background without forcing a tab switch back to Design Mode.
result: pass

### 6. Silent Data Restore (Playhead & Selection Preservation)
expected: In Animate Mode, place the playhead at a specific timestamp (e.g. 1.45s) and select a keyframe. Execute an edit on another property, then press `Ctrl+Z`. The edit is undone silently in-place: the playhead remains strictly at 1.45s (does not jump to 0.0s), and the active keyframe selection is preserved.
result: pass

### 7. Strict Runtime Transport Isolation
expected: Press Play or scrub the timeline ruler back and forth repeatedly. The animation plays or scrubs smoothly on the canvas. Inspect the Undo menu or CommandHistory: 0 history entries are created, and `CanUndo` / `CanRedo` remain completely unchanged by playback ticks or seeking.
result: pass

## Summary

total: 7
passed: 7
issues: 0
pending: 0
skipped: 0

## Gaps

[none]
