# Revit UI (WPF + Prism) Skill

Use this skill when creating new dialogs or modifying view models in Revit add-in UI projects.

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
    
    public MyDialogViewModel()
    {
        SaveCommand = new DelegateCommand(OnSave);
    }
    
    private void OnSave()
    {
        // Implementation
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
