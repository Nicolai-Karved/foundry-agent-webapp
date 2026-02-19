# Revit Manual Testing Skill

Use this skill when validating Revit add-in behavior after making changes to startup, UI, or tool code.

## Pre-Testing Setup

### 1. Build the Add-in
```powershell
dotnet build Source/Naviate.Revit.<App>.sln -c Debug
```

### 2. Attach Debugger (Optional)
1. Set breakpoints in your code
2. Start Revit
3. In Visual Studio: Debug > Attach to Process
4. Select `Revit.exe`
5. Click Attach

## Testing Protocol

### Phase 1: Startup Testing

**Objective**: Verify add-in loads without hanging or errors

1. **Launch Revit**
   - Start timer when Revit icon is clicked
   - Note: Expected time < 5 seconds to fully load

2. **Check Startup Time**
   - [ ] Revit opens without hanging
   - [ ] Total startup time acceptable (< 5 seconds)
   - [ ] No error dialogs appear

3. **Verify Ribbon Panel**
   - [ ] Custom ribbon panel/tab appears
   - [ ] All expected buttons are visible
   - [ ] Icons display correctly (not blank/missing)
   - [ ] Tooltips show on hover

4. **Check Startup Logs**
   - Navigate to `%TEMP%` or configured log location
   - Open most recent log file
   - [ ] No errors or warnings in startup log
   - [ ] All services initialized successfully

**If startup test fails**:
- Check startup logs for exceptions
- Review `OnStartup()` timing
- Use revit-startup skill to diagnose hangs

### Phase 2: UI Testing

**Objective**: Verify all UI components render and respond correctly

1. **Main Window**
   - Click main add-in button to open primary window
   - [ ] Window opens without delay
   - [ ] All UI elements render correctly
   - [ ] Window is properly sized and positioned
   - [ ] No visual artifacts or rendering issues

2. **UI Interaction**
   - [ ] Buttons respond to clicks
   - [ ] Input fields accept text
   - [ ] Dropdowns/combos populate and allow selection
   - [ ] Data grids display data (if applicable)
   - [ ] Progress indicators work (if applicable)

3. **Dialogs** (if applicable)
   - [ ] Modal dialogs open on demand
   - [ ] Dialogs properly block interaction with parent
   - [ ] OK/Cancel buttons work as expected
   - [ ] Dialogs close cleanly

4. **Web UI** (if using WebView)
   - [ ] Web content loads completely
   - [ ] JavaScript console shows no errors (F12 dev tools)
   - [ ] User interactions work (clicks, input, etc.)
   - [ ] Communication with C# backend works

**If UI test fails**:
- Check UI logs for binding errors
- Verify ViewModel properties are correct
- Check Prism registration (use revit-ui-prism skill)

### Phase 3: Tool/Feature Testing

**Objective**: Verify core add-in functionality works correctly

1. **Read Operations**
   - Trigger feature that reads Revit elements
   - [ ] Elements are retrieved correctly
   - [ ] Data displays accurately
   - [ ] No exceptions thrown

2. **Write Operations**
   - Trigger feature that modifies Revit model
   - [ ] Transaction starts successfully
   - [ ] Parameters are written correctly
   - [ ] Changes appear in Revit immediately
   - [ ] Transaction commits successfully
   - [ ] Undo works correctly (Ctrl+Z)

3. **Error Handling**
   - Test invalid inputs or edge cases
   - [ ] Appropriate error messages shown
   - [ ] No crashes or unhandled exceptions
   - [ ] Transactions roll back on error
   - [ ] User can recover and continue working

**If tool test fails**:
- Check tool logs for exceptions
- Verify transaction wrapping (use revit-tool-safety skill)
- Test with simpler model/scenario

## Debugging Techniques

### Using Breakpoints
1. Set breakpoint in Visual Studio
2. Perform action in Revit that hits breakpoint
3. Step through code (F10/F11)
4. Inspect variables in Locals/Watch windows
5. Continue execution (F5)

### Viewing Logs
```powershell
# Open temp folder
explorer %TEMP%

# Or navigate to configured log location
# Look for files named like:
# NaviateRevit_<date>_<time>.log
```

### Using Output Window
- In Visual Studio: View > Output
- Select "Debug" from dropdown
- View Debug.WriteLine() messages from code

### Web UI Debugging (if applicable)
1. Open add-in window with web content
2. Enable dev tools (F12 or context menu)
3. Check Console tab for JavaScript errors
4. Check Network tab for failed requests

## Regression Testing Checklist

After any code change, verify:
- [ ] Startup still works (Phase 1)
- [ ] UI still opens/renders (Phase 2)
- [ ] Core features still work (Phase 3)
- [ ] No new errors in logs
- [ ] Performance hasn't degraded

## Reporting Issues

When filing a bug, include:
1. Steps to reproduce
2. Expected vs actual behavior
3. Relevant log excerpts
4. Screenshots (if UI issue)
5. Revit version
6. Add-in version/build
