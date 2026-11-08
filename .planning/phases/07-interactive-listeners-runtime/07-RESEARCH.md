# Phase 7: Interactive Listeners, State Machine Runtime and Transition Execution - Research
**Researched:** 2026-11-08
**Domain:** DirectX + C# 5.0 Native Interactive State Machine Runtime, Event Dispatching & Transition Blending
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01 (Play / Spacebar Gate):** The State Machine simulation and canvas listener interactions (hover, click, drag triggers) ONLY play when Spacebar hits or Play hits (`_timelineController.IsPlaying == true`). When paused or stopped, canvas pointer clicks select objects normally for design/layout editing, and animation evaluation holds with `dt = 0.0`. — **Reversibility:** costly — Controls how the viewport handles hit-testing and event dispatching between Design/Edit mode and Live Simulation.
- **D-02 (Right Inspector Placement):** Listener creation, configuration, and inspection lives exclusively in the Right Inspector under a dedicated collapsible "Listeners" section when a document object is selected. Users can create listeners, configure event kind (`PointerEnter`, `PointerExit`, `PointerDown`, `PointerUp`, `Click`, `Tap`, `DragStart`, `Drag`, `DragEnd`), select an action (`SetBool`, `SetNumber`, `ToggleBool`, `FireTrigger`), bind to an existing State Machine Input, set the parameter value, enable/disable, and delete listeners. — **Reversibility:** reversible — UI layout choice scoped to Inspector panel generation.
- **D-03 (Transactional History):** All listener additions, modifications, and deletions are recorded in `CommandHistory` with full atomic undo/redo support. Live runtime input changes during playback do NOT pollute document history. — **Reversibility:** reversible — Standard ICommand implementations.
- **D-04 (Interrupted Snapshot Blending):** When an in-flight transition is interrupted by another valid transition, the engine snapshots the current interpolated render pose and smoothly blends from that snapshot towards the new target state with zero visual snapping or popping. — **Reversibility:** costly — Implemented directly inside `StateMachineRuntime.BlendPoses` and transition state resolution.
- **D-05 (Transition Priority Resolution):** Deterministic priority: Transitions originating from the current active state are evaluated first in their graph creation order. Transitions originating from "Any State" are evaluated second as lower-priority fallback transitions. — **Reversibility:** reversible — Pure transition sorting rule inside `StateMachineRuntime.FindValidTransition`.
- **D-06 (Non-Destructive Evaluation):** State poses and transition blend poses apply strictly to `RenderTransform`, preserving document layout geometry, corner radiuses, and Auto Layout structures. — **Reversibility:** one-way — Architectural foundation established in Phase 1 & 2.

### the agent's Discretion

- Exact visual styling of listener list items, badges, and event icons inside the Right Inspector (matching Figma/Kerf dark theme).
- Internal event dispatcher registration structure using fast target ID lookup dictionaries.

### Deferred Ideas (OUT OF SCOPE)

- None — discussion remained strictly within Phase 7 scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **STATE-04** | Interactive canvas event listeners (`PointerEnter`, `PointerExit`, `PointerDown`, `PointerUp`, `Click`, `Tap`, `DragStart`, `Drag`, `DragEnd`) linked to state machine inputs and transitions | Identified `RetainedSceneGraph.HitTest` local point transform, `InteractiveEventDispatcher` lifecycle tracker, and expanded `PointerEventKind` enum in `StateMachineTypes.cs`. |
| **STATE-05** | State blending engine supporting smooth cross-fade interpolation between active clips and rest poses | Validated `StateMachineRuntime.BlendPoses` with `restPose` fallback, easing curve evaluation via `CubicBezierEase`, and clip advancement via `ClipInstance.Advance`. |
| **LISTENER-01** | Centralized event dispatcher receiving canvas pointer interactions and routing them to target object listeners | Mapped canvas mouse handlers in `StudioView.cs`, isolated playback simulation gating (`_timelineController.IsPlaying == true`), and fast $O(1)$ dictionary listener routing. |
| **LISTENER-02** | Right Inspector "Listeners" panel for the currently selected object (create, edit, bind input, toggle enable, delete with undo/redo) | Detailed `StudioView.RefreshPropertyInspector()` integration, collapsible listener card list, input type validation, and `CommandHistory` reversible commands. |
| **RUNTIME-01** | StateMachineRuntime evaluation engine: state activation, compound condition evaluation, exit times, and graph-order transition priority | Analyzed `FindValidTransition` graph-order priority (current state transitions before AnyState), compound condition groups (OR of ANDs), and 100-step zero-duration chain guard. |
| **TRANS-EXEC-01** | Real animation playback executing assigned clips with speed and looping, blending outgoing and incoming state transforms non-destructively | Verified `ClipInstance.Advance` loop modes (`OneShot`, `Loop`, `PingPong`), `curState.Speed`, and non-destructive `RenderTransform` matrix application. |
| **BLEND-01** | Non-destructive transition blending with in-flight snapshot crossfading to eliminate visual pops | Evaluated `ActiveTransitionState.SnapshotPose` frozen pose sampling and interpolation during transition interruptions in `StateMachineRuntime.Update`. |
</phase_requirements>

## Summary

Kerf Studio's motion graphics engine refactor has reached Phase 7: connecting canvas interactions, listeners, state machine inputs, transition conditions, animation clips, and the animation evaluator into a unified runtime execution pipeline. In Phase 6, the graph data models (`StateMachineDefinition`, `MachineLayer`, `MachineState`, `StateTransition`, `ConditionGroup`, `MachineInput`), validation engine (`StateMachineValidator`), and 3-column visual node graph UI were completed. Phase 7 transforms this static graph definition into a live interactive simulation.

