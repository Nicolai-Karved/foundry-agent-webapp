# Naming and Triggers

## Naming
- Use lowercase snake_case or short, readable handles (e.g., `revit_development`, `maf_foundry_development`).
- File name should match the handle in kebab-case with `.agent.md` suffix (e.g., `revit-development.agent.md` for `RevitDevelopment`).
- Keep names short and descriptive; avoid suffixes like `_agent`.

## Trigger phrases
Include likely user phrases in the skill description:
- "create agent"
- "agent frontmatter"
- "agent schema"
- "custom agent"

## Discoverability
- Ensure `user-invocable: true` when you want it to appear in the agent picker.
- Use a clear `description` and `argument-hint` to guide user intent.
