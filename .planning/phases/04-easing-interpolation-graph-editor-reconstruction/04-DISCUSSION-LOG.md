# Phase 4: Easing, Interpolation and Graph Editor Reconstruction - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-06
**Phase:** 04-easing-interpolation-graph-editor-reconstruction
**Areas discussed:** Graph Editor Workspace Layout, Curve Visualization & Multi-Track Overlay, Tangent Handle Behaviors & Constraints, Spring Physics & Easing Presets

---

## 1. Graph Editor Workspace Layout

| Option | Description | Selected |
|--------|-------------|----------|
| Timeline Toggle Button | Add a '[Graph Editor]' toggle button replacing track lanes with 2D curve canvas | |
| Split Panel (Vertical Split) | Show track lanes on top and dedicated Graph Editor below simultaneously with resizable splitter | ✓ |
| Popup / Floating Window | Open curve editor in floating modal dialog | |

**User's choice:** Split Panel with vertical split and resizable splitter. Always open in animate mode, with an option/button to collapse it on toggle.
**Time Sync:** Unified synchronized time — horizontal scrolling, zoom level, and playhead position are 100% synchronized between tracks and curve graph.

---

## 2. Curve Visualization & Multi-Track Overlay

| Option | Description | Selected |
|--------|-------------|----------|
| Value Graph Primary | Display actual property values (pixels, degrees, scale, opacity) on vertical axis with keyframe points and curve handles | ✓ |
| Value Graph Only | Focus exclusively on Value Graph | |
| Dual Mode | Split display showing both Value and Velocity curves | |

| Multi-Track Option | Description | Selected |
|--------------------|-------------|----------|
| Selected Property Tracks Overlay | Show curves for selected property tracks | |
| Single Active Track | Only display the curve of the single property track clicked in the timeline to keep the curve canvas clean | ✓ |
| Track Visibility Eye Toggles | Add eye/graph icon next to each track | |

**User's choice:** Value Graph primary with Single Active Track focus.
**Framing:** Auto-fit to keyframe extents with pan/zoom and 'F' shortcut to reframe.
**Keyframe Interaction:** Handle-only editing on graph canvas (keyframe points are fixed in time/value; time/value adjusted in timeline and inspector).

---

## 3. Tangent Handle Behaviors & Constraints

| Option | Description | Selected |
|--------|-------------|----------|
| Aligned by default with Alt-break | Handles maintain smooth 180° continuous tangent slope; Alt-dragging breaks handle for sharp velocity changes; inspector has Smooth and Broken toggle buttons | ✓ |
| Independent / Broken by default | Outgoing and incoming handles move freely | |
| Strictly Segment-Owned Handles | Each keyframe only has outgoing handle | |

**Constraints:** Free value overshoot ($y < 0$ or $y > 1$), clamped horizontal time $[0, 1]$ to prevent non-monotonic time inversion.
**Modifiers:** Free dragging by default; Shift snaps handles flat (0 slope) or 45°, with magnetic snap to 0.0 and 1.0.
**Styling:** Circular grips with whiskers (Teal incoming, Amber outgoing) with 10px hit-test radius.

---

## 4. Spring Physics & Easing Presets

| Option | Description | Selected |
|--------|-------------|----------|
| Physical Parameters + Quick Presets | Numerical inputs for Mass, Stiffness, Damping, Initial Velocity in inspector, plus quick one-click spring presets (Gentle, Snappy, Bouncy, Stiff) | ✓ |
| Physical Sliders Only | Sliders and number fields without presets | |
| Perceptual Controls | Response time and damping ratio controls | |

**Waveform Visualization:** Evaluated damped waveform — render exact analytical damped harmonic oscillator waveform in the graph canvas.
**Presets Organization:** Visual Grid Picker — popover panel showing a grid of miniature curve previews for each of the 21 presets.
**Customization:** Modifying any tangent handle or numeric control point immediately switches the preset status to "Custom Bézier".

---

## the agent's Discretion

- Standard mathematical formulas for the 21 easing presets.
- Color palette for grid lines and curve strokes conforming to KerfBrand dark theme.
- Spring settling threshold (0.1% amplitude cutoff).

---

## Deferred Ideas

- State machine transition blend curves (Phase 6).
- Velocity / speed curve editing mode (Phase 6/7).
- Undo/redo integration into CommandHistory (Phase 5).
