# Plan 01-02 Summary: Render Pipeline Integration & Automated Verification

**Phase:** 01-independent-transform-pipeline-render-separation
**Plan:** 02
**Status:** Completed
**Execution:** Inline

## What Was Done

1. **Direct2D Canvas Draw Pipeline Integration (`GpuRenderer.cs`)**:
   - In `RenderNodeToVisual`, added `MatrixTransform` applied to `DrawingVisual.Transform` when `layer.RenderTransform` is active (`!renderMat.IsIdentity`).
   - Because WPF visuals are hierarchical container visuals, applying the transform matrix to the parent visual automatically transforms the layer and all child elements (including Auto Layout frames) in hardware without altering layout properties or vector geometries.

2. **PropertyBinder & StudioView Refactor (`PropertyBinder.cs`, `StudioView.cs`)**:
   - Refactored `PropertyBinder.ApplyPoseToLayer`:
     - Scale properties (`PropKey.ScaleX`, `PropKey.ScaleY`) map directly to `layer.RenderTransform.ScaleX` and `ScaleY` rather than mutating `layer.Width` or `layer.Height`.
     - Translation properties (`PropKey.X`, `PropKey.Y`) map to `layer.RenderTransform.TranslationX` and `TranslationY` relative to rest origin.
     - Rotation maps to `layer.RenderTransform.Rotation`.
   - Refactored `StudioView.ApplyAnimationPose`:
     - Resets `layer.RenderTransform.Reset()` on each frame instead of modifying `layer.Width/Height` and restoring from snapshots.
     - Removed destructive `ApplyAnimatedParentTransformToChildren` and `BezierVectorEngine.ScaleGeometry` loops.
     - Updated `RestoreAnimationRestPose` to call `RenderTransform.Reset()`, ensuring zero drift on canvas return.

3. **Automated Verification Test Suite (`TestRunner.cs`)**:
   - Added Test #50: `TestNonDestructiveRenderTransformsAndAutoLayoutPreservation`:
     - Tests Auto Layout frame containing child rectangles and Bézier paths.
     - Applies scale (2.0x, 1.5x), 30° rotation, and translation via `PropertyBinder`.
     - Verifies `frame.Width`, `frame.Height`, `X`, and `Y` remain unmutated at design rest coordinates (240x120, 100, 100).
     - Verifies child Auto Layout bounds and Bézier path anchor coordinates remain unmutated.
     - Verifies center pivot transformation invariance and inverted hit-testing.
     - Verifies `RenderTransform.Reset()` restores identity with zero coordinate drift.

## Verification

- `cd native-windows; .\build.bat --test`:
  - **All 50 tests passed!** (49 existing suites + 1 new suite, 0 failures, 0 regressions).
- C# 5 compatibility maintained.

