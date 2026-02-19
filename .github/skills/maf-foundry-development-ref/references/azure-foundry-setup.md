# Azure AI Foundry setup (quickstart + production)

Use this reference when the user needs to set up or configure Azure AI Foundry resources for a C# Agent Framework app.

## Quickstart (minimal setup)
1. **Choose subscription and resource group**.
2. **Create Azure AI services account** and **Foundry project**.
3. **Select and deploy a model** in Foundry.
4. Capture values needed by the app:
   - Project endpoint: `AZURE_FOUNDRY_PROJECT_ENDPOINT`
   - Deployment name: `AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME`
5. **Auth** (preferred order):
   - Managed Identity (recommended for production)
   - Azure CLI credential for local dev

### Notes
- Do not assume model or deployment names; ask or list available models.
- Use Azure tooling for provisioning and inspection when requested.
- Avoid API keys unless the user explicitly asks for them.

## Production considerations (summary)
- **Identity**: Use Managed Identity + RBAC roles scoped to the Foundry project.
- **Secrets**: Store any secrets in Key Vault if absolutely required.
- **Networking**: Consider private endpoints for AI services; restrict public access.
- **Observability**: Enable logging and tracing; standardize on OpenTelemetry.
- **Governance**: Document model choice and version; track deployment changes.

## When to switch to infrastructure reference
If the user asks for security, compliance, private networking, or monitoring, read:
- references/infra-production.md
