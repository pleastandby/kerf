# Kerf Studio — Native Windows GPU-Accelerated Vector Design Application

![Windows](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows)
![DirectX](https://img.shields.io/badge/Graphics-Direct3D%2011%20Hardware-green)
![Rust](https://img.shields.io/badge/Engine-Rust%201.99%20Native%20SIMD-dea584?logo=rust)
![C# / WPF](https://img.shields.io/badge/.NET%20Framework-4.8%20WPF-512BD4?logo=csharp)
![License](https://img.shields.io/badge/license-MIT-green)

**Kerf Studio** is a standalone, GPU-accelerated native Windows desktop application for professional vector design, Bézier path manipulation, and component layout. Built with a Direct3D 11 hardware-accelerated retained scene graph and a high-performance native Rust geometric kernel (`kerf_core.dll`), Kerf Studio delivers true 60+ FPS subpixel rendering, analytical mathematical Bézier solvers, and a creative suite experience inspired by After Effects and Figma.

---

## ✨ Key Capabilities & Features

### 🎬 After Effects-Style Cinematic Launch Experience
- **Sleek Hardware Initialization Splash**:
  - Deep obsidian dark mode UI with ambient radial bloom and hardware telemetry display.
  - Features the official **Kerf Folded-Ribbon iridescent brand mark** (peach, coral, indigo, sky blue, mint & teal multi-stop gradients).
  - Progressive subsystem initialization tracker with real-time status updates:
    - Direct3D 11 retained visual engine mounting
    - Native Rust geometric kernel (`kerf_core.dll`) C-ABI binding
    - Analytical Bézier extrema solver calibration ($B'(t) = 0$)
    - Canonical document virtual file system activation
  - Smooth animated gradient progress bar with instant skip-to-canvas capability (<kbd>Space</kbd>, <kbd>Enter</kbd>, or mouse click).

### ⚡ Native Rust Graphics & Geometry Engine (`kerf_core.dll`)
- **Analytical Cubic Bézier Extrema Solver**:
  - Solves $B'(t) = 3at^2 + 2bt + c = 0$ quadratic roots analytically in native x64 assembly to obtain exact mathematical bounds without coarse sampling.
- **Adaptive de Casteljau Curve Flattening**:
  - Subdivides curves recursively with configurable tolerance, producing smooth polylines for tessellation and raster export.
- **SIMD Hardware Acceleration**:
  - Auto-detects AVX2, FMA3, and SSE4.1 CPU instructions at runtime for vector transformations and hit-testing.
- **Winding Number Point-in-Path Containment**:
  - Robust non-zero winding number algorithm to test whether coordinates are inside complex, concave, or self-intersecting Bézier paths.
- **Signed Path Area (Shoelace Formula)**:
  - Exact signed area calculation for winding orientation (CW vs. CCW) and hole detection.
- **Convex Hull (Andrew's Monotone Chain)**:
  - $O(N \log N)$ exact convex hull calculation for arbitrary vertex clouds and multi-selection bounds.
- **Fault-Tolerant C# Interop**:
  - High-speed P/Invoke bridge (`RustEngineInterop.cs`) with graceful managed fallback if native DLL is missing.

### 🖊️ Real-Time Bézier Pen & Vector Node Engine
- **Live Rubber-Band Curve Preview**:
  - Real-time elastic Bézier segment connecting the active anchor node to the cursor.
- **Loop Closure Detection**:
  - Glowing closure indicator with snap badges when hovering near the starting anchor.
- **Anchor & Tangent Handle Interaction**:
  - Independent and symmetric tangent handle dragging with subpixel precision.
- **Non-Destructive Controls**:
  - <kbd>Backspace</kbd> / <kbd>Delete</kbd>: Undo last node placement while keeping the Pen tool active.
  - <kbd>Enter</kbd> / <kbd>Esc</kbd> / Right-Click: Finalize vector path and return to Selection tool.

### 📐 Figma-Grade Auto Layout Engine
- **Flex Reflow**:
  - Horizontal Row (`->`) and Vertical Column (`↓`) directions.
- **Alignment & Distribution**:
  - 3x3 interactive alignment matrix with Packed vs. Space Between (`auto` gap).
- **Padding & Sizing**:
  - 4-side padding (Top, Right, Bottom, Left) and Hug Contents vs. Fixed Width/Height frames.

### 💾 Canonical Document Virtual File System
- **Crash Recovery & Autosave**:
  - Background atomic project persistence preventing data loss.
- **Multi-Version Migration**:
  - Transparent forward migration of legacy `.kerf` documents to canonical v3.0 schemas.
- **Production Asset Export**:
  - Pixel-perfect SVG vector export and high-DPI rasterization (1x, 2x, 3x).

---

## 🏛️ Architecture & Directory Manifest

```
Figma/
├── KerfStudio.exe                 # Standalone 64-bit Desktop Application
├── KerfStudioSetup.exe            # Windows Installer Wizard
├── kerf_core.dll                  # Native Rust Acceleration Library (x64)
├── PROGRESS.md                    # Modernization Handover & Milestone Tracker
├── README.md                      # Project Documentation
├── assets/                        # Brand Assets & Vector Logos
│   ├── kerf-logo-new.svg          # Official folded-ribbon SVG
│   └── kerf.ico                   # Multi-resolution Windows application icon
├── native-windows/                # Desktop Application Source Directory
│   ├── build.bat                  # Unified compilation script (Rust + C#)
│   ├── KerfInstaller.cs           # Native Windows Installer source
│   ├── crates/kerf_core/          # Rust Native Acceleration Core
│   │   ├── Cargo.toml
│   │   └── src/
│   │       ├── bezier.rs          # Cubic evaluation, bounds, arc length
│   │       ├── geometry.rs        # Hit testing, RDP, winding, convex hull
│   │       ├── simd.rs            # AVX2/FMA CPU capability detection
│   │       └── lib.rs             # C-ABI export surface
│   ├── src/                       # C# Desktop Application Core
│   │   ├── Program.cs             # Application Entry & Splash Lifecycle
│   │   ├── Engine/                # RetainedSceneGraph, GpuRenderer, RustEngineInterop
│   │   ├── Model/                 # CanonicalDocument, Migrations, History
│   │   ├── Tools/                 # BezierVectorEngine, AutoLayout, Booleans
│   │   ├── IO/                    # StorageManager, SvgExporter, RasterExporter
│   │   └── UI/                    # StudioView, HomePageView, MainWindow, SplashScreen, KerfLogo
│   └── tests/                     # Automated Verification Test Suite (13/13 tests)
│       ├── TestRunner.cs
│       └── TestRunner.exe
└── testdata/                      # Test files & sample projects
```

---

## 🚀 Building & Running

### Requirements
- **Operating System**: Windows 10 / 11 (64-bit)
- **.NET Framework**: 4.8 runtime (pre-installed on Windows 10/11)
- **Rust Toolchain** *(optional, for compiling `kerf_core.dll`)*: `stable-x86_64-pc-windows-gnu` or `stable-x86_64-pc-windows-msvc`

### 1. Build the Complete Desktop Suite
Run the unified build batch script from `native-windows`:
```cmd
cd native-windows
build.bat
```
This will automatically:
1. Compile `crates/kerf_core` in release mode into `kerf_core.dll`.
2. Deploy `kerf_core.dll` into the output path.
3. Compile all C# source modules into `KerfStudio.exe` with `kerf.ico` embedded.
4. Compile `KerfStudioSetup.exe` installer with registry and file associations.

### 2. Run the Verification Test Suite
```cmd
native-windows\tests\TestRunner.exe
```
Output:
```
=====================================================================
 Kerf Studio - Automated Verification Test Suite (v3.0.0)
=====================================================================
[PASS] Document Migrations: Legacy React v1/v2 to Canonical v3.0
[PASS] Document Migrations: Legacy WPF v2.4 to Canonical v3.0
[PASS] Document Persistence: Atomic Save & Re-load Roundtrip
[PASS] Command History: Add, Undo, Redo, Batch Transactions
[PASS] Transform Engine: 8-handle Aspect Ratio & Center Scaling
[PASS] Bézier Engine: Anchor & Smooth Handle Alignments
[PASS] Auto Layout Engine: Horizontal & Vertical Reflow with Hug Sizing
[PASS] Boolean Geometry Engine: Path Combination
[PASS] SVG Exporter: Compliant Vector XML Generation
[PASS] Layer Hierarchy & Management: Containers, Cycles, Nested Undo/Redo
[PASS] Bézier Pen Curves & Vector Edit Mode: Handles, HitTest, Node Mutators
[PASS] Path Geometry Sync & Corner Curving: Movement, Scaling, Corner Widgets
    [INFO] Rust Core: Native DLL Loaded (AVX2 + FMA Accelerated)
[PASS] Native Rust Acceleration: Signed Area, Point Containment & Convex Hull
=====================================================================
 Tests Completed: 13 Passed, 0 Failed
=====================================================================
```

### 3. Launch Kerf Studio
Simply double-click `KerfStudio.exe` or execute:
```cmd
KerfStudio.exe
```
Kerf Studio will open with the After Effects-style splash loading screen, initialize the Direct3D hardware context and native Rust SIMD engine, and present the studio canvas.

---

## 📄 License
MIT License. © 2026 Kerf Studio Team.
