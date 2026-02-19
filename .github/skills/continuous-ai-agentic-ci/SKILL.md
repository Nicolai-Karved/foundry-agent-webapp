---
name: continuous-ai-agentic-ci
description: Use when designing agentic CI workflows, natural-language rules, safe outputs, and reviewable automation.
---

# Continuous AI / Agentic CI

## Purpose

Define small, safe, reasoning-heavy automations that run continuously and produce reviewable artifacts.

## When to Use This Skill

- Creating agentic CI rules for docs, tests, or regressions
- Automating recurring reasoning tasks (drift detection, summaries)
- Designing safe outputs and permissions for automation

---

## Workflow

1. **Pick one narrow task**: Start with a single, high-value check.
2. **Write a clear rule**: Express intent in natural language with constraints.
3. **Set safe outputs**: Limit to PRs, issues, or reports.
4. **Default to read-only**: Expand permissions only when required.
5. **Review on a schedule**: Treat outputs like PRs from a teammate.

---

## Safe Outputs

- Outputs must be deterministic and reviewable.
- Prefer PRs for code changes and issues for analysis.
- Never auto-merge; keep humans in the loop.

---

## Guardrails

- Avoid broad, ambiguous rules that can drift in meaning.
- Keep scope small and iterate over time.
- Log assumptions and constraints in the rule text.

---

## Example Rules

- "Detect docstring drift and open a PR with suggested fixes."
- "Summarize weekly activity and open an issue with trends and risks."
- "Flag performance regressions in critical paths with evidence."
