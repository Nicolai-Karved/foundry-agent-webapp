# Revit Development Customization Architecture

This document explains how the three layers of Revit customization work together in this repository.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  Layer 1: CUSTOM INSTRUCTIONS (Always Active)               │
│  • .github/copilot-instructions.md (global rules)           │
│  • .github/instructions/csharp.instructions.md (C# rules)   │
│  Applied automatically to all C# files                      │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  Layer 2: AGENT (Explicit Invocation)                       │
│  • @RevitDevelopment                                       │
│  • .github/agents/revit-development.agent.md                      │
│  Full Revit context when you type @RevitDevelopment         │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  Layer 3: SKILLS (Automatic On-Demand)                      │
│  • revit-development-ref                                    │
│  • .github/skills/revit-development-ref/                    │
│  Detailed procedures loaded automatically when relevant     │
└─────────────────────────────────────────────────────────────┘
```

## Layer 1: Custom Instructions (Foundation)

**Files:**
- `.github/copilot-instructions.md` - Global workspace rules
- `.github/instructions/csharp.instructions.md` - C# language conventions

**Scope:** Always active, automatically applied

**Purpose:** 
- Enforce language-level coding standards (C# naming, style, Object Calisthenics)
- Define global security rules (no secrets, Managed Identity preference)
- Establish architectural priorities (Architecture → Correctness → Clarity → Rules)

**Key Principle:**
> "Use these rules for all C# code. For product-specific guidance (e.g., Revit), use Skills instead."

**What it contains:**
- ✅ C# naming conventions (PascalCase, camelCase, `_camelCase`)
- ✅ Code style rules (tabs, Allman braces, line length)
- ✅ Object Calisthenics principles
- ✅ Security rules (no secrets, Azure best practices)
- ❌ NO Revit-specific guidance (that's in Layer 3)

**When it applies:**
- Every time you edit or create a `.cs` file
- Automatically loaded without requiring explicit invocation

---

## Layer 2: Agent (Explicit Context)

**File:** `.github/agents/revit-development.agent.md`

**Invocation:** Type `@RevitDevelopment` in Copilot Chat

**Scope:** When explicitly invoked for Revit work

**Purpose:**
- Provide full Revit add-in development context
- Act as a specialized "Revit expert" persona
- Know the tech stack, solution structure, and boundaries
- Provide code examples and quick patterns
- Direct to detailed procedures in Layer 3

**What it contains:**
- ✅ Revit API and tech stack knowledge (C#, WPF/Prism, MSBuild)
- ✅ Naviate solution structure and project organization
- ✅ Executable commands (dotnet build, debugging, package management)
- ✅ Critical performance rules (startup < 2 seconds, no HTTP during OnStartup)
- ✅ Transaction safety patterns and code examples
- ✅ Testing checklists and boundaries (Always/Ask/Never)
- ✅ Reference to revit-development-ref skill for detailed procedures

**When to use:**
- Starting Revit add-in development work
- Need full context about Revit architecture and patterns
- Want a "Revit expert" to guide implementation
- Creating new Revit features or fixing Revit-specific issues

**Example invocation:**
```
@RevitDevelopment I need to create a new External Command that 
updates wall parameters. What's the safe pattern?
```

---

## Layer 3: Skills (Automatic Procedures)

**Directory:** `.github/skills/revit-development-ref/`

**Invocation:** Automatic (progressive disclosure)

**Scope:** Loaded on-demand when Copilot detects relevance

**Purpose:**
- Provide detailed, step-by-step procedures for specific tasks
- Load only the needed reference file (not all 7 at once)
- Supplement both the agent and general Copilot interactions
- Keep procedures separate from context (efficiency)

**What it contains:**

```
revit-development-ref/
├── SKILL.md (index with content map)
└── references/
    ├── setup-and-structure.md      # Project setup, solution navigation
    ├── build-deploy.md             # Build/deployment configuration
    ├── package-management.md       # NuGet package management
    ├── ui-development.md           # WPF/Prism UI patterns
    ├── startup-performance.md      # Startup optimization, hang fixes
    ├── tool-safety.md              # Transaction patterns, validation
    └── testing-validation.md       # Testing protocols, debugging
```

**How it works:**
1. You ask: "How do I fix a Revit startup hang?"
2. Copilot's metadata scan identifies `revit-development-ref` as relevant
3. Copilot loads `SKILL.md` and sees the content map
4. Copilot reads `references/startup-performance.md` for detailed procedure
5. You get step-by-step instructions without manual skill invocation

**When it loads:**
- Automatically when working on Revit-specific tasks
- When detailed procedures are needed beyond agent context
- Can be explicitly referenced by the agent
- Progressive: only loads the specific reference file needed

---

## How They Work Together

### Scenario 1: Creating a New Revit Command

1. **Instructions (Layer 1):** Enforces C# naming, style, and testability patterns
2. **Agent (Layer 2):** You type `@RevitDevelopment create a command that reads walls`
   - Agent provides transaction pattern code example
   - Agent explains where the command file should go (main project)
   - Agent reminds you to validate parameters
3. **Skills (Layer 3):** Copilot automatically loads `tool-safety.md` for detailed transaction patterns

### Scenario 2: Fixing a Startup Hang

1. **Instructions (Layer 1):** C# style rules apply to your fix
2. **Agent (Layer 2):** You type `@RevitDevelopment Revit hangs on startup`
   - Agent explains the common culprits (HTTP, credentials, ExternalEvent)
   - Agent provides target: OnStartup < 2 seconds
   - Agent references the startup-performance skill
3. **Skills (Layer 3):** Copilot loads `startup-performance.md` with step-by-step diagnosis procedures

### Scenario 3: General C# Edit (No Revit Context Needed)

1. **Instructions (Layer 1):** C# rules apply automatically
2. **Agent (Layer 2):** Not invoked (not needed)
3. **Skills (Layer 3):** May auto-load if Copilot detects relevance, but otherwise stays dormant

---

## Key Principles

### Separation of Concerns
- **Instructions:** Language rules only, NO product guidance
- **Agent:** Full Revit context when explicitly needed
- **Skills:** Detailed procedures, loaded on-demand

### Progressive Disclosure
- Instructions are always loaded (small, language-focused)
- Agent loads only when invoked (explicit @mention)
- Skills load only relevant reference files (not all 7 at once)

### No Redundancy
- Instructions delegate product guidance to Skills
- Agent references Skills for detailed procedures
- Skills focus on "how" while Agent provides "what" and "why"

### Trust Hierarchy
When guidance conflicts:
1. **Architecture principles** (from instructions)
2. **Correctness** (safe transaction patterns from agent)
3. **Clarity** (readable code)
4. **Rules** (specific conventions)

---

## For Developers

### When to update each layer:

**Update Instructions when:**
- C# language conventions change
- New global security rules needed
- Workspace-wide standards evolve

**Update Agent when:**
- Naviate solution structure changes
- New Revit API patterns emerge
- Tech stack versions change (Revit 2025, etc.)
- High-level boundaries shift

**Update Skills when:**
- Detailed procedures need refinement
- New step-by-step workflows discovered
- Common troubleshooting patterns identified
- Specific reference material needs updating

---

## Quick Reference

| I need... | Use... | How... |
|-----------|--------|--------|
| C# style rules | Instructions | Automatic (always active) |
| Full Revit context | Agent | `@RevitDevelopment` |
| Detailed procedures | Skills | Automatic (on-demand) or via agent |
| Fix startup hang | Agent + Skills | `@RevitDevelopment` mentions skill |
| Package management | Skills | Auto-loads package-management.md |
| Safe transaction pattern | Agent | Provides code example immediately |

---

## Summary

This three-layer architecture provides:
- ✅ **Consistent C# style** (Instructions, always on)
- ✅ **Expert Revit guidance** (Agent, on-demand via @mention)
- ✅ **Detailed procedures** (Skills, auto-loaded when relevant)
- ✅ **No redundancy** (each layer has a clear purpose)
- ✅ **Efficiency** (progressive disclosure, load only what's needed)
