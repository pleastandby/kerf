---
phase: 04-easing-interpolation-graph-editor-reconstruction
plan: 02
subsystem: animation-ui
tags: [graph-editor, easing, bezier, springs, handles, presets, inspector, wpf]
status: complete

requires:
  - phase: 04-easing-interpolation-graph-editor-reconstruction
    plan: 01
    provides: EasingEngine analytical closed-form springs and 21 motion presets

provides:
  - GraphEditorCanvas defining GraphViewport coordinate system and Value Graph rendering
  - Vertical split panel timeline layout with resizable GridSplitter and toolbar toggle button
  - Unified time synchronization across track lanes and graph editor canvas with cross-cutting playhead needle
  - Auto-Fit framing ('F' key) with 15% margins and zero-span fallback guards
  - Smooth Bézier curves and analytical damped harmonic spring waveforms with settling indicators
  - Fixed keyframe node positioning with 10px circular grip knobs (Teal incoming, Amber outgoing)
  - 180° continuous collinear slope enforcement, Alt-drag breaking, and Shift angle/magnetic snapping
  - 21-preset visual grid picker popover with 48×32 miniature preview curve tiles
  - Property inspector controls for Spring physical parameters (Mass, Stiffness, Damping, Initial Velocity) + 4 presets
  - Automatic Custom Bézier transition upon handle drag or numeric coordinate edit
  - Zero-latency real-time canvas evaluator synchronization via SyncRealtimeEvaluator
  - Automated integration test suite assertions in Test #56

affects:
  - Future animation authoring workflows and state machine transition curves

actuals:
  tokens: 14500
  tasks: 3
  commits: 3
  plan_head_before: 174b86002a47a487658a0fe6453a089230ed4515
  plan_head_after: e1dad8e7b125bec8bb545ea26407b4aac4516a80

tech-stack:
  added: []
  patterns:
    - "Value Graph coordinate transformation with bidirectional TimeToX, ValueToY, YToValue, and XToTime routines"
    - "Re-entrancy guarded horizontal scroll synchronization across WPF ScrollViewers"
    - "Handle-only pointer interaction on graph canvas locking keyframe time/value nodes against accidental drift"
    - "Collinear slope alignment enforcing C1 velocity continuity across adjacent cubic segments"
    - "Visual grid popover with miniature evaluated polyline curve thumbnails for all 21 motion presets"
    - "Real-time non-destructive pose evaluation dispatched directly to RetainedSceneGraph and GPU canvas"

key-files:
  created:
    - native-windows/src/UI/GraphEditorCanvas.cs
  modified:
    - native-windows/src/UI/StudioView.cs
    - native-windows/tests/TestRunner.cs

key-decisions:
  - "D-01: Graph Editor integrates as a vertical split panel below timeline tracks with a resizable 5px GridSplitter and toolbar toggle button."
  - "D-02: Horizontal scrolling, zoom level, and playhead position are 100% synchronized across tracks and graph canvas with a cross-cutting playhead needle."
  - "D-03: Single active track focus binds curve visualization to the focused timeline property track."
  - "D-04: Value Graph features Auto-Fit framing ('F' key) calculating min/max with 15% margins, plus zero-span fallbacks."
  - "D-05: Keyframe points are fixed in time/value on the graph canvas; canvas pointer drag strictly manipulates tangent handles."
  - "D-06: 180° continuous collinear slope enforced by default for smooth C1 velocity continuity; Alt-drag breaks into independent sharp angles."
  - "D-07: Vertical tangent coordinates can freely overshoot (-inf to +inf) while horizontal time progress is clamped to [0, 1]."
  - "D-08: Tangent handles styled with 10px circular grip knobs (Teal #06B6D4 incoming, Amber #F59E0B outgoing) with Shift snap flat/45° and magnetic 0.0/1.0 snap."
  - "D-09: Spring keyframes expose physical parameters (m, k, c, v0) and 4 quick presets (Gentle, Snappy, Bouncy, Stiff) in inspector."
  - "D-10: Evaluated damped spring waveforms sampled directly from analytical ODE equation showing overshoot and ripple."
  - "D-11: 21-preset visual grid popover renders miniature curve previews; modifying any handle or numeric control point auto-switches preset to Custom Bézier."
  - "D-12: Canonical Keyframe and Track models update immediately on handle drag or inspector scrub, re-evaluating layer poses in real time."

patterns-established:
  - "GraphViewport struct encapsulates all coordinate transformations and Auto-Fit bounding logic with zero allocations."
  - "SyncRealtimeEvaluator centralizes timeline seeking, PropertyBinder non-destructive pose application, and scene graph invalidation."

requirements-completed:
  - EASE-01
  - EASE-02
  - EASE-03
  - EASE-04

coverage:
  - id: D1
    description: "Graph Editor split panel layout, GridSplitter, and horizontal scroll synchronization"
    requirement: EASE-02
  - id: D2
    description: "Value Graph Auto-Fit framing with 15% margins and zero-span safety fallbacks"
    requirement: EASE-02
  - id: D3
    description: "Draggable tangent handles with 10px Teal/Amber grip knobs and 180° continuous slope"
    requirement: EASE-03
  - id: D4
    description: "Visual 21-preset grid picker popover with miniature curve previews and auto-custom transition"
    requirement: EASE-03
  - id: D5
    description: "Spring physical parameter controls, quick presets, and real-time evaluator synchronization"
    requirement: EASE-04