The phase encompasses three primary sub-systems:
1. **Interactive Canvas Event Dispatching & Document Listeners:** When in Animate mode and playback is active (`_timelineController.IsPlaying == true` via Spacebar or Play transport button per D-01), pointer interactions over the canvas are routed through a centralized `InteractiveEventDispatcher`. The dispatcher tracks hit objects using `RetainedSceneGraph.HitTest` (accounting for render transforms and clipping) and fires granular pointer events (`PointerEnter`, `PointerExit`, `PointerDown`, `PointerUp`, `Click`, `Tap`, `DragStart`, `Drag`, `DragEnd`). Registered `NodeListener` instances execute actions (`SetBool`, `SetNumber`, `ToggleBool`, `FireTrigger`, `Reset`) on bound `MachineInput` variables.
2. **Right Inspector Authoring & Transactional History:** When a document layer is selected in Animate mode, the Right Property Inspector renders a dedicated collapsible "Listeners" section (D-02). Users can add, configure, inspect, disable/enable, and delete listeners with strict input-type compatibility checks. All authoring operations are backed by atomic reversible commands (`AddListenerCommand`, `ModifyListenerCommand`, `DeleteListenerCommand`) in `CommandHistory` (D-03), while live playback mutations occur purely in memory.
3. **StateMachineRuntime Execution & Interrupted Snapshot Blending:** The runtime executes state clip instances, checks normalized `ExitTime`, evaluates compound condition groups (OR of ANDs), respects deterministic graph-order priority (D-05), and drives smooth crossfades between states. When a transition is interrupted mid-flight, `ActiveTransitionState.SnapshotPose` freezes the current render transform and smoothly blends from that frozen pose into the new target state with zero visual snapping or popping (D-04, D-06).

