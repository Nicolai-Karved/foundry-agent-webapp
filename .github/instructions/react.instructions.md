---
description: React/TypeScript rules for UI code
applyTo: "**/*.{ts,tsx,js,jsx}"
---

# React/TypeScript Rules

Use these rules for React UI code. For non-React JS/TS files, follow local patterns and keep changes minimal.

## Framework & Tooling
- React 19 (compiler-first)
- TypeScript
- Vite
- MUI

---

## React & TypeScript Rules (Hard)
- **Do NOT use** `useMemo`, `useCallback`, or `React.memo` unless required by a third-party library
- Let the React compiler handle optimization
- Components may omit explicit return types
- **Custom hooks must declare explicit return types**
- Prefer function components

### Example: hook return type required
```ts
// GOOD
export type UseThingResult = { value: number; refresh: () => Promise<void> };

export function useThing(): UseThingResult {
	// ...
	return { value, refresh };
}

// OK: component can omit return type
export function ThingView() {
	return <div />;
}
```

---

## Project Structure (if present)
- `src/apicalls` – API access only
- `src/hooks` – custom hooks
- `src/views` – routed views/pages
- Co-locate `.scss` files with components
- No cross-layer imports

---

## Styling
- Use SCSS modules where applicable
- No inline styles unless unavoidable
- Prefer composition over overrides

---

## Frontend Commands
- Install: `npm install`
- Dev: `npm run dev`
- Build: `npm run build`
- Lint: `npm run lint`

---

## Frontend Safety Rules
- Do not log tokens or auth payloads
- Do not re-enable commented-out Auth0 logic unless explicitly requested
- Respect configured CORS ports