---
name: revit-development-ref
description: Detailed reference for Revit add-in development covering setup, build/deploy, package management, UI, startup optimization, safety, and testing.
---

# Revit Add-in Development Reference

Use this skill when you need detailed, step-by-step procedures for specific Revit add-in development tasks.

## Scope
This reference provides comprehensive procedures for:
- Project setup and solution navigation
- Build and deployment configuration
- NuGet package management with centralized versioning
- WPF/Prism UI development
- Startup performance optimization and hang diagnosis
- Safe Revit operations with transactions
- Systematic testing and validation

## Content Map

Read only what you need based on the task at hand:

| Reference File | When to Read |
|----------------|--------------|
| references/setup-and-structure.md | Setting up new projects, verifying project configuration, understanding solution structure |
| references/build-deploy.md | Configuring build/deploy, troubleshooting deployment, setting up debug environment |
| references/package-management.md | Adding/updating NuGet packages, managing dependencies, switching Revit versions |
| references/ui-development.md | Creating WPF views and view models with Prism, wiring up MVVM patterns |
| references/startup-performance.md | Diagnosing startup hangs, fixing blocking calls, optimizing initialization |
| references/tool-safety.md | Implementing safe Revit operations, transaction patterns, parameter validation |
| references/testing-validation.md | Systematic testing protocols, debugging techniques, regression testing |

## Typical Workflow

1. **Start with your task**: Identify what you need to do (e.g., "fix startup hang")
2. **Read the relevant reference**: Use the content map above to find the right file
3. **Follow the procedures**: Each reference contains step-by-step instructions
4. **Verify your work**: Run build/test commands to confirm success

## Quick Reference Checklists

### New Project Setup
- [ ] C# language version matches target Revit
- [ ] Revit SDK referenced and resolvable
- [ ] Solution follows Naviate multi-project structure
- [ ] Main project has `IsMainProject` set
- [ ] `.addin` manifest exists in Resources

### Before Committing Code
- [ ] Build succeeds without errors
- [ ] Startup time < 5 seconds
- [ ] No blocking calls in `OnStartup()`
- [ ] All write operations wrapped in transactions
- [ ] Package versions only in `Directory.Packages.props`
- [ ] No secrets or credentials committed

### Before Reporting a Bug
- [ ] Can reproduce the issue consistently
- [ ] Checked logs for error details
- [ ] Verified Revit and add-in versions
- [ ] Noted expected vs actual behavior
- [ ] Captured relevant screenshots

## Related Guidance
For agent-based development with full context, use the **@RevitDevelopment** custom agent.
