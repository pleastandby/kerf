# Kerf 3.1 Beta — Interactive Design, Motion & Prototyping Platform

![Windows](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows)
![DirectX](https://img.shields.io/badge/Graphics-Direct3D%2011%20Hardware-green)
![Rust](https://img.shields.io/badge/Engine-Rust%20Native%20SIMD-dea584?logo=rust)
![C# / WPF](https://img.shields.io/badge/.NET%20Framework-4.8%20WPF-512BD4?logo=csharp)
[![npm version](https://img.shields.io/npm/v/kerf-react.svg?logo=npm&color=CB3837)](https://www.npmjs.com/package/kerf-react)
![Tests](https://img.shields.io/badge/Tests-87%20Passed%20%2F%200%20Failed-success)
![License](https://img.shields.io/badge/license-MIT-green)

**Kerf** is a high-performance interactive design, real-time prototyping, and vector motion platform paired with a lightweight web runtime. Built with a hardware-accelerated Direct3D 11 renderer and a native Rust geometric kernel, Kerf bridges the gap between vector illustration, interactive state machines, procedural rigging constraints, and production web deployment.

Design on the canvas, rig with constraints and state machines, prototype interactive micro-interactions, and export directly to `.kfx` packages playable natively on the web with [`kerf-react`](https://www.npmjs.com/package/kerf-react).

---

## ✨ Core Platform Capabilities

### 🕹️ Interactive State Machines & 13 Rive-Style Listeners
Create production-grade interactive prototypes and UI widgets with full state-driven logic:
- **State Machine Runtime**: Visual state graph with interactive states, entry points, and condition groups.
- **Continuous Blending**: 1D and Direct blend trees interpolating timeline animations smoothly based on numeric inputs.
- **13 Interactive Listeners**:
  - `Pointer Enter`, `Pointer Exit`, `Pointer Down`, `Pointer Up`, `Pointer Move`, `Pointer Drag`, `Click`, `Focus`, `Blur`, `Keyboard`, `Gamepad`, `Text Input`, `Semantic Action`.
- **Pointer Coordinate Tracking**: Map cursor movement (canvas X/Y or normalized 0–100% relative to target layer bounds) directly to State Machine numeric inputs for dynamic eye/cursor tracking, parallax, and hover effects.
- **Hardware & Keyboard Inputs**: Filter by specific keys (e.g. `Space`, `Enter`), gamepad buttons, or semantic accessibility actions.

### 🔗 Rigging & Animation Constraints Engine
Procedural constraint solver running directly in the graphics pipeline with strength blending (0% to 100%):
- **Transform**: Synchronize or offset position, scale, and rotation relative to target nodes.
- **Distance**: Maintain `Exact`, `Closer`, or `Further` Euclidean distance limits from target objects.
- **Translation, Scale & Rotation**: Constrain individual axes with minimum/maximum clamping and offset angles.
- **IK (Inverse Kinematics)**: 2-bone planar analytical IK solver with interactive bend inversion.
- **Follow Path**: Constrain any shape, text, or group along a vector path's arc length (0% to 100%) with automatic tangent vector alignment (`Orient to Path`).
- **Scroll**: Clamp and track scroll offsets along vertical, horizontal, or bidirectional axes.

### ✂️ Multi-Target Clipping & Inverse Hole Masking
Rive-grade geometric clipping system:
- **Multi-Target Clipping**: Clip any shape, path, group, or frame to multiple target shapes simultaneously.
- **Inverse Hole Masking**: Toggle between **Clip Inside** and **Clip Outside (Hole)** to punch procedural cutouts through layers.

### 🖊️ Precision Vector Design & Advanced Strokes
- **Bézier Pen Engine**: Live rubber-band curve preview, loop closure detection, and independent or symmetric tangent handles.
- **Advanced Stroke Effects**: Outside stroke clipping, interactive **Trim Path** (start, end, offset with live animation preview), and dynamic **Stroke Tapering**.
- **Multiple Fills & Strokes**: Stack arbitrary solid, linear, and radial paint layers with smooth drag-and-drop reordering.
- **On-Canvas Gradient Rollers**: Freely movable radial and linear gradient start, end, and focal points with on-canvas pins and ramp midpoints.

### 📐 Figma-Grade Auto Layout & Smart Guides
- **Flexbox Auto Layout**: Automatic horizontal row and vertical column reflow, 3x3 alignment matrix, packed vs. space-between (`auto` gap), 4-side padding, and Hug vs. Fixed sizing.
- **Smart Distribution Guides**: Automatic equal-spacing measurement guides and grid alignments when dragging objects.
- **Magnetic Snapping Toggle**: Instant header toggle for freehand drawing vs. precision snapping.

### 🖼️ Photoshop-Grade Image Editing & Cropping
- **Direct Drag-and-Drop**: Drop raster assets (PNG, JPEG, WebP) directly onto the canvas.
- **On-Canvas Crop Tool**: Double-tap images to activate live 8-handle crop bounds.
- **Image Adjustments**: Real-time non-destructive brightness, contrast, saturation, and tint adjustments.

### ⚡ Native Rust SIMD Acceleration (`kerf_core.dll`)
- **Analytical Cubic Bézier Extrema Solver**: Exact bounding boxes calculated via quadratic derivative roots ($B'(t) = 0$) in native x64 assembly.
- **Adaptive de Casteljau Flattening**: Recursive curve subdivision for ultra-smooth GPU tessellation.
- **AVX2 / SSE4.1 SIMD**: Vectorized point-in-polygon winding number calculations and convex hull evaluation.

---

## 📦 Web & React Runtime (`kerf-react`)

Export your interactive animations as `.kfx` files and render them seamlessly in any web app:

```bash
npm install kerf-react
```

### Basic Component Usage

```tsx
import React from 'react';
import { Kerf } from 'kerf-react';

export function InteractiveHero() {
  return (
    <div style={{ width: 800, height: 600 }}>
      <Kerf 
        src="/assets/robot-mascot.kfx" 
        autoplay 
        loop 
        stateMachine="MainMachine"
        inputs={{
          isHovered: true,
          pointerX: 50.0
        }}
        onStateChange={(state) => console.log('Current state:', state)}
      />
    </div>
  );
}
```

### Hook API (`useKerf`)

```tsx
import React from 'react';
import { useKerf } from 'kerf-react';

export function CustomCanvasPlayer() {
  const { canvasRef, setBool, setNumber, fireTrigger } = useKerf({
    src: '/assets/toggle-button.kfx',
    autoplay: true,
    stateMachine: 'ToggleMachine'
  });

  return (
    <div>
      <canvas ref={canvasRef} style={{ width: 300, height: 150 }} />
      <button onClick={() => fireTrigger('submit')}>Submit</button>
    </div>
  );
}
```

For more details and API options, visit [packages/kerf-react](packages/kerf-react).

---

## 📤 Universal Export Studio

Export your work from Kerf Studio to multiple production formats:
- **`.kfx` Motion Package**: Compressed, format-versioned runtime package for web, desktop, and game engines.
- **Standalone HTML5 Web Player**: Single-file, zero-dependency HTML player with embedded state machine controls and requestAnimationFrame runner for instant sharing.
- **Vector Documents**: Clean SVG paths and multi-page vector PDF documents.
- **High-DPI Raster Images**: PNG, JPEG, and WebP exports at 1x, 2x, 3x, and 4x scales.
- **H.264 MP4 Video**: High-bitrate 60fps video export with configurable After Effects-style render settings.
- **Animated GIF**: Loopable GIF sequences with palette optimization.

---

## 🏛️ Architecture & Directory Manifest

```
Figma/
├── build-output/
│   └── release/
│       ├── Kerf.exe               # Native 64-bit Desktop Application
│       ├── KerfSetup.exe          # Standalone Windows Installer
│       └── kerf_core.dll          # Native Rust Acceleration Library (x64)
├── assets/                        # Brand Assets, Logos & Sample Packages
│   ├── Kerf-Logo.svg              # Official Kerf logo
│   └── kerf.ico                   # Multi-resolution application icon
├── native-windows/                # Desktop Application Source Directory
│   ├── build.bat                  # Unified compilation script (Rust + C#)
│   ├── KerfInstaller.cs           # Standalone Installer source
│   ├── crates/kerf_core/          # Rust Native Acceleration Core
│   │   ├── Cargo.toml
│   │   └── src/                   # Bézier mathematics, SIMD, winding, convex hull
│   ├── src/                       # Desktop Application Core (C# 5.0 / WPF)
│   │   ├── Engine/                # Direct3D GpuRenderer, ConstraintsEngine, StateMachineRuntime
│   │   ├── Model/                 # CanonicalDocument, Migrations, CommandHistory
│   │   ├── Tools/                 # BezierVectorEngine, AutoLayoutEngine
│   │   ├── IO/                    # WebPlayerGenerator, KfxPackage, Mp4Exporter, SvgExporter
│   │   └── UI/                    # StudioView, HomePageView, MainWindow, Dialogs
│   └── tests/                     # Automated Verification Test Suite (87 tests)
│       ├── TestRunner.cs
│       └── TestRunner.exe
└── packages/                      # Distributed Packages
    └── kerf-react/                # Official React / TypeScript runtime published to npm
```

---

## 🚀 Building & Running from Source

### Requirements
- **OS**: Windows 10 / 11 (64-bit)
- **.NET Framework**: 4.8 runtime (pre-installed on Windows 10/11)
- **Rust Toolchain** *(optional, for rebuilding `kerf_core.dll`)*: `stable-x86_64-pc-windows-gnu` or `stable-x86_64-pc-windows-msvc`
- **Node.js**: >= 18.x (for building `packages/kerf-react`)

### 1. Build Desktop Application & Standalone Installer
```cmd
cd native-windows
build.bat
```
This produces:
- `build-output\release\Kerf.exe` (Desktop Application)
- `build-output\release\KerfSetup.exe` (Self-contained Installer)
- `build-output\release\kerf_core.dll` (Rust Native Core)

### 2. Run Automated Verification Tests
```cmd
cd native-windows
build.bat --test
```
Runs the full 87-test automated verification test suite verifying constraints, clipping, 13 listeners, layout engines, and render transforms.

### 3. Build Web Runtime (`kerf-react`)
```cmd
npm run build --prefix packages/kerf-react
```

---

## 📄 License
MIT License. © 2026 Kerf Team.
