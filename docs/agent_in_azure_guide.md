# Agent in Azure Guide

This guide explains how to set up a **service principal (SP)** for an AI/automation agent in Azure with **least privilege** and predictable behavior across CLI and VS Code tooling.

Use this for any agent and any resource group by replacing placeholders.

---

## Why this pattern

- Keeps agent access scoped to only what it needs.
- Avoids using personal user accounts in automation.
- Makes permissions auditable and repeatable.

---

## Prerequisites

- Azure CLI installed and authenticated as an admin/operator who can create app registrations and role assignments.
- Target subscription ID and resource group name.
- Clear list of operations the agent must perform (read-only, deploy, query logs, etc.).

---

## 1) Define your target scope

Use a **resource group scope** when possible:

- Subscription: `<subscription_id>`
- Resource Group: `<resource_group_name>`
- Scope string:
  `/subscriptions/<subscription_id>/resourceGroups/<resource_group_name>`

> Prefer resource-group scope over subscription scope to reduce blast radius.

---

## 2) Create a service principal and assign role

### PowerShell (single line)

`az ad sp create-for-rbac --name "<sp_name>" --role "<role_name>" --scopes "/subscriptions/<subscription_id>/resourceGroups/<resource_group_name>"`

### Bash (multiline)

`az ad sp create-for-rbac \
  --name "<sp_name>" \
  --role "<role_name>" \
  --scopes "/subscriptions/<subscription_id>/resourceGroups/<resource_group_name>"`

> **Important:** In PowerShell, `\` line continuation does not work as in Bash. Use one line, or use PowerShell backtick if needed.

Expected output shape:

```json
{
  "appId": "<client_id>",
  "password": "<client_secret>",
  "tenant": "<tenant_id>"
}
```

Save the secret securely. The password is shown only once.

---

## 3) Set secret lifetime (example: 3 months)

If you need a specific validity window, add a new credential with an end date:

`az ad app credential reset --id "<client_id>" --display-name "<secret_name>" --end-date (Get-Date).AddMonths(3).ToString("yyyy-MM-dd") --query "{appId:appId,password:password,tenant:tenant}" -o json`

Notes:
- `--append` adds a new secret without deleting existing secrets. Default is to remove old secrets)
- Rotate old secrets out after consumers are updated.

---

## 4) Sign in as service principal (Azure CLI)

`az login --service-principal --username "<client_id>" --password "<client_secret>" --tenant "<tenant_id>"`

Verify active identity:

`az account show --query "{user:user.name,userType:user.type,tenant:tenantId,subscription:id,subscriptionName:name}" -o json`

Expected: `"userType": "servicePrincipal"`.

---

## 5) Verify effective permissions

List role assignments for the SP:

`az role assignment list --assignee "<client_id>" --all --query "[].{role:roleDefinitionName,scope:scope}" -o table`

You should only see scopes you explicitly granted.

---

## 6) Export accessible resource inventory (audit artifact)

Create a local artifact for review:

`$outDir = "docs/artifacts"; New-Item -ItemType Directory -Force -Path $outDir | Out-Null; $stamp = Get-Date -Format "yyyy-MM-dd_HHmmss"; $outFile = Join-Path $outDir "azure-resource-inventory-$stamp.json"; az graph query -q "resources | project name, type, subscriptionId, resourceGroup, location, id" --first 1000 -o json > $outFile; Write-Output $outFile`

Notes:
- `--first` maximum is `1000` per query.
- For larger estates, page results and merge artifacts.

---

## 7) Understand CLI vs VS Code Azure extension identity

These are **different authentication contexts**:

- **Azure CLI context**: used by `az` commands in terminal.
- **Azure extension context**: used by VS Code Azure extension tooling.

You can be logged in as SP in CLI while still signed in as a personal user in VS extension.

If your goal is strict least privilege for agent tooling, avoid leaving a broad user signed in where extension-based Azure tools can use it.

---

## 8) Sign out Azure extension account (if needed)

To sign out in VS Code:

1. Select the **Accounts** button in the vertical toolbar.
2. Find your account in the list of accounts.
3. Select the **Sign Out** option for your account.

---

## 9) Recommended minimum-permission strategy

1. Start with smallest scope (resource group).
2. Start with least-privilege role, not `Contributor`, unless required.
3. Validate required operations.
4. Add only missing permissions/roles.
5. Re-check role assignments and resource inventory artifact.

---

## 10) Security checklist

- Never commit `client_secret` to source control.
- Treat terminal output with secrets as sensitive.
- Store secrets in Key Vault or secure secret store.
- Rotate secrets regularly and on exposure.
- Remove stale credentials from app registration.
- Prefer managed identity over client secrets where supported.

---

## Quick template values

- SP name: `<sp_name>`
- Tenant: `<tenant_id>`
- Subscription: `<subscription_id>`
- Resource Group: `<resource_group_name>`
- Scope: `/subscriptions/<subscription_id>/resourceGroups/<resource_group_name>`
- Role: `<role_name>`

---

## Troubleshooting

### `Missing expression after unary operator '--'`
You likely used Bash-style `\` multiline continuation in PowerShell. Use a single line or PowerShell continuation style.

### Secret not shown again
Azure does not let you read existing secret values. Create a new secret with `az ad app credential reset`.

### Inventory query returns fewer results than expected
`az graph query --first` is capped at 1000. Use pagination for full export.
