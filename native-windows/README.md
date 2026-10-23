# Kerf Studio — Native Windows Architecture & Reference Manual

## Overview
**Kerf Studio (v3.0.0)** is a production-grade native Windows desktop vector design and prototyping application inspired by Figma and Adobe Illustrator. Compiled to a standalone 64-bit Windows executable (`KerfStudio.exe`) using .NET Framework 4.0/4.8 Direct3D hardware-accelerated milcore composition.

Unlike browser-based wrappers, Kerf Studio is built specifically as a primary Windows desktop application with transactional local storage, native `.kerf` file associations, a retained Direct3D scene graph, and honest hardware telemetry.

---

## Technical Architecture (v3.0.0)

```
┌────────────────────────────────────────────────────────┐
│                   KerfStudio.exe (v3.0.0)              │
│       Native 64-bit Standalone Direct3D Vector Studio  │
├────────────────────────────────────────────────────────┤
│  Windows Shell & UI Routing Layer                      │
│  • Custom WindowChrome (Frameless, Minimize/Max/Close) │
│  • View Router (HomePageView <-> StudioView)           │
│  • Save-Before-Closing Modal Guard & Recovery Alert    │
├────────────────────────────────────────────────────────┤
│  Retained Scene Graph & GPU Render Pipeline            │
│  • RetainedSceneGraph: DrawingVisual caching per node  │
│  • Spatial Indexing & AABB Viewport Culling            │
│  • Direct3D Pixel Shaders: Blur & DropShadowEffect     │
│  • Multi-pass Inner Shadows with geometric clipping    │
│  • Multi-stop Linear & Radial Gradient Brushes         │
│  • Truthful Telemetry: Microsecond frame time & Tier   │
│  • Tier 0 Software Rasterizer Fallback                 │
├────────────────────────────────────────────────────────┤
│  Canonical Document Container & Transactional Engine   │
│  • Canonical KerfDocument v3.0.0 (Pages, Layers, Fills)│
│  • DocumentMigrations: Lossless React v1/v2 & WPF v2.4 │
│  • CommandHistory: ICommand Undo/Redo & Batch Actions  │
├────────────────────────────────────────────────────────┤
│  Interactive Tool Island & Geometry Engine             │
│  • 8-handle Transform Box with Aspect Lock & Rotation  │
│  • Bézier Vector Pen: Anchor nodes & Smooth handles    │
│  • Boolean Operations: Union, Subtract, Intersect, Xor │
│  • Figma Auto Layout: Horizontal, Vertical, Wrap, Grid │
│  • Card-based Layer Panel: Depth indent & reordering   │
├────────────────────────────────────────────────────────┤
│  Storage, Export & Deployment                          │
│  • Atomic Transactional Saves (write tmp + rename)     │
│  • Autosave Recovery Engine (%LocalAppData%\Autosave)  │
│  • Validated XML SVG Vector Exporter                   │
│  • High-DPI Raster Exporter (PNG/JPEG at 1x, 2x, 3x)   │
│  • Windows Installer Wizard (Registry & Shortcuts)     │
└────────────────────────────────────────────────────────┘
```

---

## Canonical Document Model (v3.0.0)

Every design is stored in a clean, versioned JSON container:
- `KerfDocument`: Project metadata, SchemaVersion ("3.0.0"), AssetLibrary, Pages.
- `Page`: Canvas settings, background color, dot/grid spacing, guides, hierarchical Layers.
- `LayerNode`:
  - Bounds & Transform: `X`, `Y`, `Width`, `Height`, `Rotation`, `ScaleX`, `ScaleY`.
  - Corner Radii: Uniform `CornerRadius` or 4-corner individual radii.
  - Geometry: `VectorGeometry` with Bézier anchor nodes, incoming/outgoing tangent handles.
  - Fills: `Paint[]` supporting Solid colors, Linear gradients (angle, stops), Radial gradients, Images.
  - Strokes: `StrokeData[]` supporting inside/center/outside alignment, line caps, line joins, dash patterns.
  - Effects: `EffectData[]` with Drop Shadows, Inner Shadows, and Gaussian Layer Blur.
  - Layout: `AutoLayoutData` with direction, padding, gap, alignment, justify, and hug/fill sizing.
  - Typography: `TypographyData` with font family, weight, size, line-height, text case, and alignment.

---

## Building and Testing

### Compilation
From the `native-windows/` directory:
```cmd
build.bat
```
This invokes `csc.exe` with C# 5 compatibility, compiling `src/**/*.cs` into `..\KerfStudio.exe` and `KerfInstaller.cs` into `..\KerfStudioSetup.exe`.

### Automated Verification Tests
Run the test suite:
```cmd
csc /target:exe /platform:x64 /out:tests\TestRunner.exe /r:System.dll,System.Core.dll,System.Xaml.dll,System.Web.Extensions.dll,PresentationCore.dll,PresentationFramework.dll,WindowsBase.dll src\Model\*.cs src\Engine\*.cs src\Tools\*.cs src\IO\*.cs tests\TestRunner.cs
tests\TestRunner.exe
```

---

## Keyboard Shortcuts
- <kbd>V</kbd>: Select & Transform Tool
- <kbd>H</kbd>: Hand Pan Tool (or hold <kbd>Space</kbd> + Left Drag)
- <kbd>F</kbd>: Frame Tool
- <kbd>R</kbd>: Rectangle Tool
- <kbd>O</kbd>: Ellipse Tool
- <kbd>S</kbd>: Star Tool
- <kbd>P</kbd>: Polygon Tool
- <kbd>B</kbd>: Bézier Pen Tool
- <kbd>T</kbd>: Typography / Text Tool
- <kbd>L</kbd>: Line Tool
- <kbd>A</kbd>: Arrow Tool
- <kbd>Ctrl</kbd> + <kbd>Z</kbd>: Undo
- <kbd>Ctrl</kbd> + <kbd>Y</kbd>: Redo
- <kbd>Ctrl</kbd> + <kbd>S</kbd>: Save Project (.kerf)
- <kbd>Delete</kbd> / <kbd>Backspace</kbd>: Delete Selected Layer(s)
- <kbd>Shift</kbd> (during transform): Constrain Aspect Ratio / Snap Angle
- <kbd>Alt</kbd> (during transform): Scale from Center
