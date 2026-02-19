---
name: repo-docs
description: Create or update repository documentation with clear structure and minimal changes.
---
# Repository Documentation Skill

Use this skill when creating or updating docs/markdown files in the repo.

## Workflow
1. Read existing documents for tone and structure.
2. Start from the documentation index/README when available.
2. Make minimal edits; preserve headings and anchors.
3. Use Markdown links for cross-references.
4. Prefer current docs over archive material unless explicitly requested.
5. If adding examples, keep them short and runnable.

## Guardrails
- Do not modify source code while documenting unless requested.
- Do not include secrets or internal tokens in docs.
- Keep changes localized to the docs being updated.
