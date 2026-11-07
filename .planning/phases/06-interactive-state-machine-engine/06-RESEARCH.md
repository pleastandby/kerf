# Phase 6: Interactive State Machine Engine - Research
**Researched:** 2026-11-07  
**Domain:** WPF Visual Node Graph Authoring, Finite State Machine Architecture, Compound Condition Trees, Addressable Input Systems, Transactional Undo/Redo  
**Confidence:** HIGH  

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01: Workspace Layout & Placement.** Bottom Dock / Split View. The State Machine Editor lives in the expandable bottom panel (toggling with Timeline Clips). The left sidebar hosts the **Inputs list** (`Bool`, `Number`, `Trigger`), the center hosts the pan/zoomable **Graph Canvas**, and the right sidebar hosts the **State & Transition Inspector**. The main design canvas remains visible above for live animation preview. — *Reversibility:* reversible — UI layout placement within existing dock system.
- **D-02: Stable String GUIDs with Index Compatibility Adapters.** All nodes, transitions, and inputs receive stable unique string IDs (e.g. `sm_node_<guid>`, `sm_trans_<guid>`, `sm_input_<guid>`). Helper index adapters and migration loaders preserve compatibility so existing documents, legacy tests, and runtimes continue working without data loss. — *Reversibility:* one-way — Core data model identifier contract.
- **D-03: Compound Condition Groups (OR between groups, AND within a group).** Each transition contains a list of `ConditionGroup` objects. A transition fires if ANY group evaluates to true (OR); within each group, ALL individual conditions must evaluate to true (AND). Conditions reference stable input IDs with typed expression evaluation. — *Reversibility:* costly — Affects transition condition schema and evaluator logic.
- **D-04: Port Drag-to-Target & Canvas Marquee.** Dragging from a node's output port draws a live connection wire following the pointer; releasing over a valid target node creates the transition. Clicking empty canvas clears selection; dragging on empty canvas marquee selects multiple nodes. Nodes display directional connection wires. — *Reversibility:* reversible — User interaction and canvas pointer event handling.
- **D-05: State Node Types & Configuration.** Four first-class node types:
  - **Entry State:** Initial execution point, must have valid outgoing transitions.
  - **Animation State:** References an existing clip from `Document.Animation.Clips`, configurable playback speed, looping flag, and exit-time settings.
  - **Any State:** Allows transitions from eligible active states with priority rules.
  - **Exit State:** Termination of state machine flow.
  Graph positions (`GraphX`, `GraphY`) persist independently of runtime state.
- **D-06: Unified Command History Integration.** Reversible transactional commands in `CommandHistory.cs` covering:
  - `CreateStateNodeCommand`, `DeleteStateNodesCommand`, `MoveStateNodesCommand`, `RenameStateNodeCommand`.
  - `CreateTransitionCommand`, `DeleteTransitionsCommand`, `ModifyTransitionCommand`.
  - `CreateInputCommand`, `DeleteInputCommand`, `ModifyInputCommand`.
  - `ModifyConditionGroupsCommand`.
  Continuous node dragging batches into a single command on pointer release. Multi-selection actions batch via `CompositeCommand`.
- **D-07: Graph Validation Engine.** Non-destructive validator reporting clear diagnostic errors and warnings for:
  - Missing Entry State.
  - Dangling or invalid connections.
  - Missing destination or source nodes.
  - Duplicate node or input IDs.
  - Invalid clip references.
  - Type-incompatible conditions (e.g. comparing Number input with string).
  - Unreachable states.
  The engine reports validation results clearly without silently mutating or discarding invalid data.

### the agent's Discretion

- Visual styling of nodes and ports to conform with `KerfBrand` dark theme (mint accents for active/selected states, dark card backgrounds).
- Exact zoom step thresholds and pan clamping logic for graph navigation.

### Deferred Ideas (OUT OF SCOPE)

- **Phase 7 (Runtime Execution & Interaction Listeners):** Canvas interaction event listeners (Hover In/Out, Pointer Down/Up, Click) bound to state inputs, and full multi-clip cross-fading runtime evaluation.
- **v2 Feature (Blend Trees):** Advanced 1D / 2D directional blend trees (deferred to future milestone per REQUIREMENTS.md).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **STATE-01** | State machine graph model containing Entry, Exit, AnyState, and custom animation states. | Researched `MachineState` and `MachineLayer` in `StateMachineTypes.cs`. Designed stable string GUID schema (`sm_node_<guid>`), first-class node types (`Entry`, `Exit`, `Any`, `Animation`), clip index/name binding, and independent canvas coordinate persistence (`GraphX`, `GraphY`). |
| **STATE-02** | Directional transitions with duration, blend curves, and conditional checks. | Researched `StateTransition` and `TransitionCondition` in `StateMachineTypes.cs` and `StateMachineRuntime.cs`. Designed compound `ConditionGroup` architecture (OR between groups, AND within a group), cubic Bézier blend curve parameters (`X1`, `Y1`, `X2`, `Y2`), normalized exit-time checks, and mid-wire directional arrow rendering. |
| **STATE-03** | State machine inputs system supporting Boolean, Numeric, and Trigger variables. | Researched `MachineInput` in `StateMachineTypes.cs`. Designed addressable stable string GUID schema (`sm_input_<guid>`), default/initial values, runtime evaluation, trigger auto-clearing after 1 update frame, and left sidebar inputs panel. |
| **STATE-06** | Interactive UI panel for inspecting, creating, and wiring state machine nodes and transitions. | Researched StudioView bottom dock architecture in `StudioView.cs`. Designed 3-column split view (Left Inputs list, Center pan/zoom canvas, Right Node & Transition inspector with condition group builder), port drag-to-target wire creation, and empty canvas marquee selection. |
</phase_requirements>

