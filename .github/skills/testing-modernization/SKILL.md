---
name: testing-modernization
description: Use when assessing test gaps, modernizing test strategy, or adding contract tests, integration tests, and domain-layer tests.
---

# Testing Modernization

## Purpose

Modernize test strategy by identifying gaps and layering tests for stability and coverage.

## When to Use This Skill

- You need to assess a test suite for systemic gaps
- You are adding features that impact multiple modules
- You are refactoring with risk of behavioral regressions

---

## Workflow

1. **Assess**: Inventory current tests and identify missing layers (contract, integration, domain).
2. **Prioritize**: Start with contract tests for repositories and service boundaries.
3. **Add targeted integration tests**: Focus on highest-risk flows and interfaces.
4. **Add domain tests**: Validate invariants and business rules.
5. **Document coverage**: Summarize gaps closed and remaining risks.

---

## Guardrails

- Avoid writing only unit tests when cross-module behavior changes.
- Keep tests deterministic and small; avoid fragile end-to-end setups unless necessary.
- Align tests with the architecture boundaries you defined.

---

## Prompt Patterns

- "Analyze the current test suite and identify systemic gaps. Recommend a modernization plan."
- "Add contract tests for the repository interface and update integration tests for the service boundary."
- "List the highest-risk flows and add integration tests for those only."
