## TL;DR
Implement **Option B** as a *single, shared Azure AI Search index* with **section/paragraph-level chunking** + metadata (standardId/sectionId) so the same PDFs can power both compliance validation and general search. Implement **Option C** as a *dual-retrieval design* (or dual-index) that adds a **hierarchical “section → paragraph” retrieval stage** for higher precision and lower token cost. In both cases, keep prompts cache-friendly by making a stable prefix (system + invariant instructions) and placing standards selection + retrieved citations at the end.

---

# Assumptions for this plan
- **Standards source**: PDFs (e.g., ISO 19650 series). Other document types (e.g., DOCX) may be used for *comparison/validation outputs*.
- **Requirement**: Standards must be split **exactly** per *section and/or paragraph*.
- **Indexing constraint**: The same standards documents must also remain usable for other search scenarios.

---

# Option B — Single index, multi-purpose, paragraph/section chunking (recommended baseline)
## Architecture
1. **Storage**: Azure Blob Storage container (standards PDFs + docx as needed).
2. **Extraction**: Azure AI Document Intelligence (Layout/Read) to get:
   - page text
   - paragraphs/lines
   - bounding boxes (optional)
3. **Chunking**: Custom chunker that creates **hierarchical units**:
   - `section` chunks (title + section body)
   - `paragraph` chunks (one paragraph each)
4. **Index**: Azure AI Search index with:
   - `content` (chunk text)
   - `contentVector` (embedding)
   - `standardId`, `standardName`, `standardVersion`
   - `sectionId`, `sectionTitle`, `sectionPath` (e.g., `5.3.2`)
   - `paragraphId`, `pageNumber`, `startOffset`, `endOffset`
   - `chunkType` (`section` or `paragraph`)
   - `sourceUri` (blob URL)
5. **Query**: Hybrid retrieval (BM25 + vector) with:
   - **filter**: `standardId in (…)` and (optionally) `chunkType eq 'paragraph'`
   - **exactness**: add a lexical clause (e.g., must-include terms) when required.

## Why Option B works for your constraints
- Single index supports *all* downstream use cases.
- Exact paragraph/section retrieval is achieved through deterministic chunk IDs and offsets.
- Standards selection becomes a **filter**, not prompt text (reduces prompt variability and improves caching).

---

## Implementation steps (Option B)
### 1) Define your index schema
Create an index with fields similar to:
- `id` (key) = `${standardId}|${chunkType}|${sectionId}|${paragraphId}`
- `standardId` (filterable, facetable)
- `standardName` (filterable, facetable)
- `sectionId` (filterable)
- `sectionTitle` (searchable)
- `chunkType` (filterable)
- `pageNumber` (filterable)
- `content` (searchable)
- `contentVector` (vector, searchable)

### 2) Build the indexing pipeline
**Best-practice approach:**
- Use an **Azure Function** (or Container App) as the chunking + enrichment service.
- The indexer pipeline calls your function to return normalized chunks.

**Two common patterns:**
- **Pattern B1 (Recommended)**: run a scheduled ingestion job (Function/Logic App) that:
  1) downloads blob
  2) runs Document Intelligence
  3) chunks into records
  4) computes embeddings
  5) uploads to Search (push indexing)

- **Pattern B2 (Indexer + WebApiSkill)**: use Search Indexer with a skillset that calls a Web API skill for chunking.

### 3) Implement exact section/paragraph splitting
Use Document Intelligence Layout output:
- Detect headings by font size/style when available, otherwise rule-based:
  - heading patterns like `^\d+(\.\d+)*\s+` (e.g., `5.3.2`)
- Build `sectionPath` as a stack of headings.
- Each paragraph gets:
  - stable `paragraphId`
  - `sectionPath` and `sectionId`
  - page reference

### 4) Query integration in Azure Foundry / Agent Framework
- User selects standards → your orchestrator adds **filter** to Search query.
- Specialist agent receives:
  - retrieved paragraphs (with citations)
  - the document under validation
- Agent outputs JSON tasks with citations.

