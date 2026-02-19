---
name: agentic-workflows
description: Use for agent mode, architecture-aware planning, impact mapping, multi-step refactor work, safe migrations, and coordinated multi-file changes.
---

# Agentic Workflows

## Purpose

Guide structured, architecture-aware agentic work that spans multiple files and steps.

## When to Use This Skill

- Decomposing a system into layers or modules
- Planning multi-step changes or refactors
- Coordinating schema changes, migrations, and rollout steps
- Updating tests and documentation alongside feature work

---

## Workflow

1. **Map impact first**: Identify affected layers, modules, data access, tests, and docs.
2. **Define boundaries**: Clarify domain, interface, and infrastructure responsibilities.
3. **Plan in increments**: Break work into reversible steps with checkpoints.
4. **Execute selectively**: Implement only the next planned slice, then re-evaluate.
5. **Validate**: Run tests or targeted checks after each meaningful change.

---

## Guardrails

- Avoid sweeping rewrites across many files at once.
- Preserve existing behavior unless explicitly told to change it.
- Call out assumptions and risks before executing a risky step.
- Keep migrations backward compatible unless explicitly instructed otherwise.

---

## Prompt Patterns

- "Analyze this service and propose a modular decomposition with domain, interface, and infrastructure layers. Identify coupling risks."
- "Create a step-by-step refactor plan to extract validation into a domain service. List affected files and tests."
- "Propose an additive, backward-compatible migration and a rollback plan."
- "Execute steps 1-2 only, then stop and summarize risks before continuing."
