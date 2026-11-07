# Requirements: Kerf Studio Animation Engine Reconstruction

**Defined:** 2026-11-05
**Core Value:** An industrial-grade animation engine that operates with non-destructive independent transforms, stable keyframes, visual graph curve editing, transactional undo/redo, and interactive state machines without destabilizing or corrupting the design canvas.

## v1 Requirements

Requirements for the animation engine refactor milestone. Each maps directly to roadmap phases.

### Independent Transform & Render Pipeline

- [ ] **TRANS-01**: Independent render transform data structure supporting translation, scale, rotation, skew, and anchor/origin points.
- [ ] **TRANS-02**: Canvas rendering evaluates transforms at render time without mutating underlying layer geometric dimensions (`Width`, `Height`).
- [ ] **TRANS-03**: Auto Layout containers and nested children preserve exact layout bounds during and after animated scale transforms.
- [ ] **TRANS-04**: Transform clear/reset restores original visual design bounds with zero drift.

### Core Animation Data Model & Binding

- [ ] **MODEL-01**: Stable layer targeting using unique GUID/string IDs, eliminating 32-bit hash collision vulnerabilities.
- [ ] **MODEL-02**: Strongly typed track properties (Position, Scale, Rotation, Opacity, FillColor, StrokeColor, CornerRadius) with typed interpolation routines.
- [ ] **MODEL-03**: Keyframe model with unique ID, timestamp, value, interpolation mode (Linear, Hold/Step, Bézier), and tangent control handles.
- [ ] **MODEL-04**: Robust serialization and deserialization of animation clips and tracks to/from project JSON.

### Easing & Graph Curve Editor

- [x] **EASE-01**: Robust cubic Bézier easing math supporting custom control points $(x_1, y_1, x_2, y_2)$ and standard presets (Ease In, Ease Out, Ease In-Out, Bounce, Spring).
- [x] **EASE-02**: Interactive Direct2D curve/graph editor widget displaying property curves over time.
- [x] **EASE-03**: Draggable tangent control handles allowing intuitive curve sculpting on selected keyframes.
- [x] **EASE-04**: Synchronized playhead and real-time canvas preview during curve editing.

### Command History & Full Undo/Redo

- [ ] **HIST-01**: Full undo/redo integration for keyframe operations (Add, Remove, Move, Modify Value).
- [ ] **HIST-02**: Full undo/redo integration for easing and curve handle modifications.
- [ ] **HIST-03**: Full undo/redo integration for track and clip management (Add, Delete, Reorder).
- [ ] **HIST-04**: Scrubbing and drag operations transactionally batch updates into a single atomic history command on pointer release.

### Runtime Playback & Transport

- [ ] **PLAY-01**: Comprehensive transport controls (Play, Pause, Stop, Seek, Step forward/back, Framerate configuration).
- [ ] **PLAY-02**: Isolated rest pose evaluation buffer preventing runtime playback from overwriting design canvas model properties.
- [ ] **PLAY-03**: Playback modes (Once, Loop, Ping-Pong) with frame-accurate timing.
- [ ] **PLAY-04**: Immediate rest pose recovery on playback stop or switching to design mode.

### Interactive State Machine & Logic

- [x] **STATE-01**: State machine graph model containing Entry, Exit, AnyState, and custom animation states.
- [x] **STATE-02**: Directional transitions with duration, blend curves, and conditional checks.
- [x] **STATE-03**: State machine inputs system supporting Boolean, Numeric, and Trigger variables.
- [ ] **STATE-04**: Interactive canvas event listeners (Hover Enter/Exit, Pointer Down/Up, Click) linked to state transitions.
- [ ] **STATE-05**: State blending engine supporting smooth cross-fade interpolation between active clips.
- [x] **STATE-06**: Interactive UI panel for inspecting, creating, and wiring state machine nodes and transitions.

### Verification, Migration & Deprecation

- [ ] **MIGR-01**: Automated migration converting legacy animation data structures to the new engine schema.
- [ ] **MIGR-02**: Safe deprecation and removal of old destructive property binder code once replacement is verified.
- [ ] **MIGR-03**: Comprehensive automated unit and integration test suite verifying 100% test pass rate with zero regressions across all 49 existing test cases.

## v2 Requirements

Deferred to subsequent releases. Tracked but not in current refactor milestone.

### Advanced Motion & Physics

- **ADV-01**: Full skeletal rigging and inverse kinematics (IK) bones.
- **ADV-02**: Physics-based collision simulation and gravity engine.
- **ADV-03**: Multi-track audio synchronization with waveform visualization.

## Out of Scope

Explicitly excluded to keep refactor focused and prevent scope creep.

| Feature | Reason |
|---------|--------|
| Complete application rewrite | Existing design canvas, auto layout, tools, and rendering architecture are proven and must be preserved. |
| Third-party closed animation runtimes (e.g. Rive runtime) | The engine must be 100% native, self-contained C# code without proprietary binary blobs. |
| Rewriting vector canvas tools | Canvas drawing, pen tool, and shape tools operate reliably; refactor is strictly scoped to animation subsystem. |

## Traceability

Which phases cover which requirements.

| Requirement | Phase | Status |
|-------------|-------|--------|
| TRANS-01 | Phase 1 | Pending |
| TRANS-02 | Phase 1 | Pending |
| TRANS-03 | Phase 1 | Pending |
| TRANS-04 | Phase 1 | Pending |
| MODEL-01 | Phase 2 | Pending |
| MODEL-02 | Phase 2 | Pending |
| MODEL-03 | Phase 2 | Pending |
| MODEL-04 | Phase 2 | Pending |
| EASE-01 | Phase 4 | Complete |
| EASE-02 | Phase 4 | Complete |
| EASE-03 | Phase 4 | Complete |
| EASE-04 | Phase 4 | Complete |
| HIST-01 | Phase 4 | Pending |
| HIST-02 | Phase 4 | Pending |
| HIST-03 | Phase 4 | Pending |
| HIST-04 | Phase 4 | Pending |
| PLAY-01 | Phase 5 | Pending |
| PLAY-02 | Phase 5 | Pending |
| PLAY-03 | Phase 5 | Pending |
| PLAY-04 | Phase 5 | Pending |
| STATE-01 | Phase 6 | Complete |
| STATE-02 | Phase 6 | Complete |
| STATE-03 | Phase 6 | Complete |
| STATE-04 | Phase 7 | Pending |
| STATE-05 | Phase 7 | Pending |
| STATE-06 | Phase 6 | Complete |
| MIGR-01 | Phase 7 | Pending |
| MIGR-02 | Phase 7 | Pending |
| MIGR-03 | Phase 7 | Pending |

**Coverage:**
- v1 requirements: 28 total
- Mapped to phases: 28
- Unmapped: 0 ✓

---
*Requirements defined: 2026-11-05*
*Last updated: 2026-11-05 after initialization*

