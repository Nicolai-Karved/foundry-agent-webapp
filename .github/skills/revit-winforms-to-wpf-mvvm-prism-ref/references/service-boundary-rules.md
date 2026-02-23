# Service Boundary Rules

Use this reference to enforce separation between UI, business logic, and Revit model operations.

## Mandatory Boundaries

- UI layer (View + ViewModel) may orchestrate flow but must not contain business rules.
- Revit model operations must be encapsulated in service classes.
- Service classes must be UI-agnostic.
- ViewModel classes should not directly manipulate Revit model objects.

## Layering Order

Implement bottom-up in this order:
1. Methods
2. Classes
3. Services
4. Commands
5. UI

## Service Design Guidance

- Keep service contracts focused and cohesive.
- Separate pure business rules from infrastructure concerns when feasible.
- Validate inputs before API operations.
- Return clear result models for UI-friendly status handling.

## Anti-Patterns to Remove During Migration

- Revit API calls inside WinForms/WPF code-behind.
- Business calculations inside click handlers.
- Shared mutable static UI state driving business behavior.
- Tight coupling between dialog controls and document mutation logic.
