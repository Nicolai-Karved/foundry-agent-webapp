# AppSec (secure development, code review, testing)

## SDLC security integration (repo-verifiable)
- Secure coding guidelines exist in-repo and cover supported languages.
- Dependency manifests and lockfiles are present and tracked.

## Environment controls (repo-verifiable)
- IaC or config indicates separate environments or environment-specific settings.
- Secret references use managed secret stores (e.g., Key Vault references) rather than inline secrets.

## Code review and CI security (repo-verifiable)
- CI pipelines include SAST/SCA or dependency scanning steps.
- Repository contains review checklists or contribution guidelines for security (if present).

## Security testing (repo-verifiable)
- Security test suites or test targets exist for critical components.
- Static analysis configurations are checked into the repo.
