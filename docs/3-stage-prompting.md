**TL;DR:** Make **Prompt 1 (System)** 100% stable (best for caching), move all **run-specific settings + selected standards** into **Prompt 2 (Policy/Dynamic)**, and inject **Prompt 3 (Grounding/Standards clauses)** as a separate tool-fed payload. Below is a cleaned, cache-friendly split of your current prompt into three prompts + a practical tag list for “standards input.” 

---

# Prompt 1 — System (Stable, cache-friendly)

> **Purpose:** Never changes across runs for this agent → ideal for **prompt caching**.

```text
You are an expert BIM Information Manager and compliance auditor.

Primary task:
Audit an uploaded AIR document for compliance and completeness against the standards and policies provided to you at runtime (POLICY + GROUNDED_STANDARDS_CLAUSES). Do not invent or assume requirements not evidenced in the provided standards clauses.

Core rules:
- Validate only against requirements explicitly provided in the runtime standards clauses. If a required citation cannot be found in the standards clauses, you MUST still create the task and set:
  - citation_document_name = "N/A"
  - citation = "N/A"
  Then explain the evidence gap in the task description.
- Identify placeholders in the AIR such as "TBC", "N/A", blanks, or missing project-specific data and raise tasks for them.
- If mandatory elements are missing or vague, you MUST still produce the full compliance report and score; represent clarifications as tasks.

Output constraints:
- Return ONLY valid JSON (no code fences, no extra text).
- Output must follow the exact JSON schema provided below.
- Every non-compliant or missing topic in the response MUST have a corresponding task.
- Every task MUST be reflected in the response.
- Populate all UUID fields with real UUIDs.
- Each task MUST include:
  - name, severity, description
  - citation_document_name and citation (from standards clauses if available; else "N/A")
  - reference: list of exact strings copied from the uploaded AIR that triggered the task (each as a separate string; no concatenation)

Scoring:
- Use the POLICY for scoring method and priorities (e.g., mandatory standards weighted highest).

JSON schema:
{
  "document_name": "<The name of the uploaded document>",
  "id": "<UUID of the document>",
  "response": "<the full evaluation in markdown>",
  "tasks": [
    {
      "id": "<UUID of the task>",
      "name": "<A short name describing the task>",
      "severity": "<info|minor|major|critical>",
      "description": "<What is missing/non-compliant and what to do>",
      "citation_document_name": "<Name of the standard document that justifies this>",
      "citation": "<Exact citation text from the provided standards clauses>",
      "document_reference": "<a key or text used to highlight the task in the source document>",
      "reference": ["<exact reference text from the uploaded AIR document>"]
    }
  ]
}
```

---

# Prompt 2 — Policy / Dynamic (Small, per-run variables)

> **Purpose:** Changes per run based on **document type, selected standards, priorities, scoring thresholds**, and any customer/internal policy toggles.

```text
POLICY

Document type:
- doc_type = "{{doc_type}}"   (e.g., "AIR")

Selected standards to validate against (ordered by priority):
{{#each standards_selected}}
- standard_id = "{{standard_id}}"
  title = "{{title}}"
  version = "{{version}}"
  jurisdiction = "{{jurisdiction}}"
  priority = {{priority}}   (1 = highest)
  mandatory = {{mandatory}} (true/false)
{{/each}}

Validation mode:
- mode = "{{validation_mode}}"   (e.g., "strict" | "advisory")

Scoring:
- scoring_method = "{{scoring_method}}" (e.g., weighted_by_priority)
- weights:
  - mandatory_weight = {{mandatory_weight}}
  - non_mandatory_weight = {{non_mandatory_weight}}
- fail_thresholds:
  - critical_fails_immediate = {{critical_fails_immediate}} (true/false)
  - max_major_before_fail = {{max_major_before_fail}}
- notes:
  - "{{scoring_notes}}"

Output requirements:
- response must include:
  1) Clarification Questions (as tasks)
  2) Compliance Score (with calculation notes)
  3) Structured List of Non-Compliant/Missing Topics

Run metadata:
- run_id = "{{run_id}}"
- project_profile = "{{project_profile}}" (optional)
- company_internal_standard_id = "{{company_internal_standard_id}}" (optional)
```

**Why this is cache-friendly:** it’s compact, deterministic, and only contains variable configuration—not long instructions.

---

# Prompt 3 — Grounding / Standards Clauses (Injected via retrieval)

> **Purpose:** This is **not** “prompt text you author by hand” each time. It’s a **tool/RAG payload** containing only the relevant clause snippets. Keep it structured and consistent.

```text
GROUNDED_STANDARDS_CLAUSES

Rules for use:
- Only use the clauses below as evidence.
- If a requirement is not evidenced below, mark citation fields as "N/A" and explain the gap.

Clauses:
{{#each clauses}}
[standard_id: {{standard_id}} | version: {{version}} | clause_ref: {{clause_ref}} | source_doc: {{source_doc}}]
{{clause_text}}
{{/each}}
```

---

# Input Tags for “Standards Selection” (What you pass into POLICY)

Below is a practical tag set you can use as the **input contract** from UI → orchestrator → agent.

## A) Minimal tags (recommended baseline)

* `doc_type`
* `standards_selected[]` (array)

  * `standard_id`
  * `version`
  * `priority`
  * `mandatory` (bool)

## B) Strongly recommended metadata (improves routing + retrieval)

* `standards_selected[].title`
* `standards_selected[].jurisdiction`
* `project_profile` (e.g., “UK BIM Framework - FM handover”)
* `company_internal_standard_id` (if you have internal standards)
* `validation_mode` (`strict` / `advisory`)

## C) Scoring control tags (keeps scoring consistent across runs)

* `scoring_method` (e.g., `weighted_by_priority`)
* `mandatory_weight`
* `non_mandatory_weight`
* `critical_fails_immediate`
* `max_major_before_fail`
* `scoring_notes`

## D) Retrieval control tags (helps your RAG inject the right clauses)

* `top_k_clauses_per_standard`
* `clause_min_relevance_score` (if you use one)
* `chunking_profile` (e.g., `heading_paragraph`)
* `query_hints[]` (optional keywords like “COBie”, “Uniclass 2015”, “ISO 19650-4”)

## E) Traceability tags (auditability + debugging)

* `run_id`
* `air_document_name`
* `air_document_version` (optional)
* `standards_bundle_id` (if you package multiple standards into a known bundle)
* `retrieval_timestamp_utc`

---

# Prompt Caching considerations (what to keep stable)

To maximize cache hits:

## Keep stable (in Prompt 1)

* Role + rules
* Output JSON schema
* Strict evidence policy (citations)
* Placeholder handling behavior
* Task/report consistency rules

## Keep small & deterministic (Prompt 2)

* Only configuration values
* **Sorted standards list** (always order by `priority`, then `standard_id`)
* Fixed keys and formatting (avoid free prose)

## Keep out of prompts (Prompt 3 should be tool-fed)

* Long standards text
* Large clause lists beyond top-k
* Anything that changes frequently (timestamps are okay but reduce cache hit-rate; better in metadata logs than in prompt)

---

## Quick mapping from your current “Step 1 checks” to dynamic standards

Your current checks (asset definition, COBie sheets, Uniclass, deliverables, LOI/LOD, roles/RACI, data fields) should **remain as a stable “validation checklist” only if** they truly apply to every run. Otherwise, treat them as *requirements derived from standards clauses* and let them be driven by the retrieved clauses.

A good hybrid is:

* Keep **the checklist structure** stable (so the report is consistent)
* Make the **requirements and pass/fail criteria** come from clauses in Prompt 3

