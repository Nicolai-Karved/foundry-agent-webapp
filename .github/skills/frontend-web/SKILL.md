---
name: frontend-web
description: Build, lint, and validate frontend changes in Front-end/** using Vite/React/MUI conventions.
---
# Frontend (React/Web) Workflow Skill

Use this skill when you modify or review code under `Front-end/**`.

## Commands
- Install: `npm install`
- Dev: `npm run dev`
- Build: `npm run build`
- Lint: `npm run lint`

## Workflow
1. Confirm the change stays within `Front-end/**` boundaries.
2. Follow React compiler-first guidance (avoid `useMemo`/`useCallback`/`React.memo` unless required).
3. Ensure custom hooks have explicit return types.
4. Run `npm run lint` and `npm run build` when relevant.

## Guardrails
- Do not log tokens or auth payloads.
- Do not re-enable commented-out Auth0 logic unless explicitly requested.
- Respect configured CORS ports.
