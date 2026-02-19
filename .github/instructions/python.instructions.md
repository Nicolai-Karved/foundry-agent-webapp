---
description: Python rules (dependencies, FastAPI patterns, container-first when applicable)
applyTo: "**/*.py"
---

# Python Rules

Use these rules for all Python code. For product-specific guidance, use Skills instead of adding product rules here.

## General
- Follow existing project structure and patterns
- Keep Python version aligned with the project’s configured version

## Dependency Management
- Use the repo’s existing dependency tooling (e.g., Poetry + requirements.txt if present)
- Avoid ad-hoc `pip install` changes unless instructed

## Container-First (when the service is containerized)
- Ensure the app runs in Docker with dependencies baked into the image
- Avoid relying on system Python on the host

## FastAPI (if used)
- Follow existing routing and dependency injection patterns
- Keep configuration in environment variables and settings modules

## Azure Identity (if applicable)
- Prefer Managed Identity for Azure service access whenever supported
- Avoid API keys unless explicitly required