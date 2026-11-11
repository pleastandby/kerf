# Phase 8: Persistence, Runtime Integration, Export and Legacy Decommissioning - Context

**Gathered:** 2026-11-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Finalize the reconstructed animation engine by implementing reliable project persistence, integrating all animation subsystems, establishing portable runtime foundations, and completely removing obsolete animation architecture.

This is the final integration milestone of the 8-phase animation engine reconstruction. It delivers:
1. **Full Project Persistence**: Complete serialization and deserialization of animation documents, clips, tracks, keyframes, transforms, Bézier curves, spring parameters, state machines, transitions, compound conditions, inputs, and listeners via stable GUIDs.
2. **Schema Versioning & Migration**: Migration review architecture that auto-heals valid references, flags and preserves unresolved/orphaned tracks and listeners safely as inactive, and produces detailed migration reports.
3. **Native `.kfx` Export Format**: A ZIP-based runtime package containing a clean separation between an optimized runtime payload (`manifest.json`, `scene.json`, `animations.json`, `state-machines.json`, `assets/`) and optional editor metadata (`metadata.json`).
4. **Decoupled Headless Standalone Runtime**: A runtime architecture and Developer SDK (`KerfPlayer`) with pluggable rendering backends (Canvas 2D, SVG, Direct2D) independent of DOM, WPF, or React UI frameworks.
5. **Complete Legacy Animation Decommissioning**: Dependency-aware removal of obsolete evaluators, legacy destructive property binders, deprecated timeline code, and dummy components, paired with comprehensive test modernization and zero dead code.
6. **Final Deliverable**: Comprehensive `ANIMATION_ENGINE_FINAL_REPORT.md` documenting architecture, removed legacy components, runtime API, format specifications, and test coverage.

</domain>

<decisions>
## Implementation Decisions

### 1. Export Format & Package Structure (`.kfx`)
- **D-01:** **ZIP-Based Native Container Format** — **Reversibility:** one-way — *Format specification establishes the public package standard for all future runtimes.*
  Exported runtime animations use the `.kfx` file extension as a standard ZIP container. The internal structure is strictly standardized:
  ```text
  animation.kfx
  ├── manifest.json
  ├── scene.json
  ├── animations.json
  ├── state-machines.json
  ├── assets/
  │   ├── images/
  │   ├── vectors/
  │   ├── fonts/
  │   └── audio/
  └── metadata.json (optional)
  ```
  - `manifest.json`: Format version, runtime compatibility, entry point identifiers, asset index, and authoring metadata.
  - `scene.json`: Optimized vector hierarchy, paths, fills, strokes, and static layer transforms.
  - `animations.json`: Clips, property tracks, keyframes, cubic Bézier control points, and spring parameters.
  - `state-machines.json`: State nodes, transitions, compound condition groups, inputs, and listeners.
  - `assets/`: Deduplicated relative-path binary assets (images, vectors, fonts, audio).
  - Validation: Packages must be verified for JSON schema integrity and relative asset path security before loading.

- **D-02:** **Dual-Payload Architecture (Runtime vs Editor Metadata)** — **Reversibility:** costly — *Enforces strict boundary between runtime execution and editing round-trips.*
  - The runtime payload (`scene.json`, `animations.json`, `state-machines.json`) is optimized, stripped of all editor-specific UI states (zoom, pan, selection, ruler guides, inspector expansion, command history).
  - Optional `metadata.json` stores source authoring information, original layer names, guides, and editor configurations.
  - Runtime players load and execute exclusively from the runtime payload; missing `metadata.json` never invalidates runtime playback.
  - Projects exported with metadata can be re-opened and edited inside Kerf Studio without loss of structure.

### 2. Migration & Reference Resilience
- **D-03:** **Auto-Heal with Interactive Migration Review** — **Reversibility:** costly — *Alters project save/load contract and migration schema.*
  - Valid references are automatically migrated and bound to stable GUIDs.
  - When target objects referenced by animation tracks or state machine listeners cannot be found (e.g. deleted layers, renamed legacy hash targets):
    1. Unresolved references are preserved as orphaned data marked `IsActive = false`.
    2. Unresolved tracks and listeners are barred from evaluating in the runtime (no crashes or null-ref exceptions).
    3. An interactive Migration Review Dialog presents all unresolved items, allowing the user to Keep (preserve as orphaned), Rebind (link to an existing canvas object), or Purge (delete explicitly).
    4. Heuristic binding based on name or coordinates is strictly forbidden.
    5. A structured `MigrationReport` records resolved, unresolved, recovered, and purged references.

