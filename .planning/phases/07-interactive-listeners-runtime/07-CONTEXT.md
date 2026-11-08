# Phase 7: Interactive Listeners, State Machine Runtime and Transition Execution - Context

**Gathered:** 2026-11-08
**Status:** Ready for planning

<domain>
## Phase Boundary

Connect canvas interactions, listeners, state machine inputs, transition conditions, animation clips, and the animation evaluator into one coherent execution pipeline without duplicating existing systems or altering underlying layout geometry.

### In Scope:
- Centralized event dispatcher receiving canvas pointer interactions and routing them to target object listeners.
- Document object listener model supporting PointerEnter, PointerExit, PointerDown, PointerUp, Click, Tap, DragStart, Drag, and DragEnd.
- Listener actions: Set Boolean, Set Number, Toggle Boolean, Fire Trigger, Reset inputs with input type validation.
- Right Inspector "Listeners" panel for the currently selected object (create, edit, bind input, enable/disable, delete with full undo/redo).
- StateMachineRuntime execution engine: state activation, compound condition evaluation, exit times, durations, and graph-order transition priority.
- Animation clip playback and non-destructive transition blending (in-flight snapshot crossfading).
- Spacebar and Play transport controls governing simulation mode: the state machine only runs when playing; while paused, canvas clicks select objects normally.
- Automated verification tests covering listener dispatch, input manipulation, transition execution, and snapshot crossfade blending.

### Out of Scope:
- Exporting to external formats (Rive .riv binary, Lottie JSON, or WebGL shaders).
- Replacing the existing animation evaluator or introducing parallel dispatchers.
- Modifying design mode layout properties (width, height, flexbox auto layout).
</domain>

<decisions>
## Implementation Decisions

### 1. Transport & Interaction Control
- **D-01 (Play / Spacebar Gate):** The State Machine simulation and canvas listener interactions (hover, click, drag triggers) ONLY play when Spacebar hits or Play hits (`_timelineController.IsPlaying == true`). When paused or stopped, canvas pointer clicks select objects normally for design/layout editing, and animation evaluation holds with `dt = 0.0`. — **Reversibility:** costly — Controls how the viewport handles hit-testing and event dispatching between Design/Edit mode and Live Simulation.

### 2. Listener Architecture & Inspector
- **D-02 (Right Inspector Placement):** Listener creation, configuration, and inspection lives exclusively in the Right Inspector under a dedicated collapsible "Listeners" section when a document object is selected. Users can create listeners, configure event kind (`PointerEnter`, `PointerExit`, `PointerDown`, `PointerUp`, `Click`, `Tap`, `DragStart`, `Drag`, `DragEnd`), select an action (`SetBool`, `SetNumber`, `ToggleBool`, `FireTrigger`), bind to an existing State Machine Input, set the parameter value, enable/disable, and delete listeners. — **Reversibility:** reversible — UI layout choice scoped to Inspector panel generation.
- **D-03 (Transactional History):** All listener additions, modifications, and deletions are recorded in `CommandHistory` with full atomic undo/redo support. Live runtime input changes during playback do NOT pollute document history. — **Reversibility:** reversible — Standard ICommand implementations.

### 3. Transition Execution & Blending
- **D-04 (Interrupted Snapshot Blending):** When an in-flight transition is interrupted by another valid transition, the engine snapshots the current interpolated render pose and smoothly blends from that snapshot towards the new target state with zero visual snapping or popping. — **Reversibility:** costly — Implemented directly inside `StateMachineRuntime.BlendPoses` and transition state resolution.
- **D-05 (Transition Priority Resolution):** Deterministic priority: Transitions originating from the current active state are evaluated first in their graph creation order. Transitions originating from "Any State" are evaluated second as lower-priority fallback transitions. — **Reversibility:** reversible — Pure transition sorting rule inside `StateMachineRuntime.FindValidTransition`.
- **D-06 (Non-Destructive Evaluation):** State poses and transition blend poses apply strictly to `RenderTransform`, preserving document layout geometry, corner radiuses, and Auto Layout structures. — **Reversibility:** one-way — Architectural foundation established in Phase 1 & 2.

### the agent's Discretion
- Exact visual styling of listener list items, badges, and event icons inside the Right Inspector (matching Figma/Kerf dark theme).
- Internal event dispatcher registration structure using fast target ID lookup dictionaries.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing:**
- `.planning/ROADMAP.md` § Phase 7 — High-level requirements and phase deliverables.
- `native-windows/src/Engine/Animation/StateMachineTypes.cs` — `StateMachineDefinition`, `NodeListener`, `ListenerAction`, `MachineInput`, `StateTransition`.
- `native-windows/src/Engine/Animation/StateMachineRuntime.cs` — `StateMachineRuntime` transition resolution, `BlendPoses`, pointer events.
- `native-windows/src/Engine/Animation/AnimationTimelineController.cs` — Centralized clock, playback state, evaluation pipeline.
- `native-windows/src/UI/StudioView.cs` — Canvas interaction routing, transport toggles, right inspector generation.
- `native-windows/src/Engine/CommandHistory.cs` — Reversible command patterns for animation and state machine authoring.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `NodeListener` & `ListenerAction` in `StateMachineTypes.cs`: Already defined data structures for attaching listeners to node IDs.
- `StateMachineRuntime.OnPointerEvent`: Existing pointer event entrypoint for dispatching events to registered listeners.
- `StateMachineRuntime.BlendPoses`: Robust pose blending engine with rest-pose fallback and snapshot capture.
- `PropertyBinder.ApplyPoseToLayer`: Non-destructive render transform application.

### Established Patterns
- Split-screen layout in `StudioMode.Animate` with transport bar at top and context-sensitive right panel.
- Transactional undo/redo via `ICommand` subclasses executing custom callbacks to refresh inspectors and canvas.

### Integration Points
- Canvas mouse handlers (`HandleCanvasMouseDown`, `HandleCanvasMouseMove`, `HandleCanvasMouseUp`) in `StudioView.cs`: When `_currentStudioMode == StudioMode.Animate` and `IsPlaying == true`, route events to centralized event dispatcher.
- Right Inspector: When in Animate mode with a canvas object selected, render the "Listeners" property group.
</code_context>

<specifics>
## Specific Ideas
- Users can create button hover & press interactions directly:
  - Add Listener on Rectangle -> Event: `PointerEnter` -> Action: `SetBool` (`isHovered = true`)
  - Add Listener on Rectangle -> Event: `PointerExit` -> Action: `SetBool` (`isHovered = false`)
  - Add Listener on Rectangle -> Event: `Click` -> Action: `FireTrigger` (`buttonClick`)
- Hitting Spacebar or clicking `▶ Play` starts live playback and interaction; hitting Spacebar again pauses playback and restores canvas selection.
</specifics>

<deferred>
## Deferred Ideas
- None — discussion remained strictly within Phase 7 scope.
</deferred>

---

*Phase: 7-Interactive Listeners, State Machine Runtime and Transition Execution*
*Context gathered: 2026-11-08*

