---
name: maf-foundry-development-ref
description: Build Microsoft Agent Framework agents in C# using Azure AI Foundry. Covers planning-first workflow, setup, model selection, and infrastructure guidance.
---

# Microsoft Agent Framework + Azure AI Foundry (C#)

Use this skill when the user wants to build a Microsoft Agent Framework agent in **C#** backed by **Azure AI Foundry**, or needs help setting up Foundry resources and infrastructure for agent apps.

## Scope
- **C# only** (Microsoft Agent Framework .NET)
- Azure AI Foundry **project setup**, **model deployment**, and **auth**
- Quickstart vs production infrastructure guidance
- Planning-first workflow with optimization logging

## Guardrails
- Prefer **Managed Identity**; avoid API keys when possible.
- Never include secrets or tokens.
- Use Azure tooling for resource operations when requested; don’t fabricate deployments.
- Keep guidance aligned with Microsoft Agent Framework and Azure AI Foundry docs.
- Validate version guidance against the official release feed before recommending package updates.
- Start with a plan before implementation changes.
- Use relevant repo skills as needed (do not duplicate their content).
- Maintain an implementation process log for optimization.

## Upstream source priority
1. Official framework repo and docs: https://github.com/microsoft/agent-framework
2. Official latest release pointer: https://github.com/microsoft/agent-framework/releases/latest
3. Official .NET sample paths under `dotnet/samples/GettingStarted/*`
4. Community sample repo for additional patterns: https://github.com/rwjdk/MicrosoftAgentFrameworkSamples

When sample patterns differ, prefer official behavior for compatibility, and treat community samples as optional enhancement references.

## Choose a path
- **Quickstart**: small demo or proof-of-concept → read the quickstart sections in the references.
- **Production**: enterprise use → also read the production infrastructure reference.

## Content map (read only what you need)
| File | When to read |
|------|-------------|
| references/azure-foundry-setup.md | Setting up Foundry resources, projects, model deployment, and auth |
| references/agent-framework-csharp.md | Building the C# agent app using Microsoft Agent Framework |
| references/infra-production.md | Production readiness: networking, RBAC, monitoring, secrets, governance |
| references/dotnet-rc1-upgrade.md | Upgrading existing .NET/C# projects to `dotnet-1.0.0-rc1` |

## Minimal workflow
1. Confirm the user wants **C#** + **Microsoft Agent Framework** + **Azure AI Foundry**.
2. Create or update a **plan** before implementing changes.
3. Check the current latest release tag before proposing package/version updates.
4. Review official .NET samples, then cross-check community samples for practical enhancements.
5. Select the **model** and **deployment** in Foundry (don’t assume names).
6. Create the agent app with **AIProjectClient** (not AzureOpenAIClient).
7. Configure auth (Managed Identity or Azure CLI) and required environment variables.
8. Add production controls if needed (RBAC, network, logging, Key Vault).
9. Capture decisions, fixes, and learnings in the process log.

## Existing-project upgrades (RC1)
If the request is to update an existing .NET project to the RC candidate, use:
- `references/dotnet-rc1-upgrade.md`

## Implementation process log
Create and maintain a root-level log file:
`implementation-process-log.md`

Use it to record:
- Decisions made (model choice, auth method, deployment name)
- Issues encountered and fixes applied
- Project-specific adjustments
- Follow-up optimization ideas

## Quick checklists
- [ ] Foundry project created
- [ ] Model deployed and deployment name recorded
- [ ] `AZURE_FOUNDRY_PROJECT_ENDPOINT` set
- [ ] `AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME` set
- [ ] C# packages installed with `--prerelease`
- [ ] Uses `AIProjectClient` for Foundry agents

## Related guidance
If you need deeper infrastructure or governance detail, read the production reference file.
