---
phase: 08-persistence-runtime-export-decommission
plan: 01
subsystem: persistence-animation
tags: [schema-v4, persistence, migrations, fnv1a-auto-heal, orphan-resilience, wpf-migration-dialog]
requires:
  - phase: 07-polish-verification-hardening
    provides: interactive state machine, listeners, event dispatcher
provides:
  - schema v4.0.0 project persistence pipeline
  - transparent legacy v1-v3 document upgrade
  - FNV-1a hash to string GUID auto-healing
  - orphaned reference deactivation with non-destructive evaluation guards
  - MigrationReport telemetry and interactive WPF MigrationReviewDialog
  - transactional undo/redo resolution command in CommandHistory
affects:
  - 08-02-PLAN.md (runtime export and decommissioning)
actuals:
  tokens: 12500
  tasks: 2
  commits: 2
  plan_head_before: b9c453a603a7ac584f7ee03164ac60c93432ff11
  plan_head_after: 4367c94f1c9535eb0e94bb545934fa769eb070db
tech-stack:
  added: []
  patterns:
    - non-destructive evaluation deactivation (IsActive = false)
    - reverse FNV-1a hash map resolution to canonical layer GUID
    - transactional document state snapshot restore for orphan reconciliation
key-files:
  created:
    - native-windows/src/Model/MigrationReport.cs
    - native-windows/src/UI/Dialogs/MigrationReviewDialog.cs
  modified:
    - native-windows/src/Model/CanonicalDocument.cs
    - native-windows/src/Engine/Animation/AnimationTypes.cs
    - native-windows/src/Engine/Animation/StateMachineTypes.cs
    - native-windows/src/Engine/Animation/StateMachineRuntime.cs
    - native-windows/src/Model/DocumentMigrations.cs
    - native-windows/src/IO/StorageManager.cs
    - native-windows/src/UI/StudioView.cs
    - native-windows/src/Engine/CommandHistory.cs
    - native-windows/tests/TestRunner.cs
key-decisions:
  - "Default KerfDocument schema version upgraded to 4.0.0; DocumentMigrations provides automated upgrade with reverse FNV-1a hash auto-healing"
  - "Orphaned animation tracks and state machine listeners default to IsActive = false, preventing runtime crashes while preserving authoring data"
  - "Interactive WPF MigrationReviewDialog empowers users to Keep, Rebind, or Purge unresolved items with full CommandHistory undo/redo support"
requirements-completed: [MIGR-01, MIGR-02, MIGR-03, PERSIST-01]
coverage:
  - id: D1
    description: "Schema v4.0.0 project persistence, transparent legacy v1-v3 upgrade, and FNV-1a auto-healing"
    requirement: MIGR-01
    verification:
      - kind: unit
        ref: "native-windows/tests/TestRunner.cs#TestSchemaV4PersistenceAndOrphanMigration"
        status: pass
    human_judgment: false
  - id: D2
    description: "Orphaned track and listener deactivation resilience without runtime evaluation exceptions"
    requirement: MIGR-03
    verification:
      - kind: unit
        ref: "native-windows/tests/TestRunner.cs#TestSchemaV4PersistenceAndOrphanMigration"
        status: pass
    human_judgment: false
  - id: D3
    description: "Interactive WPF MigrationReviewDialog and transactional Keep/Rebind/Purge workflows"
    requirement: PERSIST-01
    verification:
      - kind: unit
        ref: "native-windows/tests/TestRunner.cs#TestSchemaV4PersistenceAndOrphanMigration"
        status: pass
    human_judgment: false
duration: 25min
completed: 2026-11-11
status: complete
---

# Phase 08 Plan 01: Persistence, GUID Auto-Healing & Orphan Resilience Summary

**Implemented schema v4.0.0 persistence, transparent legacy v1-v3 document upgrade, reverse FNV-1a hash-to-GUID auto-healing, non-destructive orphaned reference deactivation (`IsActive = false`), MigrationReport telemetry, and the interactive WPF MigrationReviewDialog with transactional Keep/Rebind/Purge undo/redo.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-11-11T08:18:22Z
- **Completed:** 2026-11-11T08:43:00Z
- **Tasks:** 2 completed
- **Files modified:** 11 files (2 created, 9 modified)

## Accomplishments

- **Schema v4.0.0 Project Persistence (PERSIST-01, MIGR-01):** Updated `KerfDocument` constructor default to schema `"4.0.0"`. `DocumentMigrations.LoadAndMigrate` supports direct deserialization of schema v3/v4 documents and transparent dictionary-based upgrades from legacy v1 and v2 documents.
- **Stable GUID Auto-Healing (MIGR-02):** Replaced legacy reliance on 32-bit FNV-1a numeric hashes with stable string GUIDs on `Track.Id` and `NodeListener.TargetLayerId`. Implemented `HealAndValidateReferences` in `DocumentMigrations` to map legacy integer hashes back to matching layer GUIDs.
- **Orphan Reference Resilience (MIGR-03):** Missing or deleted layer targets are preserved safely with `IsActive = false`. Guarded `Clip.SampleToPose` and `StateMachineRuntime.OnPointerEvent` to prevent null dereferences or incorrect pose evaluation when targets are missing.
- **Interactive Migration Review Dialog (D-03):** Implemented `MigrationReviewDialog.cs` in WPF featuring brand styling, migration metrics summary badges, and per-item resolution selectors (Keep Inactive, Rebind to Layer..., Purge). Wired modal trigger into `StudioView.cs` project loading.
- **Transactional Undo/Redo (CommandHistory):** Created `ResolveOrphanedReferencesCommand` in `CommandHistory.cs` to enable full undo and redo of orphan rebinding and purging actions.
- **Automated Verification:** Added Test #74 (`TestSchemaV4PersistenceAndOrphanMigration`) in `TestRunner.cs`, asserting 100% test pass rate across all 74 unit and integration tests.

