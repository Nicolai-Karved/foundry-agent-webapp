---
name: revit-winforms-to-wpf-mvvm-prism-ref
description: Specialized migration guide for converting legacy Revit WinForms UI to WPF + MVVM + Prism with UI-agnostic services and Revit-safe execution.
---

# Revit WinForms to WPF/MVVM/Prism Conversion Reference

Use this skill when modernizing legacy Revit add-ins that currently use WinForms dialogs or code-behind-heavy UI patterns.

## Scope

This skill is for migration and modernization tasks where you need to:
- Analyze WinForms screens and event handlers
- Extract embedded business logic and Revit API manipulation from UI classes
- Move business logic into UI-agnostic service classes
- Rebuild the UI in WPF XAML using MVVM with Prism
- Preserve Revit safety constraints (main-thread API context and transaction safety)

## Required User Interaction

Before generating the new WPF dialog, ask the user for a layout source:
- Preferred: a layout file (for example, design spec, wireframe export, or structured layout notes)
- Alternative: a screenshot or image of the intended dialog

If the user does not have a layout file or image:
- Offer to proceed using the existing WinForms dialog as the baseline
- Recreate the layout and interaction flow as closely as possible in WPF
- Note any areas where exact visual parity is not feasible and document the approximation

## Conversation Template (Deterministic)

Use this wording pattern before generating the WPF View:

1. **Ask for layout input**
	- "Please share a layout file or image for the new dialog (wireframe, mockup, screenshot, or layout notes)."
2. **If provided**
	- "Great, I will use that as the primary layout blueprint and map it to WPF/MVVM/Prism."
3. **If not provided**
	- "No problem—I can proceed by aligning the new WPF dialog as closely as possible to the current WinForms layout."
	- "I will also call out any places where exact visual parity is not feasible and document the approximation."

Record the selected path in the migration notes as one of:
- `LayoutSource: ProvidedAsset`
- `LayoutSource: WinFormsAlignedFallback`

## Architectural Rules (Mandatory)

1. New or updated UI must use **WPF + MVVM + Prism**.
2. Do not introduce new WinForms components.
3. Business rules and model manipulation must live in **Service** classes (UI-agnostic).
4. All Revit model writes must be wrapped in a **Transaction**.
5. Revit API calls must run in a valid **Revit main-thread/API context**.
6. Organize implementation bottom-up: **Methods → Classes → Services → Commands → UI**.

## Content Map

| Reference File | When to Read |
|----------------|--------------|
| references/migration-playbook.md | End-to-end migration sequence and planning |
| references/winforms-to-prism-mapping.md | Mapping WinForms controls/events to WPF + Prism patterns |
| references/service-boundary-rules.md | Extracting and shaping service/model boundaries |
| references/threading-and-external-event.md | Main-thread safety and modeless UI marshaling with ExternalEvent |
| references/validation-checklist.md | Regression and architecture validation after migration |

## Trigger Phrases

This skill should activate when requests mention phrases such as:
- "convert WinForms to WPF"
- "migrate Revit dialog to Prism"
- "move logic out of WinForms"
- "modernize Revit UI to MVVM"
- "replace WinForms event handlers with commands"

## Output Expectations

A successful migration should produce:
- WPF View(s) in XAML
- ViewModel(s) with Prism command bindings
- Service classes containing business logic and Revit operations
- Clear command-to-service orchestration
- Revit-safe execution points for all API access
- A layout decision record: provided design asset or WinForms-aligned fallback
