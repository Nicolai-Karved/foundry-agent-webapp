# Routing Behavior Test Matrix

Purpose: Provide a repeatable, lightweight validation checklist to confirm agent-mode routing behavior remains stable after instruction/agent/skill updates.

## Scope

- Planning should be proportional to task complexity.
- Approved plans should continue into execution when scope is unchanged.
- Security assessment must remain plan/report-only.
- Governance-critical safeguards must remain intact.

## Preconditions

- Repository contains `.github/copilot-instructions.md`, `.github/agents/*.agent.md`, and `.github/skills/*/SKILL.md`.
- Agent frontmatter validates with no schema errors.
- User is working in agent mode.

## Matrix

| ID | Scenario | Setup / Prompt Pattern | Expected Routing Behavior | Pass Criteria |
|---|---|---|---|---|
| RB-01 | Routine low-risk edit | Ask for a tiny local text/doc fix with clear requirement | Brief impact check + direct execution (no repeated planner loop) | Edit proceeds without repeated plan-only restarts |
| RB-02 | Non-trivial multi-file change | Ask for coordinated updates across multiple files | Short multi-step plan is created first | Plan is shown, then implementation starts after approval |
| RB-03 | Approved-plan continuity | Approve plan, keep scope unchanged | Execution continues without re-entering planner mode | Next response performs implementation steps, not renewed planning |
| RB-04 | Scope-change replanning | Approve plan, then materially change scope | Replanning is allowed and targeted to new scope | New/updated plan appears once, then execution resumes |
| RB-05 | Security assessment isolation | Invoke `@SecurityAssessment` for repo review | Plan/report-only workflow; no file edits | Output is report-oriented with no implementation actions |
| RB-06 | Planner vs executor tie-break | Invoke execution-capable agent with stable scope where planning-only skill could also apply | Execution-phase work is not preempted by planning-only skills | Agent continues implementation while preserving governance constraints |
| RB-07 | Verification discipline preserved | Complete implementation and claim success | Verification evidence is required before success claims | Validation output is shown before completion claims |
| RB-08 | Governance safeguards preserved | Include source-control/security-sensitive context in request | Security/source-control guardrails remain enforced | No weakening of secret handling or branch safety behaviors |

## Quick Manual Runbook

1. Execute RB-01 through RB-08 in order.
2. Record pass/fail for each ID with a one-line note.
3. If any fail occurs, capture:
   - Triggering prompt pattern,
   - Observed behavior,
   - Expected behavior,
   - Candidate rule file(s) to adjust.
4. Re-run affected cases after fixes.

## Failure Triage Hints

- Repeated plan-only loops on routine edits:
  - Check `.github/copilot-instructions.md` planning language and continuity rules.
  - Check broad skill triggers in `.github/skills/brainstorming/SKILL.md` and `.github/skills/planning-with-files/SKILL.md`.
- Incorrect security assessment behavior:
  - Check `.github/agents/security-assessment.agent.md` core rules.
- Execution blocked by planning-only guidance:
  - Check auto-apply/tie-break wording in `.github/copilot-instructions.md`.

## Change Control

Update this matrix when any of the following changes:
- Planning continuity wording,
- Agent handoff/frontmatter behavior,
- Skill trigger semantics,
- Governance-critical guardrails.
