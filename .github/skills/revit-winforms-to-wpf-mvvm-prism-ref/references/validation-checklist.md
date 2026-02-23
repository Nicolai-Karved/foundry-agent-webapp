# Migration Validation Checklist

Use this checklist before considering migration complete.

## Architecture Validation

- [ ] New or updated UI is WPF (not WinForms).
- [ ] View logic is binding/visual only.
- [ ] ViewModel contains UI state + commands only.
- [ ] Business rules and Revit model manipulation live in services.
- [ ] Layer order is preserved: Methods → Classes → Services → Commands → UI.

## Revit Safety Validation

- [ ] Every Revit model write is inside a `Transaction`.
- [ ] API access runs in a valid Revit main-thread/API context.
- [ ] Modeless execution paths use `ExternalEvent` where required.
- [ ] Failure paths roll back safely and surface clear errors.

## Functional Validation

- [ ] Migrated UI reproduces required behaviors from the legacy dialog.
- [ ] Commands trigger expected service operations.
- [ ] No startup performance regressions are introduced.
- [ ] Existing workflows behave consistently after migration.
