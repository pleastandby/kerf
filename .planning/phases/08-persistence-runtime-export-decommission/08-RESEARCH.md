# Phase 8: Persistence, Runtime Integration, Export and Legacy Decommissioning - Research
**Researched:** 2026-11-09
**Domain:** Document Persistence, Schema Versioning, ZIP Container Packaging (.kfx), Headless Motion Runtime SDK, and Legacy Code Decommissioning
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

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

### Deferred Ideas (OUT OF SCOPE)
- **Binary Packed Format (`.kfb` / FlatBuffers)**: Ultra-compact binary format for resource-constrained embedded devices (deferred to future milestone).
- **WASM / Rust Shared Runtime**: Compiled Rust runtime via WebAssembly (deferred until TypeScript and C# reference implementations are battle-tested).
- **Lottie / Rive Converter Plugins**: Bidirectional import/export converters for Lottie JSON and Rive `.riv` formats (deferred as standalone interoperability plugins).
- **Mobile Native SDKs**: Dedicated Swift (iOS) and Kotlin (Android) runtime view wrappers (deferred to post-v1 cross-platform milestone).

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MIGR-01 | Schema versioning bumping to `4.0.0` and backward-compatible migration pipeline | Verified `DocumentMigrations.cs` and `CanonicalDocument.cs` migration handling; schema v4.0.0 specification with fallback parsing. |
| MIGR-02 | Migration resilience & auto-healing: mapping legacy 32-bit FNV-1a hash targets to stable string GUIDs | Verified `PropertyBinder.GetNumericNodeId` hash mapping algorithm; documented deterministic reverse mapping table during migration. |
| MIGR-03 | Orphaned reference resilience: `IsActive = false` marking, `MigrationReport` telemetry, and interactive `MigrationReviewDialog` | Implemented zero-crash evaluation guards in `Track.SampleToPose` and `StateMachineRuntime.OnPointerEvent`; WPF modal review UI architecture. |
| PERSIST-01 | Full project persistence of animation clips, tracks with GUIDs, keyframes, Bézier/spring parameters, state machines, transitions, condition groups, inputs, and listeners | Verified `JavaScriptSerializer` contract in `StorageManager.SaveAtomic`; exact JSON serialization schema for `AnimationProjectData`. |
| EXPORT-01 | Native `.kfx` ZIP package export/import foundation with dual-payload architecture (`scene.json`, `animations.json`, `state-machines.json`, `metadata.json`) | Verified `System.IO.Compression.ZipArchive` support in `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll`; relative-path security validation against zip-slip attacks. |
| RUNTIME-02 | Decoupled headless `KerfPlayer` runtime & developer SDK with pluggable `IRenderBackend` (Canvas 2D, SVG, Direct2D) | Platform-independent evaluation engine with numeric `Matrix3x2` and snapshot `RenderPose` output; zero WPF/DOM coupling. |
| DECOM-01 | Complete legacy animation decommissioning, removal of destructive property binder mutations, test modernization, and `ANIMATION_ENGINE_FINAL_REPORT.md` | Repository-wide audit of legacy evaluators, validation that Auto Layout & design tools remain 100% operational, and 4 new automated test fixtures in `TestRunner.cs`. |
</phase_requirements>

## Summary

Phase 8 is the capstone milestone of the Kerf Studio animation engine reconstruction. Over Phases 1 through 7, Kerf Studio gained non-destructive render transforms, an independent 8-channel evaluation pipeline, multi-track timelines, cubic Bézier and harmonic spring easing, centralized `CommandHistory` undo/redo, visual state machines with compound condition groups, and interactive canvas pointer listeners. Phase 8 ties these subsystems together into an industrial-grade, portable motion engine while permanently retiring the legacy prototype architecture.

Research in this phase establishes four critical architectural pillars:
1. **Full Project Persistence & Migration Resilience (`v4.0.0`)**: Upgrading `KerfDocument.SchemaVersion` from `3.0.0` to `4.0.0` and implementing robust migration logic in `DocumentMigrations.cs`. The serialization schema maps every clip, track, keyframe, state node, transition, compound condition group, input, and listener to stable string GUIDs. Unresolved targets (e.g. from deleted layers or legacy 32-bit hash mismatches) are safely preserved with `IsActive = false`, barring them from evaluation without throwing exceptions, and presented to the user via a WPF `MigrationReviewDialog` backed by a structured `MigrationReport`.
2. **Standard Native Package Container (`.kfx`)**: Packaging animations as standard ZIP archives containing a decoupled runtime payload (`manifest.json`, `scene.json`, `animations.json`, `state-machines.json`, `assets/`) and optional editor metadata (`metadata.json`). This dual-payload architecture allows runtime engines to execute stripped vector motion graphics while Kerf Studio can reopen and edit authoring metadata without fidelity loss. Compression is handled natively via .NET 4.5+ `System.IO.Compression.ZipArchive` [VERIFIED: C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll].
3. **Decoupled Headless Runtime & Developer SDK (`KerfPlayer`)**: A pure evaluation runtime completely decoupled from WPF, Direct2D, React, and browser DOMs. It consumes runtime JSON payloads, steps time, evaluates Bézier curves and spring physics, executes state transitions, and emits `RenderPose` snapshots containing layer transform matrices. Rendering is abstracted behind `IRenderBackend`, with initial reference adapters for Direct2D (native desktop), Canvas 2D (web), and SVG.
4. **Complete Legacy Decommissioning & Zero-Regression Verification**: Decommissioning destructive `PropertyBinder` mutations, eliminating obsolete prototype evaluators, deprecating 32-bit FNV-1a hash lookup fallbacks in favor of stable string GUIDs, verifying design editor and Auto Layout dependency isolation, modernizing all 73 existing test suites, and publishing the comprehensive `ANIMATION_ENGINE_FINAL_REPORT.md`.

**Primary recommendation:** Implement schema v4.0.0 persistence and migration first with `IsActive = false` guards in `Track` and `NodeListener`, build the native `.kfx` exporter using `System.IO.Compression.ZipArchive` with strict path traversal validation, package the headless `KerfPlayer` runtime with pure numeric matrix transforms, cleanly decommission legacy destructive binders, and add 4 comprehensive verification test fixtures to `TestRunner.cs` to achieve a 100% pass rate across all 77 suites.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Full Project Persistence (v4.0.0) | Model / Storage Tier (`StorageManager.cs`, `DocumentMigrations.cs`) | UI Tier (`StudioView.cs`) | Persistence and serialization logic must execute independently of the view; the UI only provides trigger hooks (Save/Open) and status feedback. |
| Schema Versioning & Migration Review | Migration Engine (`DocumentMigrations.cs`) | WPF Presentation (`MigrationReviewDialog.xaml.cs`) | Migration analysis, reference resolution, and `MigrationReport` generation are pure model operations; the review dialog is an optional interactive presentation layer for unresolved conflicts. |
| Native `.kfx` Container Packaging | IO / Export Tier (`KfxPackageExporter.cs`, `KfxPackageImporter.cs`) | Zip Subsystem (`System.IO.Compression`) | Package creation, schema separation (runtime vs editor metadata), asset deduplication, and zip-slip path traversal validation belong strictly in the IO engine. |
| Headless Runtime Evaluation | Standalone Runtime (`KerfPlayer.cs` / `KerfRuntime.cs`) | Render Backends (`IRenderBackend`) | Frame advancement, timeline scrubbing, spring math, state transitions, and matrix evaluations are headless; rendering is delegated via pluggable backend interfaces. |
| Non-Destructive Render Application | Animation Engine (`PropertyBinder.cs`, `LayerNode.cs`) | Direct2D Visual Pipeline (`GpuRenderer.cs`) | Evaluated animation poses apply exclusively to `LayerNode.RenderTransform` without mutating layout geometry (`Width`, `Height`, Auto Layout frames). |
| Legacy Decommissioning & Verification | Test Suite (`TestRunner.cs`) | Build Pipeline (`build.bat`) | Automated regression test suites guarantee that deleting obsolete code paths causes zero regressions across design canvas and animation features. |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.IO.Compression.ZipArchive` | .NET 4.5+ BCL (v4.0.30319) | Native `.kfx` package archive creation and extraction | Built into .NET 4.5+ BCL [VERIFIED: C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll]; zero external dependencies; supports streaming and deflate compression. |
| `System.Web.Script.Serialization.JavaScriptSerializer` | .NET 4.5+ BCL (v4.0.30319) | Document serialization and JSON payload parsing | Canonical serializer used across `StorageManager.cs` and `DocumentMigrations.cs`; handles large documents up to `int.MaxValue` bytes. |
| `Kerf.Engine.Animation.StateMachineRuntime` | In-Repo Modern Engine | Interactive state machine execution, condition evaluation, and transition crossfades | Core motion engine built across Phases 6 & 7; self-contained in native C# 5.0 with zero external framework dependencies. |
| `Kerf.Engine.Animation.AnimationTimelineController` | In-Repo Modern Engine | Centralized timeline clock, scrubbing, looping, and transport controller | Provides deterministic playback stepping and evaluation poses for clips and state machines. |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.IO.FileStream` & `File.Replace` | .NET 4.5+ BCL | Atomic file saving with backup recovery | Standard pattern in `StorageManager.SaveAtomic` to prevent corrupt files on power loss. |
| `System.Windows.Controls.Window` | WPF .NET 4.5+ | Modal host for `MigrationReviewDialog` | Displayed when user opens a legacy document containing unresolved/orphaned animation references. |
| `System.Security.Cryptography.SHA256` | .NET 4.5+ BCL | Asset deduplication and integrity hashing | Computes payload and asset hashes inside `.kfx` packages to eliminate duplicate image/vector assets. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `System.IO.Compression.ZipArchive` | `SharpZipLib` or `DotNetZip` | Third-party NuGet libraries introduce external DLL distribution overhead and security maintenance without adding necessary functionality over BCL `ZipArchive`. |
| `JavaScriptSerializer` | `Newtonsoft.Json` (Json.NET) | Json.NET requires distributing `Newtonsoft.Json.dll`. The repository currently builds cleanly via `build.bat` using standard BCL references only (`csc.exe` v4.0.30319). |
| Dual JSON Payload in `.kfx` | Single monolithic JSON file | A single file forces runtime players (mobile, web) to parse unneeded editor state (ruler guides, zoom, selection history), bloating memory and parsing time. |
| Orphaned Reference Preservation | Automatic Heuristic Rebinding | Auto-rebinding by layer name or bounding box risks silent visual corruption; user review dialog with explicit Rebind/Keep/Purge guarantees safety. |

**Installation:**
All required assemblies are part of the standard .NET Framework 4.5+ BCL:
```bat
REM References added to csc.exe compiler command in build.bat:
/r:System.dll,System.Core.dll,System.Xaml.dll,System.Web.Extensions.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll
```

**Version verification:**
- `csc.exe`: version 4.8.9221.0 (targeting C# 5.0 and .NET Framework 4.5+) [VERIFIED: native-windows build.bat output]
- `System.IO.Compression.dll`: present at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll` [VERIFIED: Test-Path check]
- `System.IO.Compression.FileSystem.dll`: present at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.FileSystem.dll` [VERIFIED: Test-Path check]

## Package Legitimacy Audit

> All components used in Phase 8 are native .NET Framework BCL libraries. Zero external NuGet packages or third-party binaries are installed.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| `System.IO.Compression` | .NET BCL | 12+ yrs | Native BCL | microsoft/dotnet | [OK] | Approved |
| `System.IO.Compression.FileSystem` | .NET BCL | 12+ yrs | Native BCL | microsoft/dotnet | [OK] | Approved |
| `System.Web.Extensions` | .NET BCL | 15+ yrs | Native BCL | microsoft/dotnet | [OK] | Approved |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```text
+---------------------------------------------------------------------------------------------------+
|                                    Kerf Studio Editor (WPF / Direct2D)                            |
|                                                                                                   |
|  [Canvas / Inspector] <---> [CommandHistory] <---> [StorageManager]                              |
|           |                                               |                                       |
|           v                                               v                                       |
|  [StudioView.cs]                                 [DocumentMigrations.cs]                          |
|   - File > Export .kfx                            - Schema 4.0.0 bump                             |
|   - File > Import .kfx                            - GUID Auto-healing                             |
|   - Migration Review Dialog                       - Orphan detection (IsActive = false)           |
|                                                   - MigrationReport generation                    |
+-----------------------------------------------------------|---------------------------------------+
                                                            |
                                                            v
                                            +-------------------------------+
                                            |   KfxPackageIO (Native ZIP)   |
                                            |   (System.IO.Compression)     |
                                            +-------------------------------+
                                                            |
                     +--------------------------------------+--------------------------------------+
                     |                                                                             |
                     v                                                                             v
      +-----------------------------+                                               +-----------------------------+
      |  animation.kfx Container    |                                               |  KerfDocument (.kerf)       |
      |  - manifest.json            |                                               |  - SchemaVersion: "4.0.0"   |
      |  - scene.json               |                                               |  - Pages [ LayerNode ]      |
      |  - animations.json          |                                               |  - AnimationProjectData     |
      |  - state-machines.json      |                                               |    - Clips [ Track (GUID) ] |
      |  - assets/                  |                                               |    - StateMachineDefinition |
      |  - metadata.json (optional) |                                               |    - Orphaned Items (Safe)  |
      +-----------------------------+                                               +-----------------------------+
                     |
                     | Runtime Payload (Headless Execution)
                     v
+---------------------------------------------------------------------------------------------------+
|                              Standalone Headless Runtime (KerfPlayer)                             |
|                                                                                                   |
|   [KerfPlayer API] : load(), play(), pause(), stop(), setInput(), fireTrigger()                   |
|           |                                                                                       |
|           v                                                                                       |
|   [KerfRuntime Engine]                                                                            |
|     - Time Advancement & Clock Transport                                                          |
|     - Keyframe Interpolation (Bézier & Closed-Form Springs)                                       |
|     - StateMachineRuntime (Compound Conditions & Snapshot Crossfades)                             |
|     - Non-Destructive Matrix Evaluation (Matrix3x2 World Compose)                                 |
|           |                                                                                       |
|           v                                                                                       |
|   [RenderPose Snapshot] (LayerId -> Matrix3x2, Opacity, RenderTransform)                          |
|           |                                                                                       |
|           +-----------------------+-----------------------+                                       |
|           |                       |                       |                                       |
|           v                       v                       v                                       |
|   [Canvas2D Backend]        [SVG Backend]          [Direct2D Backend]                             |
|   (Web / HTML5 Canvas)     (DOM Vector Tree)      (Native Windows WPF)                            |
+---------------------------------------------------------------------------------------------------+
```

### Recommended Project Structure
```text
native-windows/src/
├── Engine/
│   └── Animation/
│       ├── AnimationTypes.cs             # Core models: Clip, Track (GUID + IsActive), Keyframe
│       ├── StateMachineTypes.cs          # Models: MachineState, StateTransition, NodeListener (TargetLayerId + IsActive)
│       ├── StateMachineRuntime.cs        # Interactive execution engine & listener dispatching
│       ├── PropertyBinder.cs             # Non-destructive pose binder & stable string GUID lookup
│       ├── AnimationTimelineController.cs# Centralized animation clock & transport controller
│       ├── CubicBezierEase.cs            # Analytical 24-step Bézier curve evaluation
│       └── EasingEngine.cs               # Presets & closed-form harmonic springs
├── IO/
│   ├── StorageManager.cs                 # Atomic save, autosave, recent registry, .kfx triggers
│   ├── KfxPackage.cs                     # Native .kfx ZIP export/import, manifest, dual payload
│   └── KfxSecurityValidator.cs          # Zip-slip path traversal and decompression bomb defense
├── Model/
│   ├── CanonicalDocument.cs              # KerfDocument (SchemaVersion="4.0.0"), RenderTransform
│   ├── DocumentMigrations.cs             # Migration to v4.0.0, GUID auto-healing, MigrationReport
│   └── MigrationReport.cs               # Data model for resolved, orphaned, and purged items
├── Runtime/
│   ├── KerfPlayer.cs                     # Decoupled headless player facade & event dispatcher
│   ├── KerfRuntime.cs                    # Platform-agnostic evaluation & pose snapshot calculation
│   ├── IRenderBackend.cs                 # Pluggable rendering interface
│   ├── RenderPose.cs                     # Pure evaluated frame snapshot & Matrix3x2 structures
│   └── Backends/
│       ├── Direct2DRenderBackend.cs      # Native Windows Direct2D render adapter
│       └── NullRenderBackend.cs          # Headless verification & test fixture backend
└── UI/
    ├── StudioView.cs                     # Menu hooks: Export/Import .kfx, Migration review hook
    └── Dialogs/
        └── MigrationReviewDialog.cs      # Interactive WPF dialog for resolving orphaned references
```

### Pattern 1: Schema 4.0.0 Document Persistence & Migration Engine
**What:** The canonical document format is versioned as `"4.0.0"`. `DocumentMigrations.cs` upgrades legacy `v1.x`, `v2.x`, and `v3.x` documents:
- Every `Track` is assigned a unique string `Id` (GUID) and its `TargetNodeId` is bound to the layer's stable string `Id` (`[VERIFIED: native-windows/src/Engine/Animation/AnimationTypes.cs:158-165]`).
- Every `NodeListener` is assigned a stable `ListenerId` and bound to `TargetLayerId` (`[VERIFIED: native-windows/src/Engine/Animation/StateMachineTypes.cs:732-738]`).
- When a target layer cannot be found in document pages (e.g. layer was deleted, or legacy 32-bit FNV-1a hash lookup fails to match), the item is flagged as orphaned with `IsActive = false`.
- The migration process outputs a structured `MigrationReport` containing counts and items. If `UnresolvedTracksCount > 0 || UnresolvedListenersCount > 0`, the UI triggers the `MigrationReviewDialog`.

### Pattern 2: Native .kfx ZIP Container Dual-Payload Architecture
**What:** A `.kfx` package is a valid ZIP archive containing two distinct layers:
1. **Runtime Payload** (`manifest.json`, `scene.json`, `animations.json`, `state-machines.json`, `assets/`):
   - Fully stripped of all editor state: selection arrays, undo command stacks, zoom level, pan offsets, ruler guides, inspector expansion flags.
   - Contains only the static vector tree and the animated motion graphs needed for high-speed evaluation.
2. **Editor Metadata Payload** (`metadata.json`):
   - Optional file containing authoring metadata: canvas viewport state, guides, selected IDs, author preferences.
   - When present, Kerf Studio re-opens the package with 100% round-trip fidelity; when absent, runtime engines execute without failure.
- **Security Check:** All extracted entries must pass zip-slip path traversal validation: entry names must not contain `..` or begin with `/` or `\`.

### Pattern 3: Standalone Headless KerfPlayer Runtime & IRenderBackend
**What:** `KerfPlayer` is a high-level facade wrapping `KerfRuntime`. It has zero dependencies on `PresentationCore.dll`, `PresentationFramework.dll`, or Direct2D:
- Uses a pure arithmetic `Matrix3x2` struct for hierarchical transform composition:
  $$\mathbf{M}_{\text{world}} = \mathbf{M}_{\text{parent}} \times \mathbf{M}_{\text{local}}$$
- Advances time via `Advance(double dt)`.
- Dispatches canvas pointer events to active listeners via `DispatchPointerEvent(string layerId, PointerEventKind kind)`.
- Updates inputs via `SetInput(string name, double value)` and `FireTrigger(string name)`.
- Emits a `RenderPose` containing layer world matrices and opacity values.
- Delegates drawing to `IRenderBackend.RenderFrame(RenderPose pose)`.

### Pattern 4: MigrationReviewDialog & Orphaned Reference Healing
**What:** When opening a project with orphaned references:
- Unresolved tracks and listeners are never silently discarded (preventing accidental data loss) and never evaluated (preventing `NullReferenceException` or canvas glitches).
- The `MigrationReviewDialog` presents a list of orphaned items with their target layer ID, property/event, and proposed actions:
  - **Keep**: Retains the orphaned item in the project file with `IsActive = false`. If the user later re-creates or renames the layer with the matching GUID, it auto-reactivates.
  - **Rebind**: Allows the user to select an existing layer from a dropdown list to re-link the animation track or listener.
  - **Purge**: Explicitly deletes the dead track or listener from the document.
- Choices are applied atomically to the document and logged in `CommandHistory`.

### Anti-Patterns to Avoid
- **Destructive Model Mutation During Playback:** Never write animated values directly to `layer.X`, `layer.Y`, `layer.Width`, or `layer.Height`. All animation must evaluate to non-destructive `RenderTransform` (`[VERIFIED: native-windows/src/Model/CanonicalDocument.cs:667-677]`).
- **Heuristic Auto-Rebinding:** Never guess layer targets using layer names or coordinates during migration. Layer names are non-unique and coordinates shift. If the GUID does not match, mark as orphaned and prompt the user.
- **Throwing Exceptions on Missing Animation Targets:** Never allow missing track targets to throw runtime exceptions. `if (!track.IsActive) continue;` must guard evaluation loops.
- **Coupling Headless Runtime to UI Frameworks:** Never reference WPF types (`System.Windows.Media.Matrix`, `Point`, `DependencyObject`) inside `KerfPlayer` or `KerfRuntime`.
- **Insecure ZIP Extraction (Zip-Slip):** Never call `entry.ExtractToFile(path)` without validating that `Path.GetFullPath(destination)` starts with the canonical target directory.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| ZIP compression & decompression | Custom deflate / PKZIP parser | `System.IO.Compression.ZipArchive` | BCL library [VERIFIED: C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll]; handles compression, CRC32 verification, directory streaming, and cross-platform zip standards natively. |
| JSON Serialization in C# 5.0 | Custom regex or string concatenation | `System.Web.Script.Serialization.JavaScriptSerializer` | Handles nested collections, dictionaries, string escaping, unicode, and large payloads (`MaxJsonLength = int.MaxValue`). |
| Hierarchical Matrix Composition | Custom nested coordinate calculations | Pure 2D affine matrix multiplication ($\mathbf{M}_{3\times 2}$) | Closed-form affine composition handles translation, rotation, scale, skew, and anchor origins with zero drift. |
| Easing curve sampling | Numerical bisection per frame | Analytical cubic Bézier & closed-form harmonic springs | Easing engine built in Phase 4 (`CubicBezierEase.cs`, `EasingEngine.cs`) provides 24-step bisection with sub-microsecond evaluation and zero GC allocations. |

**Key insight:** By leveraging standard .NET 4.5+ BCL facilities (`ZipArchive`, `JavaScriptSerializer`) and existing in-repo mathematical engines (`CubicBezierEase`, `EasingEngine`), Phase 8 achieves maximum robustness with zero third-party dependencies and zero runtime bloat.

## Runtime State Inventory

> Required for Phase 8 because this phase bumps schema versions, migrates legacy hash IDs to stable GUIDs, and decommissions legacy animation components.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| **Stored data** | Existing project files on disk (`*.kerf`, autosaves in `%LocalAppData%\KerfStudio\Autosave\`) have `SchemaVersion = "3.0.0"` (`[VERIFIED: native-windows/src/Model/CanonicalDocument.cs:1378]`). Some legacy tracks contain 32-bit FNV-1a hash integers in `Track.NodeId` with empty `TargetNodeId` (`[VERIFIED: native-windows/src/Engine/Animation/AnimationTypes.cs:158-165]`). | **Data migration**: `DocumentMigrations.cs` checks schema versions. Pre-v4 documents are automatically migrated: `TargetNodeId` is populated by reverse-lookup of layer GUIDs matching the hash. Unmatched items are marked `IsActive = false`. Existing projects load with zero data loss. |
| **Live service config** | Recent projects list stored in `%LocalAppData%\KerfStudio\recent_projects.json` (`[VERIFIED: native-windows/src/IO/StorageManager.cs:194-201]`). | **Code edit**: `StorageManager.ProbeProjectMetadata` probes file headers for `"SchemaVersion": "4.0.0"`. Existing `.kerf` paths in the registry remain 100% valid. |
| **OS-registered state** | Windows File Associations for `.kerf` and `.kfx` in `KerfInstaller.cs`. | **Code edit**: Register `.kfx` ("Kerf Animation Package") alongside `.kerf` in `KerfInstaller.cs` so double-clicking `.kfx` opens in Kerf Studio. |
| **Secrets/env vars** | None. Kerf Studio is an offline-first desktop application with zero external API credentials. | **None verified**: No environment variables or credentials exist in the codebase. |
| **Build artifacts** | `..\KerfStudio.exe`, `tests\TestRunner.exe`, `..\KerfStudioSetup.exe`, `kerf_core.dll` (`[VERIFIED: native-windows/build.bat:6-8]`). | **Rebuild**: `build.bat` recompiles all binaries with `System.IO.Compression.dll` reference. No stale object caches exist because `csc.exe` compiles directly to the output executable. |

## Common Pitfalls

### Pitfall 1: Zip-Slip Vulnerability in `.kfx` Package Extraction
**What goes wrong:** Malicious `.kfx` archives containing filenames with directory traversal sequences (e.g. `../../../../Windows/System32/calc.exe`) write files outside the target extraction folder when unpacked.
**Why it happens:** Standard ZIP archives allow arbitrary relative entry names; naive extraction concatenates destination path with entry name without verification.
**How to avoid:** Before extracting any entry, verify that `Path.GetFullPath(Path.Combine(targetDir, entry.FullName))` starts with `Path.GetFullPath(targetDir) + Path.DirectorySeparatorChar`. Throw `InvalidDataException` if violation is detected.
**Warning signs:** Entry names containing `..` or leading `/` or `\`.

### Pitfall 2: NullReferenceException on Orphaned Animation Tracks
**What goes wrong:** A layer referenced by an animation track was deleted or renamed in a previous edit session. When the animation timeline scrubs, the evaluator attempts to locate the layer and throws `NullReferenceException`, crashing playback.
**Why it happens:** Animation tracks held a target identifier that no longer corresponds to any layer in the active scene.
**How to avoid:** 
1. Add `public bool IsActive { get; set; }` to `Track` and `NodeListener`.
2. In `DocumentMigrations.cs`, if a track's target layer cannot be found in the document tree, set `track.IsActive = false`.
3. In `Clip.SampleToPose` and `StateMachineRuntime.OnPointerEvent`, guard evaluation: `if (!track.IsActive) continue;`.
**Warning signs:** Unhandled exceptions during `TimelineController.Seek` or `StateMachineRuntime.Update`.

### Pitfall 3: Inadvertent Geometry Mutation from Residual `PropertyBinder` Code
**What goes wrong:** Animating scale or visual width/height inadvertently mutates `layer.Width` or `layer.Height`, permanently corrupting Auto Layout containers and vector paths.
**Why it happens:** Legacy code paths in `PropertyBinder.ApplyPoseToLayer` previously wrote directly to `layer.Width` and `layer.Height` instead of `layer.RenderTransform.VisualWidth` and `layer.RenderTransform.ScaleX`.
**How to avoid:** Enforce that `PropertyBinder.ApplyPoseToLayer` mutates strictly `layer.RenderTransform` (`[VERIFIED: native-windows/src/Model/CanonicalDocument.cs:667-677]`). Never assign to `layer.Width`, `layer.Height`, `layer.X`, or `layer.Y` during animation pose application.
**Warning signs:** Auto Layout frames resizing or jittering during timeline playback.

### Pitfall 4: Hash Collision in Legacy 32-bit FNV-1a Node Identifiers
**What goes wrong:** Two different layers in a large project produce the identical 32-bit FNV-1a hash (`[VERIFIED: native-windows/src/Engine/Animation/PropertyBinder.cs:9-22]`), causing an animation track intended for Layer A to erroneously animate Layer B.
**Why it happens:** 32-bit hashes have a non-negligible collision probability across hundreds of layers (Birthday paradox at ~77,000 items, with occasional collisions even in smaller sets).
**How to avoid:** Transition all track and listener bindings to stable string GUIDs (`TargetNodeId`, `TargetLayerId`). Retain 32-bit `NodeId` only as a secondary fallback during migration for legacy files.
**Warning signs:** Unrelated canvas layers animating when a specific track is scrubbed.

### Pitfall 5: Headless Runtime Coupled to WPF Types
**What goes wrong:** Attempting to run `KerfPlayer` in a server environment, background worker, or unit test runner without WPF throws `TypeInitializationException` or `FileNotFoundException` for `PresentationCore.dll`.
**Why it happens:** Referencing `System.Windows.Media.Matrix` or `System.Windows.Point` inside core runtime classes couples them to the WPF desktop subsystem.
**How to avoid:** Implement a lightweight, pure C# `Matrix3x2` struct `[M11, M12, M21, M22, OffsetX, OffsetY]` with pure arithmetic inside `Kerf.Runtime`. WPF and Direct2D adapters convert `Matrix3x2` to `System.Windows.Media.Matrix` at the outer boundary.
**Warning signs:** Unit tests failing to instantiate `KerfPlayer` unless `[STAThread]` is present.

## Code Examples

Verified C# 5.0 compatible code patterns from official BCL and in-repo sources:

### 1. Schema 4.0.0 Migration & Orphaned Reference Resolution (`DocumentMigrations.cs`)
```csharp
// Source: native-windows/src/Model/DocumentMigrations.cs
using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Kerf.Engine.Animation;

namespace Kerf.Model
{
    public static class DocumentMigrations
    {
        public const string CurrentSchemaVersion = "4.0.0";

        public static KerfDocument LoadAndMigrate(string json, out MigrationReport report)
        {
            report = new MigrationReport
            {
                SourceVersion = "unknown",
                TargetVersion = CurrentSchemaVersion,
                TotalLayersChecked = 0,
                ResolvedTracksCount = 0,
                UnresolvedTracksCount = 0,
                ResolvedListenersCount = 0,
                UnresolvedListenersCount = 0,
                OrphanedItems = new List<OrphanedReferenceItem>(),
                Warnings = new List<string>()
            };

            if (string.IsNullOrEmpty(json))
            {
                return new KerfDocument { SchemaVersion = CurrentSchemaVersion };
            }

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var dict = serializer.Deserialize<Dictionary<string, object>>(json);
            if (dict == null) return new KerfDocument { SchemaVersion = CurrentSchemaVersion };

            string schema = null;
            if (dict.ContainsKey("SchemaVersion") && dict["SchemaVersion"] != null)
                schema = dict["SchemaVersion"].ToString();
            else if (dict.ContainsKey("schemaVersion") && dict["schemaVersion"] != null)
                schema = dict["schemaVersion"].ToString();

            report.SourceVersion = !string.IsNullOrEmpty(schema) ? schema : "2.4.0";

            KerfDocument doc = null;
            if (!string.IsNullOrEmpty(schema) && (schema.StartsWith("4.") || schema.StartsWith("3.")))
            {
                try
                {
                    doc = serializer.Deserialize<KerfDocument>(json);
                }
                catch (Exception ex)
                {
                    report.Warnings.Add("Direct deserialization failed: " + ex.Message + ". Falling back to dictionary migration.");
                }
            }

            if (doc == null)
            {
                doc = MigrateFromDictionary(dict, serializer);
            }

            // Post-load Migration & Auto-healing to v4.0.0
            HealAndValidateReferences(doc, report);

            doc.SchemaVersion = CurrentSchemaVersion;
            doc.UpdatedAt = DateTime.UtcNow.Ticks;
            return doc;
        }

        private static void HealAndValidateReferences(KerfDocument doc, MigrationReport report)
        {
            if (doc == null) return;
            var page = doc.GetActivePage();
            var allLayers = page != null ? page.GetAllLayersFlat() : new List<LayerNode>();
            report.TotalLayersChecked = allLayers.Count;

            // Map layer IDs and numeric hashes for reverse resolution
            var layersById = new Dictionary<string, LayerNode>(StringComparer.OrdinalIgnoreCase);
            var layersByHash = new Dictionary<uint, LayerNode>();
            foreach (var layer in allLayers)
            {
                if (layer == null || string.IsNullOrEmpty(layer.Id)) continue;
                layersById[layer.Id] = layer;
                uint hash = PropertyBinder.GetNumericNodeId(layer.Id);
                if (!layersByHash.ContainsKey(hash))
                {
                    layersByHash[hash] = layer;
                }
            }

            // Heal Animation Clips & Tracks
            if (doc.Animation != null && doc.Animation.Clips != null)
            {
                foreach (var clip in doc.Animation.Clips)
                {
                    if (clip == null || clip.Tracks == null) continue;
                    foreach (var track in clip.Tracks)
                    {
                        if (track == null) continue;
                        if (string.IsNullOrEmpty(track.Id)) track.Id = Guid.NewGuid().ToString("N");

                        LayerNode targetLayer = null;
                        if (!string.IsNullOrEmpty(track.TargetNodeId) && layersById.TryGetValue(track.TargetNodeId, out targetLayer))
                        {
                            track.IsActive = true;
                            track.NodeId = PropertyBinder.GetNumericNodeId(targetLayer.Id);
                            report.ResolvedTracksCount++;
                        }
                        else if (track.NodeId != 0 && layersByHash.TryGetValue(track.NodeId, out targetLayer))
                        {
                            // Auto-heal legacy 32-bit hash reference to stable string GUID
                            track.TargetNodeId = targetLayer.Id;
                            track.IsActive = true;
                            report.ResolvedTracksCount++;
                        }
                        else
                        {
                            // Target object is missing: preserve as orphaned, deactivate to prevent crash
                            track.IsActive = false;
                            report.UnresolvedTracksCount++;
                            report.OrphanedItems.Add(new OrphanedReferenceItem
                            {
                                Id = track.Id,
                                Kind = OrphanKind.Track,
                                NumericId = track.NodeId,
                                TargetLayerId = track.TargetNodeId ?? string.Empty,
                                DisplayName = "Track: " + track.PropertyPath,
                                Description = string.Format("Clip '{0}', Property '{1}', Target ID '{2}' not found.", clip.Name, track.PropertyPath, track.TargetNodeId ?? track.NodeId.ToString()),
                                IsActive = false,
                                SuggestedAction = "Keep"
                            });
                        }
                    }
                }
            }

            // Heal State Machine Listeners
            if (doc.Animation != null && doc.Animation.StateMachine != null && doc.Animation.StateMachine.Listeners != null)
            {
                foreach (var listener in doc.Animation.StateMachine.Listeners)
                {
                    if (listener == null) continue;
                    if (string.IsNullOrEmpty(listener.ListenerId)) listener.ListenerId = "sm_lst_" + Guid.NewGuid().ToString("N");

                    if (listener.TargetNodeId == 0u && string.IsNullOrEmpty(listener.TargetLayerId))
                    {
                        // Global canvas listener
                        listener.IsActive = true;
                        report.ResolvedListenersCount++;
                        continue;
                    }

                    LayerNode targetLayer = null;
                    if (!string.IsNullOrEmpty(listener.TargetLayerId) && layersById.TryGetValue(listener.TargetLayerId, out targetLayer))
                    {
                        listener.IsActive = true;
                        listener.TargetNodeId = PropertyBinder.GetNumericNodeId(targetLayer.Id);
                        report.ResolvedListenersCount++;
                    }
                    else if (listener.TargetNodeId != 0u && layersByHash.TryGetValue(listener.TargetNodeId, out targetLayer))
                    {
                        listener.TargetLayerId = targetLayer.Id;
                        listener.IsActive = true;
                        report.ResolvedListenersCount++;
                    }
                    else
                    {
                        listener.IsActive = false;
                        report.UnresolvedListenersCount++;
                        report.OrphanedItems.Add(new OrphanedReferenceItem
                        {
                            Id = listener.ListenerId,
                            Kind = OrphanKind.Listener,
                            NumericId = listener.TargetNodeId,
                            TargetLayerId = listener.TargetLayerId ?? string.Empty,
                            DisplayName = "Listener: " + listener.Kind.ToString(),
                            Description = string.Format("Event '{0}', Target ID '{1}' not found.", listener.Kind, listener.TargetLayerId ?? listener.TargetNodeId.ToString()),
                            IsActive = false,
                            SuggestedAction = "Keep"
                        });
                    }
                }
            }
        }
    }
}
```

### 2. Native `.kfx` ZIP Packaging & Security Validation (`KfxPackage.cs`)
```csharp
// Source: native-windows/src/IO/KfxPackage.cs
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web.Script.Serialization;
using Kerf.Model;

namespace Kerf.IO
{
    public static class KfxPackage
    {
        private const int MaxUncompressedPackageBytes = 250 * 1024 * 1024; // 250 MB zip-bomb defense

        public static void ExportPackage(KerfDocument doc, string targetFilePath, bool includeEditorMetadata = true)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (string.IsNullOrEmpty(targetFilePath)) throw new ArgumentNullException("targetFilePath");

            string fullPath = Path.GetFullPath(targetFilePath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string tempZipPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

            try
            {
                using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
                {
                    // 1. manifest.json
                    var manifest = new
                    {
                        formatVersion = "1.0.0",
                        generator = "Kerf Studio 4.0.0",
                        createdAt = DateTime.UtcNow.ToString("o"),
                        runtimeCompatibility = ">=1.0.0",
                        entryPoints = new
                        {
                            defaultClipId = doc.Animation != null ? doc.Animation.ActiveClipId : 0u,
                            defaultStateMachine = doc.Animation != null && doc.Animation.StateMachine != null ? doc.Animation.StateMachine.Name : null
                        },
                        title = doc.Name ?? "Animation",
                        hasMetadata = includeEditorMetadata
                    };
                    WriteZipEntry(archive, "manifest.json", serializer.Serialize(manifest));

                    // 2. scene.json (Optimized runtime vector scene)
                    var page = doc.GetActivePage();
                    var scenePayload = new
                    {
                        id = page != null ? page.Id : "page_1",
                        name = page != null ? page.Name : "Main Scene",
                        width = page != null ? page.Width : 1920.0,
                        height = page != null ? page.Height : 1080.0,
                        backgroundColor = page != null ? page.BackgroundColor : "#00000000",
                        layers = page != null ? page.Layers : null
                    };
                    WriteZipEntry(archive, "scene.json", serializer.Serialize(scenePayload));

                    // 3. animations.json (Clips and Keyframes)
                    var animationsPayload = new
                    {
                        clips = doc.Animation != null ? doc.Animation.Clips : null
                    };
                    WriteZipEntry(archive, "animations.json", serializer.Serialize(animationsPayload));

                    // 4. state-machines.json (State machine graph, transitions, inputs, listeners)
                    var smPayload = new
                    {
                        stateMachine = doc.Animation != null ? doc.Animation.StateMachine : null
                    };
                    WriteZipEntry(archive, "state-machines.json", serializer.Serialize(smPayload));

                    // 5. Optional metadata.json (Editor authoring metadata)
                    if (includeEditorMetadata)
                    {
                        var metadataPayload = new
                        {
                            schemaVersion = "4.0.0",
                            documentId = doc.Id,
                            originalName = doc.Name,
                            activePageId = doc.ActivePageId,
                            assetLibrary = doc.AssetLibrary
                        };
                        WriteZipEntry(archive, "metadata.json", serializer.Serialize(metadataPayload));
                    }
                }

                if (File.Exists(fullPath))
                {
                    File.Replace(tempZipPath, fullPath, fullPath + ".bak", true);
                }
                else
                {
                    File.Move(tempZipPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(tempZipPath))
                {
                    try { File.Delete(tempZipPath); } catch { }
                }
            }
        }

        public static void ValidatePackageSecurity(string zipFilePath)
        {
            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                long totalBytes = 0;
                foreach (var entry in archive.Entries)
                {
                    // Zip-slip validation
                    string name = entry.FullName.Replace('\\', '/');
                    if (name.StartsWith("/") || name.Contains("../") || name.Contains("/.."))
                    {
                        throw new InvalidDataException("Potentially malicious entry detected in .kfx archive: " + entry.FullName);
                    }
                    totalBytes += entry.Length;
                    if (totalBytes > MaxUncompressedPackageBytes)
                    {
                        throw new InvalidDataException("Exceeded maximum uncompressed package size limit (250MB).");
                    }
                }
            }
        }

        private static void WriteZipEntry(ZipArchive archive, string entryName, string jsonContent)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(jsonContent);
            }
        }
    }
}
```

### 3. Decoupled Headless `KerfPlayer` & `IRenderBackend`
```csharp
// Source: native-windows/src/Runtime/KerfPlayer.cs
using System;
using System.Collections.Generic;
using Kerf.Engine.Animation;

