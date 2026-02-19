# .github/copilot-instructions.md
## Copilot Instructions (Global)

These instructions are the **global source of guidance** for IDE/agent-assisted changes in this repository.
Path-scoped rules in .github/instructions override this file for their targets.

---

## Precedence Order
When rules conflict, follow this order:

**Architecture → Correctness → Clarity → Rules**

---

## 1) Trust These Instructions
These instructions contain essential information to work efficiently in this codebase.  
**Trust this guidance first** and only search for additional information if these instructions are incomplete or incorrect.

---

## 2) Rule Philosophy

> Object Calisthenics rules are **guiding principles**, not hard constraints.  
> Deviations are acceptable in infrastructure, orchestration, and integration layers where clarity, framework constraints, or operational concerns take precedence.

The intent is to improve maintainability, readability, testability, and operational safety—not to enforce artificial compliance.

---

## 3) Copilot House Style (Mandatory)

The following rules define how Copilot and other IDE/agent-assisted tools must operate in this repository.  
These rules are mandatory and override local workflow conventions unless explicitly stated otherwise.

Adopt the following operating principles for all tasks in this workspace. These rules are mandatory and must always be enforced.

### A. Workflow Discipline (Always Enforced)

1. **Read Existing Patterns First**  
   Study the current codebase, conventions, and architectural patterns before making changes.

2. **Impact Mapping and Planning**  
   Always map impact and dependencies before updating existing code or adding new features.  
   For non-trivial, high-impact, or multi-file work, produce a short multi-step plan before making changes. For routine, low-risk, local edits, perform a lightweight impact check and proceed.

3. **Decision Threshold**  
   If no significant impact or dependencies are detected, proceed with the update.  
   If impact is non-trivial, prefer incremental and reversible changes.

4. **Minimal, Local Changes**  
   Make the smallest possible change scoped to the local area.  
   Avoid unnecessary refactors or stylistic rewrites.

5. **Validate After Each Edit**  
   Validate logic, behavior, and assumptions after each meaningful edit.

6. **Build and Test**  
   Run builds and tests (or equivalent validation steps) before finishing a task.

7. **Explicit Deviation Documentation**  
   Clearly document any deviations from existing patterns, architecture, or workflow.

### B. Agent Planning and Autonomy

1. **Planning First**  
   Use proportional planning before making changes.  
   Create a multi-step plan for non-trivial, high-impact, or multi-file work; use a brief plan for routine low-risk edits. Prefer incremental, reversible execution.

2. **Clarification Handling (Non-Blocking)**  
   Ask explicit clarification questions when required.
   If questions are not surfaced in the UI, do not block indefinitely.  
   Retry once with explicit questions, then proceed with safe, least-invasive assumptions.

2.1 **Plan Update Continuity**  
   If the user adjusts a plan or rejects suggested options, update the plan once and continue execution without re-entering planning mode unless scope changes materially.

2.2 **Approved Plan Continuity**
   After a plan is approved (or explicitly adopted) and scope remains unchanged, continue implementation directly. Do not restart planning loops for routine execution steps.

3. **Assumption Management**  
   Choose the least disruptive option when proceeding without answers.  
   Clearly document assumptions inline using comments or TODO markers.

4. **Continuity of Work**  
   If blocked on one task, continue with another task from the plan.  
   Return later to unresolved items.

5. **Safety and Compatibility Defaults**  
   Preserve existing UX and behavior unless explicitly instructed otherwise.  
   Maintain compatibility with both local and production environments by default.

6. **Transparency and Auditability**  
   Surface plans, decisions, assumptions, and open questions clearly.  
   Never silently change architecture-critical behavior.

### C. Agentic Guardrails (Global)

1. **Architecture Before Execution**  
   Start with impact mapping and boundaries before multi-file changes.

2. **Incremental Execution**  
   Prefer small, reversible steps with checkpoints and reviews.

3. **Safe Outputs**  
   Favor reviewable artifacts (PRs, issues, reports) and keep humans in the loop.

---

## 4) Path-Scoped Rules (Critical)
Before generating code, determine the target path and apply the corresponding instruction file strictly:

- `**/*.cs` → C# rules: .github/instructions/csharp.instructions.md
- `**/*.py` → Python rules: .github/instructions/python.instructions.md
- `**/*.{ts,tsx,js,jsx}` → React/TypeScript rules: .github/instructions/react.instructions.md
- Shared files → Global rules only (this file)

---

## 4.1) Skills Catalog (Trigger Hints)

Use these phrases to help Copilot select the right skill when relevant:

- **agentic-workflows**: agent mode, architecture-aware planning, impact mapping, multi-step refactor, safe migration
- **testing-modernization**: test gaps, contract tests, integration tests, domain tests, modernization plan
- **copilot-cli-workflows**: Copilot CLI, terminal workflows, delegation, headless automation
- **mcp-integration-consumer**: MCP tools, tool integration, scope tool access, external tools
- **continuous-ai-agentic-ci**: agentic CI, natural-language rules, safe outputs, reviewable automation

Notes:
- This list is non-exhaustive; check .github/skills for the full catalog.

## 4.2) Agent and Skill Auto-Apply Rules

When a task clearly matches a domain agent or skill, treat those instructions as mandatory even if the user does not explicitly invoke the agent. Examples include Revit add-ins (revit development agent/skill) and Microsoft Agent Framework (maf foundry agent/skill).

If multiple agents or skills apply at once, use this resolution order:
1. Product or domain-specific instructions (for example, Revit, MAF)
2. Architecture or layer-specific instructions (domain/application/infrastructure)
3. Tooling or workflow skills (testing, CI, docs)
4. General global instructions

Execution continuity rule:
- Planning-only skills must not preempt execution-phase work once an implementation-capable agent is selected and scope is unchanged.
- If planning and execution guidance both apply at the same priority, use the least-disruptive option that preserves forward progress and governance constraints.

If two applicable sources conflict at the same priority, ask a clarification question and proceed with the least disruptive option.

---

## 5) Shared Rules

### Security & Secrets
- Never commit secrets or tokens
- Prefer secure storage (credential managers, environment variables, or user-local config files)
- Never log secrets or auth payloads

### Azure (Infrastructure & Identity)
- Always follow Azure best practices for infrastructure setup
- Prefer Managed Identity between services; avoid API keys when possible (e.g., LLM access in Azure AI Foundry)
- Prefer Azure Agent Framework for AI services (C# or Python) unless the existing project stack dictates otherwise

### Build & Diagnostics
- Respect configured warning suppressions
- Do not “fix” suppressed warnings without context

### Source Control
- Never commit directly to `main` or `develop`
- Use a sub-branch on GitHub or Azure DevOps
- If asked to commit to `main` or `develop`, request explicit confirmation first

---

## 6) Pragmatism Rule (Escape Hatch)
When a rule reduces clarity, correctness, or debuggability:
1. Prefer correctness
2. Document the deviation
3. Keep it local and explicit

---

**This file is authoritative for global Copilot behavior.**
