---
name: skill-developer
description: Operational guide for implementing and maintaining GitHub Copilot skills in this repo. Use when creating, updating, or consolidating skills, aligning names/metadata, or structuring references, scripts, and assets.
---

# Skill Developer

## Purpose

Operational guidance for creating and maintaining GitHub Copilot skills in this repository.

## When to Use

Use this skill when you need to:
- Create a new skill folder and SKILL.md
- Update or consolidate existing skills
- Align skill metadata with intended triggers
- Add references, scripts, or assets
- Ensure skills comply with repo safety and clarity rules

---

## Skill Structure (GitHub Copilot)

Skills live under .github/skills in a folder named after the skill:

```
.github/skills/<skill-name>/
  SKILL.md
  references/
  scripts/
  assets/
```

Only SKILL.md is required. references, scripts, and assets are optional.

---

## Metadata and Triggers

Copilot uses only the YAML frontmatter fields to decide when to load a skill:

- name: unique, kebab-case
- description: include the task, domain, and trigger keywords

If a skill does not trigger, adjust the description to include explicit terms the user is likely to say.

---

## Authoring Checklist

- Keep SKILL.md under 500 lines; move details into references.
- Use clear headings and short sections.
- Prefer actionable steps over long explanations.
- Avoid external network calls unless the user explicitly requests them.
- Do not include secrets or internal-only credentials.

---

## Consolidation Guidance

If two skills overlap:
- Keep one focused on content design (principles, structure).
- Keep the other focused on operational steps (where files live, how to name, how to maintain).
- Remove duplicate prose and keep a single source of truth.
---

## Validation Checklist

When creating or updating a skill, verify:

- [ ] Folder name matches `name` in frontmatter
- [ ] Frontmatter includes `name` and `description` only
- [ ] Description includes clear trigger keywords
- [ ] References/scripts/assets paths are relative and exist
- [ ] No legacy agent-specific paths or hooks are referenced
- [ ] No external network calls unless user explicitly requested them
- [ ] SKILL.md stays under 500 lines

---

## Common Fixes

- Replace legacy tool paths with .github/skills paths
- Replace agent-specific environment variables or hooks with repo-neutral guidance
- Remove stale "skill status" sections or unused references
