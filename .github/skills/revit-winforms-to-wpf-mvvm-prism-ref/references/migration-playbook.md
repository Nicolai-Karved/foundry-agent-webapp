# Migration Playbook: WinForms to WPF/MVVM/Prism

Use this playbook to migrate legacy WinForms-based Revit features in safe, incremental steps.

## Phase 0: Collect Layout Input (Required)

1. Ask the user for a layout file or image for the new dialog.
2. If provided, use it as the primary UI blueprint.
3. If not provided, offer to proceed and align the new WPF dialog as closely as possible with the existing WinForms layout.
4. Record whether the migration follows:
   - explicit design input, or
   - WinForms-aligned fallback.

## Phase 1: Analyze Existing WinForms Surface

1. Inventory all forms, controls, and event handlers.
2. Identify each event handler that contains:
   - business rules,
   - Revit API calls,
   - data transformation.
3. Mark API write operations that require transactions.
4. Mark modeless UI flows that require ExternalEvent/API-context marshaling.

## Phase 2: Extract Logic into Services

1. Create service interfaces and implementations in a non-UI project/layer.
2. Move business rules from form code-behind/event handlers into service methods.
3. Move Revit model manipulation from UI classes into service methods.
4. Keep services UI-agnostic (no Window, UserControl, MessageBox dependencies).
5. Keep method responsibilities small and composable (Methods → Classes).

## Phase 3: Rebuild UI in WPF

1. Create XAML-based View(s) that replicate required behavior.
2. Keep visual concerns in View only (layout, styling, bindings).
3. Avoid business logic in code-behind.

## Phase 4: Implement ViewModel with Prism

1. Add ViewModel classes inheriting from Prism base patterns.
2. Model UI state as bindable properties.
3. Replace WinForms click handlers with Prism commands.
4. Inject services through constructor injection.
5. Route actions ViewModel → service methods.

## Phase 5: Revit Safety Wiring

1. Ensure all Revit model writes are wrapped in `Transaction`.
2. Ensure API access occurs in a valid Revit API context (main thread).
3. For modeless workflows, marshal execution via `ExternalEvent`.
4. Roll back on failure and surface user-safe errors.

## Phase 6: Verify and Stabilize

1. Compare old and new feature behavior.
2. Validate startup behavior has no regressions.
3. Validate command responsiveness and model integrity.
4. Confirm architecture: Methods → Classes → Services → Commands → UI.
