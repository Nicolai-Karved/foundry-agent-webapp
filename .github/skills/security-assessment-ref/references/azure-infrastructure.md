# Azure infrastructure (repo-verifiable)

## Identity and access
- IaC defines role assignments and least-privilege scopes.
- Managed identities or service principals are used with scoped permissions.

## Encryption and key management
- IaC/config enforces encryption at rest and TLS for in-transit data.
- Key management uses Key Vault or equivalent with references in config.

## Monitoring and security controls
- IaC/config enables logging/monitoring (e.g., diagnostics settings).
- Security scanning or policy-as-code configured in repo.

## Dev/test controls
- IaC/config indicates separated environments or distinct resource groups/subscriptions.
- Secrets are referenced from managed secret stores rather than inline values.