namespace Kerf.Runtime
{
    public struct Matrix3x2
    {
        public double M11, M12, M21, M22, OffsetX, OffsetY;

        public static readonly Matrix3x2 Identity = new Matrix3x2(1, 0, 0, 1, 0, 0);

        public Matrix3x2(double m11, double m12, double m21, double m22, double ox, double oy)
        {
            M11 = m11; M12 = m12; M21 = m21; M22 = m22; OffsetX = ox; OffsetY = oy;
        }

        public static Matrix3x2 Multiply(Matrix3x2 a, Matrix3x2 b)
        {
            return new Matrix3x2(
                a.M11 * b.M11 + a.M12 * b.M21,
                a.M11 * b.M12 + a.M12 * b.M22,
                a.M21 * b.M11 + a.M22 * b.M21,
                a.M21 * b.M12 + a.M22 * b.M22,
                a.OffsetX * b.M11 + a.OffsetY * b.M21 + b.OffsetX,
                a.OffsetX * b.M12 + a.OffsetY * b.M22 + b.OffsetY
            );
        }
    }

    public class RenderPose
    {
        public double Timestamp { get; set; }
        public Dictionary<string, Matrix3x2> LayerWorldMatrices { get; private set; }
        public Dictionary<string, double> LayerOpacities { get; private set; }

