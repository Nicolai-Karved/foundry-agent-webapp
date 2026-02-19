---
name: agent-creator
description: Create agent files, agent frontmatter, and custom agent setup. Triggers: create agent, agent frontmatter, agent schema, custom agent.
---

# Agent Creator

Create a new custom agent file under `.github/agents/` with consistent frontmatter, naming, and structure.

## Quick start
1. Choose the agent handle and file name.
2. Draft the agent purpose, scope, and guardrails.
3. Add YAML frontmatter using the schema.
4. Validate naming, discoverability, and references.

## When to open references
- Need the full YAML schema: read [references/agent-frontmatter-schema.md](references/agent-frontmatter-schema.md).
- Need a complete agent example: read [references/agent-template.md](references/agent-template.md).
- Need naming and trigger guidance: read [references/naming-and-triggers.md](references/naming-and-triggers.md).
- Need a final validation list: read [references/review-checklist.md](references/review-checklist.md).

## Output rules
- Keep the agent file focused and under 500 lines.
- Use concise sections: role, scope, workflow, guardrails.
- Avoid secrets or internal credentials.
- Prefer repo-specific conventions and paths.
