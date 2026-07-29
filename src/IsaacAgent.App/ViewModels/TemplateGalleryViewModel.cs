using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.App.Services;
using IsaacAgent.Core.Models;

namespace IsaacAgent.App.ViewModels;

/// <summary>
/// View model for the Mod Template Gallery.
/// Lets users browse built-in templates and scaffold new projects.
/// </summary>
public sealed partial class TemplateGalleryViewModel : ObservableObject
{
    private readonly IScaffoldingService _scaffolding;

    [ObservableProperty]
    private ModTemplate? _selectedTemplate;

    [ObservableProperty]
    private string _modName = "";

    [ObservableProperty]
    private string _modDescription = "";

    [ObservableProperty]
    private string _modAuthor = "";

    [ObservableProperty]
    private string _statusMessage = "";

    public ObservableCollection<ModTemplate> Templates { get; } = new(ModTemplates.All);

    /// <summary>
    /// Called by the view when the scaffold button is clicked.
    /// The view handles folder selection and calls ScaffoldIntoAsync.
    /// </summary>
    public Func<Task>? ScaffoldRequested { get; set; }

    public TemplateGalleryViewModel(IScaffoldingService scaffolding)
    {
        _scaffolding = scaffolding;
    }

    [RelayCommand]
    private async Task ScaffoldAsync()
    {
        if (SelectedTemplate is null)
        {
            StatusMessage = "Please select a template.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ModName))
        {
            StatusMessage = "Please enter a mod name.";
            return;
        }

        if (ScaffoldRequested is not null)
        {
            await ScaffoldRequested();
        }
    }

    /// <summary>
    /// Scaffold the selected template into the given directory via
    /// <see cref="IScaffoldingService"/>.
    /// </summary>
    public async Task<(string[]? Files, string? Error)> ScaffoldIntoAsync(string targetDir)
    {
        if (SelectedTemplate is null)
            return (null, "No template selected.");

        return await _scaffolding.ScaffoldFromTemplateAsync(
            targetDir,
            SelectedTemplate,
            ModName,
            ModDescription,
            ModAuthor);
    }
}
