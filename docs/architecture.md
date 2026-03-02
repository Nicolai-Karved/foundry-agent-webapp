# Architecture Overview

This document provides a high-level view of the **foundry-agent-webapp** architecture, focusing on the Azure backend (search, agents/workflows, blob storage, Azure Function ingestion) and the end-to-end data flows.

## System at a glance

The app is a **single-container** web application (React SPA + ASP.NET Core API) deployed to **Azure Container Apps**. It integrates with **Azure AI Foundry Agents** for chat responses and uses **Azure Blob Storage + Azure Function + Azure AI Content Understanding + Azure AI Search** to ingest and index documents for retrieval and citations.

```
User Browser
    │
    ▼
React SPA (Vite)
    │  MSAL PKCE token
    ▼
ASP.NET Core API (single container)
    │  Managed Identity
    ▼
Azure AI Foundry (Agents/Workflows)
    │  citations + tool results
    ▼
SSE stream back to UI

Document Ingestion Pipeline
    │
    ▼
Blob Storage (bim-standards/source)
    │  BlobTrigger
    ▼
Azure Function (CuIngest)
    │  calls Content Understanding analyzer
    ▼
CU JSONL Output (bim-standards/cu-output)
    │  Search indexer/skillset
    ▼
Azure AI Search index
    │  Used by agent for citations
    ▼
Chat UI citations
```

## Core components

### Frontend (React + Vite)
- Hosts the chat UI and document experiences.
- Uses **MSAL.js** to acquire Entra ID tokens.
- Streams responses via SSE from the backend.

### Backend (ASP.NET Core Minimal API)
- Serves both **/api** endpoints and the SPA (single container pattern).
- Validates JWTs (Entra ID) and enforces `Chat.ReadWrite` scope.
- Connects to **Azure AI Foundry Agents** using managed identity.
- Streams agent output and citations to the UI via SSE.

### Azure AI Foundry (Agents/Workflows)
- Hosts the configured agent(s) and workflow execution.
- Produces streaming responses, citations, and tool outputs.
- Accessed via the `Azure.AI.Projects` SDK and Agent Framework extensions.

### Azure Blob Storage
- Stores source documents for ingestion (e.g., PDFs).
- Stores CU analyzer output (`.cu.jsonl`) used by Azure AI Search.

### Azure Function: CuIngest
- Blob-triggered function that:
  - Calls **Azure AI Content Understanding** analyzer.
  - Splits and normalizes content into paragraph/section chunks.
  - Emits structured JSONL into the output container for indexing.

### Azure AI Search
- Indexer + skillset pipeline builds a searchable paragraph index.
- The agent uses this index for retrieval and citations.

### Deployment & Observability
- **Azure Container Apps** hosts the single container app.
- **Azure Container Registry** stores images.
- **Log Analytics** captures logs.
- **Managed Identity** is used for secure access to Azure resources.

## Key data flows

### 1) Chat + agent response flow
1. User signs in via Entra ID (PKCE flow).
2. Frontend sends a message to `/api/chat/stream` with a bearer token.
3. Backend forwards the request to Azure AI Foundry Agent Service.
4. Responses stream back to the client via SSE, including citations.

### 1.1) Agent routing and specialist flows (AIR / EIR / BEP)

The backend route selector in `AgentFrameworkService` chooses the agent based on the following precedence:
1. Explicit `agentRouteHint` from UI settings (`air`, `eir`, `bep`)
2. Uploaded file names (`*air*`, `*eir*`, `*bep*`)
3. Selected standards IDs containing route tokens
4. Message text heuristics
5. Default agent fallback

Unified topology target (current migration state):
- `standard-compliance-checker` — standards-grounded AIR/EIR compliance turns
- `general-bim-standard-qa` — conversational/general BIM standards Q&A turns
- `document-compliance-checker` — BEP/multi-document comparison turns

Legacy specialist IDs (`AI_AGENT_ID_AIR`, `AI_AGENT_ID_EIR`, `AI_AGENT_ID_BEP`) remain supported as fallback during migration.

#### AIR compliance flow
- Route resolves to `air` specialist agent when hint/inputs indicate AIR.
- Compliance workflow is enabled when either:
    - uploaded evidence exists (image/file), or
    - structured standards are selected for AIR route.
- Backend injects structured prompts in sequence:
    1. Policy prompt (`BuildPolicyPrompt`) with effective `doc_type = AIR`
    2. Standards grounding prompt:
         - requirements-first prompt (`BuildRequirementsFirstPrompt`) when enabled, or
         - grounded clauses prompt (`BuildGroundedClausesPrompt`)
    3. User message and attachments

#### EIR compliance flow (equivalent to AIR)
- Route resolves to `eir` specialist agent via the same routing precedence.
- Compliance workflow is enabled with the same triggers as AIR:
    - uploaded evidence, or
    - structured standards selected for EIR route.
- Backend uses the same structured prompt stack, with `doc_type = EIR` applied by route-aware policy shaping.

#### BEP comparison flow
- Route resolves to `bep` when explicit hint or BEP/AIR/EIR file combinations indicate comparison.
- Backend injects a BEP comparison context prompt and can still use standards grounding when provided.

### 1.2) Why the stored Agent prompt may appear "not used"

The Foundry Agent's stored system prompt is still part of execution, but the backend intentionally prepends strong, structured user prompts (policy + requirements/clauses) before the final user message for compliance turns. This means:
- Output shape and citation behavior are primarily driven by runtime prompt scaffolding.
- The agent prompt should remain stable, concise, and role-oriented (persona, safety, tone, boundaries), while strict output schema/compliance mechanics stay in backend-generated prompts.

Recommendation:
- Keep agent prompt focused on durable behavior (scope, refusal policy, tone, high-level task intent).
- Avoid duplicating detailed JSON/schema enforcement in the stored prompt when backend already injects authoritative structured prompts.

### 2) Document ingestion + search flow
1. Documents are uploaded to Blob Storage (`source/` path).
2. **CuIngest** Function triggers on new blobs.
3. Function calls **Content Understanding** to analyze the document.
4. Structured JSONL chunks are written to `cu-output/`.
5. Azure AI Search indexer/skillset ingests JSONL into the search index.
6. The agent uses the search index for retrieval and citations.

## Deployment and provisioning (azd)

`azd up` orchestrates provisioning and configuration:
- **preprovision**: Creates Entra app registration, discovers AI Foundry resources, generates `.env` files.
- **provision**: Deploys infrastructure (Bicep).
- **postprovision**: Updates redirect URIs and RBAC for managed identity.
- **predeploy**: Builds and pushes the container image (local Docker or ACR build).

## Related documentation

- `README.md` – Project overview and developer workflow
- `infra/README.md` – Infrastructure overview and resource details
- `backend/README.md` – API and streaming details
- `deployment/README.md` and `deployment/hooks/README.md` – azd hook lifecycle
- `docs/search-setup.md` – Search artifacts and setup
- `docs/cu-paragraph-index-mapping.md` – CU output → search mapping