### 5) Prompt caching considerations (Option B)
To maximize caching:
- Keep **System prompt** constant.
- Keep **Specialist agent instructions** constant.
- Put **standards selection** into:
  - Search filter (preferred)
  - or a small JSON list at the end of the user message.
- Put **retrieved evidence** after instructions (so the prefix remains stable).

---

## Minimal C# sketch (Option B): push indexing
> This is a concise example of pushing chunk docs into Azure AI Search after you’ve chunked the standard.

```csharp
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;

public record StandardChunkDoc(
    string Id,
    string StandardId,
    string StandardName,
    string ChunkType,
    string SectionId,
    string SectionTitle,
    string ParagraphId,
    int PageNumber,
    string Content,
    float[] ContentVector,
    string SourceUri
);

public static class SearchIngest
{
    public static async Task UploadAsync(
        Uri endpoint,
        string indexName,
        AzureKeyCredential cred,
        IEnumerable<StandardChunkDoc> docs)
    {
        var client = new SearchClient(endpoint, indexName, cred);

        // Upload in batches
        const int batchSize = 500;
        foreach (var batch in docs.Chunk(batchSize))
        {
            var actions = batch.Select(d => IndexDocumentsAction.Upload(d)).ToArray();
            var result = await client.IndexDocumentsAsync(IndexDocumentsBatch.Create(actions));

            // Optional: inspect failures
            if (result.Value.Results.Any(r => !r.Succeeded))
            {
                var failures = result.Value.Results.Where(r => !r.Succeeded)
                    .Select(r => $"{r.Key}: {r.ErrorMessage}");
                throw new Exception("Indexing failed: " + string.Join("; ", failures));
            }
        }
    }
}
```

---

# Option C — Hierarchical retrieval (section → paragraph) for precision + lower cost
Option C assumes you keep the same chunked content, but add a *two-stage* retrieval pattern.

## Architecture (two common variants)
### C1) Single index, two-stage query
- Same index as Option B, but you query in two steps:
  1) Retrieve **top sections** (`chunkType='section'`) for the selected standards.
  2) For the best N sections, retrieve **paragraphs** (`chunkType='paragraph'`) filtered by `sectionId in (…)`.

### C2) Two indexes (shared source)
- **Standards-Section index**: larger section chunks → cheap broad recall.
- **Standards-Paragraph index**: paragraph chunks → high precision.
- Both indexes are fed from the same blob source (so you don’t duplicate the raw documents), but you *do* duplicate chunked representations.

## Why Option C is useful
- Higher precision for “exact paragraph/section” citation.
- Lower token and compute cost because paragraph retrieval is bounded to the most relevant sections.
- Better controllability when standards are large.

---

## Implementation steps (Option C)
### 1) Ensure both section and paragraph chunks exist
Even if you keep a single index, generate both `chunkType='section'` and `chunkType='paragraph'` entries.

### 2) Add a query orchestrator step
In your Foundry workflow (or Agent Framework orchestrator):
1. Call Search for sections (filter by selected standards).
2. Build a shortlist of section IDs.
3. Call Search again for paragraphs filtered by those section IDs.
4. Pass only the final paragraphs to the specialist agent.

### 3) Prompt caching considerations (Option C)
- The two-stage retrieval reduces variability in the final prompt because:
  - fewer paragraphs are injected
  - the injected evidence is more stable for similar questions
- Keep the *agent prefix* identical across requests; only vary:
  - `SelectedStandards[]`
  - `Evidence[]`

---

