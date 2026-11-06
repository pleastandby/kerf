# Phase 4: Easing, Interpolation and Graph Editor Reconstruction - Context

**Gathered:** 2026-11-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Build a mathematically correct easing, interpolation, and interactive Graph Editor system that connects directly to the existing animation evaluator and multi-track timeline developed in Phases 1–3.

**In Scope:**
- Centralized interpolation engine supporting Linear, Hold/Step, Cubic Bézier, Custom Bézier, and Spring interpolation across scalar, vector, and color properties.
- Full 21 mathematical easing presets: Linear, Ease In/Out/InOut, Quad (In/Out/InOut), Cubic (In/Out/InOut), Quart (In/Out/InOut), Quint (In/Out/InOut), Back, Elastic, Bounce, Smooth, and Custom Bézier.
- Cubic Bézier root solver: 8-iteration Newton-Raphson with bisection fallback solving time-progress $x(t) = \text{progress}$ to find $t$, then evaluating $y(t)$; supporting free vertical overshoot ($y < 0$ or $y > 1$).
- Deterministic analytical spring solver: Closed-form damped harmonic oscillator with Mass, Stiffness, Damping, Initial Velocity for random-access timeline scrubbing without frame-accumulation drift.
- Functional Graph Editor split panel: Vertical split directly below track lanes with resizable splitter, open by default in Animate Mode with collapse/expand toggle.
- Single-active-track focus: Displays the curve of the focused property track clicked in the timeline.
- Value Graph primary with Auto-Fit framing, vertical pan/zoom, and 'F' key to fit keyframe extents.
- Tangent handle manipulation: Smooth/collinear 180° continuous slope by default, Alt-drag to break into sharp angles, Shift to snap flat/45°, circular grip styling (Teal incoming, Amber outgoing) with 10px hit-testing.
- Handle-only editing on graph: Dragging on graph canvas shapes tangent handles; keyframe timing/value edits remain in timeline and inspector to eliminate accidental drift.
- Visual Grid Picker popover: Miniature curve previews for all 21 presets; dragging any handle auto-switches preset to "Custom Bézier".
- Graph and runtime evaluator synchronization: Canonical document model updates immediately, re-evaluating the playhead and redrawing canvas in real time.
- Automated tests: Root solver, all 21 presets, analytical springs, handle updates, evaluator synchronization.

**Out of Scope (Deferred to future phases):**
- Undo/redo integration into CommandHistory (Phase 5).
- State machine transitions and blend curves (Phase 6).
- Legacy animation property binder deprecation (Phase 7).
</domain>

<decisions>
## Implementation Decisions

### Workspace & Layout
- **D-01:** Vertical Split Panel:
  The Graph Editor is integrated as a vertical split panel directly below the track lanes within the bottom timeline area, with a resizable splitter. When entering Animate Mode, it is open by default, with a toggle button on the timeline toolbar to collapse/expand it.
- **D-02:** Unified Synchronized Time:
  Horizontal scrolling, zoom level, and playhead position are 100% synchronized between the track lanes and the Graph Editor canvas; the playhead indicator line cuts seamlessly across both views.

### Curve Visualization & Navigation
- **D-03:** Single Active Track Focus:
  The Graph Editor displays the curve for the single active property track clicked in the timeline, keeping the canvas clean and readable.
- **D-04:** Value Graph Primary with Auto-Fit Framing:
  Vertical axis displays actual property values (pixels, degrees, scale ratio, opacity %) with auto-fit framing keyframe min/max values with margins. Supports vertical drag to pan, Ctrl+wheel to zoom value axis, and 'F' key to reframe keyframes to fit. Supports toggle to Speed/Velocity Graph.
- **D-05:** Handle-Only Editing on Graph Canvas:
  Keyframe points are fixed in time and value on the graph canvas (time and values adjusted in timeline and inspector); dragging on the graph canvas is strictly dedicated to manipulating tangent control handles to avoid accidental timing/value drift.

### Tangent Handles & Constraints
- **D-06:** Aligned Tangents with Alt-Break:
  Incoming and outgoing tangent handles are aligned/collinear by default (180° continuous slope for smooth C1 velocity continuity). Alt-dragging breaks the handle into independent/sharp angles. The inspector provides explicit "Smooth" and "Broken" toggle buttons.
- **D-07:** Free Vertical Overshoot, Clamped Horizontal Time:
  Vertical tangent coordinates ($y_1, y_2$) can freely overshoot ($y < 0.0$ or $y > 1.0$) to support bouncy, anticipatory, and elastic curves. Horizontal coordinates ($x_1, x_2$) are constrained to $[0.0, 1.0]$ within the segment duration to guarantee monotonic time and prevent root-solver inversion.
- **D-08:** Tangent Handle Snapping & Styling:
  Handles feature crisp square/diamond keyframe nodes with thin whisker lines leading to circular grip knobs (Teal for incoming, Amber for outgoing) with a 10px hit-test radius. Holding Shift snaps handle angles flat (0 slope) or to 45°, with magnetic snapping to 0.0 and 1.0 values.