## Summary

Phase 6 transforms Kerf Studio's prototype animation state machine into a robust, industrial-grade, Rive-inspired visual state machine engine and authoring environment. While a basic execution runtime (`StateMachineRuntime.cs`) and preliminary canvas controls exist in the codebase, the data model currently relies on mutable integer array indices for states, transitions, and inputs. When states or inputs are reordered or deleted, existing condition references break. Furthermore, the UI lacks compound boolean logic (it only supports a flat list of AND conditions), lacks full reversible undo/redo integration in `CommandHistory.cs`, and packs inputs and listeners into an unergonomic right sidebar rather than a dedicated 3-column split dock layout.

This phase establishes:
1. **Stable String Identifiers & Compatibility Adapters:** All state nodes, transitions, and inputs receive permanent string GUID identifiers (`sm_node_<guid>`, `sm_trans_<guid>`, `sm_input_<guid>`). Helper index accessors and backward compatibility adapters guarantee that legacy documents and existing unit tests (such as Test #46) continue to compile and pass without regression.
2. **Compound Condition Groups (OR of ANDs):** Each transition transitions from a flat `List<TransitionCondition>` to a hierarchical `List<ConditionGroup>` model, firing if ANY group is true (OR) while requiring ALL conditions within a group to be true (AND).
3. **Visual Graph Authoring Environment:** An interactive WPF graph canvas with smooth pan/zoom (`ScaleTransform` and `ScrollViewer` viewport offset), marquee multi-selection, node dragging with atomic mouse-up command batching, drag-to-target connection wires with dynamic dashed Bézier previews, and directional connection wires with mid-wire arrows.
4. **Dedicated 3-Column Dock Layout:** An expandable bottom split dock featuring:
   - **Left Inputs Panel:** Add, delete, rename, and set default/runtime values for Boolean, Number, and Trigger variables.
   - **Center Graph Canvas:** Interactive node wiring with visual active-state telemetry and graph fit/zoom tools.
   - **Right Inspector Panel:** Context-sensitive inspector switching between Node Inspector (clip binding, speed, loop) and Transition Inspector (duration, exit-time, blend curve, and compound condition group builder).
5. **Transactional Command History:** Symmetrical `ICommand` classes for node CRUD, movement, renaming, transition CRUD, condition group modification, and input manipulation, batching continuous drags into single undo entries.
6. **Non-Destructive Graph Validator:** A diagnostics engine detecting missing Entry nodes, dangling transitions, unreachable states, and invalid clip references without corrupting or discarding model data.

**Primary recommendation:** Build the core data model enhancements (`sm_*` GUIDs, `ConditionGroup`, `StateMachineValidator`) and reversible commands first in `StateMachineTypes.cs` and `CommandHistory.cs`, verify complete backward compatibility against Test #46, then construct the 3-column dock UI in `StudioView.cs` and implement Test #60 in `TestRunner.cs`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| **State Machine Graph Model** (`MachineState`, `StateTransition`, `MachineLayer`) | API / Engine (`Kerf.Engine.Animation`) | Serialization / Model (`Kerf.Model`) | Core animation data structures must be purely computational and independent of WPF UI components so runtime playback and automated test fixtures run headlessly. |
| **Addressable Input System** (`MachineInput`, `InputKind`) | API / Engine (`Kerf.Engine.Animation`) | UI / StudioView | Inputs store default values and runtime values, auto-clearing triggers during runtime ticks. UI only displays and triggers mutations. |
| **Compound Condition Groups** (`ConditionGroup`, `TransitionCondition`) | API / Engine (`Kerf.Engine.Animation`) | UI / Inspector | Boolean evaluation logic (`OR` of `AND`s) belongs strictly in the engine. UI provides the visual condition builder interface. |
| **Visual Graph Canvas** (Pan, Zoom, Drag, Wire Drawing) | Client UI (`Kerf.UI.StudioView`) | Layout / Transforms | WPF `Canvas`, `ScrollViewer`, `ScaleTransform`, and mouse pointer event handling own user input, hit-testing, and rendering of visual cards and Bézier wires. |
| **Graph Diagnostics & Validation** (`StateMachineValidator`) | API / Engine (`Kerf.Engine.Animation`) | Client UI (`Kerf.UI`) | Diagnostic rules must run headlessly in CI tests and command pipelines; UI consumes diagnostics to display error badges and warnings. |
| **Undo / Redo Transaction Engine** (`CommandHistory`) | API / Engine (`Kerf.Engine`) | Client UI (`Kerf.UI`) | Encapsulates symmetric `Execute()` and `Unexecute()` commands, dirty tracking, and batching on mouse pointer release. |

## Standard Stack

### Core
| Library / Technology | Version | Purpose | Why Standard |
|----------------------|---------|---------|--------------|
| **Microsoft .NET Framework** | 4.8 / C# 5 | Host runtime and language compiler (`csc.exe`) | Project baseline runtime (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`). |
| **WPF PresentationFramework & Core** | 4.0.0.0 | Graph canvas, dock panels, controls, and vector drawing | High-performance hardware-accelerated vector UI pipeline supporting Bézier segments, transforms, and data binding. |
| **System.Windows.Media** | 4.0.0.0 | `PathGeometry`, `BezierSegment`, `ScaleTransform`, `SolidColorBrush` | Native WPF geometry primitives for rendering smooth transition wires and handles. |
| **KerfBrand** | In-repo (`src/UI/KerfBrand.cs`) | Dark Figma-inspired theme, Mint `#46EFB1` accents, button factories | Consistent design language matching established Kerf Studio dark theme. |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| **CubicBezierEase** | In-repo (`src/Engine/Animation/EasingEngine.cs`) | Transition blend curve evaluation | Evaluating normalized transition blend progress across cubic control points $(X_1, Y_1, X_2, Y_2)$. |
| **CommandHistory** | In-repo (`src/Engine/CommandHistory.cs`) | Undo/redo stack management and transaction batching | Executing all user-initiated graph edits, input changes, and condition modifications. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| **In-repo WPF Canvas & Bézier Shapes** | Third-party graph library (e.g. NodeNetwork, GraphX) | Forbidden by project constraints (must be 100% native self-contained C# code without external dependencies). Hand-crafting on WPF `Canvas` gives exact brand parity and zero external baggage. |
| **Integer-based node indexing** | Pure string-only ID model without int adapters | Breaking change: immediately breaks 59 existing unit tests and legacy project files. Index adapters provide stable string GUIDs while preserving full backward compatibility. |

## Package Legitimacy Audit

> **No external packages required.** Phase 6 builds exclusively upon the native Windows .NET Framework 4.8 runtime assemblies (`PresentationFramework.dll`, `PresentationCore.dll`, `WindowsBase.dll`, `System.dll`, `System.Core.dll`) and existing in-repo classes.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| *None (Native .NET Framework & WPF)* | N/A | >15 yrs | N/A | Microsoft Windows SDK | [OK] | Approved |

**Packages removed due to [SLOP] verdict:** None  
**Packages flagged as suspicious [SUS]:** None  

## Architecture Patterns

### System Architecture Diagram

```
+----------------------------------------------------------------------------------------------------+
|                                    Kerf Studio Main Window                                         |
|                                                                                                    |
|  +----------------------------------------------------------------------------------------------+  |
|  |                     Top Design Canvas & Animation Live Preview (Direct2D)                    |  |
|  +----------------------------------------------------------------------------------------------+  |
|                                                                                                    |
|  +==============================================================================================+  |
|  |                Expandable Bottom Panel (StudioView Animation Sub-Mode)                       |  |
|  |                                                                                              |  |
|  |  +------------------+  +---------------------------------------+  +-----------------------+  |  |
|  |  |   Left Panel     |  |          Center Panel                 |  |      Right Panel      |  |  |
|  |  |  (Width: 220px)  |  |           (1* Star)                   |  |     (Width: 280px)    |  |  |
|  |  |                  |  |                                       |  |                       |  |  |
|  |  |  INPUTS LIST     |  |  GRAPH CANVAS (Pan & Zoom)            |  |  INSPECTOR            |  |  |
|  |  |                  |  |                                       |  |                       |  |  |
|  |  |  [+ Add Input v] |  |  Toolbar: Zoom: [-][100%][+][Fit]     |  |  [Context Sensitive] |  |  |
|  |  |  - Bool          |  |           [+ State] [Diagnostics v]   |  |                       |  |  |
|  |  |  - Number        |  |                                       |  |  (If Node Selected)   |  |  |
|  |  |  - Trigger       |  |  (Entry)                              |  |  - Name, Kind, Clip   |  |  |
|  |  |                  |  |     \ (sm_trans_01)                   |  |  - Speed, Loop, Exit  |  |  |
|  |  |  Items:          |  |      v                                |  |                       |  |  |
|  |  |  * isHover [ON]  |  |   [Idle Node] ===> [Hover Node]       |  |  (If Trans Selected)  |  |  |
|  |  |  * speed   [1.5] |  |   (sm_node_1)  mid (sm_node_2)        |  |  - Duration, Easing   |  |  |
|  |  |  * onClick [⚡]  |  |                 arrow                  |  |  - Exit Time [0..1]   |  |  |
|  |  |                  |  |                                       |  |  - Condition Groups:  |  |  |
|  |  |  Default & Live  |  |  Marquee Multi-Selection Box [..]     |  |    Group 1 (AND)      |  |  |
|  |  |  Scrubbing       |  |  Port Drag-to-Target Dashed Bézier    |  |    Group 2 (OR)       |  |  |
|  |  +------------------+  +---------------------------------------+  +-----------------------+  |  |
|  +==============================================================================================+  |
+----------------------------------------------------------------------------------------------------+
                                               |
                                     Model & Event Flow
                                               v
+----------------------------------------------------------------------------------------------------+
|                                    Kerf Engine Core Layer                                          |
|                                                                                                    |
|  +--------------------------+  +-----------------------------+  +-------------------------------+  |
|  |     StateMachineTypes    |  |     StateMachineValidator   |  |        CommandHistory         |  |
|  |  - MachineState (sm_node)|  |  - Validate()               |  |  - CreateStateNodeCommand     |  |
|  |  - StateTransition (sm)  |  |  - Missing Entry            |  |  - MoveStateNodesCommand      |  |
|  |  - MachineInput (sm_in)  |  |  - Unreachable States       |  |  - DeleteStateNodesCommand    |  |
|  |  - ConditionGroup (OR)   |  |  - Dangling Connections     |  |  - CreateTransitionCommand    |  |
|  |  - Typed Condition (AND) |  |  - Type Incompatibilities   |  |  - ModifyConditionGroupsCmd   |  |
|  +--------------------------+  +-----------------------------+  +-------------------------------+  |
|                                              |                                                     |
|                                              v                                                     |
|  +----------------------------------------------------------------------------------------------+  |
|  |                         StateMachineRuntime (Headless Evaluation)                            |  |
|  |  - Update(dt, outPose)                                                                       |  |
|  |  - Evaluate ConditionGroups (Group OR, Condition AND)                                        |  |
|  |  - Instant Chain Resolution & Trigger Auto-Clear                                             |  |
|  +----------------------------------------------------------------------------------------------+  |
+----------------------------------------------------------------------------------------------------+
```

### Recommended Project Structure

```
native-windows/
├── src/
│   ├── Engine/
│   │   ├── Animation/
│   │   │   ├── StateMachineTypes.cs       # ENHANCED: Stable string GUIDs, ConditionGroup, MachineInput
│   │   │   ├── StateMachineRuntime.cs     # ENHANCED: ConditionGroup evaluation & backward compatibility
│   │   │   ├── StateMachineValidator.cs   # NEW: Non-destructive graph diagnostic rules engine
│   │   │   ├── AnimationTypes.cs          # Clip, AnimTrack, Keyframe, AnimationProjectData
│   │   │   └── EasingEngine.cs            # CubicBezierEase solver
│   │   └── CommandHistory.cs              # ENHANCED: Reversible state machine & transition commands
│   └── UI/
│       ├── StudioView.cs                  # ENHANCED: 3-column dock layout, canvas marquee, port wiring, inspector
│       └── KerfBrand.cs                   # Dark theme brushes and styles
└── tests/
    └── TestRunner.cs                      # ENHANCED: Test #60 validating all graph operations & undo/redo
```

### Pattern 1: Stable String GUID & Index Compatibility Adapter

**What:** Every state node, transition, condition group, and input is given a globally unique, stable string ID prefixed by entity type (e.g. `sm_node_<guid>`, `sm_trans_<guid>`, `sm_input_<guid>`). Helper methods map transparently between integer index and string ID.  
**When to use:** Crucial for preventing corruption when states or inputs are deleted, reordered, or serialized. Legacy tests and existing documents referencing integer indices continue functioning through index adapters.  
**Code Example:**
```csharp
// In StateMachineTypes.cs
public class MachineState
{
    public string NodeId { get; set; } // Stable unique GUID
    public int Id { get; set; }         // Backward-compatible index
    public string Name { get; set; }
    public StateKind Kind { get; set; }
    public int ClipIndex { get; set; }
    public double GraphX { get; set; }
    public double GraphY { get; set; }
    public double Speed { get; set; }
    public bool Loop { get; set; }

    public MachineState() : this(0, "State", StateKind.Animation) { }

    public MachineState(int id, string name, StateKind kind, int clipIndex = -1, string nodeId = null)
    {
        NodeId = !string.IsNullOrEmpty(nodeId) ? nodeId : "sm_node_" + Guid.NewGuid().ToString("N");
        Id = id;
        Name = name ?? "State";
        Kind = kind;
        ClipIndex = clipIndex;
        GraphX = -1.0;
        GraphY = -1.0;
        Speed = 1.0;
        Loop = true;
    }
}
```

### Pattern 2: Compound Condition Groups (OR between groups, AND within a group)

**What:** In professional state machines (such as Rive and Unity Animator), a transition fires if any alternative scenario matches (OR), where each scenario may require several inputs to match simultaneously (AND).  
**Structure:**
- `Transition.ConditionGroups` contains a list of `ConditionGroup`.
- If `ConditionGroups.Count == 0`, transition is unconditional (fires when exit time is reached).
- A transition fires if `ConditionGroups.Any(group => group.Evaluate(inputs, viewModel, def))`.
- Inside each `ConditionGroup`, all conditions must pass: `Conditions.All(cond => cond.IsTrue(inputs, viewModel, def))`.
- For backward compatibility with existing tests that directly populate `transition.Conditions`, if `ConditionGroups` is empty but `transition.Conditions.Count > 0`, the evaluator evaluates `transition.Conditions` as a single group.

### Pattern 3: Pan/Zoom Canvas with ScaleTransform & Marquee Multi-Selection

**What:** The visual graph editor requires infinite-feel panning, centered mouse-wheel zooming, and multi-node selection via empty-canvas click and drag.  
**Mechanics:**
- **Zoom:** `ScaleTransform` applied to `_stateMachineGraphCanvas.LayoutTransform`. `HandleStateGraphMouseWheel` scales by `1.12` or `1.0 / 1.12` anchored at the cursor position, clamping zoom between `25%` and `250%`.
- **Pan:** Middle mouse drag or `Space + Left Mouse` drag shifts `_stateMachineGraphScrollViewer.ScrollToHorizontalOffset` and `ScrollToVerticalOffset`.
- **Marquee:** Dragging on empty canvas (left mouse without space) creates a dashed selection rectangle (`Rectangle` in `KerfBrand.MintBrush`). On release, hit-tests all nodes against the marquee bounding box, adding intersected nodes to `_selectedGraphStateIndices`.
- **Batch Drag:** Dragging any selected node moves all selected nodes by `delta = currentPt - originPt`. On pointer release, a single `MoveStateNodesCommand` is committed to `CommandHistory`.

### Pattern 4: Port Drag-to-Target Live Bézier Connection with Mid-Wire Arrows

**What:** Clicking and dragging from a node's output port draws a live dashed Bézier curve to the mouse pointer (`KerfBrand.MintBrush`, `StrokeDashArray = { 3, 2 }`). Releasing over another node creates a new directional transition.  
**Wire Rendering:**
- Source right anchor $P_0 = (\text{from.GraphX} + \text{width}, \text{from.GraphY} + \text{height}/2)$.
- Target left anchor $P_3 = (\text{to.GraphX}, \text{to.GraphY} + \text{height}/2)$ (or target right anchor if backwards).
- Cubic Bézier control points $P_1 = (P_0.X + \text{reach}, P_0.Y)$ and $P_2 = (P_3.X - \text{reach}, P_3.Y)$.
- Mid-wire arrow is rendered at $t = 0.5$:
  - Position: $B(0.5) = \frac{1}{8} P_0 + \frac{3}{8} P_1 + \frac{3}{8} P_2 + \frac{1}{8} P_3$.
  - Tangent direction: $B'(0.5) = \frac{3}{4} (P_3 + P_2 - P_1 - P_0)$.
  - A small polygon arrow is aligned along the normalized tangent vector, indicating directional flow.

### Pattern 5: Three-Column Bottom Dock Layout

**What:** Dedicated, non-cramped bottom dock replacing collapsible right sidebars:
- **Column 0 (Width: 220px, Left):** Inputs Panel with type filtering, search bar, "+ Add Input" menu (Bool, Number, Trigger), default value editors, delete buttons, and live interactive toggles.
- **Column 1 (1* Star, Center):** Graph Canvas with header toolbar (Active state badge, Preview toggle, Zoom controls, Fit button, "+ State" button, and Diagnostics indicator).
- **Column 2 (Width: 280px, Right):** Context-sensitive Inspector:
  - When node selected: State Name, Kind, Clip selector, Speed scrub, Looping toggle, Delete State button.
  - When transition selected: From $\to$ To header, Duration scrub, Exit Time toggle & slider, Easing picker, and Condition Groups builder.
  - When empty selection: State Machine properties summary.

### Pattern 6: Reversible Transactional Command History

**What:** All state machine authoring operations implement `ICommand` (`Execute()` and `Unexecute()`):
- `CreateStateNodeCommand` / `DeleteStateNodesCommand`
- `MoveStateNodesCommand` (batches continuous dragging on pointer release)
- `RenameStateNodeCommand`
- `CreateTransitionCommand` / `DeleteTransitionsCommand` / `ModifyTransitionCommand`
- `CreateInputCommand` / `DeleteInputCommand` / `ModifyInputCommand`
- `ModifyConditionGroupsCommand`
Deleting a state node cascades to remove connected transitions; unexecuting restores both the state and all previously connected transitions cleanly.

### Pattern 7: Non-Destructive Graph Validation Engine

**What:** A diagnostics engine that evaluates the state machine definition without mutating or altering it.  
**Diagnostic Codes:**
- `MISSING_ENTRY`: No Entry state found in layer.
- `DANGLING_TRANSITION`: Transition refers to non-existent source or destination ID/index.
- `INVALID_TARGET_ENTRY`: Transition points to Entry state as destination.
- `INVALID_TARGET_ANY`: Transition points to Any state as destination.
- `UNREACHABLE_STATE`: State cannot be reached from Entry or AnyState via graph traversal.
- `INVALID_CLIP_REF`: Animation state references an invalid clip index or missing clip.
- `TYPE_MISMATCH`: Condition attempts relational comparison (`<`, `>`, `<=`, `>=`) on a Boolean variable.
The validator returns a `StateMachineValidationResult` containing a list of `ValidationIssue` entries (`Severity.Error`, `Severity.Warning`, `Severity.Info`).

### Anti-Patterns to Avoid

- **Anti-Pattern 1: Mutating Document Model on Every Pointer Move.**  
  *Why bad:* Floods the undo stack with hundreds of micro-movements, causing extreme memory pressure and sluggish undo performance.  
  *Fix:* Update local node positions visually at 60 FPS during mouse move; push a single `MoveStateNodesCommand` only on `MouseUp`.
- **Anti-Pattern 2: Breaking Array Indexing for Existing Tests.**  
  *Why bad:* Test #46 and existing serialization code rely on integer state indices and input indices.  
  *Fix:* Implement dual-mode accessors where `Id` (`int`) and `NodeId` (`string`) coexist, and lookup functions resolve either seamlessly.
- **Anti-Pattern 3: Destructive Validation Engine.**  
  *Why bad:* Automatically stripping "invalid" transitions or nodes during validation destroys user work in progress.  
  *Fix:* Validation must be strictly read-only, producing structured diagnostic issue reports for user remediation.
- **Anti-Pattern 4: Coupling Graph Authoring to Runtime Execution Ticks.**  
  *Why bad:* Creating history entries or dirtying documents during runtime animation playback corrupts undo state.  
  *Fix:* Runtime ticks (`StateMachineRuntime.Update()`) operate purely on memory poses and trigger state transitions without invoking `CommandHistory`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| **Bézier Transition Easing Math** | Custom equation solvers | In-repo `CubicBezierEase.Evaluate(...)` (`src/Engine/Animation/EasingEngine.cs`) | Verified Newton-Raphson + bisection solver already built and tested in Phase 4. |
| **Vector Geometry Rendering** | Manual pixel buffers or GDI lines | WPF `PathGeometry` with `PathFigure` and `BezierSegment` | Hardware-accelerated Direct2D-backed vector graphics with built-in stroke, dash, and clipping. |
| **Undo / Redo Architecture** | Custom undo stacks inside StudioView | In-repo `CommandHistory` and `CompositeCommand` (`src/Engine/CommandHistory.cs`) | Centralized transactional engine with dirty state tracking, event dispatching, and batching. |
| **Brand Colors and Brushes** | Hardcoded color literals (`#46EFB1`, `#2C2C2C`) | `KerfBrand.MintBrush`, `KerfBrand.FigmaPanelBrush`, `KerfBrand.FigmaCardBrush` | Guarantees consistent Figma-dark aesthetic and prevents UI visual fragmentation. |

**Key insight:** The existing codebase already provides industrial-grade easing math, command history infrastructure, and brand palettes. Phase 6 synthesizes these proven subsystems into the state machine domain rather than re-inventing them.

## Runtime State Inventory

> Required for refactor and migration phases.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| **Stored data** | Existing project documents storing `Animation.StateMachine` with integer indices (`FromState`, `ToState`, `InputIndex`, `ClipIndex`). | Code edit: Add migration loader in `StateMachineTypes.cs` and `StateMachineRuntime.cs` that auto-generates stable string GUIDs (`sm_node_*`, `sm_trans_*`, `sm_input_*`) if absent upon load, and populates default `ConditionGroup` from existing flat conditions. |
| **Live service config** | None — Kerf Studio is a fully local desktop motion graphics application without cloud backend runtime dependencies. | None — Verified by codebase inspection. |
| **OS-registered state** | None — No OS registry keys or task scheduler entries reference state machine model structures. | None — Verified by codebase inspection. |
| **Secrets/env vars** | None — No environment variables govern state machine data. | None — Verified by codebase inspection. |
| **Build artifacts** | Prebuilt binaries in `native-windows/KerfStudio.exe`, `native-windows/tests/TestRunner.exe`. | Recompile both via `build.bat` and `build.bat --test` after code edits. |

## Common Pitfalls

### Pitfall 1: C# 5 Language Limitations in `csc.exe`
**What goes wrong:** Build failure with compiler syntax errors: `Feature 'interpolated strings' is not available in C# 5`, `Feature 'expression-bodied members' is not available in C# 5`.  
**Why it happens:** The project uses the native Windows .NET Framework 4.8 C# compiler `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, which supports up to C# 5.  
**How to avoid:**
- Do NOT use string interpolation `$"..."`. Use `string.Format("...", ...)` or string concatenation.
- Do NOT use expression-bodied members `=>`. Write full method/property bodies `{ get { return ...; } }`.
- Do NOT use `nameof(...)`. Use string literals.
- Do NOT use null-conditional operators `?.` or `?[]`. Use explicit `if (x != null)`.
- Do NOT use auto-property initializers `public int X { get; set; } = 5;`. Initialize in constructors.

### Pitfall 2: Trigger Input Lifespan (Auto-Clear Rule)
**What goes wrong:** Trigger inputs cause endless state transition loops or fire repeatedly on every frame.  
**Why it happens:** Unlike booleans or numbers, triggers are instantaneous impulses. If not cleared immediately after one evaluation cycle, the transition continues to evaluate as true.  
**How to avoid:** In `StateMachineRuntime.Update(dt, outPose)`, ensure `input.Fired = false` is executed for all `InputKind.Trigger` inputs at the conclusion of the update frame (already established in `StateMachineRuntime.cs:513-520`).

### Pitfall 3: Reordering/Deleting States Breaking Transitions
**What goes wrong:** Deleting state node `2` shifts states `3` and `4` to indices `2` and `3`. Transitions referencing `FromState = 4` now point to the wrong state or throw index out of bounds exceptions.  
**Why it happens:** Relying solely on array index offsets for graph connectivity.  
**How to avoid:** Store stable `FromStateId` and `ToStateId` on each transition. Use helper lookup methods `layer.FindStateById(id)` to resolve target states. When deleting states, use `DeleteStateNodesCommand` which cascade-removes connected transitions and cleanly restores them on `Unexecute()`.

### Pitfall 4: Backward Compatibility Regression in Test #46
**What goes wrong:** Introducing new constructors or altering method signatures causes `TestVectorAnimationAndStateMachineEngine` in `TestRunner.cs` to fail to compile or assert.  
**Why it happens:** Test #46 directly instantiates:
```csharp
[VERIFIED: native-windows/tests/TestRunner.cs:2277-2292]
StateMachineDefinition smDef = new StateMachineDefinition("InteractiveCard");
int trigClick = smDef.AddInput(MachineInput.Trigger("onClick"));
int boolHover = smDef.AddInput(MachineInput.Bool("isHover", false));
int numSpeed = smDef.AddInput(MachineInput.Number("speed", 1.0));
MachineLayer layer = new MachineLayer("BaseLayer");
int sIdle = layer.AddState(new MachineState(0, "Idle", StateKind.Animation, 0));
int sHover = layer.AddState(new MachineState(1, "Hover", StateKind.Animation, 1));
int sPressed = layer.AddState(new MachineState(2, "Pressed", StateKind.Animation, 2));
layer.AddTransition(new StateTransition(sIdle, sHover, 0.2).WithCondition(new TransitionCondition(boolHover, ConditionOp.Eq, 1.0)));
```
**How to avoid:** Preserve all existing constructors, method signatures, and enum values. Add new methods and properties as overloads or extensions, ensuring existing tests compile without a single edit.

## Code Examples

### 1. In-Repo Verbatim Source Quotations

The following enums and structures are defined in `native-windows/src/Engine/Animation/StateMachineTypes.cs`:

```csharp
// [VERIFIED: native-windows/src/Engine/Animation/StateMachineTypes.cs:6-11]
    public enum InputKind
    {
        Bool = 0,
        Number = 1,
        Trigger = 2
    }
```

```csharp
// [VERIFIED: native-windows/src/Engine/Animation/StateMachineTypes.cs:48-56]
    public enum ConditionOp
    {
        Eq = 0,
        Ne = 1,
        Lt = 2,
        Le = 3,
        Gt = 4,
        Ge = 5
    }
```

```csharp
// [VERIFIED: native-windows/src/Engine/Animation/StateMachineTypes.cs:150-158]
    public enum StateKind
    {
        Entry = 0,
        Exit = 1,
        Any = 2,
        Animation = 3,
        Blend1D = 4,
        BlendDirect = 5
    }
```

```csharp
// [VERIFIED: native-windows/src/Engine/CommandHistory.cs:11-16]
    public interface ICommand
    {
        string Description { get; }
        void Execute();
        void Unexecute();
    }
```

### 2. Compound ConditionGroup Implementation Pattern

```csharp
namespace Kerf.Engine.Animation
{
    public class ConditionGroup
    {
        public string Id { get; set; }
        public List<TransitionCondition> Conditions { get; set; }

        public ConditionGroup(string id = null)
        {
            Id = !string.IsNullOrEmpty(id) ? id : "sm_cg_" + Guid.NewGuid().ToString("N");
            Conditions = new List<TransitionCondition>();
        }

        public ConditionGroup Clone()
        {
            var grp = new ConditionGroup(Id);
            if (Conditions != null)
            {
                foreach (var c in Conditions)
                {
                    if (c == null) continue;
                    grp.Conditions.Add(new TransitionCondition(c.InputIndex, c.Op, c.Value)
                    {
                        InputId = c.InputId,
                        PropertyName = c.PropertyName,
                        TextValue = c.TextValue
                    });
                }
            }
            return grp;
        }

        public bool Evaluate(IList<MachineInput> inputs, StateMachineViewModel viewModel, StateMachineDefinition def)
        {
            if (Conditions == null || Conditions.Count == 0) return true;
            for (int i = 0; i < Conditions.Count; i++)
            {
                var cond = Conditions[i];
                if (cond == null) continue;
                if (!cond.IsTrue(inputs, viewModel, def)) return false;
            }
            return true;
        }
    }
}
```

### 3. Reversible State Machine Commands Pattern

```csharp
namespace Kerf.Engine
{
    public class CreateStateNodeCommand : ICommand
    {
        public string Description { get { return "Add State Node: " + _state.Name; } }
        private readonly MachineLayer _layer;
        private readonly MachineState _state;
        private readonly Action _onChanged;

        public CreateStateNodeCommand(MachineLayer layer, MachineState state, Action onChanged)
        {
            _layer = layer;
            _state = state;
            _onChanged = onChanged;
        }

        public void Execute()
        {
            _layer.AddState(_state);
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            _layer.States.Remove(_state);
            if (_onChanged != null) _onChanged();
        }
    }

    public class MoveStateNodesCommand : ICommand
    {
        public string Description { get { return "Move State Nodes"; } }
        public class NodeMoveRecord
        {
            public MachineState State;
            public Point OldPos;
            public Point NewPos;
        }
        private readonly List<NodeMoveRecord> _records;
        private readonly Action _onChanged;

        public MoveStateNodesCommand(IEnumerable<NodeMoveRecord> records, Action onChanged)
        {
            _records = new List<NodeMoveRecord>(records);
            _onChanged = onChanged;
        }

        public void Execute()
        {
            for (int i = 0; i < _records.Count; i++)
            {
                _records[i].State.GraphX = _records[i].NewPos.X;
                _records[i].State.GraphY = _records[i].NewPos.Y;
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            for (int i = 0; i < _records.Count; i++)
            {
                _records[i].State.GraphX = _records[i].OldPos.X;
                _records[i].State.GraphY = _records[i].OldPos.Y;
            }
            if (_onChanged != null) _onChanged();
        }
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Flat condition list on transitions (AND only) | Hierarchical `ConditionGroup` objects (OR of ANDs) | Phase 6 | Enables modeling complex branching logic (e.g. fire transition if `[hover AND click]` OR `[keyPress]`). |
| Unstable integer array indexing (`FromState = 2`) | Permanent string GUIDs (`NodeId = "sm_node_..."`) with index adapters | Phase 6 | Reordering or deleting states no longer breaks existing transitions or conditions. |
| Collapsible right sidebar packing inputs and listeners together | 3-Column Split View Dock (Left Inputs, Center Canvas, Right Inspector) | Phase 6 | Conforms to modern motion design studio ergonomics (Rive/Unity) with dedicated property inspection. |
| Non-undoable graph modifications in UI | Centralized, reversible `ICommand` implementations in `CommandHistory` | Phase 6 | Full `Ctrl+Z` / `Ctrl+Y` undo/redo across state creation, movement, wiring, and condition edits. |
| Undetected dangling connections or missing entry states | Non-destructive `StateMachineValidator` with visual diagnostic badges | Phase 6 | Immediate user feedback preventing runtime crashes and silent playback stalls. |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Bottom dock split layout (Left Inputs 220px, Center Canvas 1* Star, Right Inspector 280px) fits standard 1080p and 1440p displays without clipping. [ASSUMED] | Architecture Patterns | Low. Widths can be adjusted or made resizable via `GridSplitter`. |
| A2 | Existing documents contain `Animation.StateMachine` with null or default values rather than serialized JSON files requiring binary migration. [ASSUMED] | Runtime State Inventory | Low. In-repo schema handles backward compatibility automatically via fallback loaders. |

## Open Questions (RESOLVED)

1. **GridSplitter Support for Bottom Dock Columns:**
   - *What we know:* Fixed column widths (220px, 1* Star, 280px) provide clean separation.
   - *What's unclear:* Whether users prefer resizable sidebars via `GridSplitter`.
   - *Recommendation:* Start with fixed default widths with min-width constraints, adding `GridSplitter` if space allows.
   - *RESOLVED:* Start with fixed default widths (220px left, 280px right) with min-width constraints per D-01; GridSplitter can be added in a future enhancement if needed.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| **C# Compiler (`csc.exe`)** | Build & Test runner compilation | ✓ | 4.8.9221.0 (C# 5) | Required, verified at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` |
| **Rust / Cargo** | Native core DLL (`kerf_core.dll`) | ✓ | Current release | Managed C# fallback in `GpuRenderer.cs` if DLL missing |
| **WPF Assemblies** | UI controls, canvas, and geometry | ✓ | .NET Framework 4.8 GAC | Required |
| **DirectX / Direct2D** | Hardware-accelerated canvas | ✓ | Native Windows | Software fallback rendering |

**Missing dependencies with no fallback:** None.  
**Missing dependencies with fallback:** None.  

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Custom In-Repo Unit & Integration Test Harness (`native-windows/tests/TestRunner.cs`) |
| Config file | `native-windows/build.bat` |
| Quick run command | `cmd.exe /c "cd /d native-windows && build.bat --test"` |
| Full suite command | `cmd.exe /c "cd /d native-windows && build.bat --test"` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| **STATE-01** | State machine graph model with Entry, Exit, AnyState, Animation states, and stable GUIDs | Unit / Integration | `cmd.exe /c "cd /d native-windows && build.bat --test"` | ❌ (Will be added in Test #60) |
| **STATE-02** | Compound condition groups (`OR` of `AND`s), duration, blend curves, and typed operators | Unit / Integration | `cmd.exe /c "cd /d native-windows && build.bat --test"` | ❌ (Will be added in Test #60) |
| **STATE-03** | State machine inputs system supporting Boolean, Numeric, and Trigger variables with auto-clear | Unit / Integration | `cmd.exe /c "cd /d native-windows && build.bat --test"` | ❌ (Will be added in Test #60) |
| **STATE-06** | Graph validation engine, reversible command history undo/redo, and interactive UI model wiring | Unit / Integration | `cmd.exe /c "cd /d native-windows && build.bat --test"` | ❌ (Will be added in Test #60) |
| **MIGR-03** | Zero regressions across all 59 existing test fixtures (including Test #46) | Regression | `cmd.exe /c "cd /d native-windows && build.bat --test"` | ✅ Existing (59 passing) |

### Sampling Rate
- **Per task commit:** `cmd.exe /c "cd /d native-windows && build.bat --test"` (Takes ~4 seconds synchronously).
- **Per wave merge:** Full test suite clean pass (60 tests passed, 0 failed).
- **Phase gate:** Full suite green before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] Add `TestInteractiveStateMachineEngine` scaffold (Test #60) to `native-windows/tests/TestRunner.cs`.
- [ ] Register `RunTest("Interactive State Machine Engine: Graph Model, Condition Groups, Inputs, Validation & Undo/Redo", TestInteractiveStateMachineEngine);` in `TestRunner.Main()`.

## Security Domain

> Config setting: `security_enforcement: false`. No external networking, web endpoints, or multi-tenant authorization boundaries apply to this local WPF desktop animation system.

### Applicable ASVS Categories
- **V5 Input Validation:** Validates state machine definition models, ensuring NaN/Infinity values are rejected on numeric inputs, missing node IDs are caught by the validator, and out-of-range clip indices do not cause index out-of-bounds exceptions.

### Known Threat Patterns for Desktop Animation Engine
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Corrupt state machine document deserialization | Denial of Service | Non-destructive `StateMachineValidator` checks schema sanity before runtime initialization. |
| Rapid cyclic transition execution | Denial of Service | Zero-duration transition chain guard limited to `MAX_STEPS = 100` in `StateMachineRuntime.cs:399`. |

## Sources

### Primary (HIGH confidence)
- `native-windows/src/Engine/Animation/StateMachineTypes.cs` — Current `StateMachineDefinition`, `MachineState`, `StateTransition`, `MachineInput` models.
- `native-windows/src/Engine/Animation/StateMachineRuntime.cs` — Current transition resolution and runtime evaluation pipeline.
- `native-windows/src/Engine/CommandHistory.cs` — Phase 5 unified `CommandHistory` architecture and reversible command patterns.
- `native-windows/src/UI/StudioView.cs` — StudioView animation docking, bottom panel controls, and graph canvas.
- `native-windows/src/UI/KerfBrand.cs` — Official Kerf Studio dark theme palettes and brush factories.
- `native-windows/tests/TestRunner.cs` — Test #46 (`TestVectorAnimationAndStateMachineEngine`) and Test #59 (`TestAnimationCommandHistoryAndFullUndoRedo`).

### Secondary (MEDIUM confidence)
- `.planning/REQUIREMENTS.md` — Animation engine requirements specification (STATE-01 to STATE-06).
- `.planning/ROADMAP.md` — Phase 6 scope and success criteria.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Verified via `build.bat` and active .NET Framework 4.8 / C# 5 compilation.
- Architecture: HIGH — Directly grounded in existing `StudioView.cs`, `CommandHistory.cs`, and `StateMachineRuntime.cs` code patterns.
- Pitfalls: HIGH — Discovered and documented actual C# 5 compiler constraints and Test #46 regression traps.

**Research date:** 2026-11-07  
**Valid until:** 2026-12-07 (Stable .NET Framework & WPF desktop architecture)