## Minimal C# sketch (Option C): two-stage retrieval
```csharp
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

public static class StandardsQuery
{
    public static async Task<(IReadOnlyList<SearchResult<SectionDoc>> sections,
                              IReadOnlyList<SearchResult<ParagraphDoc>> paragraphs)>
        QueryHierarchicalAsync(
            SearchClient sectionClient,
            SearchClient paragraphClient,
            string userQuery,
            IReadOnlyList<string> standardIds,
            int topSections = 5,
            int topParagraphs = 30)
    {
        // 1) Sections
        var standardsFilter = string.Join(" or ", standardIds.Select(id => $"standardId eq '{id}'"));
        var sectionOptions = new SearchOptions
        {
            Filter = $"({standardsFilter}) and chunkType eq 'section'",
            Size = topSections,
            QueryType = SearchQueryType.Semantic, // or Simple + vector hybrid
        };

        var sectionResp = await sectionClient.SearchAsync<SectionDoc>(userQuery, sectionOptions);
        var sections = await sectionResp.Value.GetResultsAsync().ToListAsync();

        var sectionIds = sections
            .Select(s => s.Document.SectionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToArray();

        if (sectionIds.Length == 0)
            return (sections, Array.Empty<SearchResult<ParagraphDoc>>());

        // 2) Paragraphs (bounded)
        var sectionFilter = string.Join(" or ", sectionIds.Select(id => $"sectionId eq '{id}'"));
        var paragraphOptions = new SearchOptions
        {
            Filter = $"({standardsFilter}) and chunkType eq 'paragraph' and ({sectionFilter})",
            Size = topParagraphs,
            QueryType = SearchQueryType.Semantic,
        };

        var paragraphResp = await paragraphClient.SearchAsync<ParagraphDoc>(userQuery, paragraphOptions);
        var paragraphs = await paragraphResp.Value.GetResultsAsync().ToListAsync();

        return (sections, paragraphs);
    }

    public record SectionDoc(string SectionId, string SectionTitle, string Content);
    public record ParagraphDoc(string ParagraphId, string SectionId, int PageNumber, string Content);
}
```

---

# Standards selection: input tags (for UI + orchestration)
Use these tags as the stable interface between the UI / orchestrator and the agents.

## Core tags (recommended)
- `standards.selected[]` — array of standard IDs (stable keys)
- `standards.mode` — `"strict" | "balanced"` (strict = exact match emphasis)
- `standards.scope` — `"section" | "paragraph" | "both"`
- `standards.editionPreference` — e.g., `"latest" | "specified"`
- `standards.language` — `"en" | "da" | ...`

## Retrieval control tags
- `retrieval.topSections` — integer (Option C)
- `retrieval.topParagraphs` — integer
- `retrieval.hybridWeight` — 0..1 (vector vs lexical)
- `retrieval.requireCitations` — boolean
- `retrieval.maxEvidenceTokens` — integer

## Output control tags
- `output.format` — `"json"` (for your current compliance schema)
- `output.includeEvidence` — boolean
- `output.taskSeverityModel` — `"low/med/high"` or your internal taxonomy

## Suggested standard ID convention
Use a canonical ID to avoid prompt variability:
- `ISO19650-0:2019`
- `ISO19650-1:2018`
- `ISO19650-2:2018`
- `ISO19650-4:2022`
- `UKBIMF-COBie-2012`
- `Uniclass2015`

---

# Practical notes for your AIR specialist agent prompt
Your existing specialist prompt is quite prescriptive (JSON schema + citations). Keep it, but make *standards dynamic* by:
1. **Do not hardcode** the standard list in the instructions.
2. Make “sources” explicit inputs:
   - `SelectedStandards[]` (IDs)
   - `Evidence[]` (retrieved chunks with metadata)
3. Require citations to be taken only from `Evidence[]`.

This aligns well with ISO 19650’s emphasis on clear requirements and structured information deliverables. fileciteturn1file1

---

# Deliverables checklist
## Option B deliverables
- Search index schema (single index)
- Ingestion job (Function/Container App)
- Chunking rules + tests (section/paragraph exactness)
- Embedding generation + retry policy
- Foundry workflow integration (filter by selected standards)

## Option C deliverables
- Same as B, plus:
- Two-stage retrieval orchestrator
- Section shortlist logic + evaluation harness
- Cost/latency baselines vs Option B

---

# Recommended rollout sequence
1. Implement **Option B** end-to-end and validate citation correctness.
2. Add **Option C** two-stage retrieval only if:
   - standards are large
   - recall/precision trade-offs are painful
   - token cost becomes material

