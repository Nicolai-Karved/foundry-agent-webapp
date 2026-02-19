---
name: copilot-cli-workflows
description: Use when working with GitHub Copilot CLI, agentic terminal workflows, delegation, delegated tasks, or headless automation.
---

# Copilot CLI Workflows

## Purpose

Use Copilot CLI to accelerate terminal-based workflows while keeping command execution safe and reviewable.

## When to Use This Skill

- Setting up or debugging projects from the terminal
- Delegating tasks using the coding agent
- Automating repeatable tasks in scripts

---

## Workflow

1. **Start small**: Ask for setup or diagnosis in plain language.
2. **Review commands**: Approve only commands you understand.
3. **Delegate carefully**: Use `/delegate` for background work and review the resulting PR.
4. **Use agents for checks**: `/agent` to select a custom agent for specialized reviews.
5. **Iterate with constraints**: Narrow scope and tool access for repeatability.

---

## Headless and Automation

- Prefer scoped permissions and allowlists for tools and paths.
- Avoid `--allow-all-tools` unless running in a disposable environment.
- Use headless `-p` mode only with explicit constraints and clear outputs.

---

## Guardrails

- Do not run destructive commands without explicit user confirmation.
- Keep a human review step before pushing or merging.
- Use output artifacts (PRs, summaries) as checkpoints.

---

## Prompt Patterns

- "Clone the repo and set it up to run."
- "What is using port 3000?"
- "Find and kill the process on port 3000."
- "/delegate Fix issue #123 and open a PR with tests updated."