**Primary recommendation:** Implement Phase 7 across three focused plans: `07-01` (Centralized Event Dispatcher, listener model extensions, and Right Inspector UI with undo/redo), `07-02` (StateMachineRuntime transition conditions, exit times, clip playback, and snapshot crossfade blending), and `07-03` (Spacebar/Play simulation mode gating, live state feedback, and automated test suite #66 in `TestRunner.cs`).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Canvas Hit-Testing & Pointer Tracking | UI / Viewport (`StudioView.cs`) | Renderer (`RetainedSceneGraph.cs`) | `RetainedSceneGraph.HitTest` transforms canvas coordinates through render matrices to resolve target layer IDs. |
| Event Dispatching & Gesture Lifecycle | Engine / Dispatcher (`InteractiveEventDispatcher.cs`) | UI (`StudioView.cs`) | Dedicated dispatcher decouples raw mouse events from canvas editing tools and manages Hover/Click/Drag states. |
| Document Listener Definition & Storage | Engine / Model (`StateMachineTypes.cs`) | Serialization (`AnimationTypes.cs`) | `NodeListener` and `ListenerAction` are persistent document data structures stored in `StateMachineDefinition`. |
| Listener Authoring & Right Inspector | UI / Inspector (`StudioView.cs`) | Engine (`CommandHistory.cs`) | Right Inspector renders listener cards, input bindings, and issues reversible commands into `CommandHistory`. |
| Simulation Gating (Spacebar / Play) | UI / Transport (`StudioView.cs`) | Engine / Clock (`AnimationTimelineController.cs`) | Controls whether canvas clicks select layout layers (paused) or trigger live interactive listeners (playing). |
| Transition Condition Evaluation | Engine / Runtime (`StateMachineRuntime.cs`) | Engine / Model (`StateMachineTypes.cs`) | Evaluates compound condition groups (OR of ANDs) against live `MachineInput` values and triggers. |
| Pose Sampling & Snapshot Blending | Engine / Runtime (`StateMachineRuntime.cs`) | Engine / Math (`CubicBezierEase.cs`) | Samples clip keyframes, calculates Bézier mix ratios, and blends poses into `RenderTransform` dictionaries. |
| Render Transform Application | Engine / Binder (`PropertyBinder.cs`) | Engine / Scene (`RetainedSceneGraph.cs`) | Non-destructively applies evaluated pose transforms directly to `layer.RenderTransform`. |

## Standard Stack

### Core
| Library / Component | Version | Purpose | Why Standard |
|---------------------|---------|---------|--------------|
| `C# 5.0 (.NET Framework 4.0)` | v4.0.30319 | Native application compiler & runtime | In-repo standard defined in `native-windows/build.bat`, compiled via `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. |
| `System.Windows.Media.Animation` / Direct2D | .NET 4.0 WPF / DirectX | Canvas presentation & Direct2D rendering pipeline | Powers low-latency native Windows vector graphics and `GpuRenderer`. |
| `StateMachineRuntime` | In-repo (`native-windows/src/Engine/Animation/StateMachineRuntime.cs`) | State machine execution, transition resolution & pose blending | High-performance state evaluation engine with rest-pose fallback and interrupted snapshot crossfading. |
| `RetainedSceneGraph` | In-repo (`native-windows/src/Engine/RetainedSceneGraph.cs`) | Scene graph visual hierarchy, matrix transforms & hit-testing | Implements inverse world-matrix transformation for accurate click and hover detection on transformed elements. |
| `CommandHistory` | In-repo (`native-windows/src/Engine/CommandHistory.cs`) | Atomic transactional undo/redo engine | Manages undo/redo stacks (`ICommand`), composite batches, and document dirty flag tracking. |

### Supporting
| Library / Component | Version | Purpose | When to Use |
|---------------------|---------|---------|-------------|
| `CubicBezierEase` | In-repo | Mathematical cubic Bézier curve evaluation | Evaluates transition easing curves (`X1`, `Y1`, `X2`, `Y2`) for smooth blend weights. |
| `PropertyBinder` | In-repo | Numeric ID hashing and pose-to-layer application | Converts layer ID strings to `uint` IDs and applies poses to `RenderTransform`. |
| `KerfBrand` | In-repo | Dark theme styling brushes, buttons, and typography | Standard dark UI styling for Right Inspector controls and badges. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| In-memory `InteractiveEventDispatcher` | Raw mouse handlers in `StudioView.cs` | Sprawling event handlers in `StudioView.cs` (22k+ lines) mix editing tools with live simulation, causing bugs and design model corruption. |
| Separate `NodeListener` dictionary lookup | Linear scan over `Definition.Listeners` | Linear scan is $O(N \cdot M)$ on every mouse move; dictionary indexed by `uint targetNodeId` provides $O(1)$ dispatch. |
| In-flight snapshot pose capture | Rewinding or resetting interrupted transitions | Snaps or pops jarringly when a user hovers/clicks rapidly; snapshot pose creates buttery smooth organic crossfades. |

## Package Legitimacy Audit

> Kerf Studio is a zero-dependency native Windows C# 5.0 / DirectX application built with standard .NET Framework assemblies (`mscorlib.dll`, `System.dll`, `System.Core.dll`, `PresentationCore.dll`, `PresentationFramework.dll`, `WindowsBase.dll`) and an optional in-repo Rust native core (`kerf_core.dll`). No external third-party package managers (NuGet, npm, pip, crates.io) are used for runtime libraries.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| System (.NET Framework) | BCL / GAC | 20+ yrs | Native OS | Microsoft BCL | [OK] | Approved |
| PresentationCore | WPF / GAC | 18+ yrs | Native OS | Microsoft WPF | [OK] | Approved |
| kerf_core.dll | In-repo | Local | Local | `native-windows/crates/kerf_core` | [OK] | Approved |

**Packages removed due to [SLOP] verdict:** None
**Packages flagged as suspicious [SUS]:** None

## Architecture Patterns

### System Architecture Diagram

```
                                 [ Canvas Pointer Event ]
                               (MouseDown / Move / MouseUp)
                                            │
                                            ▼
                           [ _timelineController.IsPlaying? ]
                                   │                 │
                             NO (Paused)        YES (Live Simulation)
                                   │                 │
                                   ▼                 ▼
                          [ Design Mode Tools ]  [ ScreenToCanvas(pt) ]
                          - Select / Marquee         │
                          - Vector Edit              ▼
                          - Bounding Gizmo       [ RetainedSceneGraph.HitTest ]
                                                (World Transform & Clip Aware)
                                                     │
                                                     ▼
                                     [ InteractiveEventDispatcher ]
                                     - Hover tracking (Enter / Exit)
                                     - Drag lifecycle (Start / Drag / End)
                                     - Click / Tap gesture recognition
                                                     │
                                                     ▼
                                        [ NodeListener.Actions ]
                                                     │
                             ┌───────────────────────┴───────────────────────┐
                             ▼                                               ▼
                      [ SetBool / SetNumber ]                         [ FireTrigger ]
                      [ ToggleBool / Reset  ]                                │
                             │                                               ▼
                             ▼                                  [ MachineInput.Fired = true ]
                      [ MachineInput.Value ]                     (Clears after 1 frame)
                             │                                               │
                             └───────────────────────┬───────────────────────┘
                                                     ▼
                                        [ StateMachineRuntime.Update(dt) ]
                                                     │
                                                     ▼
                                        [ FindValidTransition ]
                                     1. Outgoing from Current State (Priority)
                                     2. Outgoing from Any State (Fallback)
                                     3. ExitTime threshold check
                                     4. Compound Condition Groups (OR of ANDs)
                                                     │
                           ┌─────────────────────────┴─────────────────────────┐
                           │                                                   │
                           ▼                                                   ▼
                [ Transition Interrupted? ]                          [ Transition Complete? ]
                           │                                                   │
                   YES ────┴──── NO                                            ▼
                    │            │                                      [ Commit ToState ]
                    ▼            ▼                                      [ layerRt.CurTime = ToTime ]
          [ Capture Snapshot ]  [ Blend from CurrentState ]                    │
          [ ActiveTransition ]  [ to TargetState ]                             │
          [ .SnapshotPose    ]           │                                     │
                    │                    ▼                                     │
                    └───────────► [ BlendPoses(mix) ] ◄────────────────────────┘
                                         │
                                         ▼
                             [ RenderTransform Output ]
                               (X, Y, Scale, Rotation, Opacity)
                                         │
                                         ▼
                            [ PropertyBinder.ApplyPose ]
                             (Preserves Layout & Geometry)
                                         │
                                         ▼
                               [ Canvas Redraw (60 FPS) ]
```

### Recommended Project Structure

```
native-windows/src/
├── Engine/
│   ├── Animation/
│   │   ├── StateMachineTypes.cs         # Extended PointerEventKind, ActionKind, NodeListener, ListenerAction
│   │   ├── StateMachineRuntime.cs       # Transition evaluation, snapshot blending, clip playback
│   │   ├── StateMachineValidator.cs     # Diagnostic validation for listeners, inputs, conditions
│   │   ├── AnimationTimelineController.cs # Centralized playback clock and simulation gating
│   │   ├── AnimationTypes.cs            # ClipInstance, Clip, LoopMode, PropKey
│   │   ├── CubicBezierEase.cs           # Easing math for transition blends
│   │   └── PropertyBinder.cs            # Numeric ID hashing and pose application
│   ├── InteractiveEventDispatcher.cs   # NEW: Centralized canvas event router and gesture tracker
│   ├── RetainedSceneGraph.cs            # Matrix-aware hit testing and visual scene hierarchy
│   └── CommandHistory.cs                # AddListenerCommand, ModifyListenerCommand, DeleteListenerCommand
├── UI/
│   └── StudioView.cs                    # Canvas pointer handlers, Right Inspector "Listeners" section, Spacebar gate
└── tests/
    └── TestRunner.cs                    # Automated test suite (Test #66: Listeners, Runtime & Blending)
```

### Pattern 1: Granular Gesture Lifecycle in Centralized Dispatcher
**What:** Distinguish momentary pointer events (`PointerDown`, `PointerUp`) from semantic gestures (`Click`, `Tap`, `DragStart`, `Drag`, `DragEnd`) using movement delta thresholds and press tracking.
**When to use:** When processing canvas mouse interactions during simulation mode (`_timelineController.IsPlaying == true`).
**Example:**
```csharp
// Source: native-windows/src/Engine/InteractiveEventDispatcher.cs
public class InteractiveEventDispatcher
{
    private uint _lastHoverNodeId = 0u;
    private uint _pointerDownNodeId = 0u;
    private Point _pointerDownPoint;
    private bool _isDragging = false;
    private const double DragThreshold = 4.0; // pixels in canvas space

    public void OnPointerMove(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt)
    {
        if (runtime == null || sceneGraph == null) return;
        var hit = sceneGraph.HitTest(canvasPt);
        uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;

        // Hover tracking: Enter and Exit
        if (hitId != _lastHoverNodeId)
        {
            if (_lastHoverNodeId != 0u)
                runtime.OnPointerEvent(_lastHoverNodeId, PointerEventKind.Exit);
            if (hitId != 0u)
                runtime.OnPointerEvent(hitId, PointerEventKind.Enter);
            _lastHoverNodeId = hitId;
        }
        else if (hitId != 0u)
        {
            runtime.OnPointerEvent(hitId, PointerEventKind.Move);
        }

        // Drag tracking
        if (_pointerDownNodeId != 0u)
        {
            double dx = canvasPt.X - _pointerDownPoint.X;
            double dy = canvasPt.Y - _pointerDownPoint.Y;
            double distSq = dx * dx + dy * dy;

            if (!_isDragging && distSq >= DragThreshold * DragThreshold)
            {
                _isDragging = true;
                runtime.OnPointerEvent(_pointerDownNodeId, PointerEventKind.DragStart);
            }
            if (_isDragging)
            {
                runtime.OnPointerEvent(_pointerDownNodeId, PointerEventKind.Drag);
            }
        }
    }

    public void OnPointerDown(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt)
    {
        if (runtime == null || sceneGraph == null) return;
        var hit = sceneGraph.HitTest(canvasPt);
        uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;
        _pointerDownNodeId = hitId;
        _pointerDownPoint = canvasPt;
        _isDragging = false;

        if (hitId != 0u)
        {
            runtime.OnPointerEvent(hitId, PointerEventKind.Down);
        }
    }

    public void OnPointerUp(StateMachineRuntime runtime, RetainedSceneGraph sceneGraph, Point canvasPt)
    {
        if (runtime == null || sceneGraph == null) return;
        var hit = sceneGraph.HitTest(canvasPt);
        uint hitId = (hit != null) ? PropertyBinder.GetNumericNodeId(hit.Id) : 0u;

        if (_isDragging)
        {
            if (_pointerDownNodeId != 0u)
                runtime.OnPointerEvent(_pointerDownNodeId, PointerEventKind.DragEnd);
            _isDragging = false;
        }
        else if (_pointerDownNodeId != 0u && _pointerDownNodeId == hitId)
        {
            runtime.OnPointerEvent(hitId, PointerEventKind.Click);
            runtime.OnPointerEvent(hitId, PointerEventKind.Tap);
        }

        if (hitId != 0u)
        {
            runtime.OnPointerEvent(hitId, PointerEventKind.Up);
        }
        _pointerDownNodeId = 0u;
    }
}
```

### Pattern 2: In-Flight Interrupted Snapshot Blending
**What:** When a running transition $A \to B$ is interrupted by a new transition towards $C$, capture the current interpolated pose into `ActiveTransitionState.SnapshotPose` and blend $Snapshot \to C$.
**When to use:** In `StateMachineRuntime.Update` when `FindValidTransition` returns a new transition while `layerRt.ActiveTransition != null`.
**Example:**
```csharp
// Source: native-windows/src/Engine/Animation/StateMachineRuntime.cs:493-522
Dictionary<uint, Dictionary<ushort, double>> snapshot = null;
if (layerRt.ActiveTransition != null)
{
    var fromPose = new Dictionary<uint, Dictionary<ushort, double>>();
    var toPose = new Dictionary<uint, Dictionary<ushort, double>>();
    if (layerRt.ActiveTransition.SnapshotPose != null)
    {
        fromPose = layerRt.ActiveTransition.SnapshotPose;
    }
    else if (layerRt.CurrentState >= 0 && layerRt.CurrentState < layerDef.States.Count)
    {
        SampleState(layerDef.States[layerRt.CurrentState], layerRt.CurTime.Time, fromPose);
    }

    if (layerRt.ActiveTransition.ToState >= 0 && layerRt.ActiveTransition.ToState < layerDef.States.Count)
    {
        SampleState(layerDef.States[layerRt.ActiveTransition.ToState], layerRt.ActiveTransition.ToTime.Time, toPose);
    }
    double progress = Math.Max(0.0, Math.Min(1.0, layerRt.ActiveTransition.Elapsed / Math.Max(0.001, layerRt.ActiveTransition.Duration)));
    double mix = layerRt.ActiveTransition.Definition.EvaluateInterpolation(progress);
    snapshot = new Dictionary<uint, Dictionary<ushort, double>>();
    BlendPoses(fromPose, toPose, mix, snapshot, RestPose);
}

int toState = nextTrans.ToState;
double duration = nextTrans.Duration;
layerRt.ActiveTransition = new ActiveTransitionState(nextTrans, toState, duration);
layerRt.ActiveTransition.SnapshotPose = snapshot;
```

### Pattern 3: Atomic Reversible Listener Command
**What:** Encapsulate listener creation, property editing, and deletion in reversible `ICommand` classes with index restoration and callback invocation.
**When to use:** Right Inspector authoring actions in `StudioView.cs`.
**Example:**
```csharp
// Source: native-windows/src/Engine/CommandHistory.cs
public class AddListenerCommand : ICommand
{
    public string Description { get { return "Add Listener"; } }
    private readonly StateMachineDefinition _def;
    private readonly NodeListener _listener;
    private readonly Action _onChanged;

    public AddListenerCommand(StateMachineDefinition def, NodeListener listener, Action onChanged = null)
    {
        _def = def;
        _listener = listener;
        _onChanged = onChanged;
    }

    public void Execute()
    {
        if (_def != null && _listener != null)
        {
            if (_def.Listeners == null) _def.Listeners = new List<NodeListener>();
            if (!_def.Listeners.Contains(_listener))
                _def.Listeners.Add(_listener);
            if (_onChanged != null) _onChanged();
        }
    }

    public void Unexecute()
    {
        if (_def != null && _listener != null && _def.Listeners != null)
        {
            _def.Listeners.Remove(_listener);
            if (_onChanged != null) _onChanged();
        }
    }
}
```

### Anti-Patterns to Avoid
- **Falling through to design tools during playback:** In `StudioView.cs`, mouse handlers must check `_timelineController.IsPlaying` and immediately return after dispatching simulation events, preventing accidental layer dragging or vector editing during live testing.
- **Recording live playback state changes into CommandHistory:** Do NOT create undo commands when interactive listeners mutate `MachineInput.Value` or fire triggers during runtime playback. That would corrupt `CommandHistory` with hundreds of simulation keystrokes/hovers.
- **Visual snapping on transition interruption:** Never abruptly reset `ActiveTransition.Elapsed = 0` or jump to the destination state pose. Always snapshot the currently rendered pose and blend from it.
- **Mutating document model bounds (`layer.Width`, `layer.Height`, `layer.X`, `layer.Y`):** Always write animated poses into `layer.RenderTransform` via `PropertyBinder.ApplyPoseToLayer`. Never overwrite Auto Layout frames or base dimensions.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Hit-testing transformed / scaled canvas layers | Custom bounding box math or point-in-polygon | `RetainedSceneGraph.HitTest` | Layers may have rotated/scaled `RenderTransform` matrices, parent group hierarchies, and container clipping (`ClipContent`). `RetainedSceneGraph.HitTest` already handles inverse transform hierarchy accurately. |
| Cubic Bézier curve sampling | Custom polynomial solver | `CubicBezierEase.Evaluate` in `native-windows/src/Engine/Animation/CubicBezierEase.cs` | Precision root-finding algorithm tested in Phase 4 ensures monotonic curve evaluation without NaN edge cases. |
| Trigger variable lifecycle | Manual timeout counters | Single-frame auto-clearing in `StateMachineRuntime.Update` (line 617-626) | Triggers must be valid for exactly one evaluation step (`dt > 0`) to trigger transitions deterministically. |
| Pose interpolation with missing properties | Hand-rolled property dictionaries | `StateMachineRuntime.BlendPoses(..., RestPose)` | Handles rest-pose defaults (Scale=1.0, Opacity=1.0, Anchor=0.5) when a state clip does not keyframe all properties. |

**Key insight:** In direct manipulation animation tools, the most difficult bugs arise from desynchronization between what is rendered on screen and what the interaction system thinks is under the mouse. Reusing `RetainedSceneGraph.HitTest` ensures hit detection uses the exact same transformed bounds as the Direct2D renderer.

## Common Pitfalls

### Pitfall 1: Fallthrough Event Pollution in StudioView
**What goes wrong:** A user clicks a button during playback to test a state machine transition, but the canvas editor also selects the underlying text layer or starts dragging the frame.
**Why it happens:** `StudioView.HandleCanvasMouseDown` dispatches the state machine event and then continues executing line 9437 onward (vector edit mode, corner radius widget, marquee selection, transform handles).
**How to avoid:** In `HandleCanvasMouseDown`, `HandleCanvasMouseMove`, and `HandleCanvasMouseUp`, check:
`if (_currentStudioMode == StudioMode.Animate && _timelineController != null && _timelineController.IsPlaying)`
If true, dispatch to `InteractiveEventDispatcher` and execute `return;` immediately.
**Warning signs:** Bounding boxes appearing or layers moving while interacting with buttons during playback.

### Pitfall 2: High-Frequency Hover Jitter & False Enter/Exit Triggers
**What goes wrong:** Moving the mouse rapidly over adjacent layers fires multiple rapid `PointerEnter` and `PointerExit` events, triggering conflicting state transitions and infinite oscillation.
**Why it happens:** Mouse move coordinates fluctuate across layer borders on sub-pixel boundaries.
**How to avoid:**
1. The dispatcher tracks `_lastHoverNodeId`. Only fire `Exit` on the old ID and `Enter` on the new ID when `hitId != _lastHoverNodeId`.
2. Cycle guard: `StateMachineRuntime.Update` already enforces a maximum of 100 transition steps per frame (lines 474-475) to prevent infinite zero-duration transition loops.
**Warning signs:** Animation vibrating violently between hover and rest states.

### Pitfall 3: Incompatible Listener Action Binding
**What goes wrong:** A listener is created with `ActionKind.SetBool` but targets a `Number` input, or `ActionKind.FireTrigger` targeting a `Bool` input, resulting in silent failure or invalid state.
**Why it happens:** Inspector UI allowing any action kind to be paired with any input without validation.
**How to avoid:**
1. Dynamically filter compatible action kinds in the inspector based on `selectedInput.Kind`.
2. Add validation diagnostic `LISTENER_TYPE_MISMATCH` in `StateMachineValidator.cs` to flag invalid bindings.
3. In `StateMachineRuntime.OnPointerEvent`, guard against type mismatches: `SetBool` checks `input.Kind == InputKind.Bool`, `SetNumber` checks `input.Kind == InputKind.Number`, `FireTrigger` checks `input.Kind == InputKind.Trigger`.
**Warning signs:** Inputs failing to update when clicking or hovering on elements.

### Pitfall 4: Undo/Redo Stale References in Right Inspector
**What goes wrong:** Undoing a deleted listener leaves the Right Inspector displaying stale controls or failing to re-bind to the restored listener.
**Why it happens:** `CommandHistory` restores the data model, but the UI is not refreshed, or event handlers retain closed-over stale instances.
**How to avoid:** In `AddListenerCommand`, `ModifyListenerCommand`, and `DeleteListenerCommand`, invoke the `onChanged` callback on both `Execute()` and `Unexecute()`, which calls `StudioView.RefreshPropertyInspector()`.
**Warning signs:** Listener list in inspector fails to reflect `Ctrl+Z` undo actions until selecting another layer.

## Code Examples

### Discrete Values & Existing Enums (Verbatim In-Repo Sources)

```csharp
// [VERIFIED: native-windows/src/Engine/Animation/StateMachineTypes.cs:6-11]
public enum InputKind
{
    Bool = 0,
    Number = 1,
    Trigger = 2
}

// [VERIFIED: native-windows/src/Engine/Animation/StateMachineTypes.cs:55-63]
public enum ConditionOp
{
    Eq = 0,
    Ne = 1,
    Lt = 2,
    Le = 3,
    Gt = 4,
    Ge = 5
}

// [VERIFIED: native-windows/src/Engine/Animation/StateMachineTypes.cs:643-650]
public enum PointerEventKind
{
    Down = 0,
    Up = 1,
    Move = 2,
    Enter = 3,
    Exit = 4
}

// [VERIFIED: native-windows/src/Engine/Animation/StateMachineTypes.cs:652-662]
public enum ActionKind
{
    SetBool = 0,
    SetNumber = 1,
    Fire = 2,
    SetViewModelBool = 3,
    SetViewModelNumber = 4,
    FireViewModelTrigger = 5,
    SetViewModelText = 6
}

// [VERIFIED: native-windows/src/Engine/Animation/StateMachineTypes.cs:689-703]
public class NodeListener
{
    public uint TargetNodeId { get; set; }
    public PointerEventKind Kind { get; set; }
    public List<ListenerAction> Actions { get; set; }

    public NodeListener() : this(0, PointerEventKind.Down) { }

    public NodeListener(uint targetNodeId, PointerEventKind kind)
    {
        TargetNodeId = targetNodeId;
        Kind = kind;
        Actions = new List<ListenerAction>();
    }
}
```

### Extended Enums & Model Additions for Phase 7

```csharp
// Extended PointerEventKind with backward-compatible integer values
public enum PointerEventKind
{
    Down = 0,
    Up = 1,
    Move = 2,
    Enter = 3,
    Exit = 4,
    Click = 5,
    Tap = 6,
    DragStart = 7,
    Drag = 8,
    DragEnd = 9,
    // Aliases matching specs
    PointerDown = 0,
    PointerUp = 1,
    PointerMove = 2,
    PointerEnter = 3,
    PointerExit = 4
}

// Extended ActionKind with ToggleBool and Reset
public enum ActionKind
{
    SetBool = 0,
    SetNumber = 1,
    Fire = 2,
    SetViewModelBool = 3,
    SetViewModelNumber = 4,
    FireViewModelTrigger = 5,
    SetViewModelText = 6,
    ToggleBool = 7,
    Reset = 8,
    FireTrigger = 2 // Alias for Fire
}
```

### Action Dispatch Handling in StateMachineRuntime

```csharp
// Extended Action Execution in StateMachineRuntime.OnPointerEvent
public void OnPointerEvent(uint targetNodeId, PointerEventKind kind)
{
    if (Definition.Listeners == null) return;
    List<ListenerAction> actionsToRun = new List<ListenerAction>();
    for (int i = 0; i < Definition.Listeners.Count; i++)
    {
        NodeListener listener = Definition.Listeners[i];
        if (listener != null && listener.Enabled && (listener.TargetNodeId == targetNodeId || listener.TargetNodeId == 0) && listener.Kind == kind && listener.Actions != null)
        {
            actionsToRun.AddRange(listener.Actions);
        }
    }

    for (int a = 0; a < actionsToRun.Count; a++)
    {
        ListenerAction action = actionsToRun[a];
        if (action == null) continue;
        int inputIdx = action.InputIndex;
        if (inputIdx < 0 && !string.IsNullOrEmpty(action.InputId))
            inputIdx = Definition.GetInputIndex(action.InputId);

        switch (action.Kind)
        {
            case ActionKind.SetBool:
                if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count)
                    SetBool(inputIdx, action.Parameter > 0.5);
                else if (!string.IsNullOrEmpty(action.PropertyName))
                    SetBool(action.PropertyName, action.Parameter > 0.5);
                break;

            case ActionKind.ToggleBool:
                if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count && Definition.Inputs[inputIdx].Kind == InputKind.Bool)
                    SetBool(inputIdx, Definition.Inputs[inputIdx].Value <= 0.5);
                else if (!string.IsNullOrEmpty(action.PropertyName))
                {
                    var inp = Definition.FindInputById(action.PropertyName);
                    if (inp != null && inp.Kind == InputKind.Bool)
                        SetBool(inp.InputId, inp.Value <= 0.5);
                }
                break;

            case ActionKind.SetNumber:
                if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count)
                    SetNumber(inputIdx, action.Parameter);
                else if (!string.IsNullOrEmpty(action.PropertyName))
                    SetNumber(action.PropertyName, action.Parameter);
                break;

            case ActionKind.Fire:
            case ActionKind.FireTrigger:
                if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count)
                    FireTrigger(inputIdx);
                else if (!string.IsNullOrEmpty(action.PropertyName))
                    FireTrigger(action.PropertyName);
                break;

            case ActionKind.Reset:
                if (inputIdx >= 0 && inputIdx < Definition.Inputs.Count)
                {
                    var inp = Definition.Inputs[inputIdx];
                    inp.Value = inp.InitialValue;
                    inp.Fired = false;
                }
                break;

            case ActionKind.SetViewModelBool:
                SetViewModelBool(action.PropertyName, action.Parameter > 0.5);
                break;
            case ActionKind.SetViewModelNumber:
                SetViewModelNumber(action.PropertyName, action.Parameter);
                break;
            case ActionKind.FireViewModelTrigger:
                FireViewModelTrigger(action.PropertyName);
                break;
            case ActionKind.SetViewModelText:
                SetViewModelText(action.PropertyName, action.TextValue);
                break;
        }
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Ad-hoc hardcoded click handlers in `StudioView.cs` | Centralized `InteractiveEventDispatcher` with full gesture lifecycle | Phase 7 | Enables arbitrary document layers to respond to hover, down, up, click, and drag without code modifications. |
| Modal dialog for listener creation (`ShowAddListenerDialog`) | In-place Right Inspector "Listeners" collapsible panel | Phase 7 (D-02) | Matches professional design tool ergonomics (Figma / Rive), avoiding disruptive modal dialogs. |
| Direct property mutation in document layer model | Non-destructive `RenderTransform` matrix application | Phase 1 & 2 (D-06) | Protects Auto Layout frames, flexbox bounds, and design-time dimensions from animation drift. |
| Transition snapping on interruption | In-flight snapshot crossfading (`ActiveTransitionState.SnapshotPose`) | Phase 6 & 7 (D-04) | Guarantees buttery-smooth interpolated transitions even when interrupting fast recurring animations. |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | 4.0px distance threshold is optimal for distinguishing canvas Click from Drag | Pattern 1 / Dispatcher | Very low — standard desktop GUI threshold; easily adjustable constant. |
| A2 | TargetNodeId = 0u continues to represent Artboard / Global Canvas listener | Pattern 1 / Code Examples | Low — established convention in Phase 6 tests and `StudioView.cs:17776`. |

