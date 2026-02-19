---
description: C# rules (naming, style, nullability, logging)
applyTo: "**/*.cs"
---

# C# Rules

Use these rules for all C# code. For product-specific guidance (e.g., Revit), use Skills instead of adding product rules here.

## Naming Conventions
- **PascalCase**: namespaces, classes, interfaces (`I` prefix), methods, properties, events, enums
- **camelCase**: method parameters
- **Private fields**: `_camelCase`
- No abbreviations or Hungarian notation
- No non-alphanumeric characters except `_` for private fields
- Boolean names must be affirmative
- Collections are plural
- Events use verbs; handlers end with `EventHandler`, args end with `EventArgs`
- Avoid context duplication (`Order.Ship()`, not `ShipOrder()`)

---

## Code Style (.editorconfig)
- Tabs only (no spaces)
- Allman braces
- `else`, `catch`, `finally` on new line
- Max line length: 150 characters
- Explicit types preferred over `var` unless RHS makes type obvious
- Switch statements: indent case labels and contents
- File-scoped namespaces: blank line after declaration
- Using directives: `System.*` first, then alphabetical
- Spacing: no space after casts; space after control flow keywords; no space between method name and `()`
- Modifier order: `private, public, internal, protected, async, static, readonly, override, virtual, abstract, sealed`
- Accessibility modifiers always explicit
- Expression-bodied properties allowed; methods use block bodies
- Readonly fields only when initialized in constructors

---

## Object Calisthenics (Guiding)
1. One level of indentation
2. Prefer guard clauses; `else` allowed when it improves readability
3. Wrap primitives & strings when it improves domain clarity
4. First-class collections
5. One dot per line (Law of Demeter)
6. No abbreviations
7. Keep entities small (50–150 LOC)
8. Max two instance variables
9. Avoid getters/setters in domain entities; DTOs/contracts/config are exempt

---

## Nullability
- Non-nullable = invariants
- Nullable = legitimate absence
- `null!` allowed only for:
  - ORM entities
  - Configuration-bound objects
  - Serializer-controlled lifecycles

---

## Logging (Hard Rule)
- Structured logging only
- Never concatenate strings
- Never log domain objects directly
- Log identifiers only
- CA2254 must be respected
- Never log secrets or PII

---

## Build & Diagnostics
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run` / `dotnet watch`
- EF Core (if used):
  - Add migration: `dotnet ef migrations add <Name>`
  - Update DB: `dotnet ef database update`

### Compiler Diagnostics to Respect
- CA2254 (warning): logging message templates must not vary between calls
- CS8618: non-nullable field initialization
- IDE0290: primary constructor hints disabled

### ReSharper Hints
- Braces required for multiline `for`, `foreach`, `if/else`
- Primary constructor suggestions disabled
- Dictionary lookup simplifications are suggestions only