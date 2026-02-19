# Using the .github Instructions and Skills

This folder is designed to be copied into new repositories. After copying, run the project setup agent to verify instruction scopes and skills alignment.

## Setup Steps
1. Copy the entire .github folder into the new repository root.
2. Invoke the `project_setup` agent.
3. Verify that language-specific instruction files exist and are scoped correctly:
   - `.github/instructions/csharp.instructions.md` → `**/*.cs`
   - `.github/instructions/python.instructions.md` → `**/*.py`
   - `.github/instructions/react.instructions.md` → `**/*.{ts,tsx,js,jsx}`
4. Ensure product-specific guidance lives in Skills (e.g., Revit rules in `.github/skills/*`).

## Notes
- If a repository introduces a new language or framework, add a new language-specific instruction file.
- Do not introduce fixed repo names unless required.

## Agent File and Routing Conventions
- Store custom agents in `.github/agents/*.agent.md` (this repository standard).
- Keep `name` in frontmatter aligned with the invocation handle (for example, `name: RepoGovernance` maps to `repo-governance.agent.md`).
- Use proportional planning: multi-step planning for non-trivial/high-impact work, lightweight planning for routine low-risk edits.
- After a plan is approved and scope is unchanged, continue execution directly without re-entering planning mode.
- Keep `security_assessment` plan/report-only.
