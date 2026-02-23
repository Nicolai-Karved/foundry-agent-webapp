# Revit UI (WPF + MVVM + Prism) Skill

Use this skill when creating new dialogs or modifying view models in Revit add-in UI projects.

## Mandatory UI Rules

- New or updated UI must use WPF with MVVM and Prism.
- Do not introduce new WinForms components in new development.
- ViewModels orchestrate flow but do not contain business rules or direct Revit model manipulation.
- Business logic and model operations must be delegated to UI-agnostic service classes.

## Creating a New View and ViewModel

### 1. Create the ViewModel
```csharp
// In Naviate.Revit.<App>.UI/ViewModels/
public class MyDialogViewModel : BindableBase
{
    private string _title;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    
    public DelegateCommand SaveCommand { get; }
    private readonly IMyFeatureService _myFeatureService;
    
    public MyDialogViewModel(IMyFeatureService myFeatureService)
    {
        _myFeatureService = myFeatureService;
        SaveCommand = new DelegateCommand(OnSave);
    }
    
    private void OnSave()
    {
        _myFeatureService.Save();
    }
}
```

### 2. Create the View
```xaml
<!-- In Naviate.Revit.<App>.UI/Views/ -->
<Window x:Class="Naviate.Revit.App.UI.Views.MyDialogView"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:prism="http://prismlibrary.com/"
        prism:ViewModelLocator.AutoWireViewModel="True"
        Title="{Binding Title}"
        Height="400" Width="600">
    <Grid>
        <!-- UI Content -->
        <Button Command="{Binding SaveCommand}" Content="Save"/>
    </Grid>
</Window>
```

### 3. Register with Prism Container
```csharp
// In module or app startup
containerRegistry.RegisterForNavigation<MyDialogView>();
```

### 4. Show the Dialog
```csharp
// From command or another view model
var dialog = _container.Resolve<MyDialogView>();
dialog.ShowDialog();
```

## Implementation Checklist
- [ ] ViewModel inherits from `BindableBase`
- [ ] Properties use `SetProperty` for change notification
- [ ] Commands use `DelegateCommand` or `DelegateCommand<T>`
- [ ] View uses `prism:ViewModelLocator.AutoWireViewModel="True"`
- [ ] View and ViewModel follow naming convention (MyView/MyViewModel)
- [ ] UI-specific dependencies stay in UI project
- [ ] Business rules and Revit model manipulation are implemented in service classes
- [ ] Legacy WinForms is not extended in new development
