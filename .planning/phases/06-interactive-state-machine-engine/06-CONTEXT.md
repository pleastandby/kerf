# Phase 6: Interactive State Machine Engine - Context

**Gathered:** 2026-11-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 6 replaces the prototype state machine implementation with a functional, structured, Rive-inspired graph authoring environment and input architecture. 

It delivers:
1. A dedicated visual graph editor with panning, zooming, node dragging, multi-selection marquee, and connection wire rendering.
2. Structured state node types: Entry State, Animation State (referencing animation clips with speed and looping), Any State, and Exit State with stable unique IDs.
3. Structured transition connections with stable IDs, duration, priority, interruption behaviour, exit-time, blend parameters, and duplicate/invalid connection prevention.
4. An addressable Input System supporting Boolean, Number, and Trigger variables with default and runtime values, addressable by stable unique IDs.
5. A compound transition condition builder supporting comparison operators (`==`, `!=`, `<`, `<=`, `>`, `>=`) organized into logical groups (OR between groups, AND within a group).
6. Dedicated State, Transition, and Input inspector panels exposing full properties without non-functional controls.
7. A graph validation engine detecting missing Entry nodes, unreachable states, invalid animation references, and type-incompatible conditions.
8. Full transactional integration with the Phase 5 unified `CommandHistory` architecture (reversing node and transition CRUD, movement, renaming, inputs, and conditions).

**Runtime boundary:** Establishes the complete data model, validation, and graph authoring system, preparing the architecture for runtime execution in Phase 7. Does NOT build a second animation evaluator and does NOT implement canvas interaction listeners (Hover, PointerDown/Up, Click) in this phase (scoped to Phase 7).
</domain>

<decisions>
## Implementation Decisions

### Workspace Layout & Placement
- **D-01:** Bottom Dock / Split View. The State Machine Editor lives in the expandable bottom panel (toggling with Timeline Clips). The left sidebar hosts the **Inputs list** (`Bool`, `Number`, `Trigger`), the center hosts the pan/zoomable **Graph Canvas**, and the right sidebar hosts the **State & Transition Inspector**. The main design canvas remains visible above for live animation preview. — **Reversibility:** reversible — UI layout placement within existing dock system.

### Stable ID Model & Backward Compatibility
- **D-02:** Stable String GUIDs with Index Compatibility Adapters. All nodes, transitions, and inputs receive stable unique string IDs (e.g. `sm_node_<guid>`, `sm_trans_<guid>`, `sm_input_<guid>`). Helper index adapters and migration loaders preserve compatibility so existing documents, legacy tests, and runtimes continue working without data loss. — **Reversibility:** one-way — Core data model identifier contract.

### Condition Logic Architecture
- **D-03:** Compound Condition Groups (OR between groups, AND within a group). Each transition contains a list of `ConditionGroup` objects. A transition fires if ANY group evaluates to true (OR); within each group, ALL individual conditions must evaluate to true (AND). Conditions reference stable input IDs with typed expression evaluation. — **Reversibility:** costly — Affects transition condition schema and evaluator logic.

### Graph Canvas Interaction & Drag-to-Connect UX
- **D-04:** Port Drag-to-Target & Canvas Marquee. Dragging from a node's output port draws a live connection wire following the pointer; releasing over a valid target node creates the transition. Clicking empty canvas clears selection; dragging on empty canvas marquee selects multiple nodes. Nodes display directional connection wires. — **Reversibility:** reversible — User interaction and canvas pointer event handling.

### State Node Types & Configuration
- **D-05:** Four first-class node types:
  - **Entry State:** Initial execution point, must have valid outgoing transitions.
  - **Animation State:** References an existing clip from `Document.Animation.Clips`, configurable playback speed, looping flag, and exit-time settings.
  - **Any State:** Allows transitions from eligible active states with priority rules.
  - **Exit State:** Termination of state machine flow.
  Graph positions (`GraphX`, `GraphY`) persist independently of runtime state.

### Unified Command History Integration
- **D-06:** Reversible transactional commands in `CommandHistory.cs` covering:
  - `CreateStateNodeCommand`, `DeleteStateNodesCommand`, `MoveStateNodesCommand`, `RenameStateNodeCommand`.
  - `CreateTransitionCommand`, `DeleteTransitionsCommand`, `ModifyTransitionCommand`.
  - `CreateInputCommand`, `DeleteInputCommand`, `ModifyInputCommand`.
  - `ModifyConditionGroupsCommand`.
  Continuous node dragging batches into a single command on pointer release. Multi-selection actions batch via `CompositeCommand`.

### Graph Validation Engine
- **D-07:** Non-destructive validator reporting clear diagnostic errors and warnings for:
  - Missing Entry State.
  - Dangling or invalid connections.
  - Missing destination or source nodes.
  - Duplicate node or input IDs.
  - Invalid clip references.
  - Type-incompatible conditions (e.g. comparing Number input with string).
  - Unreachable states.
  The engine reports validation results clearly without silently mutating or discarding invalid data.

