# Roadmap: Kerf Studio Animation Engine Reconstruction

## Overview

This roadmap defines the architectural reconstruction of the Kerf Studio animation system into an industrial-grade motion graphics engine. The refactor proceeds across 7 small, dependency-aware, independently verifiable phases. By building from a non-destructive transform foundation upward through typed data models, visual curve editing, undo/redo integration, isolated runtime evaluation, and interactive state machines, the application gains professional motion capabilities while strictly preserving the existing design editor, auto layout, and canvas interaction pipeline.

## Phases

- [x] **Phase 1: Independent Transform Pipeline & Render Separation** - Introduce decoupled render transforms so animation never destructively mutates layer dimensions or Auto Layout bounds.
- [x] **Phase 2: Independent Transform Architecture and Animation Evaluator** - Eliminate destructive animation transformations and establish a reliable evaluation pipeline with hierarchical matrix composition, centralized clock, and renderer integration.
- [x] **Phase 3: Timeline and Keyframe Engine Reconstruction** - Build a structured, multi-track animation timeline with expandable object lanes, time ruler, stable keyframes, scrubbing, and inspector integration.
- [x] **Phase 4: Easing Engine & Interactive Graph Curve Editor** - Build cubic Bézier easing math, curve presets, and a canvas graph editor with draggable tangent handles.
- [x] **Phase 5: Animation Command History & Full Undo/Redo** - Integrate all timeline and keyframe operations into the centralized `CommandHistory` architecture.
- [ ] **Phase 6: Interactive State Machine Engine** - Full interactive state machine runtime featuring states, transitions, blend curves, inputs (Boolean/Number/Trigger), and canvas event listeners.
- [ ] **Phase 7: Verification, Legacy Migration & Safe Deprecation** - End-to-end integration tests, legacy project file migration, and deprecation of old property binders with 0 regressions.

## Phase Details

### Phase 1: Independent Transform Pipeline & Render Separation
**Goal**: Decouple render transformations from document layout bounds so animating scale, translation, rotation, and skew operates non-destructively without altering `Layer.Width`, `Layer.Height`, or Auto Layout frames.
**Depends on**: Nothing (first phase)
**Requirements**: TRANS-01, TRANS-02, TRANS-03, TRANS-04
**Success Criteria** (what must be TRUE):
  1. Setting or animating a layer's scale (e.g. 0.5x, 2.5x) visually scales its rendered appearance without altering its underlying `layer.Width` or `layer.Height`.
  2. Auto Layout containers containing scaled layers maintain their computed flexbox bounds and item distribution without distortion or clipping errors.
  3. Resetting or clearing a layer's render transform restores its exact original visual bounds with zero drift.
  4. All 49 existing test fixtures pass without regression.
**Plans**: 2 plans

Plans:
- [x] 01-01: Implement `LayerTransform` / `MatrixTransform` model with translation, scale, rotation, skew, and anchor origin support.
- [x] 01-02: Integrate render transform evaluation into Direct2D canvas renderer and layer bounds calculation while preserving Auto Layout geometry.

---

### Phase 2: Independent Transform Architecture and Animation Evaluator
**Goal**: Eliminate destructive animation transformations and establish a reliable animation evaluation pipeline with hierarchical matrix composition, centralized clock, and renderer integration.
**Depends on**: Phase 1
**Requirements**: TRANS-EVAL-01, TRANS-EVAL-02, TRANS-EVAL-03, TRANS-EVAL-04, TRANS-EVAL-05, TRANS-EVAL-06, TRANS-EVAL-07, TRANS-EVAL-08, TRANS-EVAL-09
**Success Criteria** (what must be TRUE):
  1. Original object geometry (`layer.X`, `Y`, `Width`, `Height`, Bézier anchors) remains strictly separated and unmutated by static transforms and animated transforms.
  2. Independent animation properties supported: Position X/Y, Scale X/Y, Rotation, Anchor X/Y, Opacity, Width, Height.
  3. Scaling a Frame transforms its entire visual content rather than modifying underlying child geometry.
  4. Groups and nested Frames compose transforms correctly via hierarchical matrix multiplication ($\mathbf{M}_{\text{world}} = \mathbf{M}_{\text{parent}} \times \mathbf{M}_{\text{local}}$).
  5. Auto Layout behavior is preserved with layout dimensions strictly distinguished from visual transforms.
  6. A deterministic animation evaluator calculates property values at any timestamp without coupling to UI/React render cycles.
  7. A centralized animation clock supports Play, Pause, Stop, Looping (OneShot, Loop, PingPong), Speed, and frame navigation.
  8. Evaluated transforms integrate cleanly with the canvas renderer (`GpuRenderer` and `RetainedSceneGraph`) without mutating persistent document models.
  9. Automated verification tests pass covering transform composition, scaling, rotation, anchors, nested objects, and playback evaluation.
