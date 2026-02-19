# Revit Add-in Basics Skill

Use this skill when setting up a new Revit add-in project or verifying project configuration.

## Setup Verification Checklist

### Language & SDK
1. Confirm project targets C#
2. Verify C# language version matches target Revit version compatibility
3. Ensure Revit SDK reference is present and resolvable
4. Check that Revit API types can be resolved in the IDE

### Testability Setup
1. Define interfaces for services that interact with Revit API
2. Use dependency injection for testable architecture
3. Keep business logic separate from Revit API calls where possible


---

# Revit Solution Structure Skill

Use this skill when you need to find where specific code should live or understand the project organization.

## Finding the Right Project

### When to use each project:
- **Main add-in project** (`Naviate.Revit.<App>`)
  - Entry point (IExternalApplication, IExternalCommand)
  - Ribbon registration
  - External event handlers
  
- **Business logic** (`Naviate.Revit.<App>.BusinessLogic`)
  - Core feature implementation
  - Domain models
  - Business rules
  
- **Common** (`Naviate.Revit.<App>.Common`)
  - Shared utilities
  - Helper classes
  - Extension methods
  
- **UI** (`Naviate.Revit.<App>.UI`)
  - WPF views and view models
  - Prism modules
  - UI-specific logic
  
- **Resources**
  - .addin manifests
  - Icons and images
  - Embedded resources

## Quick Navigation
1. Start at `*/Source/Naviate.Revit.<App>.sln`
2. Entry points are in the main add-in project
3. Feature logic is in BusinessLogic
4. UI code is in the UI project
