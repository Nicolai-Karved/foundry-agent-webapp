# Microsoft Agent Framework (C#) with Azure AI Foundry

Use this reference when building the **C#** agent application using Microsoft Agent Framework and Foundry.

## Package guidance (C#)
Use package install commands (do not hand-edit csproj).
- Required:
  - `Azure.Identity`
  - `Microsoft.Agents.AI.AzureAI --prerelease`
- Optional (workflows):
  - `Microsoft.Agents.AI.Workflows --prerelease`

**Important:** The `--prerelease` flag is required.

## Client and agent choice
- **Use `AIProjectClient`** for Foundry agents.
- **Do NOT** use `AzureOpenAIClient` or `PersistentAgentsClient` for Foundry.

## Minimal agent pattern (outline)
1. Read `AZURE_FOUNDRY_PROJECT_ENDPOINT` and `AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME` from environment.
2. Create `AIProjectClient` with `AzureCliCredential` (local dev) or Managed Identity (prod).
3. Create an agent with instructions and tools if needed.
4. Run the agent and stream results if desired.
5. Clean up server-side agents if created for demos.

## Common pitfalls
- Using the wrong client (AzureOpenAIClient) for Foundry.
- Forgetting `--prerelease` when installing packages.
- Mixing Python samples into C# code.

## Verification
If the user asks to verify, run `dotnet build` and fix errors before claiming completion.
