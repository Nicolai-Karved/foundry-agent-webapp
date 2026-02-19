# Azure AI Foundry production infrastructure checklist

Use this reference when the user requests production-grade setup, security, or governance.

## Identity and access
- Use **Managed Identity** for the agent app.
- Scope **RBAC** to the Foundry project and least-privilege roles.
- Rotate credentials if keys are unavoidable.

## Networking
- Prefer **private endpoints** for AI services.
- Restrict public access and egress where possible.
- Use VNet integration for the app.

## Secrets and configuration
- Store secrets in **Key Vault**.
- Keep config in app settings; avoid hard-coded values.

## Observability and reliability
- Enable **logging** and **tracing** (OpenTelemetry preferred).
- Monitor latency, errors, and token usage.
- Set alerts for quota and rate-limit events.

## Governance
- Document model choice and update cadence.
- Track deployment changes and rollbacks.
- Align with data retention and privacy policies.
