# Kerf Native Windows Studio Engine

## Overview
**Kerf Studio** is a standalone native Windows desktop vector design studio compiled to a single 64-bit Windows executable (`KerfStudio.exe`).

Unlike web browser wrappers, Kerf Native Studio runs directly on the Windows operating system and bypasses all browser sandboxes, DOM trees, and Web APIs. It interfaces directly with the Windows graphics driver through **hardware-accelerated DirectX / Direct2D and DirectWrite**, utilizing the dedicated GPU and CPU SIMD resources.

---

## Technical Stack & Architecture

Inspired by graphics-intensive suites like **Adobe After Effects**, **Adobe Illustrator**, and **Figma Desktop**:

```
┌────────────────────────────────────────────────────────┐
│                      KerfStudio.exe                    │
│            Native 64-bit Standalone Windows PE         │
├────────────────────────────────────────────────────────┤
│  Win32 Message Loop & High-DPI Raw Input Subsystem     │
│  (Window Procedure, Subpixel Pointer & Wheel Events)   │
├────────────────────────────────────────────────────────┤
│           DirectX Hardware Rendering Pipeline          │
│  • GPU Geometry & Quad Tessellation                    │
│  • DirectWrite Subpixel Font Glyph Rendering           │
│  • Hardware Bicubic Scaling & Anti-Aliased Rasterizer  │
│  • Real-Time Telemetry & Draw Call Batch Monitor       │
├────────────────────────────────────────────────────────┤
│               Core Design & Layout Engine              │
│  • Infinite Canvas (Exponential Zoom & Pan Engine)     │
│  • Figma-Grade Auto Layout (Row, Column, Spacing, Pad) │
│  • Shape Generators (Rect, Ellipse, Star, Poly, Frame) │
│  • Real-Time Property Inspector & Color Converter      │
│  • Live CSS Code Generator & Inspector                 │
├────────────────────────────────────────────────────────┤
│             Windows OS Native File Subsystem           │
│  • Native Win32 File Dialogs (Open, Save As)           │
│  • Native .kerf Project Persistence                    │
│  • Hardware RenderTargetBitmap High-Res PNG Export     │
│  • Standard SVG Vector Markup Export                   │
└────────────────────────────────────────────────────────┘
```

---

## Features

1. **Hardware-Accelerated Infinite Canvas**:
   - Smooth panning (<kbd>Space</kbd> + Left Drag or Middle Mouse Drag).
   - Cursor-centered exponential zooming (<kbd>Ctrl</kbd> + Mouse Wheel, 10% to 3200%).
   - Hardware-rendered infinite dot alignment grid.

2. **Vector Creation Tools**:
   - **Select** (<kbd>V</kbd>) & **Hand** (<kbd>H</kbd>)
   - **Frame / Artboard** (<kbd>F</kbd>)
   - **Rectangle** (<kbd>R</kbd>) & **Ellipse** (<kbd>O</kbd>)
   - **Star** (<kbd>S</kbd>) & **Polygon / Triangle** (<kbd>P</kbd>)
   - **Typography / Text** (<kbd>T</kbd>)
   - **Line / Vector Path** (<kbd>L</kbd>)

3. **Figma Auto Layout**:
   - Direction: Horizontal Row (`->`) and Vertical Column (`↓`)
   - Item spacing (gap) & 4-side padding
   - Automatic frame resizing ("Hug Contents")

4. **Inspector & Code Generation**:
   - Geometry (X, Y, Width, Height, Corner Radius, Opacity, Stroke Width)
   - Real-time RGB / Hex color picker
   - Live CSS Flexbox inspector

5. **Standalone Distribution**:
   - Single portable executable: `KerfStudio.exe` (no installers, runtimes, or browser dependencies required).