### Physics, Presets & Inspector
- **D-09:** Analytical Spring Solver & Segment Configuration:
  Spring interpolation uses a closed-form analytical damped harmonic oscillator solution ($m, k, c, v_0$), enabling instantaneous, deterministic random-access scrubbing without iterative simulation drift. Physical parameters (Mass, Stiffness, Damping, Initial Velocity) are exposed in the inspector alongside one-click spring presets (Gentle, Snappy, Bouncy, Stiff).
- **D-10:** Evaluated Damped Waveform Visualization:
  Spring segments in the Graph Editor are rendered by sampling the analytical damped wave equation directly, accurately showing overshoot peaks, ripple cycles, and settling threshold.
- **D-11:** Visual Grid Preset Picker & Custom Bézier Transition:
  The 21 easing presets are browsable via a visual popover grid with miniature curve previews. Modifying any tangent handle or numeric control point immediately switches the preset status to "Custom Bézier", preserving user curve edits.
- **D-12:** Canonical Data Model & Evaluator Integration:
  `Keyframe` and `Track` remain the single source of truth. Starting keyframe owns the outgoing segment interpolation (`InterpMode.Hold`, `Linear`, `Cubic`, `Spring`). `Track.Sample` calls the centralized interpolation engine, updating the canvas in real time during scrubbing and handle dragging.

### the agent's Discretion
- Mathematical formulations for the 21 presets (Quad, Cubic, Quart, Quint, Back, Elastic, Bounce standard formulas).
- Visual color palette for grid lines, curve strokes, and grip highlights (following KerfBrand guidelines: `#06B6D4` Teal, `#F59E0B` Amber, `#13161D` dark background, `#313845` subtle grid lines).
- Tolerance threshold for spring settling (e.g. 0.1% amplitude cutoff).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Core References
- `native-windows/src/Model/CanonicalDocument.cs` — `RenderTransform`, `LayerNode`, `Page`.
- `native-windows/src/Engine/Animation/AnimationTypes.cs` — `Keyframe`, `Track`, `Clip`, `InterpMode`, `PropKey`.
- `native-windows/src/Engine/Animation/CubicBezierEase.cs` — Newton-Raphson + Bisection time root solver.
- `native-windows/src/Engine/Animation/AnimationTimelineController.cs` — Transport clock, scrubbing, `Seek`, `Step`.
- `native-windows/src/Engine/Animation/PropertyBinder.cs` — Non-destructive pose application.
- `native-windows/src/UI/StudioView.cs` — Timeline panel, canvas host, splitter, and inspector panels.
- `native-windows/tests/TestRunner.cs` — Existing 55 test cases.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CubicBezierEase.cs`: Has base 8-iteration Newton-Raphson + 20-iteration bisection solver for $x(s) = u$. Needs extension to support unconstrained $y_1, y_2$ overshoot.
- `AnimationTimelineController.CurrentTime`: Centralized transport clock already evaluated during scrubbing and playback.
- `StudioView.cs`: Timeline dock panel layout, transport buttons, tracks canvas, and existing mini-preview curve canvas.

### Established Patterns
- All keyframes store unique GUID `Id` and are kept strictly sorted in `Track.Keys`.
- Duplicate timestamp resolution in `Track.AddKeyframe` updates existing keyframe values deterministically.
- Direct2D drawing and WPF Canvas styling use `KerfBrand` color tokens.

### Integration Points
- Expand `InterpMode` in `AnimationTypes.cs` to include `Spring` (and store spring parameters on `Keyframe`: `Mass`, `Stiffness`, `Damping`, `InitialVelocity`).
- Create `EasingEngine.cs` centralizing all 21 preset formulas and the analytical spring solver.
- Update `Track.Sample` to dispatch to `EasingEngine.Evaluate`.
- Create `GraphEditorCanvas` in `StudioView.cs` mounted in the vertical split panel below timeline tracks with resizable splitter.
- Add Visual Grid Picker popover in inspector.
- Add Test #53 to `TestRunner.cs` validating root solving, overshoot curves, 21 presets, springs, and evaluator consistency.

</code_context>

<specifics>
## Specific Ideas
- Visual Grid Picker for easing presets inspired by modern motion tools (e.g. After Effects Flow, Figma smart animate preview).
- Rive/After Effects style tangent handles with smooth 180° collinear constraint by default and Alt-break.
- Spring visualization rendered as actual damped oscillating curve rather than a generic line.

</specifics>

<deferred>
## Deferred Ideas
- Interactive state machine blend trees and transition curve editor (Phase 6).
- Velocity graph editing (dragging speed handles instead of value handles) - Value Graph is primary for Phase 4; speed graph mode can be expanded in Phase 6/7.
- Full undo/redo history for tangent handle manipulation (Phase 5).

</deferred>

---

*Phase: 04-Easing, Interpolation and Graph Editor Reconstruction*
*Context gathered: 2026-11-06*