---

# Phase 4 Plan 02: Easing, Interpolation & Graph Editor Reconstruction Summary

Complete reconstruction and integration of the interactive Graph Editor split panel, Value Graph coordinate viewport, smooth Bézier and analytical spring waveform rendering, draggable tangent handles with collinear continuity and Alt-break, visual 21-preset grid popover, inspector spring physical controls, and zero-latency real-time canvas evaluator synchronization in Kerf Studio.

## Highlights & Implemented Features

### 1. Split Panel Workspace & Time Synchronization (Tasks 1 & 2)
- Restructured timeline body into a 3-row vertical layout in `StudioView.cs`:
  - Row 0: Timeline tracks & layer headers
  - Row 1: 5px resizable WPF `GridSplitter` (`KerfBrand.FigmaBorderBrush`)
  - Row 2: Graph Editor canvas (`GraphEditorCanvas.cs`) and dedicated sidebar header panel
- Toolbar toggle button ("📈 Graph Editor" / "Timeline Only") allowing instant collapse or restoration of the graph panel height.
- Synchronized horizontal scrolling with `_isSyncingHorizontalScroll` re-entrancy guard preventing feedback loops.
- Synchronized zoom (`PixelsPerSecond`) and unified vertical playhead needle cutting seamlessly across both timeline tracks and the graph editor canvas.
- Single active track focus dynamically binds graph curve to the focused property track clicked in the timeline.

### 2. Value Graph Coordinate System & Tangent Handles (Task 2)
- Implemented `GraphViewport` struct with `TimeToX`, `ValueToY`, `YToValue`, `XToTime`, and `AutoFit` calculation routines.
- Auto-Fit framing calculates property min/max values with 15% margins ('F' key and button) with zero-span fallbacks for position, scale, and opacity.
- Handle-only editing on graph canvas: keyframe diamond nodes remain anchored against accidental time/value drift while pointer interaction shapes tangent handles.
- 10px circular grip knobs styled per KerfBrand tokens:
  - Incoming handle grip knob: Teal (`#06B6D4` / `MintBrush`)
  - Outgoing handle grip knob: Amber (`#F59E0B` / `AmberBrush`)
  - Whiskers: Subtle slate (`#596376`)
- Continuous collinear slope ($S_{\text{in}} = S_{\text{out}}$) enforced by default across adjacent segments for smooth C1 velocity continuity.
- Alt-drag breaks tangent handle into independent sharp angles; inspector provides explicit [ Smooth (180°) ] and [ Broken (Alt) ] toggles.
- Shift-drag snaps angles flat (0° slope) or to 45°, with magnetic snapping to 0.0 and 1.0.
- Horizontal handle progress clamped to $[0.0, 1.0]$ within segment span; vertical progress freely overshoots ($-\infty < y < +\infty$).

### 3. Visual 21-Preset Popover, Spring Controls & Real-Time Sync (Task 3)
- Built 21-preset visual grid picker popover in `StudioView.cs`:
  - 7 rows × 3 columns rendering miniature 48×32 preview curves for all 21 presets (Linear, Ease In/Out/InOut, Quad, Cubic, Quart, Quint, Back, Elastic, Bounce, Smooth).
  - Clicking any thumbnail applies canonical $(X_1, Y_1, X_2, Y_2)$ control points to the selected keyframe.
- Auto-custom transition: dragging any tangent handle or editing numeric control points in the inspector automatically updates `Keyframe.Preset` to `CustomBezier`.
- Spring inspector controls for `InterpMode.Spring` keyframes:
  - 4 quick preset buttons: [ Gentle ] [ Snappy ] [ Bouncy ] [ Stiff ]
  - 4 editable scrub fields: Mass ($m$), Stiffness ($k$), Damping ($c$), and Initial Velocity ($v_0$).
- Real-time evaluator synchronization via `SyncRealtimeEvaluator()`: modifying handles or parameters updates the canonical Keyframe and Track, dispatches `Track.Sample` at the playhead time, updates layer poses via `PropertyBinder`, and redraws canvas with zero latency.
- Comprehensive automated test fixture Section 9 added to Test #56 in `TestRunner.cs` verifying popover presets, auto-custom transition, spring physical parameter binding, and real-time pose evaluation.

## Deviations from Plan

None - plan executed strictly as specified with zero external dependencies and 100% C# 5.0 (.NET Framework 4.8) compliance.

## Self-Check: PASSED
- `native-windows/src/UI/GraphEditorCanvas.cs`: FOUND
- `native-windows/src/UI/StudioView.cs`: FOUND
- `native-windows/tests/TestRunner.cs`: FOUND
- Commit `a8bf619`: FOUND
- Commit `9a75796`: FOUND
- Commit `e1dad8e`: FOUND
- Automated test suite verification: 56 Passed, 0 Failed
