# Revit Threading and ExternalEvent Safety

Use this reference for safe Revit API access in migrated UI flows.

## Revit API Context Rules

1. All Revit API access must execute in a valid Revit API context on the main thread.
2. If UI is modeless, marshal Revit operations via `ExternalEvent`.
3. Never execute document mutations from background threads.

## Write Safety Rules

1. Wrap all model modifications in `Transaction`.
2. Roll back on failure.
3. Use user confirmation for destructive operations.

## Migration Checks

When migrating from WinForms, verify that:
- event handlers no longer call Revit API directly from arbitrary UI threads,
- command flows are routed to Revit-safe execution points,
- transaction boundaries are explicit and minimal.
