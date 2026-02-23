# WinForms to WPF/Prism Mapping

Use this reference to translate legacy UI patterns into WPF/MVVM/Prism patterns.

## Control and Pattern Mapping

| WinForms Pattern | WPF/Prism Equivalent |
|------------------|----------------------|
| `Form` | `Window` or Prism dialog/view |
| `UserControl` | WPF `UserControl` with binding |
| Click event handler in code-behind | `DelegateCommand` in ViewModel |
| Direct control property mutation | Two-way binding to ViewModel property |
| `MessageBox.Show` for domain decisions | Decision in service, UI message via interaction service |
| Global static state in form | Injected state/service model |

## Event Handler Replacement

- Replace imperative UI event logic with command bindings.
- Keep command handlers thin; delegate business logic to services.
- Keep validation rules centralized in services or validation layer.

## Prism Requirements

- Use constructor injection for dependencies.
- Use Prism command patterns for user actions.
- Use Prism region/navigation patterns when UI composition is modular.
- Keep ViewModel testable without Revit UI runtime dependencies.
