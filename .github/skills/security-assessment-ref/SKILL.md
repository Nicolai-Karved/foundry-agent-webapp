---
name: security-assessment-ref
description: Governance-aligned security assessment guidance covering AppSec, Auth/Session, Azure, databases, SDLC governance, threat modeling, and vulnerability management.
---

# Security Assessment Reference

Use this skill for all security assessment topics. Governance requirements live in reference files so runtime access to resources/security is not required.

## Scope rule
Only include checks that can be verified from codebase, database configuration, or infrastructure-as-code/configuration in the repo.

## Governance document index (SharePoint - narrowed)
Common base path:
https://symetrigroup.sharepoint.com/sites/IQ/Management%20System%20Documents/Forms/AllItems1.aspx

Document names:
- Secure Development Policy
- Code Review Procedure
- Development Vulnerability Management Procedure
- Access Control Policy
- Cryptography Policy
- Test and Development Data Handling Procedure
- System Policy
- Threat Modelling

## OWASP guidance
If governance does not define a control, consult OWASP project best practices:
- https://owasp.org/projects/
- https://owasp.org/www-project-top-ten/

## References (topic details)
- references/appsec.md
- references/auth-session.md
- references/azure-infrastructure.md
- references/database.md
- references/threat-modeling.md
- references/vulnerability-management.md
- references/evidence-reporting.md