### The Agent's Discretion
- Visual styling of nodes and ports to conform with `KerfBrand` dark theme (mint accents for active/selected states, dark card backgrounds).
- Exact zoom step thresholds and pan clamping logic for graph navigation.

### User Requirements (Refinement & Precision)
- **D-08:** Clean Slate Architecture (Zero Presets). All state machines, states, inputs, listeners, and focus objects are created by the user from an empty slate. No preset dummy data (like "Idle", "interrupt", or preset inputs) are generated; mockups are strictly visual references.
- **D-09:** Context-Aware Right Panel Switching. When in Animate mode and State Machine mode is active (no state timeline open), the primary right panel replaces layer transform/effects with the dedicated State / Transition Inspector. Double-clicking a state to elaborate its timeline restores the original keyframe/layer property inspector.
- **D-10:** Specialized Port Sockets. Entry state possesses an output port on its right edge only (no input port). Exit state possesses an input port on its left edge only (no output port). Any State and Custom (Animation) states possess an input port on the left edge and an output port on the right edge.
- **D-11:** Single-Wire Bidirectional Transition & Direction Badges. Transitions between pairs of states are rendered as a single connection line with two center circular badges showing `>` and `<` direction markers. Clicking a badge opens the Transition Inspector on the right panel with Duration (ms), Exit time (checkbox), Pause source when exiting (checkbox), Allow exit during transition (checkbox), collapsible Events dropdown, and collapsible Animations window.
- **D-12:** Canvas Delete Shortcut & Animation List Reordering. Pressing `Delete` or `Backspace` in the graph canvas deletes selected state nodes and transitions via transactional commands. The Animations menu provides delete options and order rearrangement for states.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` § Interactive State Machine & Logic (STATE-01 through STATE-06)
- `.planning/ROADMAP.md` § Phase 6: Interactive State Machine Engine
- `testdata/new_requirements.md` § State Machine Architecture

### Codebase Architecture & Data Models
- `native-windows/src/Engine/Animation/StateMachineTypes.cs` — Existing StateMachineDefinition, MachineState, StateTransition, MachineInput models
- `native-windows/src/Engine/Animation/StateMachineRuntime.cs` — Existing runtime evaluator and view model
- `native-windows/src/Engine/Animation/AnimationTypes.cs` — Clip, Track, and AnimationDocument structures
- `native-windows/src/Engine/CommandHistory.cs` — Phase 5 unified CommandHistory architecture and reversible commands
- `native-windows/src/UI/StudioView.cs` — StudioView animation mode docking, existing graph canvas, and inspector panels
- `native-windows/src/UI/KerfBrand.cs` — Design system brushes, typography, and controls styling

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CommandHistory` and `CompositeCommand` (`Engine/CommandHistory.cs`): Established transactional pattern with batching on drag completion.
- `KerfBrand` (`UI/KerfBrand.cs`): Mint brush (`#00F5A0`), dark card brushes (`#1E222B`, `#2A2F3D`), standard button and field constructors.
- `ScrubControls` (`UI/ScrubControls.cs`): Numeric scrub fields and sliders with `onDragComplete` lifecycle.
- `GraphEditorCanvas` (`UI/GraphEditorCanvas.cs`): Curve canvas reference for smooth Direct2D / WPF canvas interaction patterns.

### Established Patterns
- Symmetrical `ICommand` implementations with `Execute()` and `Unexecute()` maintaining document integrity.
- Pointer-down snapshot, pointer-move 60 FPS live preview, pointer-up atomic command execution.
- Strict runtime transport isolation: graph authoring mutations go through `CommandHistory`, while runtime execution ticks do not create history entries.

### Integration Points
- `StudioView._tabStateMachineBtn`: Toggles bottom dock between Timeline Clips and State Machine.
- `StudioView._stateMachineGraphCanvas`: WPF Canvas hosting graph nodes and connection wires.
- `StudioView._stateMachineInputsStack`: Left sidebar panel hosting input list and creation controls.
- `StudioView.EnsureStateMachineRuntime()`: Initializes state machine definition on active animation document.

</code_context>

<specifics>
## Specific Ideas

- Inspired by Rive's State Machine graph: clean rectangular state cards with node headers, clip badges, input sockets, and curved directional connection lines with transition arrows.
- Live dashed connection wire following the mouse pointer during drag-to-connect.
- Active state highlighting during preview playback.

</specifics>

<deferred>
## Deferred Ideas

- **Phase 7 (Runtime Execution & Interaction Listeners):** Canvas interaction event listeners (Hover In/Out, Pointer Down/Up, Click) bound to state inputs, and full multi-clip cross-fading runtime evaluation.
- **v2 Feature (Blend Trees):** Advanced 1D / 2D directional blend trees (deferred to future milestone per REQUIREMENTS.md).

</deferred>

---

*Phase: 06-interactive-state-machine-engine*
*Context gathered: 2026-11-07*

