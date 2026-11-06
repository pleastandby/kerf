# Phase 3: Timeline and Keyframe Engine Reconstruction - Context

**Gathered:** 2026-11-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace the existing single-layer, unstable timeline implementation with a reliable, multi-track animation authoring system that seamlessly integrates with the Phase 1 & 2 non-destructive transform pipeline and deterministic evaluator.

**In Scope:**
- Timeline authoring architecture: Time ruler with frame numbering, playhead scrubbing, zoom/pan, frame snapping, playback range, frame navigation.
- Multi-track hierarchy: Expandable/collapsible object summary tracks with nested independent property tracks (`PositionX/Y`, `ScaleX/Y`, `Rotation`, `AnchorX/Y`, `Opacity`, `VisualWidth/Height`).
- Stable keyframe system: Unique GUID IDs, sorted timestamps, deterministic duplicate handling, value editing, single/multi-selection, moving preserving relative timing, duplicate, copy/paste, delete.
- Transform inspector integration in Animate Mode: Keyframe diamond status toggles (`Solid Mint` / `Hollow` / `Dim`), live evaluated value readouts, jump to prev/next keyframe.
- High-performance interactions: Smooth dragging and scrubbing without unnecessary UI rebuilds or document corruption.
- Automated tests: Keyframe CRUD, multi-selection, shifting, duplicate timestamps, clipboard, and evaluator integration.

**Out of Scope (Deferred to future phases):**
- Custom cubic Bézier tangent handle graph editing and curve presets (Phase 4).
- Undo/redo integration through CommandHistory (Phase 5).
- Interactive state machines, inputs, conditions, listeners (Phase 6).
</domain>

<decisions>
## Implementation Decisions

### Data Architecture
- **D-01:** Stable Keyframe Identifiers:
  Each `Keyframe` has an immutable string `Id` (`Guid.NewGuid().ToString("N")`), `Time` (double seconds), `Value` (double), and `Interp` mode (`Hold`, `Linear`, `Cubic`).
- **D-02:** Stable Object References in Tracks:
  `Track` stores `TargetNodeId` (the canonical layer `string Id`) and `PropKey` / `PropertyPath`. `NodeId` (uint hash) is retained for fast runtime evaluator indexing.
- **D-03:** Sorted Storage & Deterministic Duplicate Policy:
  Tracks store keyframes in strict ascending timestamp order using binary search. If a keyframe is inserted within $\epsilon = 10^{-6}$s of an existing keyframe, the existing keyframe's value is updated rather than creating a duplicate.

### Timeline UI & Interaction
- **D-04:** Hierarchical Track Lanes (Dopesheet):
  Each animated object is rendered as a master track with an expander arrow `▶` / `▼` and summary keyframes. Expanding reveals sub-lanes for each active property track.
- **D-05:** Time Ruler & Frame Snapping:
  Ruler displays frame numbers (`0, 15, 30, 45, 60...`) and time marks. Clicking or dragging the ruler scrubs the playhead. When frame snapping is enabled, scrubbing and keyframe dragging quantize to $t_{\text{snap}} = \text{round}(t \times \text{Fps}) / \text{Fps}$.
- **D-06:** Multi-Keyframe Selection & Relative Timing:
  Keyframes support single selection and multi-selection (Shift/Ctrl click or box marquee). Dragging multiple keyframes shifts all selected keys by $\Delta t = t_{\text{curr}} - t_{\text{start}}$, clamping $t \ge 0.0$.
- **D-07:** Clipboard Model:
  Copying keyframes puts a serialized packet `{ PropKey, RelativeTimeOffset, Value, Interp }` into the animation clipboard. Pasting stamps them relative to the current playhead timestamp onto matching tracks of the selected layer.

### Inspector Integration
- **D-08:** Dedicated Animate Mode Transform Inspector:
  In Animate Mode, each inspector property field (`X`, `Y`, `W`, `H`, `Rotation`, `Opacity`, `ScaleX`, `ScaleY`, `AnchorX`, `AnchorY`) features a diamond keyframe toggle:
  - Solid Mint: Keyframe exists at current playhead (clicking deletes keyframe).
  - Hollow Border: Property is animated in this clip, but has no keyframe at this playhead (clicking adds keyframe).
  - Dim Gray: Property is not animated yet (clicking creates track and adds keyframe).
  - Previous / Next buttons jump playhead directly between keyframes.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Core References
- `native-windows/src/Model/CanonicalDocument.cs` — `RenderTransform`, `LayerNode`, `Page`.
- `native-windows/src/Engine/Animation/AnimationTypes.cs` — `Keyframe`, `Track`, `Clip`, `PropKey`.
- `native-windows/src/Engine/Animation/AnimationTimelineController.cs` — Transport clock, `Seek`, `Step`, `StepFrame`.
- `native-windows/src/Engine/Animation/PropertyBinder.cs` — Non-destructive pose-to-layer mapping.
- `native-windows/src/UI/StudioView.cs` — Timeline UI panels, lanes canvas, inspector controls.
- `native-windows/tests/TestRunner.cs` — Test suites #50, #51.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AnimationTimelineController`: Clock with Play, Pause, Stop, Seek, Step, StepFrame, Speed, and Loop modes.
- `PropertyBinder.ApplyPoseToLayer`: Maps evaluated pose properties to `RenderTransform`.
- `StudioView`: Has layout structure for timeline panel, transport controls, and right canvas border.

### Integration Points
- `StudioView.RefreshTimelineTracks`: Expand into hierarchical multi-object lanes.
- `StudioView.RefreshTimelineCanvas`: Support zoom, ruler frame numbers, multi-keyframe diamonds, and summary diamonds.
- `StudioView.UpdatePropertyPanel`: Inject Animate Mode keyframe diamond toggles and navigation arrows.
- `TestRunner.cs`: Add Test #52 verifying all authoring capabilities.
</code_context>
