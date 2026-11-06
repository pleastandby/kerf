---
phase: "5"
slug: "05-animation-command-history-full-undo-redo"
status: draft
nyquist_compliant: true
wave_0_complete: false
created: "2026-11-06"
---

# Phase 5 — Validation Strategy

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
- **Before `/gsd-verify-work`:** Full suite must be green (59/59 passing)
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | HIST-01, HIST-03 | — | N/A (Atomic Command Architecture) | unit | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |
| 05-01-02 | 01 | 1 | HIST-02 | — | N/A (Curve & Easing Commands) | unit | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |
| 05-01-03 | 01 | 1 | HIST-01, HIST-04 | — | N/A (Composite & Multi-Keyframe Operations) | unit/integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |
| 05-02-01 | 02 | 2 | HIST-04 | — | N/A (ScrubControls & Inspector Drag Batching) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ StudioView.cs | ⬜ pending |
| 05-02-02 | 02 | 2 | HIST-01, HIST-02, HIST-04 | — | N/A (Timeline & Graph Editor Pointer Commit) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ StudioView.cs | ⬜ pending |
| 05-02-03 | 02 | 2 | HIST-01, HIST-02, HIST-03, HIST-04 | — | N/A (Cross-Mode & Runtime Isolation Harness) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Add `Test #59: Animation Command History, Full Undo/Redo & Runtime Isolation` stubs to `native-windows\tests\TestRunner.cs`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real-time visual fluidity during scrub dragging followed by discrete undo | HIST-04 | WPF interactive GUI event loop & pointer capture | Launch `KerfStudio.exe`, scrub a property field (e.g. Width) smoothly in Animate Mode, release mouse. Verify single `Ctrl+Z` reverts entire scrub. |
| In-place entity undo across mode tabs | HIST-01 | UI Tab state transition inspection | Make an edit in Design Mode, switch to Animate Mode, press `Ctrl+Z`. Verify base layer geometry restores and rest pose updates without tab jumping. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 10s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** verified 2026-11-06
