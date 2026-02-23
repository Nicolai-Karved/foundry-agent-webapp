---
name: dotnet-rc1-upgrade-reference
description: Upgrade existing Microsoft Agent Framework .NET projects to the dotnet-1.0.0-rc1 release candidate with breaking-change checks and verification.
---

# Microsoft Agent Framework .NET RC1 Upgrade Reference

Use this skill when a user asks to upgrade an **existing** Microsoft Agent Framework **.NET/C#** project to:

- https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.0.0-rc1

## Required sources
- Official repo: https://github.com/microsoft/agent-framework
- Latest release pointer (verify before changes): https://github.com/microsoft/agent-framework/releases/latest
- RC1 release notes: https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.0.0-rc1
- Community upgrade patterns (optional): https://github.com/rwjdk/MicrosoftAgentFrameworkSamples

## Upgrade workflow
1. Confirm target: `dotnet-1.0.0-rc1`.
2. Inventory current package versions and hosting/auth patterns.
3. Update MAF-related NuGet packages to RC1-compatible versions.
4. Review RC1 breaking changes and map impacted code paths before refactoring.
5. Adjust agent/provider/workflow/event APIs as needed for RC1 compatibility.
6. Re-run build and tests; resolve compile/runtime regressions.
7. Document migration decisions and residual follow-ups in `implementation-process-log.md`.

## Verification checklist
- [ ] No lingering references to removed or renamed pre-RC1 APIs
- [ ] All MAF package versions are consistent and RC1-compatible
- [ ] Solution builds successfully
- [ ] Relevant tests pass
- [ ] Breaking-change adaptations are documented

## Guardrails
- Prefer minimal, reversible edits.
- Do not pin to speculative versions; use official release references.
- Keep secrets out of code and logs.