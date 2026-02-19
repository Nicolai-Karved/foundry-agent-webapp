# Revit Central Package Management Skill

Use this skill when adding or updating dependencies for Revit add-ins.

## Understanding Centralized Package Management

Package versions are controlled centrally to ensure consistency across all projects:
- **`Directory.Packages.props`**: Single source of truth for all package versions
- **`Directory.Build.props`**: Shared properties (TargetRevitDependency, LangVersion, etc.)
- **`Directory.Build.targets`**: Shared build behavior

## Adding a New Package

### Step 1: Add Package Reference to Project
```xml
<!-- In project .csproj file -->
<ItemGroup>
  <PackageReference Include="Newtonsoft.Json" />
</ItemGroup>
```
Note: **No version specified** in the project file.

### Step 2: Add Version to Directory.Packages.props
```xml
<!-- In Directory.Packages.props at solution root -->
<ItemGroup>
  <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
</ItemGroup>
```

### Step 3: Restore and Build
```powershell
dotnet restore
dotnet build
```

## Updating an Existing Package

### Step 1: Locate Version in Directory.Packages.props
Find the package entry:
```xml
<PackageVersion Include="Prism.Wpf" Version="8.1.97" />
```

### Step 2: Update Version
```xml
<PackageVersion Include="Prism.Wpf" Version="9.0.271" />
```

### Step 3: Restore and Rebuild
```powershell
dotnet restore
dotnet build
```

## Understanding Revit-Versioned Packages

Some packages are tied to Revit versions (e.g., Revit API packages):

### Controlled by TargetRevitDependency
```xml
<!-- In Directory.Build.props -->
<PropertyGroup>
  <TargetRevitDependency>2024</TargetRevitDependency>
</PropertyGroup>

<!-- In Directory.Packages.props -->
<ItemGroup>
  <PackageVersion Include="Revit_All_Main_Versions_API_x64" 
                  Version="$(TargetRevitDependency).0.0" />
</ItemGroup>
```

### Switching Revit Versions
1. Update `TargetRevitDependency` in `Directory.Build.props`
2. Clean solution: `dotnet clean`
3. Restore and rebuild: `dotnet restore && dotnet build`

## Troubleshooting Package Issues

### Package Version Conflict
**Symptom**: Build warnings about version conflicts

**Solution**:
1. Check if version is specified in both project and Directory.Packages.props
2. Remove version from project file (keep only in Directory.Packages.props)
3. Restore and rebuild

### Package Not Found
**Symptom**: NU1101 - Unable to find package

**Solution**:
1. Verify package name spelling in `Directory.Packages.props`
2. Check that package exists on NuGet.org or configured feeds
3. Clear NuGet cache: `dotnet nuget locals all --clear`
4. Restore again

### Wrong Package Version Loaded
**Symptom**: Intellisense shows different version than expected

**Solution**:
1. Verify version in `Directory.Packages.props`
2. Check for per-project overrides (shouldn't exist)
3. Delete `bin` and `obj` folders
4. Reload solution in IDE
5. Restore and rebuild

## Best Practices Checklist
- [ ] Package versions only in `Directory.Packages.props` (never in project files)
- [ ] No `Version="..."` attributes in project `<PackageReference>` elements
- [ ] Revit-versioned packages use `$(TargetRevitDependency)` property
- [ ] All projects in solution use same package versions
- [ ] Changes to package versions reviewed (check for breaking changes)

## Quick Reference Commands
```powershell
# List all packages and their versions
dotnet list package

# Restore packages
dotnet restore

# Clear NuGet cache
dotnet nuget locals all --clear

# Check for outdated packages
dotnet list package --outdated
```
