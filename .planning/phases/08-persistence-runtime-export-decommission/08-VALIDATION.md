---
phase: "8"
slug: "08-persistence-runtime-export-decommission"
status: draft
nyquist_compliant: true
wave_0_complete: false
created: "2026-11-09"
---

# Phase 8 — Validation Strategy

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
- **Before `/gsd-verify-work`:** Full suite must be green (77/77 passing)
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 08-01-01 | 01 | 1 | MIGR-01, MIGR-02, PERSIST-01 | — | N/A (Persistence & Migration Model) | unit/integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ CanonicalDocument.cs | ⬜ pending |
| 08-01-02 | 01 | 1 | MIGR-03 | — | N/A (WPF Migration Review Dialog & Commands) | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ MigrationReviewDialog.cs | ⬜ pending |
| 08-02-01 | 02 | 2 | EXPORT-01 | T-08-01 | Zip-slip path traversal & quota validation | integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ KfxPackage.cs | ⬜ pending |
| 08-02-02 | 02 | 2 | RUNTIME-02 | — | N/A (Headless KerfPlayer & Render Backends) | unit/integration | `cmd /c "cd native-windows && build.bat --test"` | ✅ KerfPlayer.cs | ⬜ pending |
| 08-03-01 | 03 | 3 | DECOM-01 | — | N/A (Legacy Code Removal & Transform Integrity) | regression | `cmd /c "cd native-windows && build.bat --test"` | ✅ PropertyBinder.cs | ⬜ pending |
| 08-03-02 | 03 | 3 | DECOM-01 | — | N/A (Full Suite 77/77 & Milestone Deliverable) | system | `cmd /c "cd native-windows && build.bat --test"` | ✅ TestRunner.cs | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Add `System.IO.Compression.dll` and `System.IO.Compression.FileSystem.dll` references to `native-windows\build.bat`.
- [ ] Add scaffold declarations for Tests #74, #75, #76, and #77 to `native-windows\tests\TestRunner.cs`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Migration Review Dialog UX | MIGR-03 | Interactive modal UI | Open a legacy `.kerf` document with deleted target layers: verify Migration Review dialog appears, lists orphaned items, and executes Keep/Rebind/Purge correctly. |
| Export .kfx from File Menu | EXPORT-01 | Windows Common Dialog | Trigger File > Export Runtime Animation (.kfx): verify save file dialog opens, saves valid .kfx ZIP archive, and shows success message. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 10s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** verified 2026-11-09

