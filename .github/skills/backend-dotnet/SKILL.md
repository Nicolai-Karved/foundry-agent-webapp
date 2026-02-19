---
name: backend-dotnet
description: Build, test, and validate backend changes in Source/** with dotnet tooling and repo rules.
---
# Backend (.NET) Workflow Skill

Use this skill when you modify or review code under `Source/**`.

## Commands
- Build: `dotnet build`
- Test: `dotnet test`

## Workflow
1. Identify the affected projects in `Source/`.
2. Make minimal, local changes; preserve architecture boundaries (Domain/Application/Infrastructure).
3. Validate naming, indentation (tabs), and logging rules.
4. Run `dotnet build` and `dotnet test`.
5. Document any deviations explicitly.

## Revit Add-in Notes (if applicable)
- Avoid HTTP calls or credential manager access during startup
- Defer heavy initialization to first use or after the UI is visible
- Do not create `ExternalEvent` in constructors

## Guardrails
- Never introduce framework/ORM dependencies into the Domain layer.
- Use structured logging; never concatenate strings or log secrets.
- Respect configured warning suppressions.