**Plans**: 2 plans

Plans:
- [x] 02-01: Implement hierarchical transform composition for Frames & Groups, independent 8-channel property model, and renderer integration.
- [x] 02-02: Build deterministic animation evaluator, centralized animation clock, and automated verification tests.

---

### Phase 3: Timeline and Keyframe Engine Reconstruction
**Goal**: Build a structured, reliable, multi-track animation authoring timeline featuring expandable object lanes, time ruler, frame snapping, stable keyframes, scrubbing, and transform inspector integration.
**Depends on**: Phase 2
**Requirements**: TIME-01, TIME-02, TIME-03, TIME-04, TIME-05, TIME-06, TIME-07, TIME-08
**Success Criteria** (what must be TRUE):
  1. Multiple animated objects and independent property tracks (`PositionX/Y`, `ScaleX/Y`, `Rotation`, `AnchorX/Y`, `Opacity`, `VisualWidth/Height`) can be viewed, expanded, and collapsed in the timeline.
  2. Time ruler displays frame numbers and time marks; playhead scrubbing updates the canvas in real time using the Phase 2 evaluator.
  3. Stable keyframe CRUD: create, select, multi-select, move preserving relative timing, duplicate, copy/paste, and delete with stable IDs.
  4. Animate Mode transform inspector displays current evaluated values with diamond keyframe toggles and jump navigation.
  5. Dragging keyframes or scrubbing playhead executes with high performance without unneeded UI re-renders or model corruption.
  6. Automated test fixtures pass covering keyframe CRUD, multi-selection, shifting, duplicate timestamps, clipboard, and evaluator integration.
**Plans**: 3 plans

