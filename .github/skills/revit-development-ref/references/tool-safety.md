# Revit Tool Safety Skill

Use this skill when implementing or modifying Revit tools that read/write model data.

## Safe Tool Implementation Procedure

### 1. Parameter Validation
```csharp
public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
{
    // Validate inputs first
    if (commandData?.Application?.ActiveUIDocument == null)
    {
        message = "No active document";
        return Result.Failed;
    }
    
    var doc = commandData.Application.ActiveUIDocument.Document;
    // Continue with validated inputs...
}
```

### 2. Transaction Pattern for Writes
```csharp
using (Transaction trans = new Transaction(doc, "Operation Name"))
{
    try
    {
        trans.Start();
        
        // Perform Revit write operations here
        
        trans.Commit();
        return Result.Succeeded;
    }
    catch (Exception ex)
    {
        trans.RollBack();
        message = $"Operation failed: {ex.Message}";
        return Result.Failed;
    }
}
```

### 3. User Confirmation for Destructive Operations
```csharp
// Before deleting elements
var result = TaskDialog.Show(
    "Confirm Delete",
    $"Delete {elementsToDelete.Count} elements?",
    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
);

if (result != TaskDialogResult.Yes)
    return Result.Cancelled;
```

## Implementation Checklist
- [ ] Validate all parameters before Revit API calls
- [ ] Wrap write operations in transactions
- [ ] Implement rollback on failure
- [ ] Confirm destructive operations with user
- [ ] Provide clear error messages
- [ ] Log errors for debugging
