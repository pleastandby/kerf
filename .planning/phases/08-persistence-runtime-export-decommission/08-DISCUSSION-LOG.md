# Phase 8: Persistence, Runtime Integration, Export and Legacy Decommissioning - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-09
**Phase:** 08-persistence-runtime-export-decommission
**Areas discussed:** Export Format & Packaging (.kfx), Legacy Project Migration & Reference Resilience, Standalone Runtime Architecture & Decoupling, Legacy Decommissioning & Cleanup Scope

---

## Export Format & Packaging (.kfx)

| Option | Description | Selected |
|--------|-------------|----------|
| Zip/Tar Archive Container (.kfx) | A compressed ZIP container holding manifest.json, scene.json, animations.json, state-machines.json, and externalized assets/ directories. Supports clean separation of concerns, asset compression, deduplication, and streaming/lazy loading. | ✓ |
| Single Self-Contained JSON (.kfx) | Clean UTF-8 JSON bundle containing everything in one file. Zero external asset structure, but becomes unwieldy with large embedded bitmaps. | |
| Binary Packed Container (.kfb) | Custom binary/FlatBuffers container for maximum loading speed, but opaque and harder to inspect or debug. | |

**User's choice:** Zip/Tar Archive Container (.kfx)
**Notes:** User provided an explicit format specification for a standardized ZIP-based `.kfx` bundle with a dual-payload structure (runtime payload vs optional `metadata.json` for editor round-trips).

---

## Legacy Project Migration & Reference Resilience

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-Heal with Interactive Migration Review | Automatically migrate all valid references to stable GUIDs. Preserve unresolved tracks and listeners as inactive orphaned data. Provide an interactive Migration Review dialog allowing users to Keep, Rebind, or explicitly Remove orphaned data. Never silently discard animation assets. | ✓ |
| Strict Auto-Prune on Migration | Automatically discard tracks and listeners whose target layer IDs no longer exist in the scene hierarchy upon loading. | |
| Auto-Heal with Visual Unlinked Warning Only | Mark orphaned tracks with warnings, but without a dedicated interactive resolution review dialog. | |

**User's choice:** Auto-Heal with Interactive Migration Review
**Notes:** User emphasized: Never silently discard animation tracks, keyframes, listeners, or state machine references; never heuristically bind a missing reference to an unrelated object by name or coordinates.

---

## Standalone Runtime Architecture & Decoupling

| Option | Description | Selected |
|--------|-------------|----------|
| Headless Pose Evaluator + Pluggable Render Backend | The core runtime (`KerfPlayer`) evaluates tracks, state machines, and compound conditions into a clean pose snapshot (`RenderPose`) without depending on UI, WPF, DOM, or React. Platform renderers (Canvas 2D and SVG initially, Direct2D adapter for Windows, WebGL/WebGPU in future) attach as modular adapters. | ✓ |
| Integrated Direct2D/Canvas Runtime | Bundle the renderer directly inside each runtime package so KerfPlayer owns the canvas element or drawing surface. | |
| Rust Core Engine Shared Runtime | Implement all runtime evaluation inside kerf_core.dll (Rust) and provide C-ABI / WASM bindings immediately. | |

**User's choice:** Headless Pose Evaluator + Pluggable Render Backend
**Notes:** User confirmed: Core evaluation must not depend on DOM or React; first production targets are Canvas 2D and SVG for web, with a TypeScript reference implementation and C# headless standalone runtime. Do not introduce Rust or binary engines prematurely.

---

## Legacy Decommissioning & Cleanup Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Complete Removal with Test Modernization | Safely delete all obsolete animation evaluators, old timeline code, legacy destructive binders, and dummy state machine implementations. Update existing test fixtures to exclusively use the unified modern evaluator and runtime, guaranteeing zero dead code. | ✓ |
| Deprecate with Obsolete Attributes First | Mark obsolete methods with `[Obsolete]` and maintain them as thin adapters routing to the new engine. | |

**User's choice:** Complete Removal with Test Modernization
**Notes:** User mandated: Verify dependency graph before deletion; migrate existing tests to validate the canonical architecture; ensure no obsolete animation implementation remains active or silently coexists.

---

## The Agent's Discretion

- JSON schema serialization layout inside `scene.json` and `animations.json`.
- WPF modal layout and styling for Migration Review Dialog in `StudioView.cs`.
- Standard zip compression implementation using `System.IO.Compression.ZipArchive`.

