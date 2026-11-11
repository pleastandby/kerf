---
phase: 08-persistence-runtime-export-decommission
plan: 02
subsystem: io-runtime-export
tags:
  - kfx
  - zip
  - security
  - zip-slip
  - runtime
  - headless
  - direct2d
  - render-backend
requires:
  - "08-01"
provides:
  - "Native .kfx ZIP container export and import with dual-payload separation"
  - "KfxSecurityValidator zip-slip and 250MB zip-bomb defense"
  - "Decoupled pure numeric Matrix3x2 2D affine transform engine"
  - "Headless KerfPlayer SDK facade and pluggable IRenderBackend"
affects:
  - "08-03"
tech-stack:
  added:
    - System.IO.Compression
    - System.IO.Compression.FileSystem
  patterns:
    - Dual-payload ZIP package architecture (runtime payload vs editor metadata)
    - Zip-slip canonical path containment validation
    - Pure numeric 2D affine matrix math with zero WPF dependencies
    - Pluggable render backend abstraction (NullRenderBackend for headless testing, Direct2DRenderBackend for desktop rendering)
    - Developer SDK facade pattern (KerfPlayer wrapping KerfRuntime)
key-files:
  created:
    - native-windows/src/IO/KfxSecurityValidator.cs
    - native-windows/src/IO/KfxPackage.cs
    - native-windows/src/Runtime/Matrix3x2.cs
    - native-windows/src/Runtime/RenderPose.cs
    - native-windows/src/Runtime/IRenderBackend.cs
    - native-windows/src/Runtime/Backends/NullRenderBackend.cs
    - native-windows/src/Runtime/Backends/Direct2DRenderBackend.cs
    - native-windows/src/Runtime/KerfRuntime.cs
    - native-windows/src/Runtime/KerfPlayer.cs
  modified:
    - native-windows/build.bat
    - native-windows/src/UI/StudioView.cs
    - native-windows/tests/TestRunner.cs
decisions:
  - "D-01 / D-02: Native .kfx container packaged as standard ZIP with dual-payload architecture: runtime payload (manifest.json, scene.json, animations.json, state-machines.json) separated from editor metadata (metadata.json)."
  - "EXPORT-01 / T-08-04 / T-08-05: Strict zip-slip path traversal guards reject entries containing '..', leading slashes, and external targets, and enforce a 250MB decompression bomb limit without external NuGet dependencies."
  - "D-04 / RUNTIME-02: Headless KerfPlayer / KerfRuntime evaluation engine constructed with zero dependencies on WPF, PresentationCore, Direct2D, DOM, or React."
metrics:
  duration: 18 min
  completed_date: "2026-11-11T14:33:00+05:30"
status: complete
actuals:
  tokens: 48000
  tasks: 2
  commits: 2
  plan_head_before: "4f34f863630a3b502d0d3eb74185b4ef438c1449"
  plan_head_after: "7d27bf46182c40cf1d0f507ba8e98059e66c61f2"
---

# Phase 8 Plan 2: Native .kfx Container Export/Import & Headless KerfPlayer Runtime Summary

Native `.kfx` ZIP container export/import engine with dual-payload packaging and strict zip-slip security guards, alongside a standalone headless `KerfPlayer` / `KerfRuntime` SDK with pure numeric `Matrix3x2` affine transforms and pluggable `IRenderBackend`.

## Key Objectives Delivered

1. **Native .kfx Container Packaging & Dual-Payload Separation (D-01, D-02, EXPORT-01):**
   - Configured `build.bat` with BCL `.NET Framework 4.5+` compression assemblies (`System.IO.Compression.dll` and `System.IO.Compression.FileSystem.dll`).
   - Implemented `KfxPackage.ExportPackage`: atomically creates a compressed `.kfx` ZIP package containing runtime payloads (`manifest.json`, `scene.json`, `animations.json`, `state-machines.json`) and optional editor metadata (`metadata.json`).
   - Implemented `KfxPackage.ImportPackage`: validates archive security, hydrates full `KerfDocument` instances when metadata is present, and gracefully synthesizes documents from the runtime payload alone when metadata is absent.
   - Added StudioView File menu items: `_Export Runtime Package (.kfx)...` and `_Import Animation Package (.kfx)...`.

