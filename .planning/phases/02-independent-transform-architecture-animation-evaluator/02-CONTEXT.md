# Phase 2: Independent Transform Architecture and Animation Evaluator - Context

**Gathered:** 2026-11-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Eliminate destructive animation transformations and establish a reliable, non-destructive animation evaluation pipeline with decoupled transform state, hierarchical matrix composition, a centralized animation clock, and renderer integration that strictly preserves Auto Layout and document rest geometry.

**In Scope:**
- Tri-layer separation: Original object geometry vs. Static document transforms vs. Animated transforms.
- Independent animation properties: Position X/Y, Scale X/Y, Rotation, Anchor X/Y, Opacity, Width, and Height.
- Frame content scaling: Scaling a Frame transforms its entire content visually via hardware visual composition rather than rewriting child geometry.
- Groups and nested Frames transform composition: Hierarchical matrix math ($\mathbf{M}_{\text{world}} = \mathbf{M}_{\text{parent}} \times \mathbf{M}_{\text{local}}$).
- Auto Layout preservation: Distinguish layout dimensions from visual transforms so animation never retriggers or distorts flexbox layout.
- Deterministic animation evaluator: Pure functional calculation of properties at any timestamp.
- Centralized animation clock: Play, Pause, Stop, Looping (OneShot, Loop, PingPong), Speed (0.1x to 10x), Frame Step/Navigation.
- Canvas renderer integration: Pushing evaluated transforms into `GpuRenderer` and `RetainedSceneGraph` without mutating persistent `LayerNode` models.
- Automated verification tests: Composition, scaling, rotation, anchors, nested objects, and playback evaluation.

**Out of Scope (Deferred to future phases):**
- Timeline UI, dopesheet, scrubber widgets (Phase 4/5).
- Graph curve editor & bezier handle editing (Phase 3).
- State machine graph, inputs, conditions, listeners (Phase 6).
</domain>

<decisions>
## Implementation Decisions

### Transform Composition & Model
- **D-01:** Tri-layer state separation:
  1. *Original Object Geometry*: `LayerNode.X, Y, Width, Height` and vector `BezierNode` points remain immutable during animation.
  2. *Static Document Transforms*: Flexbox layouts and static rotations evaluated only during document layout passes.
  3. *Animated Transforms*: `RenderTransform` / transient `PoseBuffer` holds per-frame animated values.
- **D-02:** 8-Channel Independent Properties:
  - `PositionX`, `PositionY`: Translation offsets $\Delta X, \Delta Y$ relative to layout origin.
  - `ScaleX`, `ScaleY`: Independent scaling factors (default 1.0).
  - `Rotation`: Degrees $\theta$ around anchor pivot.
  - `AnchorX`, `AnchorY`: Normalized pivot $[0, 1]$ (default 0.5, 0.5 center).
  - `Opacity`: Hierarchical opacity $[0, 1]$.
  - `Width`, `Height`: Visual dimension scale overrides without dirtying layout engine.
- **D-03:** Coordinate space convention:
  Keep `child.X` and `child.Y` as design-canvas coordinates, and let `RenderTransform` calculate local pivots based on `(child.X, child.Y)`. The WPF visual tree natively composes parent $\mathbf{M}_P$ and child $\mathbf{M}_C$, preserving 100% backward compatibility with all 50 existing tests and canvas editing tools.
- **D-04:** Frame & Group hierarchical composition:
  - WPF `DrawingVisual` hierarchy naturally composes parent transform onto child visuals (`visual.Transform = new MatrixTransform(localMatrix)`).
  - `RetainedSceneGraph.GetWorldTransform(LayerNode node)` provides full ancestor matrix composition for inverse hit-testing.
- **D-05:** Auto Layout isolation:
  Auto Layout computes layout bounds once during rest state. Visual transforms operate purely as GPU affine matrices on the rendered visual without dirtying or re-flowing flexbox items.

### Evaluator & Clock
- **D-06:** Deterministic pure evaluator:
  `AnimationEvaluator.SampleToPose(Clip, t, outPose)` calculates animated property channels deterministically for any timestamp $t$ without side effects on document models.
- **D-07:** Centralized Animation Clock:
  `AnimationTimelineController` manages `CurrentTime`, `Duration`, `Fps`, `Speed`, `IsPlaying`, `LoopMode` (`OneShot`, `Loop`, `PingPong`), `Seek()`, `Step()`, and `StepFrame()`.
  Clock is decoupled from React/UI render cycles and ticks cleanly via WPF rendering loop.
- **D-08:** Transient Pose Application:
  Evaluated poses apply to `layer.RenderTransform` during render frames. On stop/reset, `layer.RenderTransform.Reset()` immediately restores original rest pose in $O(1)$ time with zero drift.

### Verification
- **D-09:** Automated test suite in `TestRunner.cs`:
  Test suite #51 verifying:
  1. 8-channel property evaluation.
  2. Hierarchical transform composition across nested Frames & Groups.
  3. Anchor pivot shifting (center vs top-left vs custom).
  4. Auto Layout frame integrity under parent + child simultaneous scaling.
  5. Deterministic clock stepping, seeking, looping, and rest pose restoration.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Core References
- `native-windows/src/Model/CanonicalDocument.cs` — `RenderTransform` class, `LayerNode`, `GetRenderTransformMatrix()`.
- `native-windows/src/Engine/GpuRenderer.cs` — Direct2D visual rendering, `RenderNodeToVisual`, `MatrixTransform`.
- `native-windows/src/Engine/RetainedSceneGraph.cs` — Visual tree hierarchy, `SyncLayerHierarchy`, inverse hit-testing.
- `native-windows/src/Engine/Animation/AnimationTypes.cs` — `PropKey`, `Keyframe`, `Track`, `Clip`.
- `native-windows/src/Engine/Animation/AnimationTimelineController.cs` — Centralized transport clock, playback loop.
- `native-windows/src/Engine/Animation/PropertyBinder.cs` — Non-destructive pose-to-transform mapping.
- `native-windows/tests/TestRunner.cs` — Automated verification fixtures.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RenderTransform` in `CanonicalDocument.cs`: Already has translation, scale, rotation, skew, origins, and matrix math.
- `DrawingVisual` hierarchy in `RetainedSceneGraph.cs`: Already connects child visuals to parent visuals via `vNode.Visual.Children.Add(child.Visual)`.
- `AnimationTimelineController`: Already has core loop modes and time tracking; needs frame navigation methods and clean evaluator decoupling.

### Integration Points
- `PropertyBinder.ApplyPoseToLayer`: Maps evaluated pose properties to `layer.RenderTransform`.
- `StudioView.ApplyAnimationPose`: Drives per-frame visual update from clock pose output.
- `TestRunner.cs`: Verifies all capabilities with zero regressions.
</code_context>
