# Search Artifacts Setup

Use the script in `deployment/scripts/apply-search-artifacts.ps1` to apply the paragraph index, datasource, skillset, and indexer.

## Required environment values

- `SEARCH_SERVICE_NAME` (e.g. srch-bim-documentation)
- `SEARCH_STORAGE_CONNECTION_STRING` (connection string for the blob account hosting `bim-standards`)
- `AZURE_OPENAI_RESOURCE_URI` (e.g. https://aif-naviate-agent-dev.openai.azure.com)
- `AZURE_OPENAI_DEPLOYMENT_ID` (e.g. text-embedding-3-large)
- `AZURE_OPENAI_MODEL_NAME` (e.g. text-embedding-3-large)

The script will reuse `AZURE_RESOURCE_GROUP_NAME` and `AZURE_SUBSCRIPTION_ID` from `azd env`.

## Artifacts

- Index: `deployment/search/bim-standards-paragraph-index.json`
- Skillset: `deployment/search/bim-standards-paragraph-skillset.json`
- Datasource: `deployment/search/knowledgesource-bim-standards-datasource.json`
- Indexer: `deployment/search/knowledgesource-bim-standards-indexer.json`

## Notes

- The indexer name matches the existing indexer, so this will replace the current configuration.
- The datasource name matches the existing datasource, so this will replace the current configuration.

## Local ingestion (PDFs → JSONL → Indexer)

Use `deployment/scripts/ingest-docs-to-search.ps1` to upload PDFs from `docs/sources/`, wait for `cu-output/*.cu.jsonl`, and trigger the indexer.

### Prerequisites

- Local Functions host running for `BimCuIngest` (task `func: 9` in `.vscode/tasks.json`).
- Azure CLI logged in (`az login` or `azd auth login`).
- `.env` (or environment variables) populated with:
	- `DocumentsStorage`
	- `SEARCH_STORAGE_CONNECTION_STRING` (fallback if `DocumentsStorage` not set)
	- `SEARCH_SERVICE_NAME`

### Script usage

- Default source folder: `docs/sources/`
- Default container/prefixes: `bim-standards/source/` → `bim-standards/cu-output/`
- Indexer: `knowledgesource-bim-standards-indexer`
