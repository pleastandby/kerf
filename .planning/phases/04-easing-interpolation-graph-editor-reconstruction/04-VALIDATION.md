---
phase: "4"
slug: "04-easing-interpolation-graph-editor-reconstruction"
status: draft
nyquist_compliant: true
wave_0_complete: false
created: "2026-11-06"
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Standalone Automated Verification Test Suite (`native-windows\tests\TestRunner.cs`) |
| **Config file** | `native-windows\build.bat` |
| **Quick run command** | `cmd /c "cd native-windows && build.bat --test"` |
| **Full suite command** | `cmd /c "cd native-windows && build.bat --test"` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `cmd /c "cd native-windows && build.bat --test"`
- **After every plan wave:** Run `cmd /c "cd native-windows && build.bat --test"`
- **Before `/gsd-verify-work`:** Full suite must be green (56/56 passing)
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | EASE-01 | — | N/A (Pure Math Engine) | unit | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |
| 04-01-02 | 01 | 1 | EASE-01 | — | N/A (Closed-form ODE) | unit | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |
| 04-01-03 | 01 | 1 | EASE-04 | — | N/A (Track.Sample dispatch) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |
| 04-02-01 | 02 | 2 | EASE-02 | — | N/A (UI Layout / Splitter) | unit/UI | `cmd /c "cd native-windows && build.bat --test"` | ✅ StudioView.cs | ⬜ pending |
| 04-02-02 | 02 | 2 | EASE-03 | — | N/A (Handles / Tangents) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ StudioView.cs | ⬜ pending |
| 04-02-03 | 02 | 2 | EASE-01, EASE-04 | — | N/A (Preset Grid Picker) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ StudioView.cs | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Add `Test #56: Easing Engine, Closed-Form Harmonic Springs & Graph Editor Reconstruction` stubs to `native-windows\tests\TestRunner.cs`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Visual smoothness of graph curves and handle drag interaction | EASE-02, EASE-03 | WPF interactive GUI event loop & pointer capture | Launch `native-windows\bin\KerfStudio.exe`, enter Animate Mode, drag tangent handles, verify real-time curve redraw and canvas pose update. |
| Popover preset grid picker UX | EASE-01 | Interactive visual layout inspection | Click the easing preset button, verify 21 miniature curve thumbnails appear and clicking one updates curve. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 10s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** verified 2026-11-06

