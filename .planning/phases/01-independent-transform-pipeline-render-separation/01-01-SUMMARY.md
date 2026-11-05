# Plan 01-01 Summary: Core RenderTransform Model & Matrix Math

**Phase:** 01-independent-transform-pipeline-render-separation
**Plan:** 01
**Status:** Completed
**Execution:** Inline

## What Was Done

1. **RenderTransform Model (`CanonicalDocument.cs`)**:
   - Defined `RenderTransform` class featuring decoupled `TranslationX`, `TranslationY`, `ScaleX`, `ScaleY`, `Rotation`, `SkewX`, `SkewY`, and normalized anchor pivots `OriginX`, `OriginY` (default 0.5, 0.5).
   - Added `ToMatrix(layerX, layerY, layerWidth, layerHeight)` method calculating 2D affine matrices around the layer's local pivot:
     `M = Translate(-pivot) * Skew * Scale * Rotate * Translate(pivot + translation)`.
   - Added `IsIdentity`, `Reset()`, and `Clone()` methods.
   - Added `RenderTransform RenderTransform { get; set; }` to `LayerNode`.
   - Updated `LayerNode` constructor, `GetTransformMatrix()`, `GetRenderTransformMatrix()`, and `Clone()` to compose `RenderTransform` while preserving design bounds.

2. **Hierarchical Matrix & Hit-Testing Integration (`RetainedSceneGraph.cs` & `CanvasInteractionController.cs`)**:
   - Added `CanvasToLocalPoint(LayerNode layer, Point canvasPt)` and `LocalToCanvasPoint(LayerNode layer, Point localPt)` in `RetainedSceneGraph.cs`.
   - Updated `RetainedSceneGraph.HitTestSubtree` to transform the hit test point into local layer space using inverted transform matrices.
   - Updated `CanvasInteractionController.ContainsPoint` to test points against the inverted `RenderTransform` matrix, ensuring transformed and scaled layers are selectable.

## Verification

- Clean compilation with C# 5 (`csc.exe`).
- Executed `cd native-windows; .\build.bat --test`: All 49 existing test suites passed with 0 failures and 0 regressions.
