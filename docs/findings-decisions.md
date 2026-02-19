# Findings and decisions

## Context
- Project: foundry-agent-webapp (React/Vite frontend, ASP.NET Core backend)
- Goal: Local dev with Azure AI Foundry agent, improved document/task UI

## Findings
- Local dev requires Azure CLI/azd auth and env files for both frontend and backend.
- Port conflicts on 8080 required moving backend to 8089 and updating proxy/CORS/docs/scripts.
- PDF rendering needed a robust viewer: react-pdf with a matching pdfjs worker version and data URI handling.
- PDF text layer rendering is async; highlight must wait for text spans and match across multiple spans.
- Task selection should prefer `reference`, then fall back to `document_reference` when no match is found.
- Some references were concatenated; highlighting works better when references are split into precise strings.
- Manual search needed independent navigation and matching to avoid confusing task highlight state.
- Playwright testing is limited by auth state; manual testing works best for signed-in flows.

## Decisions
- Backend port is 8089 for local dev, with Vite proxy and CORS aligned.
- PDF viewer uses react-pdf with pdfjs worker from CDN (version aligned with react-pdf).
- PDF highlights are severity-colored underlines (not filled blocks) to preserve readability.
- Highlight matching uses normalized tokens across spans and a best-effort window.
- Manual search supports its own match count and navigation; task search kept separate.
- Task references are expected as a list of precise strings when multiple matches exist.

## UX notes
- Document panel is resizable; PDF scales to available width.
- Task panel is selectable and text is copyable.
- Manual search includes a clear (x) button and arrow navigation.

## Open follow-ups
- Validate remaining task highlight misses with actual `reference` arrays.
- Consider authentication-aware Playwright runs if automated UI tests are required.
