# Kerf 3.1 Beta — Native Windows Vector Design

![Windows](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows)
![DirectX](https://img.shields.io/badge/Graphics-Direct3D%2011%20Hardware-green)
![Rust](https://img.shields.io/badge/Engine-Rust%201.99%20Native%20SIMD-dea584?logo=rust)
![C# / WPF](https://img.shields.io/badge/.NET%20Framework-4.8%20WPF-512BD4?logo=csharp)
[![npm version](https://img.shields.io/npm/v/kerf-react.svg?logo=npm&color=CB3837)](https://www.npmjs.com/package/kerf-react)
![License](https://img.shields.io/badge/license-MIT-green)

**Kerf** is a native Windows application for vector design, Bézier editing, layout, and animation. It combines a Direct3D 11 renderer with a Rust geometry engine and a command-history editing workflow.

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
  - Transparent forward migration of legacy `.kerf` documents to the canonical v4.0.0 schema.
- **Production Asset Export**:
  - Pixel-perfect SVG vector export and high-DPI rasterization (1x, 2x, 3x).

---

## 🏛️ Architecture & Directory Manifest

```
Figma/
├── build-output/
│   └── release/
│       ├── Kerf.exe               # 64-bit Desktop Application
│       ├── KerfSetup.exe           # Standalone Windows Installer
│       └── kerf_core.dll           # Native Rust Acceleration Library (x64)
├── PROGRESS.md                    # Modernization Handover & Milestone Tracker
├── README.md                      # Project Documentation
├── assets/                        # Brand Assets & Vector Logos
│   ├── Kerf-Logo.svg              # Official mint-and-charcoal Kerf logo
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
│   └── tests/                     # Automated Verification Test Suite
│       ├── TestRunner.cs
│       └── TestRunner.exe
└── packages/                      # Distributed Runtime Packages
    └── kerf-react/                # Official React runtime published to npm (kerf-react)
```

---

## 📦 Web & React Runtime (`kerf-react`)

The official **Kerf React Runtime** is published on [npm](https://www.npmjs.com/package/kerf-react) for embedding interactive vector graphics, animations, and state machines in any web application:

```bash
npm install kerf-react
```

```tsx
import { Kerf } from 'kerf-react';

export function Banner() {
  return (
    <div style={{ width: 600, height: 400 }}>
      <Kerf src="/animation.kfx" autoplay loop />
    </div>
  );
}
```

See [packages/kerf-react](packages/kerf-react) for hook APIs (`useKerf`), interactive state machine inputs, and full documentation.

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
1. Generate a multi-resolution Windows icon from the official green `assets/Kerf-Logo.svg`.
2. Compile `crates/kerf_core` in release mode and deploy `kerf_core.dll`.
3. Compile the application as `build-output\release\Kerf.exe` with the current icon and splash image embedded.
4. Compile `build-output\release\KerfSetup.exe` with the application and native core embedded, so the installer runs without adjacent payload files.

### 2. Build and Run the Verification Test Suite
```cmd
cd native-windows
build.bat --test
```
This builds the app and standalone installer, compiles the automated test runner, runs the suite, and removes the temporary runner after success.

### 3. Launch Kerf
Double-click `build-output\release\Kerf.exe` or execute:
```cmd
build-output\release\Kerf.exe
```
Kerf opens with the splash screen, initializes the Direct3D rendering context and native Rust engine, and presents the studio canvas.

To distribute Kerf, provide `build-output\release\KerfSetup.exe`; it contains the application and native core and installs without requiring the other build outputs.

---

## 📄 License
MIT License. © 2026 Kerf Team.
