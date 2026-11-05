# Plan 02-01 Summary: Hierarchical Transform Composition & 8-Channel Property Model

**Phase:** 02-independent-transform-architecture-animation-evaluator
**Plan:** 01
**Status:** Completed
**Execution:** Inline

## What Was Done

1. **8-Channel Independent Property Model (`AnimationTypes.cs`, `CanonicalDocument.cs`)**:
   - Extended `PropKey` with `AnchorX = 14`, `AnchorY = 15`, `VisualWidth = 16`, and `VisualHeight = 17`.
   - Updated `RenderTransform`:
     - Added `AnchorX` and `AnchorY` normalized properties linked to `OriginX` and `OriginY` (default 0.5, 0.5 center).
     - Added `VisualWidth` and `VisualHeight` properties for visual scale overrides without mutating model layout dimensions.
     - Updated `Reset()`, `Clone()`, and `IsIdentity` to include the new properties.

2. **Hierarchical World Matrix Composition & Hit-Testing (`CanonicalDocument.cs`, `RetainedSceneGraph.cs`)**:
   - Implemented `LayerNode.GetWorldRenderTransformMatrix(Page page)`: Walks ancestor `ParentId` chain with cycle detection to compute:
     $$\mathbf{M}_{\text{world}}(\text{child}) = \mathbf{M}_{\text{world}}(\text{parent}) \times \mathbf{M}_{\text{local}}(\text{child})$$
   - Updated `RetainedSceneGraph.CanvasToLocalPoint` and `LocalToCanvasPoint` to optionally accept `Page page`, utilizing composite world transform inverses for hit-testing and point transformations across nested Frames and Groups.

3. **PropertyBinder & GPU Renderer Integration (`PropertyBinder.cs`, `GpuRenderer.cs`)**:
   - Refactored `PropertyBinder.ApplyPoseToLayer`:
     - Binds `PropKey.AnchorX` and `PropKey.AnchorY` to `layer.RenderTransform.AnchorX/Y`.
     - Binds `PropKey.VisualWidth` and `PropKey.VisualHeight` to `layer.RenderTransform.VisualWidth/Height` and computes non-destructive scale factors.
     - Binds `PropKey.Opacity` to `layer.Opacity`.
   - In `GpuRenderer.cs`:
     - Applied `visual.Transform = new MatrixTransform(rtMat)` for hardware transform composition.
     - Assigned `visual.Opacity = Math.Max(0.0, Math.Min(1.0, node.Opacity))` on `ContainerVisual` to guarantee hierarchical alpha blending across all child layers.

## Verification

- `cd native-windows; .\build.bat --test`:
  - Clean compilation with C# 5 (`csc.exe`).
  - **All 50 test suites passed!** (50 passed, 0 failed, 0 regressions).