*No high-risk assumptions present. All core types and behaviors are verified directly against repository source files.*

## Open Questions

1. **Should listeners created in Animate Mode be visible in the Right Inspector during Design Mode?**
   - What we know: D-02 specifies listener creation/inspection lives in the Right Inspector when an object is selected in Animate mode.
   - What's unclear: Whether users might want to view interactive listener triggers while in Design mode.
   - Recommendation: Render the "Listeners" section primarily in `StudioMode.Animate`. In `StudioMode.Design`, provide a subtle badge or toggle to view interactive triggers if desired, keeping Design mode clean.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `C# Compiler (csc.exe)` | Native App & Test Compilation | ✓ | Microsoft (R) Visual C# Compiler version 4.8.9221.0 (C# 5.0) | None needed (Native Windows .NET Framework 4.0) |
| `Rust Cargo` | High-Performance Native Core | ✓ | cargo 1.83.0 / rustc 1.83.0 | Prebuilt `kerf_core.dll` or managed fallback engine in `build.bat` |
| `build.bat --test` | Automated Verification Test Suite | ✓ | Verified executable (65/65 passed) | Direct invocation of `tests\TestRunner.exe` |

**Missing dependencies with no fallback:** None
**Missing dependencies with fallback:** None

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Custom In-Repo Native C# Verification Harness (`Kerf.Tests.TestRunner`) |
| Config file | `native-windows/build.bat` (target: `tests\TestRunner.exe`) |
| Quick run command | `cmd /c "cd native-windows && build.bat --test"` |
| Full suite command | `cmd /c "cd native-windows && build.bat --test"` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| **STATE-04** | Dispatch canvas pointer events (`Enter`, `Exit`, `Down`, `Up`, `Click`, `Tap`, `Drag`) to registered object listeners | Unit / Integration | `cmd /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test #66) |
| **LISTENER-01** | Centralized event dispatcher delivers events and updates `MachineInput` variables | Unit | `cmd /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test #66) |
| **LISTENER-02** | All 5 listener action kinds (`SetBool`, `SetNumber`, `ToggleBool`, `FireTrigger`, `Reset`) and undo/redo commands | Unit | `cmd /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test #66) |
| **RUNTIME-01** | StateMachineRuntime transition condition evaluation, ExitTime gating, and graph-order priority | Unit | `cmd /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test #66) |
| **TRANS-EXEC-01** | Animation clip playback with speed multiplier and loop modes via `ClipInstance.Advance` | Unit | `cmd /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test #66) |
| **BLEND-01** | In-flight interrupted transition snapshot crossfade with zero visual pops or snapping | Unit | `cmd /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test #66) |
| **STATE-05** | Rest-pose fallback blending and non-destructive `RenderTransform` preservation | Unit | `cmd /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test #66) |

### Sampling Rate
- **Per task commit:** Compile and run `build.bat --test` (executes in ~6 seconds).
- **Per wave merge:** Full test run green (all 65 existing tests + Test #66).
- **Phase gate:** 100% test pass rate with 0 regressions before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `native-windows/src/Engine/InteractiveEventDispatcher.cs` — New centralized event dispatcher implementation.
- [ ] `native-windows/tests/TestRunner.cs` — Test #66: `TestInteractiveListenersAndStateMachineRuntimeExecution()`.

## Sources

### Primary (HIGH confidence)
- `native-windows/src/Engine/Animation/StateMachineTypes.cs:6-53, 55-113, 643-703, 866-1021` — `MachineInput`, `TransitionCondition`, `ConditionGroup`, `PointerEventKind`, `ActionKind`, `NodeListener`, `ListenerAction`, `StateMachineDefinition`.
- `native-windows/src/Engine/Animation/StateMachineRuntime.cs:7-25, 27-41, 280-331, 345-418, 420-550, 742-795` — `ActiveTransitionState`, `LayerRuntimeState`, `OnPointerEvent`, `CanTransition`, `FindValidTransition`, `Update`, `BlendPoses`.
- `native-windows/src/Engine/Animation/AnimationTimelineController.cs:21-25, 68-89, 104-145, 156-175` — `IsPlaying`, `Step`, `EvaluateAndNotify` simulation clock gating.
- `native-windows/src/Engine/Animation/AnimationTypes.cs:444-508` — `ClipInstance`, `Advance`, `LoopMode`.
- `native-windows/src/Engine/RetainedSceneGraph.cs:451-513` — Matrix-aware `HitTest`, `CanvasToLocalPoint`, container clipping.
- `native-windows/src/Engine/CommandHistory.cs:11-44, 46-189, 2130-3101` — `ICommand`, `CommandHistory`, `CreateTransitionCommand`, `CreateInputCommand`.
- `native-windows/src/UI/StudioView.cs:9372-9435, 9855-9890, 10682-10744, 20736-20766` — Canvas pointer handlers, transport toggles, simulation mode routing.
- `native-windows/tests/TestRunner.cs:80-96, 68-76, 5961-6135, 6780-6976, 7123-7286` — Existing 65 test suites and test runner scaffold.

### Secondary (MEDIUM confidence)
- `.planning/phases/07-interactive-listeners-runtime/07-CONTEXT.md` — Implementation decisions D-01 through D-06.
- `.planning/ROADMAP.md` — Phase 7 scope, requirements, and deliverables.

## Implementation Plan Decomposition

### Plan 07-01: Centralized Event Dispatcher, Document Object Listeners & Inspector UI
- Expand `PointerEventKind` and `ActionKind` in `StateMachineTypes.cs` (`Click`, `Tap`, `DragStart`, `Drag`, `DragEnd`, `ToggleBool`, `Reset`).
- Add `ListenerId` and `Enabled` properties to `NodeListener`.
- Implement `InteractiveEventDispatcher.cs` with gesture recognition (`Hover`, `Click`, `Drag`), tracking `_lastHoverNodeId` and `_pointerDownNodeId`.
- Implement reversible commands in `CommandHistory.cs`: `AddListenerCommand`, `ModifyListenerCommand`, `DeleteListenerCommand`.
- Build the Right Property Inspector "Listeners" section in `StudioView.cs` when a layer is selected, including card layout, type-safe dropdowns, and delete/enable buttons.

### Plan 07-02: StateMachineRuntime Execution Engine & Transition Blending
- Implement compound condition evaluation (`EvaluateConditions`), exit time gating, and graph-order transition priority in `StateMachineRuntime.cs`.
- Implement `ActionKind.ToggleBool` and `ActionKind.Reset` execution in `OnPointerEvent`.
- Implement `ActiveTransitionState.SnapshotPose` frozen pose sampling for interrupted transitions in `StateMachineRuntime.Update`.
- Verify `ClipInstance.Advance` speed and loop playback with non-destructive `RenderTransform` updates.
- Enhance `StateMachineValidator.cs` with diagnostic checks for listener input compatibility (`LISTENER_TYPE_MISMATCH`).

### Plan 07-03: Spacebar & Play Simulation Gating & Automated Verification Tests
- Gate canvas interactions in `StudioView.cs`: When `_timelineController.IsPlaying == true`, canvas pointer events route to `InteractiveEventDispatcher` and return early; when paused, canvas clicks select objects normally for design/layout editing.
- Wire Spacebar toggle (`ToggleAnimationPlayback()`) to start/pause simulation.
- Implement comprehensive automated test suite in `native-windows/tests/TestRunner.cs` (Test #66) covering:
  1. Pointer event dispatching to object listeners across all event kinds.
  2. All 5 listener action kinds mutating inputs (`SetBool`, `ToggleBool`, `SetNumber`, `FireTrigger`, `Reset`).
  3. Compound transition condition evaluation (OR of ANDs) and exit times.
  4. Graph-order transition priority (outgoing before AnyState).
  5. In-flight interrupted transition snapshot crossfade blending with 0 visual pops.
  6. Reversible undo/redo for listener commands in `CommandHistory`.
- Verify all 66 test suites pass with 0 regressions via `build.bat --test`.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Verified directly from repository compilation scripts and source files.
- Architecture: HIGH — Centralized dispatcher pattern, scene graph hit testing, and runtime snapshot blending thoroughly verified.
- Pitfalls: HIGH — Identifies exact fallthrough and type mismatch issues present in existing code.

**Research date:** 2026-11-08
**Valid until:** 2026-12-08 (30 days)
