---
phase: "6"
slug: "06-interactive-state-machine-engine"
status: draft
nyquist_compliant: true
wave_0_complete: false
created: "2026-11-07"
---

# Phase 6 — Validation Strategy

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
- **Before `/gsd-verify-work`:** Full suite must be green (60/60 passing)
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 06-01-01 | 01 | 1 | STATE-01, STATE-02, STATE-03 | — | N/A (Data Model & Index Adapters) | unit | `cmd /c "cd native-windows && build.bat --test"` | ✅ StateMachineTypes.cs | ⬜ pending |
| 06-01-02 | 01 | 1 | STATE-01, STATE-02, STATE-03 | — | N/A (Validator & Test Scaffold) | unit | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |
| 06-02-01 | 02 | 2 | STATE-01, STATE-02 | — | N/A (Node & Transition Commands) | unit/integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ CommandHistory.cs | ⬜ pending |
| 06-02-02 | 02 | 2 | STATE-03 | — | N/A (Input & Condition Commands) | unit/integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ CommandHistory.cs | ⬜ pending |
| 06-03-01 | 03 | 3 | STATE-06, STATE-03 | — | N/A (3-Column Dock & Inputs Panel) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ StudioView.cs | ⬜ pending |
| 06-03-02 | 03 | 3 | STATE-06, STATE-01, STATE-02 | — | N/A (Canvas, Wires & Drag Batching) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ StudioView.cs | ⬜ pending |
| 06-03-03 | 03 | 3 | STATE-06, STATE-02, STATE-03 | — | N/A (Inspectors & Test #60 Harness) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Add `Test #60: Interactive State Machine Engine` scaffold to `native-windows\tests\TestRunner.cs`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Visual fluid drag-to-connect dashed wire | STATE-06 | WPF mouse capture & live rendering | In State Machine mode, drag from an output port: verify dashed Bézier wire tracks mouse smoothly and snaps/creates transition on valid target node. |
| Marquee box multi-selection | STATE-06 | WPF interactive canvas input | Drag on empty state machine canvas: verify semi-transparent selection box expands and highlights enclosed nodes. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 10s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** verified 2026-11-07

