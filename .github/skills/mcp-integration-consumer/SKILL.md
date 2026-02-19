---
name: mcp-integration-consumer
description: Use when integrating MCP tools with Copilot, selecting external tools for agentic workflows, or scoping tool access.
---

# MCP Integration (Consumer)

## Purpose

Extend Copilot with MCP tools while keeping scope, permissions, and outputs safe and auditable.

## When to Use This Skill

- Adding MCP tools to Copilot agent mode
- Selecting tools for a workflow that needs external data or services
- Scoping tool access for safety and repeatability

---

## Workflow

1. **Define the need**: Identify the missing capability (data, automation, analysis).
2. **Choose tools**: Prefer precise, task-focused tools over broad, risky ones.
3. **Scope permissions**: Default to read-only, then expand only if required.
4. **Validate outputs**: Require reviewable artifacts (PRs, issues, reports).
5. **Document usage**: Record tool scope and assumptions for later audits.

---

## Guardrails

- Use least-privilege access and explicit tool allowlists.
- Avoid open-ended tools for workflows with sensitive data.
- Keep a human review step before any write operations.

---

## Prompt Patterns

- "Add MCP tools needed to query GitHub issues and summarize trends. Use read-only access."
- "Limit tool access to this repo and provide output as a draft PR."
- "Use MCP tools to collect data, then summarize findings without making changes."
