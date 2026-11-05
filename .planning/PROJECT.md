# Kerf Studio — Professional Animation Engine Reconstruction

## What This Is

Kerf Studio is a native Windows vector design and animation editor (built with WPF and Direct2D) that combines Figma-style visual design, auto layout, and vector tools with an interactive motion graphics and state machine animation engine. This project executes an incremental architectural reconstruction of the animation system into a professional, non-destructive, graph-driven motion runtime.

## Core Value

An industrial-grade animation engine that operates with non-destructive independent transforms, stable keyframes, visual graph curve editing, transactional undo/redo, and interactive state machines without destabilizing or corrupting the design canvas.

## Requirements

### Validated

- ✓ Native vector design canvas with Direct2D hardware-accelerated rendering — existing
- ✓ Retained scene graph with hierarchical frames, groups, vectors, text, shapes, and components — existing
- ✓ Figma-compatible flexbox auto layout engine with nested layouts, padding, gap, and alignment — existing
- ✓ Hierarchical hit-testing, progressive selection resolution, and parent drag resolution (`CanvasInteractionController`) — existing
- ✓ Progressive container targeting for object creation, pasting, and nesting (`InsertionTargetResolver`) — existing
- ✓ Transactional command history with full undo/redo for design operations (`CommandHistory`) — existing
- ✓ Native Windows desktop UI shell with Kerf brand palette, menus, and styling — existing

### Active

- [ ] **Independent Transform Pipeline**: Decouple render transforms (translation, rotation, scale, skew, origin) from underlying document geometry so animating scale never mutates `Width`/`Height` or distorts Auto Layout frames.
- [ ] **Stable Animation Data Model**: Implement GUID-backed stable track targets, typed property binders, and persistent keyframes that survive layer hierarchy changes.
- [ ] **Easing & Graph Curve Editor**: Implement cubic Bézier curve evaluation, presets, and an interactive Direct2D canvas curve/graph editor widget with draggable tangent handles.
- [ ] **Full Undo/Redo Command History**: Integrate all timeline, keyframe, curve manipulation, and track modifications into the centralized `CommandHistory` architecture.
- [ ] **Decoupled Runtime Playback & Transport**: Deterministic timeline playback controller with scrub, loop, ping-pong, framerate sync, and isolated rest pose buffers that guarantee zero design-state corruption.
- [ ] **Interactive State Machine Engine**: Complete interactive state machine system featuring states, transitions, blend curves, inputs (Boolean, Number, Trigger), and canvas event listeners (Hover, Press, Click).
- [ ] **Migration & Deprecation**: Verify full test suite, migrate legacy project files to the new schema, and cleanly deprecate the old destructive property binder.

### Out of Scope

- Full application rewrite — The existing vector editor, auto layout, canvas controller, tools, and rendering subsystems are preserved intact.
- Modifying design canvas tools during animation engine reconstruction — Existing design tools remain operational and untouched.
- Premature deletion of legacy animation code — The existing animation code stays in place until replacement functionality is verified and migrated.
- External animation runtime dependencies — All animation logic and state machine evaluation will be self-contained in native C#.

## Context

The legacy animation system was built as an early prototype. It suffers from fundamental architectural issues:
1. **Destructive Scale Mutations**: `PropertyBinder.ApplyPoseToLayer` mutates `layer.Width` and `layer.Height` directly when animating scale. This destroys the rest geometry of shapes, corrupts Auto Layout frames, and damages vector path bounds.
2. **Identifier Instability**: Layer targets are resolved using a 32-bit FNV-1a hash of layer IDs, which risks collisions and breaks when layers are moved or renamed.
3. **Missing Undo/Redo**: Timeline scrubbing, keyframe additions, property tweaks, and ease adjustments do not register in `CommandHistory.cs`.
4. **Non-Functional Easing & Missing Curve Editor**: While `CubicBezierEase` provides mathematical evaluation, the user interface lacks an interactive curve editor to inspect and manipulate tangents.
5. **Coupled Runtime State**: Playing animations writes directly to document model properties and attempts to restore them via `_animationRestPose`. Interruptions or canvas interactions during playback corrupt design state.

## Constraints

- **Platform & Language**: Native Windows WPF/Direct2D application targeting .NET Framework 4.5+ with C# 5 compatibility (`csc.exe` v4.0.30319).
- **Test Integrity**: Must maintain 100% pass rate across the existing 49 automated test fixtures (`cd native-windows; .\build.bat --test`).
- **Incremental Refactor**: Work in small, isolated, independently verifiable phases without breaking active features.
- **Zero Regressions**: Auto Layout, progressive selection, hit testing, and vector drawing must continue functioning identically.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Non-destructive Render Transforms | Separating render transforms from geometric dimensions (`Width`/`Height`) preserves Auto Layout and document layout integrity. | — Pending |
| GUID-backed Stable Track Identifiers | Eliminates 32-bit hash collision vulnerabilities and guarantees track bindings remain intact across container moves. | — Pending |
| Decoupled Evaluation Context during Playback | Evaluating poses into an independent runtime visual buffer prevents playback from overwriting design canvas model state. | — Pending |
| Parallel Co-existence during Refactor | Keep existing animation types until new engine is built, tested, and migrated to prevent disruptions to current builds. | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-11-05 after initialization*
