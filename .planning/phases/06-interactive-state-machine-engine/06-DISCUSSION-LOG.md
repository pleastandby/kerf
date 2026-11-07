# Phase 6: Interactive State Machine Engine - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-07
**Phase:** 06-interactive-state-machine-engine
**Areas discussed:** State Machine UI Workspace Layout & Placement, Stable ID Model & Backward Compatibility, Transition Condition Logic (AND / OR Groups), Graph Canvas Interaction & Drag-to-Connect UX

---

## State Machine UI Workspace Layout & Placement

| Option | Description | Selected |
|--------|-------------|----------|
| Bottom Dock / Split View | Lives in the expandable bottom panel (toggling with Timeline Clips), with an Inputs list on the left, pan/zoom Graph Canvas in the center, and State/Transition Inspector on the right. Keeps the main canvas visible above for real-time visual animation preview. | ✓ |
| Full Viewport Mode | A dedicated full-screen State Machine workspace that replaces the main canvas/timeline entirely, maximizing graph editing space, with an embedded artboard preview. | |

**User's choice:** Bottom Dock / Split View
**Notes:** Preserves simultaneous canvas visibility for observing animated layer transforms while authoring state graph transitions.

---

## Stable ID Model & Backward Compatibility

| Option | Description | Selected |
|--------|-------------|----------|
| Stable String GUIDs with Index Compatibility Adapters | Use stable unique string IDs (e.g. `sm_state_<guid>`, `sm_trans_<guid>`, `sm_input_<guid>`) across all nodes, transitions, and inputs. Maintain helper index adapters and migration loaders so existing documents, legacy tests, and runtimes continue working without data loss. | ✓ |
| Pure GUID Redesign | Fully replace integer IDs with native Guid across all data structures, updating all existing tests and serialized schemas directly without legacy index adapters. | |

**User's choice:** Stable String GUIDs with Index Compatibility Adapters
**Notes:** Ensures clean stable graph references unaffected by node reordering or deletion, while maintaining 100% test pass rate across the 59 existing test suites.

---

## Transition Condition Logic (AND / OR Groups)

| Option | Description | Selected |
|--------|-------------|----------|
| Compound Condition Groups | OR between groups, AND within a group. Each transition has a list of Condition Groups. A transition fires if ANY group matches (OR). Within each group, ALL individual conditions must match (AND). | ✓ |
| Flat List | AND within a transition, OR via multiple transitions between the same states. | |

**User's choice:** Compound Condition Groups
**Notes:** Provides clean, intuitive boolean logic structure within the transition inspector matching industrial graph tools.

---

## Graph Canvas Interaction & Drag-to-Connect UX

| Option | Description | Selected |
|--------|-------------|----------|
| Port Drag-to-Target & Canvas Marquee | Dragging from a node's output port draws a live connection wire to the pointer; releasing over a valid target node creates the transition. Clicking empty canvas deselects; dragging on empty canvas marquee selects multiple nodes. | ✓ |
| Click-to-Connect Mode | Clicking a 'Connect' button on a selected node arms connection mode, and the next node clicked becomes the destination target. | |

**User's choice:** Port Drag-to-Target & Canvas Marquee
**Notes:** Delivers an intuitive, fluid Rive-like connection and selection experience.

---

## The Agent's Discretion

- Styling of node cards, connection bezier lines, and transition direction arrows to conform with the Kerf dark theme.
- Panning and zooming step factors and clamping thresholds.

## Deferred Ideas

- Canvas interaction event listeners (Hover Enter/Exit, Pointer Down/Up, Click) and full multi-clip cross-fade evaluation deferred to Phase 7.
- Advanced 1D/2D blend trees deferred to subsequent v2 milestones.
