# Database security (repo-verifiable)

## Access control
- Database users/roles are defined in IaC or migration scripts with least-privilege roles.
- Application connection strings use managed identities or secret references.

## Encryption and key management
- Database encryption-at-rest and TLS settings are enforced in IaC/config.
- Key references use Key Vault or equivalent secret stores.

## Dev/test data handling
- Test data seeding scripts avoid production data and use synthetic/anonymized data.
- Secrets for database access are referenced from managed secret stores.