### 3. Standalone Headless Runtime Architecture
- **D-04:** **Decoupled Headless Runtime + Pluggable Render Backend** — **Reversibility:** one-way — *Establishes the cross-platform runtime interface.*
  - The core runtime (`KerfPlayer` / `KerfRuntime`) is completely headless and platform-agnostic:
    - Zero dependencies on DOM, React, WPF, or native windowing handles.
    - Handles animation evaluation, timeline scrubbing, keyframe interpolation, Bézier curves, state machines, transition blending, input management, and listener event dispatching.
    - Produces a pure evaluated frame representation / pose snapshot (`RenderPose` / scene matrix state).
  - Rendering is decoupled through a modular `IRenderBackend` interface:
    - Initial reference backends: Canvas 2D and SVG for web; Direct2D adapter for native Windows.
    - Architecturally prepared for WebGL, WebGPU, Skia, and mobile engines.
  - Reference Web SDK (`@kerf/runtime`) provides clean developer ergonomics:
    - `load()`, `play()`, `pause()`, `stop()`, `destroy()`
    - `setInput(name, value)`, `fireTrigger(name)`
    - `readActiveStates()`, `onStateChanged(callback)`
  - Testing is executed in headless environments with deterministic assertions on calculated transform matrices and state values.

### 4. Legacy Decommissioning & Cleanup Scope
- **D-05:** **Complete Removal with Test Modernization** — **Reversibility:** costly — *Removes legacy code paths and refactors tests to unified modern API.*
  - Audit the entire repository for obsolete animation code:
    - Eliminate legacy destructive `PropertyBinder` mutations (ensuring all poses apply strictly to non-destructive `RenderTransform`).
    - Remove duplicate or obsolete prototype evaluators, unused animation stores, and dead event handlers.
    - Deprecate old 32-bit FNV-1a hash lookup fallbacks in favor of stable string GUIDs.
  - Dependency verification: Before removing any code, trace its dependency graph to guarantee no active design editor, auto layout, or canvas tool depends on it.
  - Modernize all 73 existing test suites to exercise the canonical animation engine and state machine runtime.
  - Achieve 100% pass rate across the full test suite with zero regressions.

### 5. Final Documentation Deliverable
- **D-06:** **`ANIMATION_ENGINE_FINAL_REPORT.md`** — **Reversibility:** reversible — *Deliverable documentation artifact.*
  - A definitive project artifact summarizing:
    - Final architecture of the reconstructed animation engine.
    - Inventory of removed legacy components and obsolete code.
    - New modules, data structures, and runtime APIs.
    - `.kfx` format specification and schema versioning.
    - Test coverage metrics, pass rates, and validation results.
    - Known limitations and future roadmap recommendations.

### The Agent's Discretion
- Internal JSON schema serialization layout and property naming conventions inside `scene.json` and `animations.json`.
- Specific modal layout and styling for the Migration Review Dialog inside WPF `StudioView.cs`.
- Implementation details of zip compression (standard `System.IO.Compression.ZipArchive` in .NET Framework 4.5+).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Format & Specifications
- `.planning/ROADMAP.md` § Phase 8 — Verification, Legacy Migration & Safe Deprecation.
- User Specification (2026-11-09): Native `.kfx` ZIP package structure, dual-payload architecture, developer SDK design (`@kerf/runtime`), and headless evaluation principles.

### Document Persistence & Migrations
- `native-windows/src/Model/DocumentMigrations.cs` — Document migration pipeline, `MigrateToCanonical`, `KerfDocument`, `SchemaVersion`.
- `native-windows/src/IO/StorageManager.cs` — `SaveAtomic`, `LoadDocument`, `SaveAutosave`, `JavaScriptSerializer`.
- `native-windows/src/Engine/Animation/AnimationTypes.cs` — `AnimationProjectData`, `Clip`, `Track`, `Keyframe`, `InterpMode`.

