# Phase 1: Independent Transform Pipeline & Render Separation - Context

**Gathered:** 2026-11-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 1 establishes a non-destructive transform pipeline (`RenderTransform` / `MatrixTransform`) that decouples animated transformations (translation, rotation, scale, skew, and anchor/origin points) from design layer bounds (`LayerNode.Width`, `LayerNode.Height`, `X`, `Y`).

It ensures:
1. Animating scale never mutates `Width` or `Height` on layers.
2. Auto Layout containers and nested children preserve exact flexbox bounds during animation.
3. Vector path points are not destructively scaled or rewritten during playback.
4. Canvas rendering renders transformed layers through Direct2D/WPF render transform matrices.

</domain>

<decisions>
## Implementation Decisions

### 1. Transform Representation & Data Model
- **D-01 (Transform Separation):** Introduce an explicit `RenderTransform` structure on layers containing decoupled transformation properties: `TranslationX`, `TranslationY`, `ScaleX`, `ScaleY`, `Rotation`, `SkewX`, `SkewY`, and `OriginX`, `OriginY` (normalized anchor point, defaulting to center [0.5, 0.5]).
- **D-02 (Local vs Document Bounds):** `LayerNode.Width` and `LayerNode.Height` represent intrinsic design/layout dimensions and are strictly read-only for the animation engine. The animation engine only evaluates and writes to `RenderTransform`.
- **D-03 (Matrix Composition):** Transform matrices compose down the visual scene graph: $M_{world} = M_{parent} \times M_{local}$. Local coordinate transformations do not mutate child canvas-space bounding boxes.

### 2. Auto Layout & Vector Geometry Protection
- **D-04 (Auto Layout Immunity):** Auto Layout continues to measure and arrange children using intrinsic design bounds (`Width`, `Height`, `Gap`, `Padding`). Visual scaling operates strictly at render time via GPU matrices without triggering layout reflow or child resizing.
- **D-05 (Vector Geometry Preservation):** Vector path geometry (`LayerNode.Geometry`) is never mutated during transform scaling. Vector paths retain original Bézier anchor and control points while the renderer applies the layer's transform matrix.

### 3. Rendering Pipeline Integration
- **D-06 (Direct2D Render Transforms):** `GpuRenderer.cs` and `RetainedSceneGraph.cs` push the layer's composed transform matrix onto the Direct2D render target transform stack (`RenderTarget.Transform`) before drawing the layer and its children, popping it upon completion.
- **D-07 (Zero Transform Default):** When `RenderTransform` is null or identity, the layer renders with zero overhead, maintaining exact 1:1 backward compatibility with all existing design editor features.

### 4. Elimination of Destructive Rest-Pose Hacks
- **D-08 (Deprecate Rest Pose Overwrites):** Replace the fragile `_animationRestPose` dictionary and `ApplyAnimatedParentTransformToChildren` bounds-rewriting loop with direct render transform matrix evaluation.

</decisions>

<specifics>
## Specific Ideas

- Figma-like independent transform behavior: Transforming a parent visually scales the parent and children without changing the child layers' intrinsic layout width/height or breaking auto layout spacing.
- Clean separation between Model (Document geometry) and Presentation (Render transforms).

</specifics>

<canonical_refs>
## Canonical References

- `.planning/PROJECT.md` — Core value and active requirements
- `.planning/REQUIREMENTS.md` § Independent Transform & Render Pipeline (TRANS-01 through TRANS-04)
- `.planning/ROADMAP.md` § Phase 1
- `native-windows/src/Model/CanonicalDocument.cs` — LayerNode schema and geometry definitions
- `native-windows/src/Engine/RetainedSceneGraph.cs` — Scene graph traversal and draw dispatch
- `native-windows/src/Engine/GpuRenderer.cs` — Direct2D rendering pipeline and matrix transform stack

</canonical_refs>

<code_context>
## Existing Code Insights

### Root Causes of Legacy Flaws
1. **Destructive Scale Mutations:** `PropertyBinder.ApplyPoseToLayer` mutates `layer.Width` and `layer.Height`. In `StudioView.ApplyAnimatedParentTransformToChildren`, scale is computed as `parent.Width / parentRest.Width` and multiplied into child `Width` and `Height`, while vector geometry points are permanently rewritten via `BezierVectorEngine.ScaleGeometry`.
2. **Coupled Rest-Pose Hacks:** `StudioView` stores a sidecar dictionary `_animationRestPose`. On every frame tick, it restores all layers from `_animationRestPose`, applies the new frame into layer model bounds, and recurses children. On saving, it reverts to rest pose, writes JSON, and re-seeks the timeline. Any desynchronization destroys design state.
3. **Identifier Collision Vulnerability:** `PropertyBinder.GetNumericNodeId` computes a 32-bit FNV-1a hash of layer IDs, risking hash collisions.
4. **Missing Timeline Graph Editor:** `_timelineInterpolationGraphCanvas` is only a 48px static thumbnail, not an interactive multi-key timeline curve editor.
5. **Missing Undo/Redo Commands:** Keyframe creation, timeline editing, curve handle tweaking, and state machine transitions only call `History.MarkDirty()` without registering reversible commands in `CommandHistory.cs`.

### Component Disposal & Migration Strategy
- **Reused Components:**
  - `CubicBezierEase.cs`: High precision Newton-Raphson + bisection solver.
  - `StateMachineTypes.cs` (`MachineInput`, `ViewModelPropertyDefinition`, `StateMachineViewModel`): Clean state machine abstractions.
  - `StateMachineRuntime.cs`: State transitions, condition evaluation, and pose blending logic.
- **Refactored Components:**
  - `LayerNode`: Add `RenderTransform` property.
  - `RetainedSceneGraph` & `GpuRenderer`: Support render transform matrix hierarchy.
  - `AnimationTimelineController`: Output pose transforms without touching `LayerNode.Width`/`Height`.
- **Removed Components:**
  - `PropertyBinder.ApplyPoseToLayer` destructive geometry overwrite.
  - `StudioView._animationRestPose` and `ApplyAnimatedParentTransformToChildren`.
  - `AnimationNativeBridge.cs` (orphaned unused DllImports).

</code_context>

<deferred>
## Deferred Ideas

- Full keyframe graph canvas editor widget with draggable tangents (Deferred to Phase 3).
- State machine node connection undo/redo commands (Deferred to Phase 4 / Phase 6).
- Interactive pointer listener triggers for state machine (Deferred to Phase 6).

</deferred>

---

*Phase: 01-independent-transform-pipeline-render-separation*