Plans:
- [x] 03-01: Implement core keyframe data architecture, stable GUIDs, property tracks, keyframe CRUD, clipboard, and sorting.
- [x] 03-02: Build hierarchical timeline authoring UI, expandable object lanes, time ruler, frame snapping, and multi-keyframe dragging.
- [x] 03-03: Integrate Animate Mode transform inspector, keyframe toggles, and automated verification tests (Test #52).

---

### Phase 4: Easing Engine & Interactive Graph Curve Editor
**Goal**: Build a precision cubic Bézier easing engine with standard presets and an interactive Direct2D canvas curve/graph editor widget with draggable tangent handles.
**Depends on**: Phase 3
**Requirements**: EASE-01, EASE-02, EASE-03, EASE-04
**Success Criteria** (what must be TRUE):
  1. Cubic Bézier curves evaluate accurately for custom $(x_1, y_1, x_2, y_2)$ control points and match standard presets (Ease In, Ease Out, Ease In-Out, Bounce, Spring).
  2. A dedicated Graph Editor view renders editable velocity and value curves for active property tracks.
  3. Users can select keyframes in the graph editor and interactively drag tangent handles to shape transition curves.
  4. Scrubbing or playback accurately reflects easing curves in real-time on the canvas.
**Plans**: 2 plans

Plans:
- [x] 04-01-PLAN.md — Mathematical easing engine, closed-form analytical springs, 21 presets, and Track.Sample dispatch
- [x] 04-02-PLAN.md — Interactive Graph Editor split panel, Value Graph framing, tangent handle dragging, and preset grid popover

---

### Phase 5: Animation Command History & Full Undo/Redo
**Goal**: Integrate all timeline, keyframe, curve manipulation, and track modifications into the centralized `CommandHistory` architecture.
**Depends on**: Phase 3, Phase 4
**Requirements**: HIST-01, HIST-02, HIST-03, HIST-04
**Success Criteria** (what must be TRUE):
  1. Adding, moving, deleting, or editing keyframe values can be undone (`Ctrl+Z`) and redone (`Ctrl+Y`) with immediate canvas and timeline synchronization.
  2. Modifying Bézier tangent handles in the graph editor creates atomic undo entries on pointer release.
  3. Adding or removing tracks and clips participates fully in undo/redo history without desynchronizing the scene graph.
  4. Continuous timeline scrubbing or dragging batches into a single discrete undoable command.
**Plans**: 2 plans

Plans:
- [x] 05-01-PLAN.md — Core reversible animation commands, CompositeCommand transaction batching, and Test #59 scaffold
- [x] 05-02-PLAN.md — UI pointer release batching, cross-mode in-place undo, silent data restore, and runtime isolation

---

### Phase 6: Interactive State Machine Engine
**Goal**: Implement a complete interactive state machine runtime featuring states, transitions, blend curves, inputs (Boolean, Number, Trigger), and canvas event listeners.
**Depends on**: Phase 5
**Requirements**: STATE-01, STATE-02, STATE-03, STATE-04, STATE-05, STATE-06
**Success Criteria** (what must be TRUE):
  1. Users can construct state machine graphs containing Entry, AnyState, and custom animation states connected by directional transitions.
  2. Transitions evaluate conditional logic against Boolean, Number, and Trigger state machine inputs.
  3. Canvas pointer interactions (Hover In, Hover Out, Pointer Down, Pointer Up, Click) trigger state inputs and fire transitions.
  4. Transition cross-fading blends smoothly between active animation clips without visual popping or geometry snaps.
  5. An interactive node graph UI panel allows visual inspection, state creation, and transition wiring.
**Plans**: 3 plans

Plans:
- [ ] 06-01: Build state machine graph data model, inputs system, and condition evaluation engine.
- [ ] 06-02: Implement state transition runtime with clip cross-fade blending and canvas event listener integration.
- [ ] 06-03: Create interactive State Machine visual node graph editor panel in Kerf Studio UI.

---

### Phase 7: Verification, Legacy Migration & Safe Deprecation
**Goal**: Validate end-to-end animation workflows, migrate legacy project files to the new schema, and safely deprecate legacy property binders with zero regressions.
**Depends on**: Phase 6
**Requirements**: MIGR-01, MIGR-02, MIGR-03
**Success Criteria** (what must be TRUE):
  1. Existing project files containing legacy animation data automatically convert into the new clip and track data model.
  2. Old destructive `PropertyBinder` scaling code is cleanly deprecated and removed without affecting design tools.
  3. Automated test suite expands to cover transforms, keyframe interpolation, easing mathematics, undo/redo, and state machine transitions.
  4. 100% test pass rate achieved across all test suites with 0 regressions.
**Plans**: 2 plans

Plans:
- [ ] 07-01: Build legacy animation data migrator and deprecate obsolete prototype binders.
- [ ] 07-02: Create automated test suites for the complete animation and state machine engine; verify all tests pass.

---

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Independent Transform Pipeline & Render Separation | 2/2 | Complete | 2026-11-05 |
| 2. Independent Transform Architecture and Animation Evaluator | 2/2 | Complete | 2026-11-05 |
| 3. Timeline and Keyframe Engine Reconstruction | 3/3 | Complete | 2026-11-06 |
| 4. Easing Engine & Interactive Graph Curve Editor | 2/2 | Complete | 2026-11-06 |
| 5. Animation Command History & Full Undo/Redo | 0/2 | Ready for planning | - |
| 6. Interactive State Machine Engine | 0/3 | Not started | - |
| 7. Verification, Legacy Migration & Safe Deprecation | 0/2 | Not started | - |

---
*Roadmap defined: 2026-11-05*
*Last updated: 2026-11-05 after initialization*

