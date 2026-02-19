# Revit Add-in Build & Deploy Skill

Use this skill when setting up a new Revit add-in project for debugging or troubleshooting deployment issues.

## Setting Up Debug Deployment

### Step 1: Configure Main Project
1. Open the main add-in project file (`.csproj`)
2. Add or verify `IsMainProject` property:
```xml
<PropertyGroup>
  <IsMainProject>true</IsMainProject>
</PropertyGroup>
```
3. This flag enables automatic manifest copying during debug builds

### Step 2: Verify .addin Manifest
1. Locate manifest file in `Resources` folder (e.g., `MyApp.addin`)
2. Ensure manifest content is correct:
```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>MyApp</Name>
    <Assembly>Naviate.Revit.MyApp.dll</Assembly>
    <FullClassName>Naviate.Revit.MyApp.Application</FullClassName>
    <ClientId>YOUR-GUID-HERE</ClientId>
    <VendorId>SYMO</VendorId>
    <VendorDescription>Symetri</VendorDescription>
  </AddIn>
</RevitAddIns>
```
3. Verify the manifest is set to `Copy to Output Directory`

### Step 3: Verify Build Targets
1. Check that `Directory.Build.targets` exists at solution level
2. Confirm it contains manifest deployment logic
3. The build system will:
   - Copy `.addin` to Revit Addins folder
   - Rewrite assembly path to point at build output

### Step 4: Set Revit Version
1. Open `Directory.Build.props`
2. Verify `TargetRevitDependency` matches your target Revit:
```xml
<TargetRevitDependency>2024</TargetRevitDependency>
```
3. Ensure `RevitVersion` is also set correctly

## Building the Add-in

### For Debug Testing
```powershell
# Build solution
dotnet build Source/Naviate.Revit.<App>.sln -c Debug

# Manifest should now be in:
# %APPDATA%\Autodesk\Revit\Addins\<version>\MyApp.addin
```

### For Release
```powershell
dotnet build Source/Naviate.Revit.<App>.sln -c Release
```

## Troubleshooting Deployment

### Add-in Not Appearing in Revit
1. Check Revit Addins folder:
   - Path: `%APPDATA%\Autodesk\Revit\Addins\<version>\`
   - Verify `.addin` file exists
   - Open `.addin` and verify `<Assembly>` path is correct
2. Check build output for warnings/errors
3. Verify `IsMainProject` is set in main project
4. Rebuild solution and check for MSBuild target execution

### Wrong Assembly Path in Manifest
1. Delete `.addin` from Revit Addins folder
2. Rebuild solution (manifest will be regenerated)
3. Verify path now points to build output directory

### Multiple Revit Versions
1. Ensure only one `TargetRevitDependency` is active
2. Clean solution before switching versions
3. Rebuild to regenerate manifest for new version

## Verification Checklist
- [ ] `IsMainProject` set to `true` in main add-in project
- [ ] `.addin` file exists in `Resources` folder
- [ ] `.addin` is set to copy to output directory
- [ ] `TargetRevitDependency` matches intended Revit version
- [ ] Build completes without errors
- [ ] `.addin` appears in Revit Addins folder after build
- [ ] Assembly path in deployed `.addin` points to build output
- [ ] Add-in appears in Revit after launch
