using System.Collections.ObjectModel;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IsaacAgent.Core.Models;

namespace IsaacAgent.App.ViewModels;

/// <summary>
/// View model for the Mod Template Gallery.
/// Lets users browse built-in templates and scaffold new projects.
/// </summary>
public sealed partial class TemplateGalleryViewModel : ObservableObject
{
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
    /// Scaffold the selected template into the given directory.
    /// </summary>
    public async Task<(string[]? Files, string? Error)> ScaffoldIntoAsync(string targetDir)
    {
        if (SelectedTemplate is null)
            return (null, "No template selected.");

        var name = string.IsNullOrWhiteSpace(ModName) ? "MyMod" : ModName;
        var description = string.IsNullOrWhiteSpace(ModDescription) ? "A custom Binding of Isaac mod" : ModDescription;
        var author = string.IsNullOrWhiteSpace(ModAuthor) ? "Unknown" : ModAuthor;

        try
        {
            Directory.CreateDirectory(targetDir);

            var created = new List<string>();

            // Create directories
            foreach (var dir in SelectedTemplate.Directories)
            {
                var fullPath = Path.Combine(targetDir, dir);
                Directory.CreateDirectory(fullPath);
                created.Add(dir + "/");
            }

            // Write files with placeholder substitution
            foreach (var (relPath, content) in SelectedTemplate.Files)
            {
                var fileContent = content
                    .Replace("{name}", EscapeLuaString(name))
                    .Replace("{description}", SecurityElement.Escape(description) ?? "")
                    .Replace("{author}", SecurityElement.Escape(author) ?? "");

                var fullPath = Path.Combine(targetDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(fullPath, fileContent);
                created.Add(relPath);
            }

            return (created.ToArray(), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static string EscapeLuaString(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
