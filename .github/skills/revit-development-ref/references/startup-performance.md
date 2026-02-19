# Revit Startup Safety Skill

Use this skill when modifying `OnStartup()`, `ConfigureServices()`, or any add-in initialization code, or when debugging startup hangs.

## Diagnosing a Startup Hang

### Step 1: Identify the Blocking Call
1. Attach debugger to Revit during startup
2. When hang occurs, pause execution (Break All)
3. Inspect all threads and call stacks
4. Look for these common culprits:
   - HTTP requests (HttpClient, WebRequest)
   - Credential manager access (CredentialManager.Read)
   - ExternalEvent creation in constructors
   - Heavy file I/O in static constructors

### Step 2: Check Startup Logs
1. Navigate to `%TEMP%` directory
2. Find recent startup error logs
3. Look for timeout or exception messages

## Fixing Common Startup Issues

### Issue: HTTP Calls During Startup
```csharp
// ❌ WRONG: Blocks startup
public Result OnStartup(UIControlledApplication app)
{
    var data = httpClient.GetAsync("https://api.example.com").Result;
    return Result.Succeeded;
}

// ✅ CORRECT: Defer until needed
private Task<Data> _dataTask;

public Result OnStartup(UIControlledApplication app)
{
    // Just register UI, no blocking calls
    RegisterRibbonButtons(app);
    return Result.Succeeded;
}

private async void OnButtonClicked()
{
    // Fetch data on-demand
    var data = await httpClient.GetAsync("https://api.example.com");
}
```

### Issue: Credential Access During Startup
```csharp
// ❌ WRONG: Blocks startup
public void ConfigureServices(IServiceCollection services)
{
    var creds = CredentialManager.ReadCredential("MyApp");
    services.AddSingleton(new AuthService(creds));
}

// ✅ CORRECT: Lazy initialization
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IAuthService>(sp => 
        new Lazy<AuthService>(() => 
        {
            var creds = CredentialManager.ReadCredential("MyApp");
            return new AuthService(creds);
        }).Value
    );
}
```

### Issue: ExternalEvent in Constructor
```csharp
// ❌ WRONG: Creates during construction
public class MyService
{
    private readonly ExternalEvent _event;
    
    public MyService()
    {
        _event = ExternalEvent.Create(new MyHandler());
    }
}

// ✅ CORRECT: Lazy creation
public class MyService
{
    private ExternalEvent _event;
    
    public ExternalEvent GetEvent()
    {
        if (_event == null)
            _event = ExternalEvent.Create(new MyHandler());
        return _event;
    }
}
```

## Optimization Checklist
- [ ] No HTTP calls in `OnStartup()` or `ConfigureServices()`
- [ ] No credential manager access during initialization
- [ ] No `ExternalEvent` creation in constructors
- [ ] Heavy operations deferred to background tasks
- [ ] Static constructors are lightweight
- [ ] Total startup time < 2 seconds
- [ ] `ConfigureServices()` time < 500ms

## Performance Verification
1. Add timing instrumentation:
```csharp
var sw = Stopwatch.StartNew();
// ... startup code ...
sw.Stop();
Debug.WriteLine($"Startup completed in {sw.ElapsedMilliseconds}ms");
```
2. Launch Revit and check debug output
3. Verify time is under target threshold
