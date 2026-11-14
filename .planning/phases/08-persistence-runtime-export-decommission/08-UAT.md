---
status: testing
phase: 08-persistence-runtime-export-decommission
source:
  - .planning/phases/08-persistence-runtime-export-decommission/08-01-SUMMARY.md
  - .planning/phases/08-persistence-runtime-export-decommission/08-02-SUMMARY.md
  - .planning/phases/08-persistence-runtime-export-decommission/08-03-SUMMARY.md
started: "2026-11-11T15:57:00Z"
updated: "2026-11-11T15:57:00Z"
---

## Current Test

number: 1
name: Project Persistence & Save/Reload (Schema 4.0.0)
expected: |
  Create or open an animated project in Kerf with animation clips, property tracks (Position, Scale, Rotation), and a State Machine with inputs/listeners.
  Save the project (File > Save / Ctrl+S) and reopen it (File > Open).
  Verify that all clips, tracks, keyframe curves, state machine nodes, inputs, and listeners load back completely with zero data loss, and the document is saved with SchemaVersion "4.0.0".
awaiting: user response

## Tests

### 1. Project Persistence & Save/Reload (Schema 4.0.0)
expected: All clips, tracks, keyframe Bézier curves, state machine nodes, transitions, inputs, and listeners survive save and reload with zero data loss under SchemaVersion "4.0.0".
result: pending

### 2. Legacy Migration & Interactive Migration Review Dialog
expected: Opening an older document with missing layer references triggers the modal Migration Review Dialog, clearly listing orphaned tracks/listeners with Keep Inactive, Rebind, and Purge options.
result: pending

### 3. Native .kfx ZIP Container Export & Import
expected: File > "Export Runtime Package (.kfx)..." creates a valid ZIP container containing manifest.json, scene.json, animations.json, state-machines.json, and optional metadata.json. File > "Import Animation Package (.kfx)..." imports the package cleanly into the canvas.
result: pending

### 4. Non-Destructive Render Transforms & Auto Layout Preservation
expected: Playing animations or scrubbing transforms (scale, translation, rotation) on Auto Layout frames or nested children renders smoothly without mutating layer dimensions, padding, or gap.
result: pending

### 5. Dedicated Export Window (.kfx, PNG, JPEG, SVG, PDF, MP4) & Home Page
expected: Clicking the top-right Export button (or File > Export / Ctrl+E) opens the dedicated Export modal with options for .kfx Runtime Package (with optional standalone HTML5 web player runner), PNG, JPEG, SVG, PDF (150/300 DPI vector), and MP4 video. Home Page displays Kerf 3.1 Beta branding and the feature overview.
result: pending

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps

<!-- None identified yet -->