2. **Zip-Slip & Decompression Bomb Security Guards (T-08-04, T-08-05):**
   - Implemented `KfxSecurityValidator.ValidatePackageSecurity` and `ValidateExtractionPath`.
   - Rejects entries containing `..`, leading `/` or `\`, or paths attempting to escape target extraction directories with `InvalidDataException`.
   - Protects against ZIP decompression bombs by calculating cumulative uncompressed length and aborting if exceeding 250MB.

3. **Decoupled Headless Motion Runtime SDK (D-04, RUNTIME-02):**
   - Implemented `Matrix3x2` pure numeric struct with 2D affine multiplication, translation, scaling, and rotation, with zero references to `System.Windows.Media.Matrix` or `PresentationCore.dll`.
   - Implemented `RenderPose` snapshot containing timestamp, layer world matrices, opacities, and local transforms.
   - Defined `IRenderBackend` renderer interface with `NullRenderBackend` for headless automated verification and `Direct2DRenderBackend` for desktop WPF conversion.
   - Implemented `KerfRuntime` platform-agnostic evaluation engine driving clip playback, spring physics, and state machines with hierarchical world matrix composition.
   - Implemented `KerfPlayer` high-level developer facade providing `Play()`, `Pause()`, `Stop()`, `Advance(dt)`, `SetInput()`, `SetBool()`, `FireTrigger()`, and `DispatchPointerEvent()`.

4. **Automated Verification:**
   - Test #75 (`TestNativeKfxZipExportAndSecurityValidation`): verifies package export, internal ZIP entries, metadata round-trip fidelity, runtime-only package fallback, zip-slip traversal rejection, and 250MB zip-bomb defense.
   - Test #76 (`TestStandaloneHeadlessKerfPlayerRuntime`): verifies headless instantiation with `NullRenderBackend`, time stepping via `player.Advance(0.016)`, state machine transition driving via `player.SetInput("isActive", 1.0)`, `Matrix3x2` layer translation calculation, and pointer event dispatching.
   - Full automated test suite passes: **76 Passed, 0 Failed**.

## Key Files Created and Modified

- [`native-windows/build.bat`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/build.bat): Added BCL compression assembly references.
- [`native-windows/src/IO/KfxSecurityValidator.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/IO/KfxSecurityValidator.cs): Zip-slip directory traversal guards and 250MB decompression bomb limits.
- [`native-windows/src/IO/KfxPackage.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/IO/KfxPackage.cs): Atomic ZIP container export and resilient dual-payload import.
- [`native-windows/src/Runtime/Matrix3x2.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/Runtime/Matrix3x2.cs): Pure numeric 2D affine matrix struct with zero WPF dependencies.
- [`native-windows/src/Runtime/RenderPose.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/Runtime/RenderPose.cs): Evaluated layer frame snapshot.
- [`native-windows/src/Runtime/IRenderBackend.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/Runtime/IRenderBackend.cs): Pluggable rendering backend interface.
- [`native-windows/src/Runtime/Backends/NullRenderBackend.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/Runtime/Backends/NullRenderBackend.cs): Headless test render backend tracking frame count and last pose.
- [`native-windows/src/Runtime/Backends/Direct2DRenderBackend.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/Runtime/Backends/Direct2DRenderBackend.cs): Desktop WPF/Direct2D rendering bridge.
- [`native-windows/src/Runtime/KerfRuntime.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/Runtime/KerfRuntime.cs): Headless motion evaluation and hierarchical transform composition engine.
- [`native-windows/src/Runtime/KerfPlayer.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/Runtime/KerfPlayer.cs): High-level developer SDK player facade.
- [`native-windows/src/UI/StudioView.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/src/UI/StudioView.cs): Export and Import .kfx menu items and dialog handlers.
- [`native-windows/tests/TestRunner.cs`](file:///c:/Users/victus/OneDrive/Desktop/Figma/native-windows/tests/TestRunner.cs): Tests #75 and #76 automated verification.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Headless KerfPlayer input type polymorphism**
- **Found during:** Task 2 verification of Test #76.
- **Issue:** `KerfPlayer.SetInput(string, double)` originally delegated directly to `_runtime.StateMachine.SetNumber`, which ignores inputs declared with `InputKind.Bool` or `InputKind.Trigger`.
- **Fix:** Enhanced `KerfPlayer.SetInput` to inspect the target input's declared `InputKind` and route to `SetBool(idx, value > 0.5)` or `FireTrigger(idx)` when appropriate.
- **Files modified:** `native-windows/src/Runtime/KerfPlayer.cs`
- **Commit:** `7d27bf4`

## Self-Check: PASSED

- All 9 created files exist on disk in `native-windows/src/IO/` and `native-windows/src/Runtime/`.
- Both commits `d8f4a33` and `7d27bf4` exist in git history.
- Test suite executed via `native-windows\build.bat --test` with 76 tests passing and 0 failures.