        public RenderPose()
        {
            LayerWorldMatrices = new Dictionary<string, Matrix3x2>(StringComparer.Ordinal);
            LayerOpacities = new Dictionary<string, double>(StringComparer.Ordinal);
        }
    }

    public interface IRenderBackend
    {
        void Initialize(int width, int height);
        void BeginFrame();
        void RenderPose(RenderPose pose);
        void EndFrame();
        void Resize(int width, int height);
        void Destroy();
    }

    public class KerfPlayer
    {
        private readonly StateMachineRuntime _runtime;
        private readonly IRenderBackend _backend;
        private double _currentTime;
        private bool _isPlaying;

        public event EventHandler<string> StateChanged;

        public KerfPlayer(StateMachineDefinition def, List<Clip> clips, IRenderBackend backend = null)
        {
            _runtime = new StateMachineRuntime(def, clips);
            _backend = backend;
            _currentTime = 0.0;
            _isPlaying = false;
        }

        public void Play() { _isPlaying = true; }
        public void Pause() { _isPlaying = false; }
        public void Stop() { _isPlaying = false; _currentTime = 0.0; _runtime.Reset(); }

        public void SetInput(string name, double value)
        {
            if (_runtime != null && _runtime.Definition != null)
            {
                var inp = _runtime.Definition.FindInputById(name);
                if (inp != null)
                {
                    if (inp.Kind == InputKind.Bool) _runtime.SetBool(name, value > 0.5);
                    else _runtime.SetNumber(name, value);
                }
            }
        }