## Task Commits

1. **Task 1: End-to-end schema v4.0.0 persistence, GUID auto-healing, and orphaned reference resilience slice** - `3580433` (feat)
2. **Task 2: Interactive WPF MigrationReviewDialog and StudioView resolution workflow** - `4367c94` (feat)

## Files Created/Modified

- `native-windows/src/Model/CanonicalDocument.cs` - Updated `KerfDocument` default `SchemaVersion` to `"4.0.0"`.
- `native-windows/src/Engine/Animation/AnimationTypes.cs` - Added `Track.Id` (GUID) and `Track.IsActive` (boolean guard); guarded `Clip.SampleToPose`; ensured default `StateMachine` initialization.
- `native-windows/src/Engine/Animation/StateMachineTypes.cs` - Added `NodeListener.TargetLayerId` and `NodeListener.IsActive` with cloning preservation.
- `native-windows/src/Engine/Animation/StateMachineRuntime.cs` - Guarded `OnPointerEvent` and auto-unhover dispatch against inactive listeners.
- `native-windows/src/Model/MigrationReport.cs` - Created data models: `OrphanKind`, `OrphanedReferenceItem`, and `MigrationReport`.
- `native-windows/src/Model/DocumentMigrations.cs` - Implemented `CurrentSchemaVersion = "4.0.0"`, `LoadAndMigrate` telemetry, and `HealAndValidateReferences` FNV-1a auto-healing and orphan detection.
- `native-windows/src/IO/StorageManager.cs` - Added `LoadDocumentWithReport(string, out MigrationReport)`.
- `native-windows/src/UI/Dialogs/MigrationReviewDialog.cs` - Created WPF modal review window with per-item Keep, Rebind, and Purge actions.
- `native-windows/src/UI/StudioView.cs` - Integrated modal migration review on project load and diagnostic logging.
- `native-windows/src/Engine/CommandHistory.cs` - Implemented `ResolveOrphanedReferencesCommand` for transactional undo/redo.
- `native-windows/tests/TestRunner.cs` - Added Test #74 covering v4.0.0 persistence, auto-healing, evaluation safety, and resolution undo/redo.

## Decisions Made

- Upgraded default schema version to 4.0.0 while preserving backwards compatibility for all legacy files through `DocumentMigrations`.
- Defaulted unresolved animation tracks and listeners to `IsActive = false` rather than silently deleting them, ensuring zero data loss on load while preventing evaluation crashes.
- Backed the interactive review dialog with `ResolveOrphanedReferencesCommand` in `CommandHistory`, enabling non-destructive experimentation and safe undo/redo of rebinding choices.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Keyframe constructor parameter mismatch in TestRunner**
- **Found during:** Task 1 test compilation
- **Issue:** Test keyframe instances were created with two arguments `(time, val)`, which is not supported by `Keyframe(double, double, InterpMode, ...)`.
- **Fix:** Specified `InterpMode.Linear` in test keyframe initializations.
- **Files modified:** `native-windows/tests/TestRunner.cs`
- **Committed in:** `3580433`

**2. [Rule 2 - Auto-add missing critical functionality] Ensure StateMachine default initialization in AnimationProjectData**
- **Found during:** Task 1 test execution
- **Issue:** `AnimationProjectData.StateMachine` was previously initialized to `null`, requiring repeated defensive checks in `StudioView` and causing null reference exceptions when loading clean projects without state machine blocks.
- **Fix:** Initialized `StateMachine = new StateMachineDefinition()` in `AnimationProjectData` constructor and added defensive checks in `DocumentMigrations.HealAndValidateReferences`.
- **Files modified:** `native-windows/src/Engine/Animation/AnimationTypes.cs`, `native-windows/src/Model/DocumentMigrations.cs`
- **Committed in:** `3580433`

## Self-Check: PASSED

1. Created files exist:
   - `native-windows/src/Model/MigrationReport.cs`: FOUND
   - `native-windows/src/UI/Dialogs/MigrationReviewDialog.cs`: FOUND
   - `.planning/phases/08-persistence-runtime-export-decommission/08-01-SUMMARY.md`: FOUND
2. Commits exist:
   - `3580433`: FOUND
   - `4367c94`: FOUND
3. Automated test suite verification:
   - 74 tests passed, 0 failed.