### State Machine & Interactive Runtime
- `native-windows/src/Engine/Animation/StateMachineTypes.cs` — `StateMachineDefinition`, `MachineState`, `StateTransition`, `MachineInput`, `NodeListener`.
- `native-windows/src/Engine/Animation/StateMachineRuntime.cs` — `StateMachineRuntime`, state execution, compound condition evaluation, snapshot crossfade blending.
- `native-windows/src/Engine/Animation/PropertyBinder.cs` — Non-destructive render transform binder, property keys, numeric node mappings.
- `native-windows/src/Engine/Animation/CubicBezierEase.cs` & `EasingEngine.cs` — Easing calculations, 24-step bisection, spring physics.

### Testing & Verification
- `native-windows/tests/TestRunner.cs` — Full automated test suite (Tests 1 through 73).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `System.IO.Compression.ZipArchive`: Built-in .NET 4.5+ class for packaging and extracting `.kfx` zip archives with compression.
- `JavaScriptSerializer`: Serializer used in `StorageManager.cs` and `DocumentMigrations.cs` (handling up to `int.MaxValue` payload lengths).
- `StateMachineRuntime` and `AnimationTimelineController`: Fully decoupled evaluation logic ready to be wrapped in a headless `KerfPlayer` facade.
- `CommandHistory`: Unified undo/redo engine ready to record migration resolution choices.

### Established Patterns
- **Non-destructive Render Transforms**: All animation evaluation applies exclusively to `layer.RenderTransform` (`TranslationX/Y`, `ScaleX/Y`, `Rotation`, `AnchorX/Y`, `Opacity`), guaranteeing layout geometry (`layer.Width`, `layer.Height`, Auto Layout frames) remains untouched.
- **Stable Identifiers**: State machine nodes (`sm_state_*`), transitions (`sm_trans_*`), inputs (`sm_input_*`), and condition groups (`sm_cg_*`) use stable string GUIDs.
- **Atomic File Saving**: `StorageManager.SaveAtomic` writes to a `.tmp` file, creates a `.bak` backup, and replaces atomically.

### Integration Points
- `StudioView.cs`: File menu entries for "Export Runtime Animation (.kfx)" and "Import Animation Package", plus migration review invocation during `LoadDocument`.
- `DocumentMigrations.cs`: Version bumping (e.g. to `4.0.0`), legacy animation payload detection, migration report generation, and orphaned reference marking.
- `TestRunner.cs`: Expansion with new test fixtures for `.kfx` export/import round-trip, migration review, headless runtime playback, and legacy cleanup verification.

</code_context>

<specifics>
## Specific Ideas

### ZIP Package Layout
```text
animation.kfx (ZIP archive)
├── manifest.json
├── scene.json
├── animations.json
├── state-machines.json
├── assets/
│   ├── images/
│   ├── vectors/
│   ├── fonts/
│   └── audio/
└── metadata.json (optional editor data)
```

### Developer SDK Pattern
```ts
import { KerfPlayer } from '@kerf/runtime';

const player = new KerfPlayer({
  canvas,
  source: '/animations/button.kfx',
  autoplay: true
});

await player.load();
player.play();
player.setInput('isHovered', true);
player.fireTrigger('OnClick');
player.pause();
player.destroy();
```

</specifics>

<deferred>
## Deferred Ideas

- **Binary Packed Format (`.kfb` / FlatBuffers)**: Ultra-compact binary format for resource-constrained embedded devices (deferred to future milestone).
- **WASM / Rust Shared Runtime**: Compiled Rust runtime via WebAssembly (deferred until TypeScript and C# reference implementations are battle-tested).
- **Lottie / Rive Converter Plugins**: Bidirectional import/export converters for Lottie JSON and Rive `.riv` formats (deferred as standalone interoperability plugins).
- **Mobile Native SDKs**: Dedicated Swift (iOS) and Kotlin (Android) runtime view wrappers (deferred to post-v1 cross-platform milestone).

</deferred>

---

*Phase: 08-persistence-runtime-export-decommission*
*Context gathered: 2026-11-09*