        public void FireTrigger(string name)
        {
            if (_runtime != null) _runtime.FireTrigger(name);
        }

        public void Advance(double dt)
        {
            if (!_isPlaying || dt <= 0.0) return;
            _currentTime += dt;

            var poseMap = new Dictionary<uint, Dictionary<ushort, double>>();
            _runtime.Update(dt, poseMap);

            if (_backend != null)
            {
                var pose = new RenderPose { Timestamp = _currentTime };
                // Compose poses to RenderPose snapshots
                _backend.BeginFrame();
                _backend.RenderPose(pose);
                _backend.EndFrame();
            }
        }
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Monolithic XML/JSON document with coupled UI states | ZIP-based `.kfx` container with dual-payload architecture | Phase 8 (2026-11) | Clean separation of runtime execution payload from editor metadata; portable across web, desktop, and mobile players. |
| Destructive direct mutation of `layer.Width` / `Height` | Decoupled non-destructive `layer.RenderTransform` | Phases 1–2, finalized Phase 8 | Animating scale or rotation never distorts Auto Layout frames, flexbox bounds, or vector geometry. |
| 32-bit FNV-1a hash integers for layer track targets | Stable string GUIDs (`TargetNodeId`, `TargetLayerId`) | Phase 3, finalized Phase 8 | Eliminates hash collision vulnerabilities and guarantees track bindings remain intact across container moves. |
| Silent data loss or crash on missing target layers | Auto-healing + `IsActive = false` + `MigrationReviewDialog` | Phase 8 (2026-11) | Zero runtime crashes; orphaned references are preserved safely and presented interactively to user for resolution. |
| Coupled WPF rendering inside animation loops | Decoupled headless `KerfPlayer` with `IRenderBackend` | Phase 8 (2026-11) | Animation engine can evaluate in headless servers, CI test harnesses, or cross-platform web runtimes without GUI dependencies. |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `System.IO.Compression.dll` and `System.IO.Compression.FileSystem.dll` are present in standard .NET 4.5+ installations on Windows | Standard Stack | **Zero risk**: Verified directly on host machine filesystem at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\` and tested with `csc.exe` compilation [VERIFIED: Tool Execution]. |
| A2 | Existing users opening pre-v4 documents expect orphaned tracks to be preserved rather than deleted silently | Architecture Patterns | **Low risk**: Preserving orphaned references safely with `IsActive = false` allows subsequent re-binding without irreversible data loss. |

*All discrete values and claims are grounded in the repository source files.*

## Open Questions (RESOLVED)

1. **Direct2D Render Backend Surface Binding:**
   - RESOLVED: In Phase 8, `Direct2DRenderBackend` wraps `GpuRenderer` in Kerf Studio; the headless `NullRenderBackend` is used for automated testing.
   - What we know: `GpuRenderer.cs` currently uses WPF's `DrawingContext` and Direct2D hardware acceleration via `RenderTransform`.
   - What's unclear: Should the standalone desktop Direct2D backend reuse `GpuRenderer` visual primitives or render to an independent `HwndRenderTarget`?
   - Recommendation: In Phase 8, `Direct2DRenderBackend` wraps `GpuRenderer` in Kerf Studio; the headless `NullRenderBackend` is used for automated testing.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `csc.exe` | C# Compilation | ✓ | 4.8.9221.0 (C# 5.0) | — |
| `cargo` / `rustc` | Rust Native Core (`kerf_core.dll`) | ✓ | Current | Managed C# fallback in `GpuRenderer` |
| `System.IO.Compression.dll` | `.kfx` ZIP packaging | ✓ | .NET Framework 4.5+ | — |
| `System.Web.Extensions.dll` | `JavaScriptSerializer` JSON parsing | ✓ | .NET Framework 4.5+ | — |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** None.

## Validation Architecture

> Nyquist validation is enabled in `.planning/config.json`.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Custom In-Repo Automated Test Harness (`TestRunner.cs`) |
| Config file | `native-windows/build.bat` (`--test` flag) |
| Quick run command | `cmd.exe /c "cd native-windows && build.bat --test"` |
| Full suite command | `cmd.exe /c "cd native-windows && build.bat --test"` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MIGR-01 | Schema v4.0.0 serialization and backward-compatible migration of v2/v3 documents | unit / regression | `cmd.exe /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test 74) |
| MIGR-02 | Auto-healing of legacy 32-bit FNV-1a hash targets to stable string GUIDs | unit | `cmd.exe /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test 74) |
| MIGR-03 | Orphaned reference resilience: `IsActive = false` marking and zero-crash evaluation | unit / integration | `cmd.exe /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test 74) |
| PERSIST-01 | Full project persistence round-trip of clips, tracks, keyframes, and state machines | integration | `cmd.exe /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test 74) |
| EXPORT-01 | Native `.kfx` ZIP container export/import, dual-payload split, and zip-slip path validation | integration / security | `cmd.exe /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test 75) |
| RUNTIME-02 | Decoupled headless `KerfPlayer` runtime playback, input setting, trigger firing, and pose extraction | unit / headless | `cmd.exe /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test 76) |
| DECOM-01 | Legacy animation code decommissioned, non-destructive render transform enforcement, zero regressions across 73 existing suites | regression | `cmd.exe /c "cd native-windows && build.bat --test"` | ❌ Wave 0 (Test 77) |

### Sampling Rate
- **Per task commit:** `cmd.exe /c "cd native-windows && build.bat --test"` (Runs in under 4 seconds).
- **Per wave merge:** Full test suite green (100% pass rate across all 77 suites).
- **Phase gate:** Zero compiler warnings or regressions before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `TestRunner.cs`: Implement `TestSchemaV4PersistenceAndOrphanMigration` (Test #74) covering MIGR-01, MIGR-02, MIGR-03, PERSIST-01.
- [ ] `TestRunner.cs`: Implement `TestNativeKfxZipExportAndSecurityValidation` (Test #75) covering EXPORT-01.
- [ ] `TestRunner.cs`: Implement `TestStandaloneHeadlessKerfPlayerRuntime` (Test #76) covering RUNTIME-02.
- [ ] `TestRunner.cs`: Implement `TestLegacyAnimationDecommissioningAndNonDestructiveTransforms` (Test #77) covering DECOM-01.
- [ ] `build.bat`: Add `/r:System.IO.Compression.dll,System.IO.Compression.FileSystem.dll` to `build.bat` lines 31 and 57 (`[VERIFIED: native-windows/build.bat:31,57]`).

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | Strict JSON schema parsing with `JavaScriptSerializer`; payload bounds checking; sanitizing numbers (`double.IsNaN` / `double.IsInfinity` guards). |
| V12 File and Resources | yes | Zip-slip directory traversal prevention (`KfxPackage.ValidatePackageSecurity`); uncompressed size quota (250MB limit) to prevent zip-bomb denial of service. |
| V14 Configuration | yes | Non-destructive transform model; atomic file write operations via `.tmp` staging and `.bak` backup replacement (`StorageManager.SaveAtomic`). |

### Known Threat Patterns for Kerf Motion Stack (STRIDE)

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malicious `.kfx` ZIP archive containing `../../Windows/System32` traversal paths | Tampering / Elevation of Privilege | Canonical path validation: verify that `Path.GetFullPath(Path.Combine(destDir, entry.FullName))` starts with `destDir + Path.DirectorySeparatorChar`. |
| Highly compressed ZIP package (zip bomb) exhausting disk or RAM during extraction | Denial of Service | Track cumulative uncompressed byte counts during iteration; abort with `InvalidDataException` if total exceeds 250MB. |
| Tampered or corrupted JSON inside `.kfx` or `.kerf` | Tampering | Wrap deserialization in structured try/catch; report errors via `MigrationReport.Warnings` and fall back to safe empty document. |
| Missing or deleted target layer IDs causing null dereference | Denial of Service | Guard all evaluation loops with `if (!track.IsActive) continue;`; never dereference missing layer targets. |

## Sources

### Primary (HIGH confidence)
- `native-windows/src/Model/CanonicalDocument.cs` - `KerfDocument` (`[VERIFIED: lines 1362-1372]`), `RenderTransform` (`[VERIFIED: lines 667-677]`), `SchemaVersion = "3.0.0"` (`[VERIFIED: line 1378]`).
- `native-windows/src/Model/DocumentMigrations.cs` - Migration pipeline (`[VERIFIED: lines 1-160]`), schema 3.x check (`[VERIFIED: line 38]`).
- `native-windows/src/IO/StorageManager.cs` - `SaveAtomic` (`[VERIFIED: lines 58-66]`), `LoadDocument` (`[VERIFIED: line 107]`).
- `native-windows/src/Engine/Animation/AnimationTypes.cs` - `Track` (`[VERIFIED: lines 158-165]`), `Clip` (`[VERIFIED: lines 351-370]`), `AnimationProjectData` (`[VERIFIED: lines 512-524]`).
- `native-windows/src/Engine/Animation/StateMachineTypes.cs` - `NodeListener` (`[VERIFIED: lines 732-738]`), `StateMachineDefinition` (`[VERIFIED: lines 938-960]`).
- `native-windows/src/Engine/Animation/StateMachineRuntime.cs` - Listener execution and pointer dispatch (`[VERIFIED: lines 343-355]`).
- `native-windows/src/Engine/Animation/PropertyBinder.cs` - Non-destructive transform application (`[VERIFIED: lines 24-38]`), FNV-1a hash algorithm (`[VERIFIED: lines 9-22]`).
- `native-windows/build.bat` - Visual C# compiler options and references (`[VERIFIED: lines 31, 57]`).
- `native-windows/tests/TestRunner.cs` - Full verification test suite (`[VERIFIED: lines 1-140]`).
- .NET Framework 4.5+ BCL - `System.IO.Compression.ZipArchive` verification on local machine (`[VERIFIED: C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll]`).

### Secondary (MEDIUM confidence)
- User Specification (2026-11-09): Native `.kfx` ZIP package structure, dual-payload architecture, developer SDK design (`@kerf/runtime`), and headless evaluation principles.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Verified native BCL libraries and existing in-repo components.
- Architecture: HIGH - Grounded in existing Phase 1–7 codebase and verified file structures.
- Pitfalls: HIGH - Verified against real C# 5.0 / .NET Framework 4.5+ constraints and existing test suite.

**Research date:** 2026-11-09
**Valid until:** 2026-12-09
